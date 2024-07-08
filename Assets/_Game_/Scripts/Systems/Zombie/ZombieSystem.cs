﻿﻿using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup)),UpdateBefore(typeof(AnimationSystem))]
[BurstCompile]
public partial struct ZombieSystem : ISystem
{
    private float3 _pointZoneMin;
    private float3 _pointZoneMax;
    private bool _init;
    private Entity _entityPlayerInfo;
    private ZombieProperty _zombieProperty;
    private EntityTypeHandle _entityTypeHandle;
    private EntityQuery _enQueryZombie;
    private EntityManager _entityManager;
    private NativeQueue<TakeDamageItem> _takeDamageQueue;
    private NativeList<CharacterSetTakeDamage> _characterSetTakeDamages;
    private NativeList<float3> _characterLtws;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInfo>();
        RequireNecessaryComponents(ref state);
        Init(ref state);
    }
    [BurstCompile]
    private void Init(ref SystemState state)
    {
        _enQueryZombie =
            SystemAPI.QueryBuilder().WithAll<ZombieInfo,LocalTransform,ZombieRuntime>().WithNone<Disabled, SetActiveSP, New>().Build();
        _takeDamageQueue = new NativeQueue<TakeDamageItem>(Allocator.Persistent);
        _characterSetTakeDamages = new NativeList<CharacterSetTakeDamage>(Allocator.Persistent);
        _characterLtws = new NativeList<float3>(Allocator.Persistent);
    }
    [BurstCompile]
    private void RequireNecessaryComponents(ref SystemState state)
    {
        state.RequireForUpdate<ZombieProperty>();
        state.RequireForUpdate<ActiveZoneProperty>();
        state.RequireForUpdate<ZombieInfo>();
    }
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_characterSetTakeDamages.IsCreated) _characterSetTakeDamages.Dispose();
        if (_takeDamageQueue.IsCreated) _takeDamageQueue.Dispose();
        if (_characterLtws.IsCreated) _characterLtws.Dispose();

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        CheckAndInit(ref state);
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        Move(ref state);
        CheckAttackPlayer(ref state,ref ecb);
        CheckZombieToDeadZone(ref state, ref ecb);
        state.Dependency.Complete();
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
    [BurstCompile]
    private void CheckAndInit(ref SystemState state)
    {
        if (!_init)
        {
            _init = true;
            var zone = SystemAPI.GetSingleton<ActiveZoneProperty>();
            _pointZoneMin = zone.pointRangeMin;
            _pointZoneMax = zone.pointRangeMax;
            _zombieProperty = SystemAPI
                .GetComponentRO<ZombieProperty>(SystemAPI.GetSingletonEntity<ZombieProperty>()).ValueRO;
            _entityPlayerInfo = SystemAPI.GetSingletonEntity<PlayerInfo>();
            _entityManager = state.EntityManager;
        }
    }
    [BurstCompile]
    private void CheckAttackPlayer(ref SystemState state,ref EntityCommandBuffer ecb)
    {
        _takeDamageQueue.Clear();
        _characterSetTakeDamages.Clear();

        foreach (var (ltw, entity) in SystemAPI.Query<RefRO<LocalToWorld>>().WithEntityAccess().WithAll<CharacterInfo>().WithNone<Disabled,SetActiveSP,New>())
        {
            _characterSetTakeDamages.Add(new CharacterSetTakeDamage()
            {
                entity = entity,
                position = ltw.ValueRO.Position
            });
        }

        var playerPosition = SystemAPI.GetComponentRO<LocalToWorld>(_entityPlayerInfo).ValueRO.Position;
        
        var job = new CheckAttackPlayerJOB()
        {
            characterSetTakeDamages = _characterSetTakeDamages,
            localToWorldTypeHandle = state.GetComponentTypeHandle<LocalToWorld>(),
            time = (float)SystemAPI.Time.ElapsedTime,
            zombieInfoTypeHandle = state.GetComponentTypeHandle<ZombieInfo>(),
            zombieRuntimeTypeHandle = state.GetComponentTypeHandle<ZombieRuntime>(),
            takeDamageQueues = _takeDamageQueue.AsParallelWriter(),
            playerPosition = playerPosition,
            distanceCheck = 10,
        };
        state.Dependency = job.ScheduleParallel(_enQueryZombie, state.Dependency);
        state.Dependency.Complete();
        
        NativeHashMap<int, float> characterTakeDamageMap =
            new NativeHashMap<int, float>(_takeDamageQueue.Count, Allocator.Temp);
        while(_takeDamageQueue.TryDequeue(out var queue))
        {
            if (characterTakeDamageMap.ContainsKey(queue.index))
            {
                characterTakeDamageMap[queue.index] += queue.damage;
            }
            else
            {
                characterTakeDamageMap.Add(queue.index,queue.damage);
            }
        }
        foreach (var map in characterTakeDamageMap)
        {
            if(map.Value == 0) continue;
            Entity entity = _characterSetTakeDamages[map.Key].entity;
            ecb.AddComponent(entity,new TakeDamage()
            {
                value = map.Value,
            });
        }
        characterTakeDamageMap.Dispose();
    }
    
    [BurstCompile]
    private void Move(ref SystemState state)
    {
        UpdateCharacterList(ref state);
        float deltaTime = SystemAPI.Time.DeltaTime;
        ZombieMovementJOB job = new ZombieMovementJOB()
        {
            deltaTime = deltaTime,
            ltTypeHandle = state.GetComponentTypeHandle<LocalTransform>(),
            zombieInfoTypeHandle = state.GetComponentTypeHandle<ZombieInfo>(true),
            characterLtws = _characterLtws,
            ltwTypeHandle = state.GetComponentTypeHandle<LocalToWorld>()
        };
        state.Dependency = job.ScheduleParallel(_enQueryZombie, state.Dependency);
        state.Dependency.Complete();
    }

    private void UpdateCharacterList(ref SystemState state)
    {
        _characterLtws.Clear();
        foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<CharacterInfo>()
                     .WithNone<Disabled, SetActiveSP>())
        {
            _characterLtws.Add(ltw.ValueRO.Position);
        }
    }

    [BurstCompile]
    private void CheckZombieToDeadZone(ref SystemState state, ref EntityCommandBuffer ecb)
    {
        _entityTypeHandle.Update(ref state);
        var chunkJob = new CheckDeadZoneJOB
        {
            ecb = ecb.AsParallelWriter(),
            entityTypeHandle = _entityTypeHandle,
            ltwTypeHandle = state.GetComponentTypeHandle<LocalToWorld>(true),
            zombieInfoTypeHandle = state.GetComponentTypeHandle<ZombieInfo>(),
            minPointRange = _pointZoneMin,
            maxPointRange = _pointZoneMax,
        };
        state.Dependency = chunkJob.ScheduleParallel(_enQueryZombie, state.Dependency);
        state.Dependency.Complete();
    }

    
    [BurstCompile]
partial struct ZombieMovementJOB : IJobChunk
{
    public ComponentTypeHandle<LocalTransform> ltTypeHandle;
    [ReadOnly] public ComponentTypeHandle<LocalToWorld> ltwTypeHandle;
    [ReadOnly] public ComponentTypeHandle<ZombieInfo> zombieInfoTypeHandle;
    [ReadOnly] public float deltaTime;
    [ReadOnly] public NativeList<float3> characterLtws;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
        in v128 chunkEnabledMask)
    {
        var lts = chunk.GetNativeArray(ref ltTypeHandle);
        var ltws = chunk.GetNativeArray(ref ltwTypeHandle);
        var zombieInfos = chunk.GetNativeArray(ref zombieInfoTypeHandle);
        for (int i = 0; i < chunk.Count; i++)
        {
            var ltw = ltws[i];
            var lt = lts[i];
            var info = zombieInfos[i];
            var direct = GetDirect(ltw.Position, info.directNormal, info.chasingRange);

            lt.Position += direct * info.speed * deltaTime;
            lt.Rotation = quaternion.LookRotationSafe(direct, math.up());
            lts[i] = lt;
        }
    }

    private float3 GetDirect(float3 position, float3 defaultDirect, float chasingRange)
    {
        var dir = defaultDirect;

        float distanceNearest = float.MaxValue; // Sử dụng float.MaxValue thay vì 99999
        float3 characterPositionNearest = default;

        foreach (var characterLtw in characterLtws)
        {
            var distance = math.distance(characterLtw, position);
            if (distance <= chasingRange && distance <= distanceNearest)
            {
                distanceNearest = distance;
                characterPositionNearest = characterLtw;
            }
        }
        
        if (distanceNearest < float.MaxValue)
        {
            if (position.ComparisionEqual(characterPositionNearest))
            {
                dir = float3.zero;
            }
            else
            {
                dir = math.normalize(characterPositionNearest - position);
            }
        }

        return dir;
    }
}

    


    [BurstCompile]
    partial struct CheckDeadZoneJOB : IJobChunk
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public EntityTypeHandle entityTypeHandle;
        [ReadOnly] public ComponentTypeHandle<LocalToWorld> ltwTypeHandle;
        public ComponentTypeHandle<ZombieInfo> zombieInfoTypeHandle;
        [ReadOnly] public float3 minPointRange;
        [ReadOnly] public float3 maxPointRange;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var ltwArr = chunk.GetNativeArray(ref ltwTypeHandle);
            var zombieInfos = chunk.GetNativeArray(ref zombieInfoTypeHandle);
            var entities = chunk.GetNativeArray(entityTypeHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                if (CheckInRange_L(ltwArr[i].Position, minPointRange, maxPointRange)) continue;
                var zombieInfo = zombieInfos[i];
                zombieInfo.hp = 0;
                zombieInfos[i] = zombieInfo;
                ecb.AddComponent(unfilteredChunkIndex, entities[i], new SetActiveSP
                {
                    state = StateID.Disable,
                });
                
                
                ecb.AddComponent(unfilteredChunkIndex, entities[i], new AddToBuffer()
                {
                    id = zombieInfo.id,
                    entity = entities[i],
                });
            }

            bool CheckInRange_L(float3 value, float3 min, float3 max)
            {
                if ((value.x - min.x) * (max.x - value.x) < 0) return false;
                if ((value.y - min.y) * (max.y - value.y) < 0) return false;
                if ((value.z - min.z) * (max.z - value.z) < 0) return false;
                return true;
            }
        }
    }
    
    [BurstCompile]
    partial struct CheckAttackPlayerJOB : IJobChunk
    {
        public ComponentTypeHandle<ZombieRuntime> zombieRuntimeTypeHandle;
        [WriteOnly] public NativeQueue<TakeDamageItem>.ParallelWriter takeDamageQueues;
        [ReadOnly] public ComponentTypeHandle<ZombieInfo> zombieInfoTypeHandle;
        [ReadOnly] public ComponentTypeHandle<LocalToWorld> localToWorldTypeHandle;
        [ReadOnly] public NativeList<CharacterSetTakeDamage> characterSetTakeDamages;
        [ReadOnly] public float time;
        [ReadOnly] public float3 playerPosition;
        [ReadOnly] public float distanceCheck;
         
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var zombieInfos = chunk.GetNativeArray(ref zombieInfoTypeHandle);
            var ltws = chunk.GetNativeArray(ref localToWorldTypeHandle);
            var zombieRuntimes = chunk.GetNativeArray(ref zombieRuntimeTypeHandle);
            for (int i = 0; i < chunk.Count; i++)
            {
                var info = zombieInfos[i];
                var ltw = ltws[i];
                var runtime = zombieRuntimes[i];
                
                if( time - runtime.latestTimeAttack < info.delayAttack) continue;
                
                if(math.distance(playerPosition,ltw.Position) > distanceCheck) continue;
                
                bool checkAttack = false;
                for (int j = 0; j < characterSetTakeDamages.Length; j++)
                {
                    var character = characterSetTakeDamages[j];
                    if (math.distance(character.position, ltw.Position) <= info.attackRange)
                    {
                        takeDamageQueues.Enqueue(new TakeDamageItem()
                        {
                            index = j,
                            damage = info.damage,
                        });
                        checkAttack = true;
                    }
                }
                
                if (checkAttack)
                {
                    runtime.latestTimeAttack = time;
                    zombieRuntimes[i] = runtime;
                }
            
            }
            
        }
    }
    
    private struct CharacterSetTakeDamage
    {
        public float3 position;
        public Entity entity;
    }
    
    private struct TakeDamageItem
    {
        public int index;
        public float damage;
    }
    
}