using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using RaycastHit = Unity.Physics.RaycastHit;

[UpdateInGroup(typeof(SimulationSystemGroup)),UpdateBefore(typeof(AnimationSystem))]
[BurstCompile]
public partial struct BulletMovementSystem : ISystem
{
    private bool _isGetComponent;
    private EntityManager _entityManager;
    private WeaponProperty _weaponProperties;
    private EntityTypeHandle _entityTypeHandle;
    private CollisionFilter _collisionFilter;
    private PhysicsWorldSingleton _physicsWorld;
    private NativeQueue<ItemTakeDamage> _takeDamageQueue;
    private NativeHashMap<Entity, float> _takeDamageMap;
    private EntityQuery _enQueryBulletInfoAlive;
    private ComponentTypeHandle<LocalTransform> _ltComponentTypeHandle;
    private ComponentTypeHandle<BulletInfo> _bulletInfoComponentTypeHandle;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        RequireNecessaryComponents(ref state);
        Init(ref state);
    }
    [BurstCompile]
    private void Init(ref SystemState state)
    {
        _ltComponentTypeHandle = state.GetComponentTypeHandle<LocalTransform>();
        _bulletInfoComponentTypeHandle = state.GetComponentTypeHandle<BulletInfo>();
        _entityTypeHandle = state.GetEntityTypeHandle();
        _takeDamageQueue = new NativeQueue<ItemTakeDamage>( Allocator.Persistent);
        _takeDamageMap =
            new NativeHashMap<Entity, float>(100, Allocator.Persistent);
        _enQueryBulletInfoAlive = SystemAPI.QueryBuilder().WithAll<BulletInfo>().WithNone<Disabled, SetActiveSP>().Build();
    }

    [BurstCompile]
    private void RequireNecessaryComponents(ref SystemState state)
    {
        state.RequireForUpdate<LayerStoreComponent>();
        state.RequireForUpdate<EffectProperty>();
        state.RequireForUpdate<WeaponProperty>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_takeDamageQueue.IsCreated)
            _takeDamageQueue.Dispose();
        if (_takeDamageMap.IsCreated)
            _takeDamageMap.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_isGetComponent)
        {
            _entityManager = state.EntityManager;
            _isGetComponent = true;
            _weaponProperties = SystemAPI.GetSingleton<WeaponProperty>();
            var layerStore = SystemAPI.GetSingleton<LayerStoreComponent>();
            _collisionFilter = new CollisionFilter()
            {
                BelongsTo = layerStore.bulletLayer,
                CollidesWith = layerStore.enemyLayer | layerStore.itemCanShootLayer,
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
        _ltComponentTypeHandle.Update(ref state);
        _bulletInfoComponentTypeHandle.Update(ref state);
        var jobChunk = new BulletMovementJOB()
        {
            ecb = ecb.AsParallelWriter(),
            physicsWorld = _physicsWorld,
            filter = _collisionFilter,
            length = _weaponProperties.length,
            time = (float)SystemAPI.Time.ElapsedTime,
            deltaTime = SystemAPI.Time.DeltaTime,
            localTransformType = _ltComponentTypeHandle,
            entityTypeHandle = _entityTypeHandle,
            currentTime = curTime,
            bulletInfoTypeHandle = _bulletInfoComponentTypeHandle,
            expired = _weaponProperties.timeLife,
            zombieDamageMapQueue = _takeDamageQueue.AsParallelWriter(),
        };
        state.Dependency = jobChunk.Schedule(_enQueryBulletInfoAlive, state.Dependency);
        state.Dependency.Complete();
        
        if (_takeDamageQueue.Count > 0)
        {
            while(_takeDamageQueue.TryDequeue(out var item))
            {
                if (item.damage == 0) continue;

                var damage = _entityManager.HasComponent<ItemCanShoot>(item.entity) ? 1 : item.damage;
                if (_takeDamageMap.ContainsKey(item.entity))
                {
                    _takeDamageMap[item.entity] += damage;
                }
                else
                {
                    _takeDamageMap.Add(item.entity,damage);
                }
            }
            
            
            
            foreach (var map in _takeDamageMap)
            {
                ecb.AddComponent(map.Key,new TakeDamage()
                {
                    value = map.Value,
                });
            }
        }
        _takeDamageQueue.Clear();
        _takeDamageMap.Clear();
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
        [WriteOnly]public NativeQueue<ItemTakeDamage>.ParallelWriter  zombieDamageMapQueue;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var ltArr = chunk.GetNativeArray(ref localTransformType);
            var entities = chunk.GetNativeArray(entityTypeHandle);
            var bulletInfos = chunk.GetNativeArray(ref bulletInfoTypeHandle);
            var setActiveSP = new SetActiveSP()
            {
                state = DisableID.Disable,
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
                    zombieDamageMapQueue.Enqueue(new ItemTakeDamage()
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

    private struct ItemTakeDamage
    {
        public Entity entity;
        public float damage;
    }
    
    // structs }
}