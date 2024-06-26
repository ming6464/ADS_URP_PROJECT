using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;
using RaycastHit = Unity.Physics.RaycastHit;

[UpdateInGroup(typeof(SimulationSystemGroup)),UpdateBefore(typeof(ZombieAnimationSystem))]
[BurstCompile]
public partial struct BulletMovementSystem : ISystem
{
    private float _expired;
    private bool _isGetComponent;
    private EntityManager _entityManager;
    private WeaponProperty _weaponProperties;
    private EntityTypeHandle _entityTypeHandle;
    private CollisionFilter _collisionFilter;
    private PhysicsWorldSingleton _physicsWorld;
    private NativeQueue<ZombieTakeDamage> _zombieDamageMapQueue;
    private NativeHashMap<Entity, float> _zombieTakeDamageMap;
    private EntityQuery _enQueryBulletInfoAlive;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<LayerStoreComponent>();
        state.RequireForUpdate<EffectProperty>();
        state.RequireForUpdate<WeaponProperty>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<WeaponInfo>();
        _entityTypeHandle = state.GetEntityTypeHandle();
        _zombieDamageMapQueue = new NativeQueue<ZombieTakeDamage>( Allocator.Persistent);
        _zombieTakeDamageMap =
            new NativeHashMap<Entity, float>(100, Allocator.Persistent);
        _enQueryBulletInfoAlive = SystemAPI.QueryBuilder().WithAll<BulletInfo>().WithNone<Disabled, SetActiveSP>().Build();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_zombieDamageMapQueue.IsCreated)
            _zombieDamageMapQueue.Dispose();
        if (_zombieTakeDamageMap.IsCreated)
            _zombieTakeDamageMap.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_isGetComponent)
        {
            _entityManager = state.EntityManager;
            _isGetComponent = true;
            _weaponProperties = SystemAPI.GetSingleton<WeaponProperty>();
            _expired = _weaponProperties.expired;
            var layerStore = SystemAPI.GetSingleton<LayerStoreComponent>();
            _collisionFilter = new CollisionFilter()
            {
                BelongsTo = layerStore.bulletLayer,
                CollidesWith = layerStore.zombieLayer,
            };
        }
        
        MovementBulletAndCheckExpiredBullet(ref state);
    }

    private void MovementBulletAndCheckExpiredBullet(ref SystemState state)
    {
        float curTime = (float)SystemAPI.Time.ElapsedTime;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        _physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        _entityTypeHandle.Update(ref state);
        var jobChunk = new BulletMovementJOB()
        {
            ecb = ecb.AsParallelWriter(),
            physicsWorld = _physicsWorld,
            filter = _collisionFilter,
            length = _weaponProperties.length,
            time = (float)SystemAPI.Time.ElapsedTime,
            deltaTime = SystemAPI.Time.DeltaTime,
            localTransformType = state.GetComponentTypeHandle<LocalTransform>(),
            entityTypeHandle = _entityTypeHandle,
            currentTime = curTime,
            bulletInfoTypeHandle = state.GetComponentTypeHandle<BulletInfo>(),
            expired = _expired,
            zombieDamageMapQueue = _zombieDamageMapQueue.AsParallelWriter(),
        };
        state.Dependency = jobChunk.Schedule(_enQueryBulletInfoAlive, state.Dependency);
        state.Dependency.Complete();
        if (_zombieDamageMapQueue.Count > 0)
        {
            while(_zombieDamageMapQueue.TryDequeue(out var item))
            {
                if (item.damage == 0)
                {
                    
                    continue;
                }
                if (_zombieTakeDamageMap.ContainsKey(item.entity))
                {
                    _zombieTakeDamageMap[item.entity] += item.damage;
                }
                else
                {
                    _zombieTakeDamageMap.Add(item.entity,item.damage);
                }
            }
            foreach (var map in _zombieTakeDamageMap)
            {
                ecb.AddComponent(map.Key,new TakeDamage()
                {
                    value = map.Value,
                });
            }
        }
        _zombieDamageMapQueue.Clear();
        _zombieTakeDamageMap.Clear();
        ecb.Playback(_entityManager);
        ecb.Dispose();
    }

    //Jobs {

    [BurstCompile]
    partial struct BulletMovementJOB : IJobChunk
    {
        [WriteOnly] public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public EntityTypeHandle entityTypeHandle;
        [ReadOnly] public ComponentTypeHandle<BulletInfo> bulletInfoTypeHandle;
        [ReadOnly] public PhysicsWorldSingleton physicsWorld;
        [ReadOnly] public CollisionFilter filter;
        [ReadOnly] public float length;
        [ReadOnly] public float time;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float currentTime;
        [ReadOnly] public float expired;
        public ComponentTypeHandle<LocalTransform> localTransformType;
        [WriteOnly]public NativeQueue<ZombieTakeDamage>.ParallelWriter  zombieDamageMapQueue;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var ltArr = chunk.GetNativeArray(localTransformType);
            var entities = chunk.GetNativeArray(entityTypeHandle);
            var bulletInfos = chunk.GetNativeArray(bulletInfoTypeHandle);
            var setActiveSP = new SetActiveSP()
            {
                state = StateID.Disable,
                startTime = time,
            };

            var eff = new EffectComponent();
            var ltHide = new LocalTransform()
            {
                Scale = 1,
                Position = new float3(999, 999, 999),
            };
            
            for (int i = 0; i < chunk.Count; i++)
            {

                var entity = entities[i];
                var bulletInfo = bulletInfos[i];
                if ((currentTime - bulletInfo.startTime) >= expired)
                {
                    ecb.SetComponent(unfilteredChunkIndex,entity,ltHide);
                    ecb.AddComponent<AddToBuffer>(unfilteredChunkIndex,entity);
                    ecb.AddComponent(unfilteredChunkIndex,entity,setActiveSP);
                    continue;
                }
                
                var lt = ltArr[i];
                float speed_New = Random.CreateFromIndex((uint)(i + 1 + (time % deltaTime)))
                    .NextFloat(bulletInfo.speed - 10f, bulletInfo.speed + 10f);
                float3 newPosition = lt.Position + lt.Forward() * speed_New * deltaTime;
                RaycastInput raycastInput = new RaycastInput()
                {
                    Start = lt.Position,
                    End = newPosition + lt.Forward() * length,
                    Filter = filter,
                };
                
                if (physicsWorld.CastRay(raycastInput, out RaycastHit hit))
                {
                    zombieDamageMapQueue.Enqueue(new ZombieTakeDamage()
                    {
                        damage = bulletInfo.damage,
                        entity = hit.Entity,
                    });
                    
                    ecb.SetComponent(unfilteredChunkIndex,entity,ltHide);
                    ecb.AddComponent<AddToBuffer>(unfilteredChunkIndex,entity);
                    ecb.AddComponent(unfilteredChunkIndex, entity, setActiveSP);
                    eff.position = hit.Position;
                    eff.rotation = quaternion.LookRotationSafe(hit.SurfaceNormal, math.up());
                    ecb.AddComponent(unfilteredChunkIndex, hit.Entity, eff);
                }
                else
                {
                    lt.Position = newPosition;
                    ltArr[i] = lt;
                }
            }
        }
    }
    //Jobs }
    
    
    // structs {

    private struct ZombieTakeDamage
    {
        public Entity entity;
        public float damage;
    }
    
    // structs }
}