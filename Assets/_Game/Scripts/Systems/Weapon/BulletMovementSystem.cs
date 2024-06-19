using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateBefore(typeof(ZombieAnimationSystem))]
[BurstCompile]
public partial struct BulletMovementSystem : ISystem
{
    private float _speed;
    private float _damage;
    private float _expired;
    private bool _isGetComponent;
    private EntityManager _entityManager;
    private WeaponProperties _weaponProperties;
    private EntityTypeHandle _entityTypeHandle;
    private CollisionFilter _collisionFilter;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
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

    [BurstCompile]
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
            return;
        }
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        DisableExpiredBullets(ref state, ref ecb);

        if (!state.Dependency.IsCompleted)
        {
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            ecb = new EntityCommandBuffer(Allocator.TempJob);
            return;
        }
        
        PhysicsWorldSingleton physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        EntityQuery euQuery = SystemAPI.QueryBuilder().WithAll<BulletInfo>().WithNone<Disabled, SetActiveSP>().Build();
        _entityTypeHandle.Update(ref state);
        var jobChunk = new BulletColliderJOBChunk()
        {
            ecb = ecb.AsParallelWriter(),
            physicsWorld = physicsWorld,
            filter = _collisionFilter,
            speed = _weaponProperties.bulletSpeed,
            length = _weaponProperties.length,
            time = (float)SystemAPI.Time.ElapsedTime,
            deltaTime = (float)SystemAPI.Time.DeltaTime,
            localTransformType = state.GetComponentTypeHandle<LocalTransform>(),
            entityTypeHandle = _entityTypeHandle,
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
public partial struct BulletColliderJOBChunk : IJobChunk
{
    [WriteOnly] public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public EntityTypeHandle entityTypeHandle;
    [ReadOnly] public PhysicsWorldSingleton physicsWorld;
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
        for (int i = 0; i < chunk.Count; i++)
        {
            var lt = ltArr[i];
            float3 newPosition = lt.Position + lt.Forward() * speed * deltaTime;
            
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
            
            if (physicsWorld.CastRay(raycastInput, out RaycastHit hit))
            {
                setActiveSP.state = StateID.Wait;
                ecb.AddComponent(unfilteredChunkIndex,hit.Entity, setActiveSP);
                ecb.RemoveComponent<LocalTransform>(unfilteredChunkIndex,hit.Entity);
                ecb.RemoveComponent<LocalTransform>(unfilteredChunkIndex,entities[i]);
                setActiveSP.state = StateID.CanDisable;
                ecb.AddComponent(unfilteredChunkIndex,entities[i],setActiveSP);
            }
            else
            {
                lt.Position = newPosition;
                ltArr[i] = lt;
            }
        }
    }
}