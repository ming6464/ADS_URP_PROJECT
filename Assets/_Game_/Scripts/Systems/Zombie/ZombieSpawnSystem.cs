﻿using Unity.Burst;
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
    private float2 _spawnAmountRange;
    private float _timeDelay;
    private float2 _timeRangeTimeRange;
    private LocalTransform _localTransform;
    private float3 _pointRandomMin;
    private float3 _pointRandomMax;
    private Entity _entityZombieProperty;
    private ZombieProperty _zombieProperties;
    private EntityTypeHandle _entityTypeHandle;
    private int _totalSpawnCount;
    private NativeArray<BufferZombieStore> _zombieStores;
    private EntityManager _entityManager;
    private EntityQuery _enQueryZombieNotUnique;
    private EntityQuery _enQueryZombieNew;
    
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _isInit = false;
        state.RequireForUpdate<ZombieProperty>();
        _totalSpawnCount = 0;
        _entityTypeHandle = state.GetEntityTypeHandle();
        _enQueryZombieNotUnique = SystemAPI.QueryBuilder().WithAll<ZombieInfo, NotUnique>().WithNone<Disabled,SetActiveSP>().Build();
        _enQueryZombieNew = SystemAPI.QueryBuilder().WithAll<ZombieInfo, New>().WithNone<Disabled, SetActiveSP>()
            .Build();
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_zombieStores.IsCreated)
            _zombieStores.Dispose();
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
            _numberSpawn = _zombieProperties.spawner.numberSpawn;
            _spawnAmountRange = _zombieProperties.spawner.spawnAmountRange;
            _timeDelay = _zombieProperties.spawner.timeDelay;
            _timeRangeTimeRange = _zombieProperties.spawner.timeRange;
            _zombieStores = SystemAPI.GetBuffer<BufferZombieStore>(_entityZombieProperty).ToNativeArray(Allocator.Persistent);
            _isInit = true;
        }

        var ratioTime = math.clamp((float) SystemAPI.Time.ElapsedTime, _timeRangeTimeRange.x, _timeRangeTimeRange.y);
        _numberSpawnPerFrame = (int)math.remap(_timeRangeTimeRange.x, _timeRangeTimeRange.y, _spawnAmountRange.x,
            _spawnAmountRange.y, ratioTime);
        
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        bool hasEntityNew = false;
        HandleNewZombie(ref state, ref ecb);
        SpawnZombie(ref state,ref ecb,ref hasEntityNew);
        ecb.Playback(_entityManager);
        ecb.Dispose();
        if (hasEntityNew)
        {
            ecb = new EntityCommandBuffer(Allocator.TempJob);
            _entityTypeHandle.Update(ref state);
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
            chasingRange = data.chasingRange,
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
        if ((SystemAPI.Time.ElapsedTime - _latestSpawnTime) < _timeDelay)return;
        _entityTypeHandle.Update(ref state);
        int numberZombieSet = _totalSpawnCount;
        DynamicBuffer<BufferZombieDie> bufferZombieDie = _entityManager.GetBuffer<BufferZombieDie>(_entityZombieProperty);

        
        if (_isAllowRespawn)
        {
            var aliveZombie = SystemAPI.QueryBuilder().WithAll<ZombieInfo>().WithNone<Disabled, SetActiveSP>().Build().ToEntityArray(Allocator.Temp);
            numberZombieSet = aliveZombie.Length;
            aliveZombie.Dispose();
           
        }

        if (!_isSpawnInfinity && numberZombieSet >= _numberSpawn)return;
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
            
            random = Random.CreateFromIndex((uint)(++_seedCount));
            int idZombieRandom = _zombieStores[random.NextInt(0,_zombieStores.Length)].id;
            BufferZombieStore zombieDataRandom = GetZombieData(idZombieRandom);
            
            Entity entityNew = default;
            bool checkGetFromPool = false;
            for (int j = 0; j < bufferZombieDie.Length; j++)
            {
                if (idZombieRandom == bufferZombieDie[j].id)
                {
                    entityNew = bufferZombieDie[j].entity;
                    checkGetFromPool = true;
                    bufferZombieDie.RemoveAt(j);
                    break;
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
            ecb.AddComponent<New>(entityNew);
            i++;
        }
        _latestSpawnTime = (float)SystemAPI.Time.ElapsedTime;
        _totalSpawnCount += numberZombieCanSpawn;
    }
    
    private void HandleNewZombie(ref SystemState state, ref EntityCommandBuffer ecb)
    {
        _entityTypeHandle.Update(ref state);
        var job = new HandleNewZombieJOB()
        {
            entityTypeHandle = _entityTypeHandle,
            ecb = ecb.AsParallelWriter()
        };
        state.Dependency = job.ScheduleParallel(_enQueryZombieNew, state.Dependency);
        state.Dependency.Complete();
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
    
}