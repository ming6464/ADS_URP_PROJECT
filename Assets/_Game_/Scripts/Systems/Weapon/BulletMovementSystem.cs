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

[UpdateBefore(typeof(ZombieAnimationSystem))]
[BurstCompile]
public partial struct BulletMovementSystem : ISystem
{
    private float _speed;
    private float _damage;
    private float _expired;
    private bool _isGetComponent;
    private WeaponProperties _weaponProperties;
    private EntityTypeHandle _entityTypeHandle;
    private CollisionFilter _collisionFilter;
    private PhysicsWorldSingleton _physicsWorld;
    private NativeArray<Entity> _pointsEffectDisable;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<LayerStoreComponent>();
        state.RequireForUpdate<EffectProperty>();
        state.RequireForUpdate<WeaponProperties>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<WeaponInfo>();
        _entityTypeHandle = state.GetEntityTypeHandle();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_pointsEffectDisable.IsCreated)
            _pointsEffectDisable.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        state.Dependency.Complete();
        if (!_isGetComponent)
        {
            _isGetComponent = true;
            _weaponProperties = SystemAPI.GetSingleton<WeaponProperties>();
            _speed = _weaponProperties.bulletSpeed;
            _damage = _weaponProperties.bulletDamage;
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
        EntityQuery euQuery = SystemAPI.QueryBuilder().WithAll<BulletInfo>().WithNone<Disabled, SetActiveSP>().Build();
        _pointsEffectDisable = SystemAPI.QueryBuilder().WithAll<EffectComponent, Disabled>().Build()
            .ToEntityArray(Allocator.TempJob);
        _entityTypeHandle.Update(ref state);
        var jobChunk = new BulletMovementJOB()
        {
            ecb = ecb.AsParallelWriter(),
            physicsWorld = _physicsWorld,
            filter = _collisionFilter,
            speed = _weaponProperties.bulletSpeed,
            length = _weaponProperties.length,
            time = (float)SystemAPI.Time.ElapsedTime,
            deltaTime = SystemAPI.Time.DeltaTime,
            localTransformType = state.GetComponentTypeHandle<LocalTransform>(),
            entityTypeHandle = _entityTypeHandle,
            hitFlashPointDisable = _pointsEffectDisable,
            currentTime = curTime,
            bulletInfoTypeHandle = state.GetComponentTypeHandle<BulletInfo>(),
            expired = _expired,
        };
        state.Dependency = jobChunk.ScheduleParallel(euQuery, state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(state.EntityManager);
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
        [ReadOnly] public float speed;
        [ReadOnly] public float length;
        [ReadOnly] public float time;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public NativeArray<Entity> hitFlashPointDisable;
        [ReadOnly] public float currentTime;
        [ReadOnly] public float expired;
        public ComponentTypeHandle<LocalTransform> localTransformType;

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
                if ((currentTime - bulletInfos[i].startTime) >= expired)
                {
                    ecb.RemoveComponent<LocalTransform>(unfilteredChunkIndex,entity);
                    ecb.AddComponent(unfilteredChunkIndex,entities,new SetActiveSP()
                    {
                        state = StateID.CanDisable
                    });
                    continue;
                }
                var lt = ltArr[i];
                float speed_New = Random.CreateFromIndex((uint)(i + 1 + (time % deltaTime)))
                    .NextFloat(speed - 10f, speed + 10f);
                float3 newPosition = lt.Position + lt.Forward() * speed_New * deltaTime;

                RaycastInput raycastInput = new RaycastInput()
                {
                    Start = lt.Position,
                    End = newPosition + lt.Forward() * length,
                    Filter = filter,
                };
                
                if (physicsWorld.CastRay(raycastInput, out RaycastHit hit))
                {
                    setActiveSP.state = StateID.Wait;
                    ecb.AddComponent(unfilteredChunkIndex, hit.Entity, setActiveSP);
                    ecb.RemoveComponent<LocalTransform>(unfilteredChunkIndex, hit.Entity);
                    ecb.RemoveComponent<LocalTransform>(unfilteredChunkIndex, entity);
                    setActiveSP.state = StateID.CanDisable;
                    ecb.AddComponent(unfilteredChunkIndex, entity, setActiveSP);
                    Entity effNew;

                    if (countPointUsed < totalCountPoint)
                    {
                        effNew = hitFlashPointDisable[countPointUsed];
                        ecb.RemoveComponent<Disabled>(unfilteredChunkIndex,effNew);
                        ecb.AddComponent(unfilteredChunkIndex,effNew,new SetActiveSP()
                        {
                            state = StateID.CanEnable
                        });
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
}