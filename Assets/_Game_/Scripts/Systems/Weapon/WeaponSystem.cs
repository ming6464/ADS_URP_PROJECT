using _Game_.Scripts.Systems.Other.Obstacle;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile,UpdateInGroup(typeof(InitializationSystemGroup)),UpdateBefore(typeof(TurretSystem)),UpdateAfter(typeof(PlayerSpawnSystem))]
public partial struct WeaponSystem : ISystem
{
    private Entity _weaponEntityInstantiate;
    private EntityManager _entityManager;
    private Entity _entityPlayer;
    private Entity _entityWeaponAuthoring;
    private WeaponProperty _weaponProperties;
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
    private EntityQuery _enQueryWeapon;
    private NativeArray<BufferWeaponStore> _weaponStores;
    private NativeQueue<BufferBulletSpawner> _bulletSpawnQueue;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInput>();
        state.RequireForUpdate<WeaponProperty>();
        state.RequireForUpdate<PlayerInfo>();
        state.RequireForUpdate<CharacterInfo>();
        _bulletSpawnQueue = new NativeQueue<BufferBulletSpawner>(Allocator.Persistent);
        _enQueryWeapon = SystemAPI.QueryBuilder().WithAll<WeaponInfo>().WithNone<Disabled, SetActiveSP>().Build();
        _isSpawnDefault = false;
        _isGetComponent = false;
        _idCurrentWeapon = -1;
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_bulletSpawnQueue.IsCreated)
            _bulletSpawnQueue.Dispose();
    }


    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        
        if (!_isSpawnDefault)
        {
            _entityWeaponAuthoring = SystemAPI.GetSingletonEntity<WeaponProperty>();
            _weaponProperties = SystemAPI.GetSingleton<WeaponProperty>();
            _weaponStores = SystemAPI.GetBuffer<BufferWeaponStore>(_entityWeaponAuthoring).ToNativeArray(Allocator.Persistent);
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
            _isGetComponent = true;
        }
        if (!_shootAuto)
        {
            _pullTrigger = SystemAPI.GetSingleton<PlayerInput>().pullTrigger;
        }
        Shot(ref state);
        UpdateDataWeapon(ref state,ref ecb);
        UpdateWeapon(ref state,ref ecb);
        
        ecb.Playback(_entityManager);
        ecb.Dispose();
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
            foreach (var (collection,entity) in SystemAPI.Query<RefRO<ItemCollection>>().WithEntityAccess()
                         .WithNone<Disabled, SetActiveSP>())
            {
                switch (collection.ValueRO.type)
                {
                    case ItemType.Weapon:
                        ecb.AddComponent(entity,new SetActiveSP()
                        {
                            state = StateID.Disable,
                        });
                        if(_idCurrentWeapon == collection.ValueRO.id) continue;
                        ChangeWeapon(collection.ValueRO.id,ref ecb);
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
    private BufferWeaponStore GetWeapon(int id)
    {
        BufferWeaponStore weaponStore = new BufferWeaponStore();

        foreach (var ws in _weaponStores)
        {
            if(ws.id != id) continue;
            weaponStore = ws;
            break;
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
                var characterInfo = _entityManager.GetComponentData<CharacterInfo>(parent.ValueRO.Value);
                characterInfo.weaponEntity = weaponEntity;
                ecb.SetComponent(parent.ValueRO.Value,characterInfo);
                
                ecb.RemoveComponent<Parent>(entity);
                ecb.AddComponent(entity,new SetActiveSP()
                {
                    state = StateID.Disable,
                });
            }
            _isNewWeapon = false;
        }
        
        var buffer = _entityManager.GetBuffer<BufferCharacterNew>(_entityPlayer);
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
            var characterInfo = _entityManager.GetComponentData<CharacterInfo>(b.entity);
            characterInfo.weaponEntity = weaponEntity;
            _entityManager.SetComponentData(b.entity,characterInfo);
        }
        buffer.Clear();
    }

    private void Shot(ref SystemState state)
    {
        if(!_pullTrigger) return;
        if ((SystemAPI.Time.ElapsedTime - _timeLatest) < _cooldown) return;
        _bulletSpawnQueue.Clear();

        var job = new PutEventSpawnBulletJOB()
        {
            bulletPerShot = _bulletPerShot,
            damage = _damage,
            speed = _speed,
            spaceAnglePerBullet = _spaceAnglePerBullet,
            parallelOrbit = _parallelOrbit,
            bulletSpawnQueue = _bulletSpawnQueue.AsParallelWriter(),
            ltwComponentTypeHandle = state.GetComponentTypeHandle<LocalToWorld>()
        };
        state.Dependency = job.ScheduleParallel(_enQueryWeapon, state.Dependency);
        state.Dependency.Complete();
        _timeLatest = (float)SystemAPI.Time.ElapsedTime;
        if (_bulletSpawnQueue.Count > 0)
        {
            var bufferSpawnBullet = state.EntityManager.AddBuffer<BufferBulletSpawner>(_entityWeaponAuthoring);
            while(_bulletSpawnQueue.TryDequeue(out var queue))
            {
                bufferSpawnBullet.Add(queue);
            }
        }
    }
    
    partial struct PutEventSpawnBulletJOB : IJobChunk
    {
        public int bulletPerShot;
        public float damage;
        public float speed;
        public float spaceAnglePerBullet;
        public bool parallelOrbit;
        public NativeQueue<BufferBulletSpawner>.ParallelWriter bulletSpawnQueue;
        [ReadOnly] public ComponentTypeHandle<LocalToWorld> ltwComponentTypeHandle;
        
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var ltws = chunk.GetNativeArray(ltwComponentTypeHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                var ltw = ltws[i];
                bulletSpawnQueue.Enqueue(new BufferBulletSpawner()
                {
                    bulletPerShot = bulletPerShot,
                    damage = damage,
                    parallelOrbit = parallelOrbit,
                    speed = speed,
                    spaceAnglePerBullet = spaceAnglePerBullet,
                    lt = new LocalTransform()
                    {
                        Position = ltw.Position,
                        Rotation = ltw.Rotation,
                        Scale = 1,
                    }
                });
            }
            
        }
    }

}
