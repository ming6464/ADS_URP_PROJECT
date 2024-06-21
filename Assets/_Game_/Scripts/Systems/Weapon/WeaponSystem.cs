using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(PlayerSystem))]
[BurstCompile]
public partial struct WeaponSystem : ISystem
{
    private Entity _bulletEntityPrefab;
    private WeaponProperties _weaponProperties;
    private float _timeLatest;
    private float _cooldown;
    
    private bool _isSpawn;
    private bool _isGetComponent;
    private bool _shootAuto;
    private bool _pullTrigger;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInput>();
        state.RequireForUpdate<WeaponRunTime>();
        state.RequireForUpdate<WeaponProperties>();
        state.RequireForUpdate<PlayerInfo>();
        state.RequireForUpdate<CharacterInfo>();
        _isSpawn = false;
        _isGetComponent = false;
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


            _shootAuto = _weaponProperties.shootAuto;
            _pullTrigger = _shootAuto;
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            return;
        }
        if (!_isGetComponent)
        {
            _bulletEntityPrefab = _weaponProperties.entityBullet;
            _isGetComponent = true;
        }

        if (!_shootAuto)
        {
            _pullTrigger = SystemAPI.GetSingleton<PlayerInput>().pullTrigger;
        }
        
        Shot(ref state);
    }

    private void Shot(ref SystemState state)
    {
        if(!_pullTrigger) return;
        if ((SystemAPI.Time.ElapsedTime - _timeLatest) < _cooldown) return;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        var entityManager = state.EntityManager;
        var entitiesDisable = SystemAPI.QueryBuilder().WithAll<Disabled>().WithAll<BulletInfo>().Build().ToEntityArray(Allocator.Temp);
        var bulletEntityPrefab = _bulletEntityPrefab;
        float spaceAngleAnyBullet = _weaponProperties.spaceAngleAnyBullet;
        float time = (float)(SystemAPI.Time.ElapsedTime);
        BulletInfo bulletInfo = new BulletInfo { startTime = time };
        byte countUsing = 0;
        LocalTransform lt;
        float3 angleRota;
        
        foreach (var weaponAspect in SystemAPI.Query<WeaponAspect>())
        {
            float subtractIndex = 0.5f;
            int halfNumberPreShot = (int)math.ceil(_weaponProperties.bulletPerShot / 2f);
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
        
        ecb.Playback(entityManager);
        _timeLatest = time;
        entitiesDisable.Dispose();
        
        void SpawnBullet(LocalTransform lt)
        {
            Entity entity;
            if (countUsing < entitiesDisable.Length)
            {
                entity = entitiesDisable[countUsing];
                ecb.RemoveComponent<Disabled>(entity);
                ecb.AddComponent(entity, new SetActiveSP()
                {
                    state = StateID.CanEnable,
                    startTime = time,
                });
            }
            else
            {
                entity = ecb.Instantiate(bulletEntityPrefab);
            }
            ecb.AddComponent(entity,bulletInfo);
            ecb.AddComponent(entity,lt);
            countUsing++;
        }
        
    }
}
