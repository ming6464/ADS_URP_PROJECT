using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[BurstCompile]
public partial struct ZombieSpawnSystem : ISystem
{
    private int _seedCount;
    private float _latestSpawnTime;
    private bool _isInit;
    private bool _isSpawnInfinity;
    private bool _isAllowRespawn;
    private int _numberSpawn;
    private int _numberSpawnPerFrame;
    private float2 _spawnRange;
    private float _timeDelay;
    private float _tímeSpawn;
    private LocalTransform _localTransform;
    private float3 _pointRandomMin;
    private float3 _pointRandomMax;
    private Entity _entityZombieProperty;
    private ZombieProperty _zombieProperties;
    private EntityTypeHandle _entityTypeHandle;
    private int _totalSpawnCount;
    private int _numberZombieAlive;
    private NativeArray<BufferZombieStore> _zombieStores;
    private NativeArray<SpawnData> _spawnDataArray;
    private bool _applyTotalCount;
    private EntityManager _entityManager;
    private EntityQuery _enQueryZombieNotUnique;
    
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _isInit = false;
        state.RequireForUpdate<ZombieProperty>();
        _totalSpawnCount = 0;
        _entityTypeHandle = state.GetEntityTypeHandle();
        _enQueryZombieNotUnique = SystemAPI.QueryBuilder().WithAll<ZombieInfo, NotUnique>().WithNone<Disabled>()
            .WithNone<SetActiveSP>().Build();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_zombieStores.IsCreated)
            _zombieStores.Dispose();
        if (_spawnDataArray.IsCreated)
            _spawnDataArray.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _entityManager = state.EntityManager;
        if (!_isInit)
        {
            _entityZombieProperty = SystemAPI.GetSingletonEntity<ZombieProperty>();
            _zombieProperties = SystemAPI.GetComponentRO<ZombieProperty>(_entityZombieProperty).ValueRO;
            _localTransform = DotsEX.LocalTransformDefault();
            _pointRandomMin = _zombieProperties.spawner.posMin;
            _pointRandomMax = _zombieProperties.spawner.posMax;
            _latestSpawnTime = -_zombieProperties.spawner.timeDelay;
            _isSpawnInfinity = _zombieProperties.spawner.spawnInfinity;
            _isAllowRespawn = _zombieProperties.spawner.allowRespawn;
            _spawnRange = _zombieProperties.spawner.numberSpawnPerFrameRange;
            _timeDelay = _zombieProperties.spawner.timeDelay;
            _tímeSpawn = _zombieProperties.spawner.timeSpawn;
            _zombieStores = SystemAPI.GetBuffer<BufferZombieStore>(_entityZombieProperty).ToNativeArray(Allocator.Persistent);
            _isInit = true;
            _applyTotalCount = _zombieProperties.applyTotalCount;
            if (_applyTotalCount)
            {
                _numberSpawn = _zombieProperties.spawner.numberSpawn;
            }
            else
            {
                foreach (var store in _zombieStores)
                {
                    _numberSpawn += store.numberSpawn;
                }
            }

            _spawnDataArray = new NativeArray<SpawnData>(_zombieStores.Length,Allocator.Persistent);

        }

        _numberSpawnPerFrame =
            (int) math.lerp(_spawnRange.x, _spawnRange.y, math.clamp(SystemAPI.Time.ElapsedTime / _tímeSpawn,0,1));
        
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        bool hasEntityNew = false;
        SpawnZombie(ref state,ref ecb,ref hasEntityNew);
        ecb.Playback(_entityManager);
        ecb.Dispose();
        if (hasEntityNew)
        {
            _entityTypeHandle.Update(ref state);
            ecb = new EntityCommandBuffer(Allocator.TempJob);
            var uniquePhysic = new UniquePhysicColliderJOB()
            {
                ecb = ecb.AsParallelWriter(),
                entityTypeHandle = _entityTypeHandle,
                physicColliderTypeHandle = state.GetComponentTypeHandle<PhysicsCollider>(),
            };
            state.Dependency = uniquePhysic.ScheduleParallel(_enQueryZombieNotUnique, state.Dependency);
            state.Dependency.Complete();
            ecb.Playback(_entityManager);
            ecb.Dispose();
        }
    }

    private ZombieInfo GetZombieInfo(int id)
    {
        var data = GetZombieData(id);
        return new ZombieInfo()
        {
            id = data.id,
            hp = data.hp,
            speed = data.speed,
            damage = data.damage,
            attackRange = data.attackRange,
            delayAttack = data.delayAttack,
            directNormal = _zombieProperties.directNormal,
        };
    }
    private BufferZombieStore GetZombieData(int id)
    {
        foreach (var zombie in _zombieStores)
        {
            if (zombie.id == id) return zombie;
        }

        return new BufferZombieStore()
        {
            id = id - 1,
        };
    }
    
    private void SpawnZombie(ref SystemState state,ref EntityCommandBuffer ecb,ref bool hasNewEntity)
    {
        if((SystemAPI.Time.ElapsedTime - _latestSpawnTime) < _timeDelay) return;
        int numberZombieSet = _totalSpawnCount;
        DynamicBuffer<BufferZombieDie> bufferZombieDie = _entityManager.GetBuffer<BufferZombieDie>(_entityZombieProperty);

        var aliveZombie = SystemAPI.QueryBuilder().WithAll<ZombieInfo>().WithNone<Disabled, SetActiveSP>().Build().ToEntityArray(Allocator.Temp);
        aliveZombie.Dispose();
        if (_isAllowRespawn)
        {
            _numberZombieAlive -= bufferZombieDie.Length;
            numberZombieSet = _numberZombieAlive;
        }
        if(!_isSpawnInfinity && numberZombieSet >= _numberSpawn) return;
        int numberZombieCanSpawn = _isSpawnInfinity ? _numberSpawnPerFrame : (math.min(_numberSpawn - numberZombieSet,_numberSpawnPerFrame));
        var zomRunTime = new ZombieRuntime()
        {
            latestTimeAttack = (float)SystemAPI.Time.ElapsedTime,
        };
        int i = 0;
        while (i < numberZombieCanSpawn)
        {
            Random random = Random.CreateFromIndex((uint)(_seedCount * 1.5f));
            _localTransform.Position = random.GetRandomRange(_pointRandomMin, _pointRandomMax);
            int idZombieRandom;
            BufferZombieStore zombieDataRandom;
            int checkOverFlow = 0;
            do
            {
                random = Random.CreateFromIndex((uint)(++_seedCount));
                idZombieRandom = _zombieStores[random.NextInt(0,_zombieStores.Length)].id;
                zombieDataRandom = GetZombieData(idZombieRandom);
                checkOverFlow++;
                if (checkOverFlow == 10)
                {
                    return;
                }
            }
            while(idZombieRandom != zombieDataRandom.id);
            Entity entityNew = default;
            bool checkGetFromPool = false;
            for (int j = 0; j < bufferZombieDie.Length; j++)
            {
                if (idZombieRandom == bufferZombieDie[j].id)
                {
                    entityNew = bufferZombieDie[j].entity;
                    checkGetFromPool = true;
                    bufferZombieDie.RemoveAt(j);
                }
            }
            
            if (checkGetFromPool)
            {
                ecb.AddComponent(entityNew,_localTransform);
                ecb.RemoveComponent<Disabled>(entityNew);
                ecb.AddComponent(entityNew, new SetActiveSP()
                {
                    state = StateID.Enable,
                });
            }
            else
            {
                entityNew = ecb.Instantiate(zombieDataRandom.entity);
                ecb.AddComponent<NotUnique>(entityNew);
                ecb.AddComponent(entityNew,_localTransform);
                hasNewEntity = true;
            }

            var zombieInfo = GetZombieInfo(idZombieRandom);
            zombieInfo.directNormal = _zombieProperties.directNormal;
            ecb.AddComponent(entityNew, zomRunTime);
            ecb.AddComponent(entityNew,zombieInfo);
            
            i++;
        }
        _latestSpawnTime = (float)SystemAPI.Time.ElapsedTime;
        _totalSpawnCount += numberZombieCanSpawn;
        _numberZombieAlive += numberZombieCanSpawn;
    }
}

[BurstCompile]
public partial struct UniquePhysicColliderJOB : IJobChunk
{
    [WriteOnly] public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public EntityTypeHandle entityTypeHandle;
    public ComponentTypeHandle<PhysicsCollider> physicColliderTypeHandle;
    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var physicColliders = chunk.GetNativeArray(physicColliderTypeHandle);
        for (int i = 0; i < chunk.Count; i++)
        {
            var collider = physicColliders[i];
            collider.Value = collider.Value.Value.Clone();
            physicColliders[i] = collider;
            
        }
        ecb.RemoveComponent<NotUnique>(unfilteredChunkIndex,chunk.GetNativeArray(entityTypeHandle));
    }
}