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

[UpdateInGroup(typeof(SimulationSystemGroup)),UpdateBefore(typeof(ZombieAnimationSystem))]
[BurstCompile]
public partial struct BulletMovementSystem : ISystem
{
    private float _expired;
    private bool _isGetComponent;
    private EntityManager _entityManager;
    private WeaponProperties _weaponProperties;
    private EntityTypeHandle _entityTypeHandle;
    private CollisionFilter _collisionFilter;
    private PhysicsWorldSingleton _physicsWorld;
    private NativeArray<Entity> _pointsEffectDisable;
    private NativeQueue<ZombieTakeDamage> _zombieDamageMapQueue;
    private NativeHashMap<Entity, float> _zombieTakeDamageMap;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<LayerStoreComponent>();
        state.RequireForUpdate<EffectProperty>();
        state.RequireForUpdate<WeaponProperties>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<WeaponInfo>();
        _entityTypeHandle = state.GetEntityTypeHandle();
        _zombieDamageMapQueue = new NativeQueue<ZombieTakeDamage>( Allocator.Persistent);
        _zombieTakeDamageMap =
            new NativeHashMap<Entity, float>(1000, Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_pointsEffectDisable.IsCreated)
            _pointsEffectDisable.Dispose();
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
            _weaponProperties = SystemAPI.GetSingleton<WeaponProperties>();
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
        _zombieDamageMapQueue.Clear();
        _zombieTakeDamageMap.Clear();
        float curTime = (float)SystemAPI.Time.ElapsedTime;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        _physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        EntityQuery euQuery = SystemAPI.QueryBuilder().WithAll<BulletInfo>().WithNone<Disabled, SetActiveSP>().Build();
        _pointsEffectDisable = SystemAPI.QueryBuilder().WithAll<EffectComponent, Disabled>().Build()
            .ToEntityArray(Allocator.TempJob);
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
            hitFlashPointDisable = _pointsEffectDisable,
            currentTime = curTime,
            bulletInfoTypeHandle = state.GetComponentTypeHandle<BulletInfo>(),
            expired = _expired,
            zombieDamageMapQueue = _zombieDamageMapQueue.AsParallelWriter(),
        };
        state.Dependency = jobChunk.Schedule(euQuery, state.Dependency);
        state.Dependency.Complete();
        if (_zombieDamageMapQueue.Count > 0)
        {
            while(_zombieDamageMapQueue.TryDequeue(out var item))
            {
                Debug.Log("_ damage take " + item.damage);
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
        ecb.Playback(_entityManager);
        ecb.Dispose();
        _pointsEffectDisable.Dispose();
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
        [ReadOnly] public NativeArray<Entity> hitFlashPointDisable;
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
            int countPointUsed = 0;
            int totalCountPoint = hitFlashPointDisable.Length;
            var lt_eff = new LocalTransform()
            {
                Scale = 1,
            };
            var setActiveSP = new SetActiveSP()
            {
                startTime = time,
            };
            for (int i = 0; i < chunk.Count; i++)
            {

                var entity = entities[i];
                var bulletInfo = bulletInfos[i];
                if ((currentTime - bulletInfo.startTime) >= expired)
                {
                    ecb.RemoveComponent<LocalTransform>(unfilteredChunkIndex,entity);
                    ecb.AddComponent(unfilteredChunkIndex,entities,new SetActiveSP()
                    {
                        state = StateID.Disable
                    });
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
                    
                    ecb.RemoveComponent<LocalTransform>(unfilteredChunkIndex, entity);
                    setActiveSP.state = StateID.Disable;
                    ecb.AddComponent(unfilteredChunkIndex, entity, setActiveSP);
                    Entity effNew;

                    if (countPointUsed < totalCountPoint)
                    {
                        effNew = hitFlashPointDisable[countPointUsed];
                        ecb.RemoveComponent<Disabled>(unfilteredChunkIndex,effNew);
                        setActiveSP.state = StateID.Enable;
                        ecb.AddComponent(unfilteredChunkIndex,effNew,setActiveSP);
                        ++countPointUsed;
                    }
                    else
                    {
                        effNew = ecb.CreateEntity(unfilteredChunkIndex);
                        ecb.AddComponent<EffectComponent>(unfilteredChunkIndex, effNew);
                    }

                    lt_eff.Position = hit.Position;
                    lt_eff.Rotation = quaternion.LookRotationSafe(hit.SurfaceNormal, math.up());
                    ecb.AddComponent(unfilteredChunkIndex, effNew, lt_eff);
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