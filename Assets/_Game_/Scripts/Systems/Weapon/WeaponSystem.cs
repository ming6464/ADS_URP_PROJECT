using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[BurstCompile]
public partial struct WeaponSystem : ISystem
{
    private Entity _bulletEntityPrefab;
    private WeaponProperties _weaponProperties;
    private float _timeLatest;
    private float _cooldown;
    
    // private NativeList<WeaponAspect> _weaponAspects;
    private bool _isSpawn;
    private bool _isGetComponent;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WeaponRunTime>();
        state.RequireForUpdate<WeaponProperties>();
        state.RequireForUpdate<PlayerInfo>();
        state.RequireForUpdate<CharacterInfo>();
        _isSpawn = false;
        _isGetComponent = false;
        // _weaponAspects = new NativeList<WeaponAspect>(Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        // if (_weaponAspects.IsCreated)
        // {
        //     _weaponAspects.Dispose();
        // }
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!_isSpawn)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            var weaponRuntime = SystemAPI.GetSingleton<WeaponRunTime>();
            _weaponProperties = SystemAPI.GetSingleton<WeaponProperties>();
            _cooldown = weaponRuntime.cooldown;
            _timeLatest = -_cooldown;
            Entity entityInstantiate = _weaponProperties.entityWeapon;
            foreach (var aspect in SystemAPI.Query<CharacterAspect>())
            {
                var playerEntity = aspect.entity;
                Entity weaponEntity = ecb.Instantiate(entityInstantiate);
                ecb.AddComponent(weaponEntity, new Parent() { Value = playerEntity });
                ecb.AddComponent(weaponEntity, new LocalTransform() { Position = _weaponProperties.offset, Rotation = quaternion.identity, Scale = 1 });
                ecb.AddComponent<WeaponInfo>(weaponEntity);
            }
            _isSpawn = true;
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            return;
        }
        if (!_isGetComponent)
        {
            _bulletEntityPrefab = _weaponProperties.entityBullet;
            // foreach (var aspect in SystemAPI.Query<WeaponAspect>())
            // {
            //     _weaponAspects.Add(aspect);
            // }
            _isGetComponent = true;
        }
        Shot(ref state);
    }

    private void Shot(ref SystemState state)
    {
        if ((SystemAPI.Time.ElapsedTime - _timeLatest) < _cooldown) return;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        var entityManager = state.EntityManager;
        var entitiesDisable = SystemAPI.QueryBuilder().WithAll<Disabled>().WithAll<BulletInfo>().Build().ToEntityArray(Allocator.Temp);
        int halfNumberPreShot = (int)math.ceil(_weaponProperties.bulletPerShot / 2f);
        var bulletEntityPrefab = _bulletEntityPrefab;
        float spaceAngleAnyBullet = _weaponProperties.spaceAngleAnyBullet;
        float time = (float)(SystemAPI.Time.ElapsedTime);
        BulletInfo bulletInfo = new BulletInfo { startTime = time };
        byte countUsing = 0;
        float subtractIndex = 0.5f;
        LocalTransform lt;
        float3 angleRota;
        
        foreach (var weaponAspect in SystemAPI.Query<WeaponAspect>())
        {
            lt = new LocalTransform()
            {
                Position = weaponAspect.PositionWorld,
                Rotation = weaponAspect.RotationWorld,
                Scale = 1,
            };
            angleRota = MathExt.QuaternionToFloat3(lt.Rotation);
            if (halfNumberPreShot % 2 != 0)
            {
                SpawnBullet(lt);
                --halfNumberPreShot;
                subtractIndex = 0;
            }
            
            for (int i = 1; i <= halfNumberPreShot; i++)
            {
                float3 angleRotaNew = angleRota;
                float angle = (i - subtractIndex) * spaceAngleAnyBullet;
                float angle1 = angleRotaNew.y + angle;
                float angle2 = angleRotaNew.y - angle;
                angleRotaNew.y = angle1;
                lt.Rotation = MathExt.Float3ToQuaternion(angleRotaNew);
                SpawnBullet(lt);
                angleRotaNew.y = angle2;
                lt.Rotation = MathExt.Float3ToQuaternion(angleRotaNew);
                SpawnBullet(lt);
            }
            
        }
        void SpawnBullet(LocalTransform lt)
        {
            Entity entity;
            if (countUsing < entitiesDisable.Length)
            {
                entity = entitiesDisable[countUsing];
                ecb.RemoveComponent<Disabled>(entity);
                ecb.SetComponent(entity,bulletInfo);
                ecb.AddComponent(entity, new SetActiveSP()
                {
                    state = StateID.CanEnable,
                    startTime = time,
                });
            }
            else
            {
                entity = ecb.Instantiate(bulletEntityPrefab);
                ecb.AddComponent(entity,bulletInfo);
            }
            countUsing++;
            ecb.AddComponent(entity,lt);
        }
        ecb.Playback(entityManager);
        _timeLatest = time;
    }
}
