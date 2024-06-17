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
        PhysicsWorldSingleton physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        EntityManager entityManager = state.EntityManager;
        DisableExpiredBullets(ref state, ref ecb);

        SetActiveSP disableSp = new SetActiveSP()
        {
            startTime = (float)SystemAPI.Time.ElapsedTime,
        };
        
        foreach (var bulletAspect in SystemAPI.Query<BulletAspect>())
        {
            float3 newPosition = bulletAspect.Position + bulletAspect.LocalTransform.Forward() * _weaponProperties.bulletSpeed * SystemAPI.Time.DeltaTime;
            
            RaycastInput raycastInput = new RaycastInput()
            {
                Start = bulletAspect.Position,
                End = newPosition + bulletAspect.LocalTransform.Forward() * _weaponProperties.length,
                Filter = new CollisionFilter()
                {
                    BelongsTo = 1u << 6,
                    CollidesWith = 1u << 7
                }
            };
            
            if (physicsWorld.CastRay(raycastInput, out RaycastHit hit))
            {
                // disableSp.key = StateKEY.Wait;
                disableSp.status = 1;
                ecb.AddComponent(hit.Entity,disableSp);
                ecb.RemoveComponent<LocalTransform>(hit.Entity);
                ecb.RemoveComponent<LocalTransform>(bulletAspect.entity);
                disableSp.status = 3;
                ecb.AddComponent(bulletAspect.entity,disableSp);

            }
            else
            {
                bulletAspect.Position = newPosition;
            }
        }
        ecb.Playback(entityManager);
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
                ecb.AddComponent(entity,new SetActiveSP
                {
                    status = 3,
                    startTime = 0f,
                });
            }
        }
        
    }
}