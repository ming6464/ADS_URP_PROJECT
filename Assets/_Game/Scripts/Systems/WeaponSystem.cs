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
        Entity newBulletEntity = ecb.Instantiate(_bulletEntityPrefab);
        ecb.AddComponent<BulletInfo>(newBulletEntity);
        ecb.AddComponent(newBulletEntity, new LocalTransform()
        {
            Position = _weaponAspect.Position,
            Rotation = _weaponAspect.Rotation,
            Scale = 1,
        });
        ecb.Playback(state.EntityManager);
        _weaponRunTime.timeLatest = (float)SystemAPI.Time.ElapsedTime;
    }
}

