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
    private PlayerProperty _playerProperties;
    private WeaponRunTime _weaponRunTime;

    private WeaponAspect _weaponAspect;
    private bool _isSpawn;
    private bool _isGetComponent;
    
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WeaponRunTime>();
        state.RequireForUpdate<PlayerProperty>();
        state.RequireForUpdate<WeaponProperties>();
        _isSpawn = false;
        _isGetComponent = false;
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!_isSpawn)
        {
            var entity = SystemAPI.GetSingletonEntity<WeaponProperties>();
            _weaponProperties = SystemAPI.GetComponentRO<WeaponProperties>(entity).ValueRO;
            Entity entityInstantiate = _weaponProperties.entityWeapon;
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            Entity weaponEntity = ecb.Instantiate(entityInstantiate);
            ecb.AddComponent<LocalTransform>(weaponEntity);
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
        
        _weaponAspect = SystemAPI.GetAspect<WeaponAspect>(_weaponEntitySingleton);
        _playerProperties = SystemAPI.GetSingleton<PlayerProperty>();
        
        
        UpdateTransform(ref state);
        Shot(ref state);
    }
    private void UpdateTransform(ref SystemState state)
    {
        _weaponAspect.Position = _playerProperties.worldPosition + _weaponProperties.offset;
        _weaponAspect.Rotation = _playerProperties.rotation;
    }
    

    private void Shot(ref SystemState state)
    {
        if((SystemAPI.Time.ElapsedTime - _weaponRunTime.timeLatest) < _weaponRunTime.cooldown) return;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        int halfNumberPreShot = (int)math.ceil(_weaponProperties.bulletPerShot / 2f);

        LocalTransform  lt = new LocalTransform()
        {
            Position = _weaponAspect.Position,
            Rotation = _weaponAspect.Rotation,
            Scale = 1,
        };
        Entity newBulletEntity;
        float3 angleRota = MathExt.QuaternionToFloat3(lt.Rotation);
        float spaceAngleAnyBullet = _weaponProperties.spaceAngleAnyBullet;
        float subtractIndex = 0.5f;
        if (halfNumberPreShot % 2 != 0)
        {
            --halfNumberPreShot;
            newBulletEntity = ecb.Instantiate(_bulletEntityPrefab);
            ecb.AddComponent<BulletInfo>(newBulletEntity);
            ecb.AddComponent(newBulletEntity, lt);
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
            newBulletEntity = ecb.Instantiate(_bulletEntityPrefab);
            ecb.AddComponent<BulletInfo>(newBulletEntity);
            ecb.AddComponent(newBulletEntity, lt);

            angleRotaNew.y = angle2;
            lt.Rotation = MathExt.Float3ToQuaternion(angleRotaNew);
            newBulletEntity = ecb.Instantiate(_bulletEntityPrefab);
            ecb.AddComponent<BulletInfo>(newBulletEntity);
            ecb.AddComponent(newBulletEntity, lt);
            
        } 
        ecb.Playback(state.EntityManager);
        _weaponRunTime.timeLatest = (float)SystemAPI.Time.ElapsedTime;
    }
}

