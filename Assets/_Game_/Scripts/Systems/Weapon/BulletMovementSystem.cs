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
    private Entity _effHitFlashEntity;
    private EntityManager _entityManager;
    private WeaponProperties _weaponProperties;
    private EntityTypeHandle _entityTypeHandle;
    private CollisionFilter _collisionFilter;
    private PhysicsWorldSingleton _physicsWorld;
    
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EffectProperty>();
        state.RequireForUpdate<WeaponProperties>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<WeaponInfo>();
        _entityTypeHandle = state.GetEntityTypeHandle();
        _collisionFilter = new CollisionFilter()
        {
            BelongsTo = 1u << 6,
            CollidesWith = 1u << 7,
        };
    }

    public void OnUpdate(ref SystemState state)
    {
        _entityManager = state.EntityManager;
        if (!_isGetComponent)
        {
            _isGetComponent = true;
            _weaponProperties = SystemAPI.GetSingleton<WeaponProperties>();
            _speed = _weaponProperties.bulletSpeed;
            _damage = _weaponProperties.bulletDamage;
            _expired = _weaponProperties.expired;
            var effProperty = SystemAPI.GetSingleton<EffectProperty>();
            _effHitFlashEntity = effProperty.effHitFlash;
            return;
        }
        _physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        DisableExpiredBullets(ref state, ref ecb);

        if (!state.Dependency.IsCompleted)
        {
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            ecb = new EntityCommandBuffer(Allocator.TempJob);
            return;
        }
        
        EntityQuery euQuery = SystemAPI.QueryBuilder().WithAll<BulletInfo>().WithNone<Disabled, SetActiveSP>().Build();
        _entityTypeHandle.Update(ref state);
        var jobChunk = new BulletMovementJOB()
        {
            ecb = ecb.AsParallelWriter(),
            _physicsWorld = _physicsWorld,
            filter = _collisionFilter,
            speed = _weaponProperties.bulletSpeed,
            length = _weaponProperties.length,
            time = (float)SystemAPI.Time.ElapsedTime,
            deltaTime = SystemAPI.Time.DeltaTime,
            localTransformType = state.GetComponentTypeHandle<LocalTransform>(),
            entityTypeHandle = _entityTypeHandle,
            effHitFlash = _effHitFlashEntity,
        };
        state.Dependency = jobChunk.ScheduleParallel(euQuery,state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
    
    private void DisableExpiredBullets(ref SystemState state,ref EntityCommandBuffer ecb)
    {
        float curTime = (float)SystemAPI.Time.ElapsedTime;
        EntityQuery entityQuery = SystemAPI.QueryBuilder().WithNone<Disabled>().WithNone<SetActiveSP>()
            .WithAll<BulletInfo>().Build();
        
        using (var bulletsSetExpire = entityQuery.ToEntityArray(Allocator.Temp))
        {
            foreach (Entity entity in bulletsSetExpire)
            {
                BulletInfo bulletInfo = _entityManager.GetComponentData<BulletInfo>(entity);
                
                if((curTime - bulletInfo.startTime) < _expired) continue;
                ecb.RemoveComponent<LocalTransform>(entity);
                ecb.AddComponent(entity, new SetActiveSP()
                {
                    state = StateID.CanDisable,
                });
            }
        }
        
    }
}

[BurstCompile]
public partial struct BulletMovementJOB : IJobChunk
{
    [WriteOnly] public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public Entity effHitFlash;
    [ReadOnly] public EntityTypeHandle entityTypeHandle;
    [ReadOnly] public PhysicsWorldSingleton _physicsWorld;
    [ReadOnly] public CollisionFilter filter;
    [ReadOnly] public float speed;
    [ReadOnly] public float length;
    [ReadOnly] public float time;
    [ReadOnly] public float deltaTime;
    public ComponentTypeHandle<LocalTransform> localTransformType;
    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var ltArr = chunk.GetNativeArray(localTransformType);
        var entities = chunk.GetNativeArray(entityTypeHandle);
        int count = chunk.Count;
        for (int i = 0; i < count; i++)
        {
            var lt = ltArr[i];

            float speed_ = Random.CreateFromIndex((uint)(i + 1 + (time % deltaTime))).NextFloat(speed - 10f, speed + 10f);
            
            float3 newPosition = lt.Position + lt.Forward() * speed_ * deltaTime;
            
            RaycastInput raycastInput = new RaycastInput()
            {
                Start = lt.Position,
                End = newPosition + lt.Forward() * length,
                Filter = filter,
            };

            var setActiveSP = new SetActiveSP()
            {
                startTime =  time,
            };
             
            if (_physicsWorld.CastRay(raycastInput, out RaycastHit hit))
            {
                setActiveSP.state = StateID.Wait;
                ecb.AddComponent(unfilteredChunkIndex,hit.Entity, setActiveSP);
                ecb.RemoveComponent<LocalTransform>(unfilteredChunkIndex,hit.Entity);
                ecb.RemoveComponent<LocalTransform>(unfilteredChunkIndex,entities[i]);
                setActiveSP.state = StateID.CanDisable;
                ecb.AddComponent(unfilteredChunkIndex,entities[i],setActiveSP);
                var effNew = ecb.CreateEntity(unfilteredChunkIndex);
                ecb.AddComponent<EffectComponent>(unfilteredChunkIndex,effNew);
                
                var lt_eff = new LocalTransform()
                {
                    Position = hit.Position,
                    Rotation = quaternion.LookRotationSafe(hit.SurfaceNormal, math.up()),
                    Scale = 1,
                };
                ecb.AddComponent(unfilteredChunkIndex,effNew,lt_eff);
                ecb.AddComponent(unfilteredChunkIndex,effNew,DotsEX.ConvertDataLocalToWorldTf(lt_eff));
            }
            else
            {
                lt.Position = newPosition;
                ltArr[i] = lt;
            }
        }
    }
}