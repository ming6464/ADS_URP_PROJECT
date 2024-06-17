using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct WeaponSystem : ISystem
{
    private Entity _bulletEntityPrefab;
    private Entity _playerEntity;
    private Entity _weaponEntitySingleton;
    private WeaponProperties _weaponProperties;
    private WeaponRunTime _weaponRunTime;

    private WeaponAspect _weaponAspect;
    private bool _isSpawn;
    private bool _isGetComponent;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WeaponRunTime>();
        state.RequireForUpdate<WeaponProperties>();
        state.RequireForUpdate<PlayerInfo>();
        _isSpawn = false;
        _isGetComponent = false;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!_isSpawn)
        {
            var entity = SystemAPI.GetSingletonEntity<WeaponProperties>();
            _playerEntity = SystemAPI.GetSingletonEntity<PlayerInfo>();
            _weaponProperties = SystemAPI.GetComponentRO<WeaponProperties>(entity).ValueRO;
            Entity entityInstantiate = _weaponProperties.entityWeapon;
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            Entity weaponEntity = ecb.Instantiate(entityInstantiate);
            ecb.AddComponent(weaponEntity, new Parent() { Value = _playerEntity });
            ecb.AddComponent(weaponEntity, new LocalTransform() { Position = _weaponProperties.offset, Rotation = quaternion.identity, Scale = 1 });
            ecb.AddComponent<WeaponInfo>(weaponEntity);
            _isSpawn = true;
            ecb.Playback(state.EntityManager);
            return;
        }
        if (!_isGetComponent)
        {
            _weaponEntitySingleton = SystemAPI.GetSingletonEntity<WeaponInfo>();
            _bulletEntityPrefab = _weaponProperties.entityBullet;
            _isGetComponent = true;
        }
        Shot(ref state);
    }

    private void Shot(ref SystemState state)
    {
        if ((SystemAPI.Time.ElapsedTime - _weaponRunTime.timeLatest) < _weaponRunTime.cooldown) return;
        var entityManager = state.EntityManager;
        var entitiesDisable = SystemAPI.QueryBuilder().WithAll<Disabled>().WithAll<BulletInfo>().Build().ToEntityArray(Allocator.Temp);
        byte countUsing = 0;
        _weaponAspect = SystemAPI.GetAspect<WeaponAspect>(_weaponEntitySingleton);
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        int halfNumberPreShot = (int)math.ceil(_weaponProperties.bulletPerShot / 2f);
        var bulletEntityPrefab = _bulletEntityPrefab;
        LocalTransform lt = new LocalTransform()
        {
            Position = _weaponAspect.PositionWorld,
            Rotation = _weaponAspect.RotationWorld,
            Scale = 1,
        };

        BulletInfo bulletInfo = new BulletInfo()
        {
            startTime = (float)SystemAPI.Time.ElapsedTime,
        };
        
        float3 angleRota = MathExt.QuaternionToFloat3(lt.Rotation);
        float spaceAngleAnyBullet = _weaponProperties.spaceAngleAnyBullet;
        float subtractIndex = 0.5f;
        if (halfNumberPreShot % 2 != 0)
        {
            spawnBullet(lt);
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
            spawnBullet(lt);

            angleRotaNew.y = angle2;
            lt.Rotation = MathExt.Float3ToQuaternion(angleRotaNew);
            spawnBullet(lt);
        }

        ecb.Playback(state.EntityManager);
        _weaponRunTime.timeLatest = (float)SystemAPI.Time.ElapsedTime;



        void spawnBullet(LocalTransform lt)
        {
            Entity entity;
            if (countUsing + 1 < entitiesDisable.Length)
            {
                entity = entitiesDisable[countUsing];
                ecb.SetComponent(entity,bulletInfo);
                ecb.RemoveComponent<Disabled>(entity);
                foreach (LinkedEntityGroup linked in entityManager.GetBuffer<LinkedEntityGroup>(entity))
                {
                    Entity entity2 = linked.Value;
                    ecb.RemoveComponent<Disabled>(entity2);
                }
                countUsing++;
            }
            else
            {
                entity = ecb.Instantiate(bulletEntityPrefab);
                ecb.AddComponent(entity,bulletInfo);
                
            }
            ecb.AddComponent(entity,lt);
        }
    }
}
