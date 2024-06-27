using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup)),UpdateBefore(typeof(AnimationSystem))]
[BurstCompile]
public partial struct ZombieSystem : ISystem
{
    private float3 _pointZoneMin;
    private float3 _pointZoneMax;
    private bool _init;
    private ZombieProperty _zombieProperty;
    private EntityTypeHandle _entityTypeHandle;
    private EntityQuery _enQueryZombie;
    private NativeQueue<TakeDamageItem> _takeDamageQueue;
    private NativeList<CharacterSetTakeDamage> _characterSetTakeDamages;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ZombieProperty>();
        state.RequireForUpdate<ActiveZoneProperty>();
        state.RequireForUpdate<ZombieInfo>();
        _enQueryZombie =
            SystemAPI.QueryBuilder().WithAll<ZombieInfo,LocalTransform,ZombieRuntime>().WithNone<Disabled, SetActiveSP>().Build();
        _takeDamageQueue = new NativeQueue<TakeDamageItem>(Allocator.Persistent);
        _characterSetTakeDamages = new NativeList<CharacterSetTakeDamage>(Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (_characterSetTakeDamages.IsCreated) _characterSetTakeDamages.Dispose();
        if (_takeDamageQueue.IsCreated) _takeDamageQueue.Dispose();
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!_init)
        {
            _init = true;

            var zone = SystemAPI.GetSingleton<ActiveZoneProperty>();
            _pointZoneMin = zone.pointRangeMin;
            _pointZoneMax = zone.pointRangeMax;
            _enQueryZombie = SystemAPI.QueryBuilder().WithAll<ZombieInfo, LocalTransform>().Build();
            _zombieProperty = SystemAPI
                .GetComponentRO<ZombieProperty>(SystemAPI.GetSingletonEntity<ZombieProperty>()).ValueRO;
        }
        state.Dependency.Complete();
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        Move(ref state);
        CheckAttackPlayer(ref state);
        CheckZombieToDeadZone(ref state, ref ecb);
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
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private void CheckAttackPlayer(ref SystemState state)
    {
        _takeDamageQueue.Clear();
        _characterSetTakeDamages.Clear();

        foreach (var (ltw, entity) in SystemAPI.Query<RefRO<LocalToWorld>>().WithEntityAccess().WithNone<Disabled,SetActiveSP>())
        {
            _characterSetTakeDamages.Add(new CharacterSetTakeDamage()
            {
                entity = entity,
                position = ltw.ValueRO.Position
            });
        }
        
        var job = new CheckAttackPlayerJOB()
        {
            characterSetTakeDamages = _characterSetTakeDamages,
            localToWorldTypeHandle = state.GetComponentTypeHandle<LocalToWorld>(),
            time = (float)SystemAPI.Time.ElapsedTime,
            zombieInfoTypeHandle = state.GetComponentTypeHandle<ZombieInfo>(),
            zombieRuntimeTypeHandle = state.GetComponentTypeHandle<ZombieRuntime>(),
            takeDamageQueues = _takeDamageQueue.AsParallelWriter()
        };
        state.Dependency = job.ScheduleParallel(_enQueryZombie, state.Dependency);
        state.Dependency.Complete();
    }

    private void Move(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        ZombieMovementJOB job = new ZombieMovementJOB()
        {
            deltaTime = deltaTime,
            ltTypeHandle = state.GetComponentTypeHandle<LocalTransform>(),
            zombieInfoTypeHandle = state.GetComponentTypeHandle<ZombieInfo>(true)
        };
        state.Dependency = job.ScheduleParallel(_enQueryZombie, state.Dependency);
        state.Dependency.Complete();
    }

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
        [ReadOnly] public ComponentTypeHandle<ZombieInfo> zombieInfoTypeHandle;
        [ReadOnly] public float deltaTime;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var lts = chunk.GetNativeArray(ltTypeHandle);
            var zombieInfos = chunk.GetNativeArray(zombieInfoTypeHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                var lt = lts[i];
                var zombie = zombieInfos[i];
                lt.Position += zombie.directNormal * zombie.speed * deltaTime;
                lt.Rotation = quaternion.LookRotation(zombie.directNormal, math.up());
                lts[i] = lt;
            }
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
            var ltwArr = chunk.GetNativeArray(ltwTypeHandle);
            var zombieInfos = chunk.GetNativeArray(zombieInfoTypeHandle);
            var entities = chunk.GetNativeArray(entityTypeHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                if (CheckInRange(ltwArr[i].Position, minPointRange, maxPointRange)) continue;
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

            bool CheckInRange(float3 value, float3 min, float3 max)
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
        
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var zombieInfos = chunk.GetNativeArray(zombieInfoTypeHandle);
            var ltws = chunk.GetNativeArray(localToWorldTypeHandle);
            var zombieRuntimes = chunk.GetNativeArray(zombieRuntimeTypeHandle);
            for (int i = 0; i < chunk.Count; i++)
            {
                var info = zombieInfos[i];
                var ltw = ltws[i];
                var runtime = zombieRuntimes[i];
                
                if( time - runtime.latestTimeAttack < info.delayAttack) continue;
            
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