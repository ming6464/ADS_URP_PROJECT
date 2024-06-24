using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup)),UpdateAfter(typeof(PlayerSpawnSystem))]
[BurstCompile]
public partial struct WeaponSystem : ISystem
{
    private Entity _weaponEntityInstantiate;
    private EntityManager _entityManager;
    private Entity _entityPlayer;
    private Entity _bulletEntityPrefab;
    private WeaponProperties _weaponProperties;
    private float _timeLatest;
    
    private bool _isSpawnDefault;
    private bool _isGetComponent;
    private bool _shootAuto;
    private bool _pullTrigger;
    private int _idCurrentWeapon;
    private float3 _offset;
    private float _cooldown;
    private int _bulletPerShot;
    private float _damage;
    private float _speed;
    private float _spaceAnglePerBullet;
    private bool _parallelOrbit;

    private bool _isNewWeapon;
    private NativeArray<WeaponStore> _weaponStores;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInput>();
        state.RequireForUpdate<WeaponProperties>();
        state.RequireForUpdate<PlayerInfo>();
        state.RequireForUpdate<CharacterInfo>();
        _isSpawnDefault = false;
        _isGetComponent = false;
        _idCurrentWeapon = -1;
    }


    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        
        if (!_isSpawnDefault)
        {
            var entityWeaponProperties = SystemAPI.GetSingletonEntity<WeaponProperties>();
            _weaponProperties = SystemAPI.GetSingleton<WeaponProperties>();
            _weaponStores = SystemAPI.GetBuffer<WeaponStore>(entityWeaponProperties).ToNativeArray(Allocator.Persistent);
            _entityPlayer = SystemAPI.GetSingletonEntity<PlayerInfo>();
            _shootAuto = _weaponProperties.shootAuto;
            _pullTrigger = _shootAuto;
            _entityManager = state.EntityManager;
            UpdateDataWeapon(ref state,ref ecb);
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            _isSpawnDefault = true;
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
        UpdateDataWeapon(ref state,ref ecb);
        UpdateWeapon(ref state,ref ecb);
        ecb.Playback(_entityManager);
        ecb.Dispose();
        Shot(ref state);
    }

    private void UpdateDataWeapon(ref SystemState state,ref EntityCommandBuffer ecb)
    {
        if (!_isSpawnDefault)
        {
            int getId = SystemAPI.GetSingleton<PlayerInfo>().idWeapon;
            if (_idCurrentWeapon != getId)
            {
                ChangeWeapon(getId,ref ecb);
            }
        }
        else
        {
            foreach(var (collect,entity) in SystemAPI.Query<RefRO<ItemCollection>>().WithEntityAccess().WithNone<Disabled,SetActiveSP>())
            {
                switch (collect.ValueRO.type)
                {
                    case ItemType.Weapon:
                        ecb.AddComponent(entity,new SetActiveSP()
                        {
                            state = StateID.Disable,
                        });
                        if(_idCurrentWeapon == collect.ValueRO.id) continue;
                        ChangeWeapon(collect.ValueRO.id,ref ecb);
                        break;
                }
           
            }
        }
    }
    private void ChangeWeapon(int id,ref EntityCommandBuffer ecb)
    {
        _idCurrentWeapon = id;
        var weapon = GetWeapon(id);
        _offset = weapon.offset;
        _bulletPerShot = weapon.bulletPerShot;
        _cooldown = weapon.cooldown;
        _timeLatest = -_cooldown;
        _weaponEntityInstantiate = weapon.entity;
        _spaceAnglePerBullet = weapon.spaceAnglePerBullet;
        _parallelOrbit = weapon.parallelOrbit;
        _damage = weapon.damage;
        _speed = weapon.speed;
        _isNewWeapon = true;
    }
    private WeaponStore GetWeapon(int id)
    {
        WeaponStore weaponStore = new WeaponStore();

        foreach (var ws in _weaponStores)
        {
            if(ws.id != id) continue;
            weaponStore = ws;
        }
        
        return weaponStore;
    }

    private void UpdateWeapon(ref SystemState state,ref EntityCommandBuffer ecb)
    {
        if (_isNewWeapon)
        {
            foreach (var (wpInfo,parent, entity) in SystemAPI.Query<RefRO<WeaponInfo>,RefRO<Parent>>().WithEntityAccess()
                         .WithNone<Disabled,SetActiveSP>())
            {
                Entity weaponEntity = _entityManager.Instantiate(_weaponEntityInstantiate);
                ecb.AddComponent(weaponEntity, new Parent() { Value = parent.ValueRO.Value });
                ecb.AddComponent(weaponEntity, new LocalTransform() { Position = _offset, Rotation = quaternion.identity, Scale = 1 });
                ecb.AddComponent(weaponEntity,new WeaponInfo()
                {
                    id = _idCurrentWeapon,
                });
                var entityGroup = _entityManager.GetBuffer<LinkedEntityGroup>(weaponEntity).ToNativeArray(Allocator.Temp);
                _entityManager.GetBuffer<LinkedEntityGroup>(parent.ValueRO.Value).AddRange(entityGroup);
                entityGroup.Dispose();
                
                ecb.RemoveComponent<Parent>(entity);
                ecb.AddComponent(entity,new SetActiveSP()
                {
                    state = StateID.Disable,
                });
            }
            _isNewWeapon = false;
        }
        
        var buffer = _entityManager.GetBuffer<CharacterNewBuffer>(_entityPlayer);
        if(buffer.Length == 0) return;
        foreach (var b in buffer)
        {
            Entity weaponEntity = _entityManager.Instantiate(_weaponEntityInstantiate);
            ecb.AddComponent(weaponEntity, new Parent() { Value = b.entity });
            ecb.AddComponent(weaponEntity, new LocalTransform() { Position = _offset, Rotation = quaternion.identity, Scale = 1 });
            ecb.AddComponent(weaponEntity,new WeaponInfo()
            {
                id = _idCurrentWeapon,
            });
            var entityGroup = _entityManager.GetBuffer<LinkedEntityGroup>(weaponEntity).ToNativeArray(Allocator.Temp);
            _entityManager.GetBuffer<LinkedEntityGroup>(b.entity).AddRange(entityGroup);
            entityGroup.Dispose();
        }
        buffer.Clear();
    }

    private void Shot(ref SystemState state)
    {
        if(!_pullTrigger) return;
        if ((SystemAPI.Time.ElapsedTime - _timeLatest) < _cooldown) return;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        var entityManager = state.EntityManager;
        var entitiesDisable = SystemAPI.QueryBuilder().WithAll<BulletInfo>().WithAll<Disabled>().Build().ToEntityArray(Allocator.Temp);
        var bulletEntityPrefab = _bulletEntityPrefab;
        float spaceAngleAnyBullet = _spaceAnglePerBullet;
        float time = (float)(SystemAPI.Time.ElapsedTime);
        BulletInfo bulletInfo = new BulletInfo { startTime = time, damage = _damage, speed = _speed};
        byte countUsing = 0;
        LocalTransform lt;
        float3 angleRota;
        
        foreach (var weaponAspect in SystemAPI.Query<WeaponAspect>())
        {
            float subtractIndex = 0.5f;
            int halfNumberPreShot = (int)math.ceil(_bulletPerShot / 2f);
            lt = new LocalTransform()
            {
                Position = weaponAspect.PositionWorld,
                Rotation = weaponAspect.RotationWorld,
                Scale = 1,
            };
            angleRota = MathExt.QuaternionToFloat3(lt.Rotation);
            if (halfNumberPreShot % 2 != 0)
            {
                SpawnBullet_L(lt);
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
                SpawnBullet_L(lt);
                angleRotaNew.y = angle2;
                lt.Rotation = MathExt.Float3ToQuaternion(angleRotaNew);
                SpawnBullet_L(lt);
            }
        }
        
        ecb.Playback(entityManager);
        _timeLatest = time;
        entitiesDisable.Dispose();
        
        void SpawnBullet_L(LocalTransform lt)
        {
            Entity entity;
            if (countUsing < entitiesDisable.Length)
            {
                entity = entitiesDisable[countUsing];
                ecb.AddComponent(entity,lt);
                ecb.AddComponent(entity, new SetActiveSP()
                {
                    state = StateID.Enable,
                    startTime = time,
                });
                ecb.RemoveComponent<Disabled>(entity);
                countUsing++;
            }
            else
            {
                entity = ecb.Instantiate(bulletEntityPrefab);
                ecb.AddComponent(entity,lt);
            }
            ecb.AddComponent(entity,bulletInfo);
            
            
        }
    }
}
