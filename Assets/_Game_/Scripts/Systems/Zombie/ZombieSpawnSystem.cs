using NUnit.Framework.Internal;
using Rukhanka;
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
    private int _numberSpawnPerFrame;
    private LocalTransform _localTransformDefault;
    private ZombieProperty _zombieProperties;
    private ZombieSpawner _zombieSpawner;
    private EntityTypeHandle _entityTypeHandle;
    private NativeArray<BufferZombieStore> _zombieStores;
    private NativeArray<BufferZombieNormalSpawnID> _zombieNormalSpawnIds;
    private NativeList<BufferZombieBossSpawn> _zombieBossSpawns;
    private NativeArray<BufferZombieSpawnRange> _zombieSpawnRanges;
    private EntityManager _entityManager;
    private EntityQuery _enQueryZombieNotUnique;
    private EntityQuery _enQueryZombieNew;
    private int _totalSpawnCount;
    private ComponentTypeHandle<PhysicsCollider> _physicsColliderTypeHandle;
    
    #region OnCreate

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        RequireNecessaryComponents(ref state);
        Init(ref state);
    }
    [BurstCompile]
    private void Init(ref SystemState state)
    {
        _entityTypeHandle = state.GetEntityTypeHandle();
        _enQueryZombieNotUnique = SystemAPI.QueryBuilder().WithAll<ZombieInfo, NotUnique>().WithNone<Disabled,SetActiveSP>().Build();
        _enQueryZombieNew = SystemAPI.QueryBuilder().WithAll<ZombieInfo, New>().WithNone<Disabled, SetActiveSP>()
            .Build();
    }

    [BurstCompile]
    private void RequireNecessaryComponents(ref SystemState state)
    {
        _physicsColliderTypeHandle = state.GetComponentTypeHandle<PhysicsCollider>();
        state.RequireForUpdate<ZombieProperty>();
        state.RequireForUpdate<ZombieSpawner>();
        state.RequireForUpdate<DataProperty>();
    }

    #endregion

    #region Destroy
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_zombieStores.IsCreated)
            _zombieStores.Dispose();
        if (_zombieNormalSpawnIds.IsCreated)
            _zombieNormalSpawnIds.Dispose();
        if (_zombieBossSpawns.IsCreated)
            _zombieBossSpawns.Dispose();
        if (_zombieSpawnRanges.IsCreated)
            _zombieSpawnRanges.Dispose();
    }

    #endregion

    #region Update

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _entityManager = state.EntityManager;
        CheckAndInit(ref state);
        UpdateNumberSpawn(ref state);
        HandleNewZombie(ref state);
        CheckAndSpawnZombie(ref state);
        HandleZombieUniquePhysic(ref state);
    }

    [BurstCompile]
    private void HandleZombieUniquePhysic(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        _entityTypeHandle.Update(ref state);
        _physicsColliderTypeHandle.Update(ref state);
        var uniquePhysic = new UniquePhysicColliderJOB()
        {
            ecb = ecb.AsParallelWriter(),
            entityTypeHandle = _entityTypeHandle,
            physicColliderTypeHandle = _physicsColliderTypeHandle,
        };
        state.Dependency = uniquePhysic.ScheduleParallel(_enQueryZombieNotUnique, state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(_entityManager);
        ecb.Dispose();
    }
    
    [BurstCompile]
    private void UpdateNumberSpawn(ref SystemState state)
    {
        var ratioTime = math.clamp((float) SystemAPI.Time.ElapsedTime, _zombieSpawner.timeRange.x, _zombieSpawner.timeRange.y);
        _numberSpawnPerFrame = (int)math.remap(_zombieSpawner.timeRange.x, _zombieSpawner.timeRange.y, _zombieSpawner.spawnAmountRange.x,
            _zombieSpawner.spawnAmountRange.y, ratioTime);
    }
    
    [BurstCompile]
    private void CheckAndInit(ref SystemState state)
    {
        if (_isInit) return;
        _isInit = true;
        _zombieProperties = SystemAPI.GetSingleton<ZombieProperty>();
        _zombieSpawner = SystemAPI.GetSingleton<ZombieSpawner>();
        _localTransformDefault = DotsEX.LocalTransformDefault();
        _latestSpawnTime = _zombieSpawner.cooldown;
        
        //Get data
        _zombieNormalSpawnIds = _entityManager.GetBuffer<BufferZombieNormalSpawnID>(_zombieProperties.entity).ToNativeArray(Allocator.Persistent);
        var nativeArr = _entityManager.GetBuffer<BufferZombieBossSpawn>(_zombieProperties.entity)
            .ToNativeArray(Allocator.Persistent);
        _zombieBossSpawns = new NativeList<BufferZombieBossSpawn>(Allocator.Persistent);
        _zombieBossSpawns.AddRange(nativeArr);
        nativeArr.Dispose();
        _zombieSpawnRanges = _entityManager.GetBuffer<BufferZombieSpawnRange>(_zombieProperties.entity)
            .ToNativeArray(Allocator.Persistent);
        _zombieStores = SystemAPI.GetSingletonBuffer<BufferZombieStore>().ToNativeArray(Allocator.Persistent);
    }
    
    [BurstCompile]
    private ZombieInfo GetZombieInfo(BufferZombieStore data,float3 directNormal)
    {
        return new ZombieInfo()
        {
            id = data.id,
            hp = data.hp,
            speed = data.speed,
            damage = data.damage,
            attackRange = data.attackRange,
            delayAttack = data.delayAttack,
            directNormal = directNormal,
            chasingRange = data.chasingRange,
        };
    }
    
    [BurstCompile]
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
    
    [BurstCompile]
    private void CheckAndSpawnZombie(ref SystemState state)
    {
        CheckAndSpawnNormalZombie(ref state);
        CheckAndSpawnBossZombie(ref state);
    }
    
    [BurstCompile]
    private void CheckAndSpawnBossZombie(ref SystemState state)
    {
        return;
        if (!_zombieBossSpawns.IsCreated) return;
        float time = (float) SystemAPI.Time.ElapsedTime;
        var zomRunTime = new ZombieRuntime()
        {
            latestTimeAttack = (float)SystemAPI.Time.ElapsedTime,
        };
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        var list = new NativeList<int>(Allocator.Temp);
        for(int i = 0; i < _zombieBossSpawns.Length; i ++)
        {
            var boss = _zombieBossSpawns[i];
            if(boss.timeDelay > time)continue;
            _localTransformDefault.Position = boss.position;
            BufferZombieStore zombieDataRandom = GetZombieData(boss.id);
            Entity entityNew = ecb.Instantiate(zombieDataRandom.entity);
            ecb.AddComponent<NotUnique>(entityNew);
            ecb.AddComponent(entityNew,_localTransformDefault);
            list.Add(i);
            ecb.AddComponent(entityNew, zomRunTime);
            ecb.AddComponent(entityNew,GetZombieInfo(zombieDataRandom,boss.directNormal));
            ecb.AddComponent<New>(entityNew);
            ecb.AddComponent(entityNew,new BossInfo());
            _zombieBossSpawns.Clear();
            break;
        }
        ecb.Playback(_entityManager);
        ecb.Dispose();
        list.Dispose();
    }

    [BurstCompile]
    private void CheckAndSpawnNormalZombie(ref SystemState state)
    {
        if (SystemAPI.Time.ElapsedTime - _latestSpawnTime >= _zombieSpawner.cooldown)
        {
            var zombieSpawnRuntime = _entityManager.GetComponentData<ZombieSpawnRuntime>(_zombieProperties.entity);
            int numberZombieSet = _totalSpawnCount;
            if (_zombieSpawner.allowRespawn)
            {
                numberZombieSet = zombieSpawnRuntime.zombieAlive;
            }

            if (!_zombieSpawner.spawnInfinity && numberZombieSet >= _zombieSpawner.totalNumber)return;
            int numberZombieCanSpawn = _zombieSpawner.spawnInfinity ? _numberSpawnPerFrame : (math.min(_zombieSpawner.totalNumber - numberZombieSet,_numberSpawnPerFrame));
            int lengthRange = _zombieSpawnRanges.Length;
            int numberSpawnPerRange = numberZombieCanSpawn / lengthRange;
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            for (int i = 0; i < lengthRange; i++)
            {
                var range = _zombieSpawnRanges[i];
                int numberSpawn = numberSpawnPerRange;
                if (lengthRange - i == 1)
                {
                    numberSpawn = numberZombieCanSpawn - lengthRange * i;
                }
                zombieSpawnRuntime.zombieAlive += numberSpawn;
                SpawnNormalZombie(ref state, ref ecb, range,numberSpawn);
            }
            _latestSpawnTime = (float)SystemAPI.Time.ElapsedTime;
            _totalSpawnCount += numberZombieCanSpawn;
            ecb.AddComponent(_zombieProperties.entity,zombieSpawnRuntime);
            ecb.Playback(_entityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    private void SpawnNormalZombie(ref SystemState state, ref EntityCommandBuffer ecb, BufferZombieSpawnRange range,int numberSpawn)
    {
        DynamicBuffer<BufferZombieDie> bufferZombieDie = _entityManager.GetBuffer<BufferZombieDie>(_zombieProperties.entity);
        var zomRunTime = new ZombieRuntime()
        {
            latestTimeAttack = (float)SystemAPI.Time.ElapsedTime,
        };
        for (int j = 0; j < numberSpawn; j++)
        {
            _localTransformDefault.Position = Random.CreateFromIndex((uint)(_seedCount * 1.5f)).GetRandomRange(range.posMin, range.posMax);
            BufferZombieStore zombieDataRandom = GetZombieData(GetRandomZombieNormalID(ref state));
            Entity entityNew = default;
            bool checkGetFromPool = GetZombieToPool(ref state,ref bufferZombieDie,ref entityNew,zombieDataRandom.id);
            
            if (checkGetFromPool)
            {
                ecb.AddComponent(entityNew,_localTransformDefault);
                ecb.RemoveComponent<Disabled>(entityNew);
                ecb.AddComponent(entityNew, new SetActiveSP()
                {
                    state = DisableID.Enable,
                });
                ecb.AddComponent(entityNew, new SetAnimationSP()
                {
                    state = StateID.Enable
                });
            }
            else
            {
                entityNew = ecb.Instantiate(zombieDataRandom.entity);
                ecb.AddComponent<NotUnique>(entityNew);
                ecb.AddComponent(entityNew,_localTransformDefault);
            }
                
            ecb.AddComponent(entityNew, zomRunTime);
            ecb.AddComponent(entityNew,GetZombieInfo(zombieDataRandom,range.directNormal));
            ecb.AddComponent<New>(entityNew);
        }
    }
    
    [BurstCompile]
    private bool GetZombieToPool(ref SystemState state, ref DynamicBuffer<BufferZombieDie> bufferZombieDie,ref Entity entity, int id)
    {
        for (int k = 0; k < bufferZombieDie.Length; k++)
        {
            if (id == bufferZombieDie[k].id)
            {
                entity = bufferZombieDie[k].entity;
                bufferZombieDie.RemoveAt(k);
                return true;
            }
        }

        return false;
    }
    [BurstCompile]
    private int GetRandomZombieNormalID(ref SystemState state)
    {
        var random = Random.CreateFromIndex((uint)(++_seedCount));
        int index = random.NextInt(0, _zombieNormalSpawnIds.Length);
        return _zombieNormalSpawnIds[index].id;
    }
    [BurstCompile]
    private void HandleNewZombie(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        _entityTypeHandle.Update(ref state);
        var job = new HandleNewZombieJOB()
        {
            entityTypeHandle = _entityTypeHandle,
            ecb = ecb.AsParallelWriter()
        };
        state.Dependency = job.ScheduleParallel(_enQueryZombieNew, state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(_entityManager);
        ecb.Dispose();
    }
    
    [BurstCompile]
    partial struct HandleNewZombieJOB : IJobChunk
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        public EntityTypeHandle entityTypeHandle;
        
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(entityTypeHandle);
            for (int i = 0; i < chunk.Count; i++)
            {
                ecb.RemoveComponent<New>(unfilteredChunkIndex,entities[i]);
            }
        }
    }
    
    [BurstCompile]
    partial struct UniquePhysicColliderJOB : IJobChunk
    {
        [WriteOnly] public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public EntityTypeHandle entityTypeHandle;
        public ComponentTypeHandle<PhysicsCollider> physicColliderTypeHandle;
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var physicColliders = chunk.GetNativeArray(ref physicColliderTypeHandle);
            for (int i = 0; i < chunk.Count; i++)
            {
                var collider = physicColliders[i];
                collider.Value = collider.Value.Value.Clone();
                physicColliders[i] = collider;
            
            }
            ecb.RemoveComponent<NotUnique>(unfilteredChunkIndex,chunk.GetNativeArray(entityTypeHandle));
        }
    }

    #endregion
}