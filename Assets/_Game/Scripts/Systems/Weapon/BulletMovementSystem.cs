using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[BurstCompile]
public partial struct BulletMovementSystem : ISystem
{
    private float _speed;
    private float _damage;
    private float _expired;
    private bool _isGetComponent;
    private EntityManager _entityManager;
    private WeaponProperties _weaponProperties;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WeaponProperties>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<WeaponInfo>();
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
        EntityManager entityManager = state.EntityManager;
        var filter = new CollisionFilter()
        {
            BelongsTo = 1u << 6,
            CollidesWith = 1u << 7
        };
        
        
        var job = new BulletColliderJOB()
        {
            ecb = ecb.AsParallelWriter(),
            physicsWorld = physicsWorld,
            filter = filter,
            speed = _weaponProperties.bulletSpeed,
            length = _weaponProperties.length,
            time = (float)SystemAPI.Time.ElapsedTime,
            deltaTime = (float)SystemAPI.Time.DeltaTime,
        };

        state.Dependency = job.ScheduleParallel(state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        
        
        
        // foreach (var bulletAspect in SystemAPI.Query<BulletAspect>())
        // {
        //     float3 newPosition = bulletAspect.Position + bulletAspect.LocalTransform.Forward() * _weaponProperties.bulletSpeed * SystemAPI.Time.DeltaTime;
        //     
        //     RaycastInput raycastInput = new RaycastInput()
        //     {
        //         Start = bulletAspect.Position,
        //         End = newPosition + bulletAspect.LocalTransform.Forward() * _weaponProperties.length,
        //         Filter = new CollisionFilter()
        //         {
        //             BelongsTo = 1u << 6,
        //             CollidesWith = 1u << 7
        //         }
        //     };
        //
        //     var setActiveSP = new SetActiveSP()
        //     {
        //         startTime =  (float)SystemAPI.Time.ElapsedTime,
        //     };
        //     
        //     if (physicsWorld.CastRay(raycastInput, out RaycastHit hit))
        //     {
        //         setActiveSP.state = StateID.Wait;
        //         ecb.AddComponent(hit.Entity, setActiveSP);
        //         ecb.RemoveComponent<LocalTransform>(hit.Entity);
        //         ecb.RemoveComponent<LocalTransform>(bulletAspect.entity);
        //         setActiveSP.state = StateID.CanDisable;
        //         ecb.AddComponent(bulletAspect.entity,setActiveSP);
        //     }
        //     else
        //     {
        //         bulletAspect.Position = newPosition;
        //     }
        // }
        // ecb.Playback(entityManager);
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
public partial struct BulletColliderJOB : IJobEntity
{
    [WriteOnly] public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public PhysicsWorldSingleton physicsWorld;
    [ReadOnly] public CollisionFilter filter;
    [ReadOnly] public float speed;
    [ReadOnly] public float length;
    [ReadOnly] public float time;
    [ReadOnly] public float deltaTime;
    
    private void Execute(BulletAspect bulletAspect, [EntityIndexInQuery] int entityIndexQuery)
    {
        float3 newPosition = bulletAspect.Position + bulletAspect.LocalTransform.Forward() * speed * deltaTime;
            
        RaycastInput raycastInput = new RaycastInput()
        {
            Start = bulletAspect.Position,
            End = newPosition + bulletAspect.LocalTransform.Forward() * length,
            Filter = filter,
        };

        var setActiveSP = new SetActiveSP()
        {
            startTime =  time,
        };
            
        if (physicsWorld.CastRay(raycastInput, out RaycastHit hit))
        {
            setActiveSP.state = StateID.Wait;
            ecb.AddComponent(entityIndexQuery,hit.Entity, setActiveSP);
            ecb.RemoveComponent<LocalTransform>(entityIndexQuery,hit.Entity);
            ecb.RemoveComponent<LocalTransform>(entityIndexQuery,bulletAspect.entity);
            setActiveSP.state = StateID.CanDisable;
            ecb.AddComponent(entityIndexQuery,bulletAspect.entity,setActiveSP);
        }
        else
        {
            bulletAspect.Position = newPosition;
        }
    }
}