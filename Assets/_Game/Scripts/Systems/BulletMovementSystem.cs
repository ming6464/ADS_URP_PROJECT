using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using RaycastHit = Unity.Physics.RaycastHit;

[BurstCompile]
public partial struct BulletMovementSystem : ISystem
{
    private float speed;
    private float damage;
    private bool getConponent;
    private WeaponProperties _weaponProperties;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WeaponProperties>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<WeaponInfo>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!getConponent)
        {
            getConponent = true;
            _weaponProperties = SystemAPI.GetSingleton<WeaponProperties>();
            speed = _weaponProperties.bulletSpeed;
            damage = _weaponProperties.bulletDamage;
            return;
        }
        
        PhysicsWorldSingleton physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        EntityManager entityManager = state.EntityManager;
        
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
                ecb.AddComponent<DisableSP>(hit.Entity);
                ecb.AddComponent<DisableSP>(bulletAspect.entity);
                // ecb.DestroyEntity(hit.Entity);
                // ecb.DestroyEntity(bulletAspect.entity);
            }
            else
            {
                bulletAspect.Position = newPosition;
            }
        }
        ecb.Playback(entityManager);
    }
}