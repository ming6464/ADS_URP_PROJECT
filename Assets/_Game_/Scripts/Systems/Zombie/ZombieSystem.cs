using Rukhanka;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[UpdateInGroup(typeof(SimulationSystemGroup)), UpdateBefore(typeof(AnimationStateSystem))]
[BurstCompile]
public partial struct ZombieSystem : ISystem
{
    private bool _init;
    private float3 _pointZoneMin;
    private float3 _pointZoneMax;
    private Entity _entityPlayerInfo;
    private EntityTypeHandle _entityTypeHandle;
    private EntityManager _entityManager;
    private NativeQueue<TakeDamageItem> _takeDamageQueue;
    private NativeList<CharacterSetTakeDamage> _characterSetTakeDamages;
    private NativeList<float3> _characterLtws;
    private ComponentTypeHandle<LocalToWorld> _ltwTypeHandle;
    private ComponentTypeHandle<ZombieRuntime> _zombieRunTimeTypeHandle;
    private ComponentTypeHandle<ZombieInfo> _zombieInfoTypeHandle;
    private ComponentTypeHandle<LocalTransform> _ltTypeHandle;
    private LayerStoreComponent _layerStore;
    private CollisionFilter _collisionFilter;
    private PhysicsWorldSingleton _physicsWorld;
    private ZombieProperty _zombieProperty;

    // Query
    private EntityQuery _enQueryZombie;
    private EntityQuery _enQueryZombieNormal;
    private EntityQuery _enQueryZombieBoss;
    private EntityQuery _enQueryZombieNew;

    // HASH
    private uint _bossOgreAttackHash;
    private uint _finishAttackHash;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        RequireNecessaryComponents(ref state);
        Init(ref state);
    }

    [BurstCompile]
    private void Init(ref SystemState state)
    {
        _ltwTypeHandle = state.GetComponentTypeHandle<LocalToWorld>();
        _zombieRunTimeTypeHandle = state.GetComponentTypeHandle<ZombieRuntime>();
        _zombieInfoTypeHandle = state.GetComponentTypeHandle<ZombieInfo>();
        _ltTypeHandle = state.GetComponentTypeHandle<LocalTransform>();
        _enQueryZombieNew = SystemAPI.QueryBuilder().WithAll<ZombieInfo, New>().WithNone<Disabled, AddToBuffer>()
            .Build();
        _enQueryZombie =
            SystemAPI.QueryBuilder().WithAll<ZombieInfo, LocalTransform>()
                .WithNone<Disabled, AddToBuffer, New, SetAnimationSP>().Build();
        _enQueryZombieNormal =
            SystemAPI.QueryBuilder().WithAll<ZombieInfo, LocalTransform>()
                .WithNone<Disabled, AddToBuffer, New, SetAnimationSP, BossInfo>().Build();
        _enQueryZombieBoss =
            SystemAPI.QueryBuilder().WithAll<BossInfo, LocalTransform>()
                .WithNone<Disabled, AddToBuffer, New, SetAnimationSP>().Build();
        _takeDamageQueue = new NativeQueue<TakeDamageItem>(Allocator.Persistent);
        _characterSetTakeDamages = new NativeList<CharacterSetTakeDamage>(Allocator.Persistent);
        _characterLtws = new NativeList<float3>(Allocator.Persistent);
        _bossOgreAttackHash = FixedStringExtensions.CalculateHash32("BossOgreAttack");
        _finishAttackHash = FixedStringExtensions.CalculateHash32("FinishAttack");
    }

    [BurstCompile]
    private void RequireNecessaryComponents(ref SystemState state)
    {
        state.RequireForUpdate<LayerStoreComponent>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<PlayerInfo>();
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
        SetUpNewZombie(ref state);
        Move(ref state);
        AvoidFlock(ref state);
        CheckAttackPlayer(ref state);
        CheckDeadZone(ref state);
        CheckAnimationEvent(ref state);
    }
    

    [BurstCompile]
    private void AvoidFlock(ref SystemState state)
    {
        if(!_zombieProperty.comparePriorities) return;
        
        UpdatePriority(ref state);
        
        var avoidDatas = new NativeList<AvoidData>(Allocator.TempJob);

        foreach (var (ìnfo, lt,entity) in SystemAPI.Query<RefRO<ZombieInfo>, RefRO<LocalTransform>>().WithEntityAccess().WithNone<Disabled,SetActiveSP,AddToBuffer>())
        {
            avoidDatas.Add(new AvoidData()
            {
                entity = entity,
                info = ìnfo.ValueRO,
                lt = lt.ValueRO,
            });
        }

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        var job = new ZombieAvoidJOB()
        {
            avoidDatas = avoidDatas,
            zombieProperty = _zombieProperty,
            deltaTime = SystemAPI.Time.DeltaTime,
            ecb = ecb.AsParallelWriter(),
        };

        state.Dependency = job.Schedule(avoidDatas.Length, (int)avoidDatas.Length / 100);
        state.Dependency.Complete();
        ecb.Playback(_entityManager);
        ecb.Dispose();
        avoidDatas.Dispose();
    }
    
    [BurstCompile]
    private void UpdatePriority(ref SystemState state)
    {
        var playerPosition = SystemAPI.GetComponentRO<LocalToWorld>(_entityPlayerInfo).ValueRO.Position;
        _ltTypeHandle.Update(ref state);
        _zombieInfoTypeHandle.Update(ref state);
        var job = new UpdatePriorityJOB()
        {
            ltTypeHandle = _ltTypeHandle,
            zombieInfoTypeHandle = _zombieInfoTypeHandle,
            playerPos = playerPosition,
        };

        state.Dependency = job.ScheduleParallel(_enQueryZombie, state.Dependency);
        state.Dependency.Complete();
    }

    [BurstCompile]
    private void SetUpNewZombie(ref SystemState state)
    {
        _ltTypeHandle.Update(ref state);
        _zombieInfoTypeHandle.Update(ref state);
        var job = new SetUpNewZombieJOB()
        {
            ltTypeHandle = _ltTypeHandle,
            zombieInfoTypeHandle = _zombieInfoTypeHandle,
        };
        state.Dependency = job.ScheduleParallel(_enQueryZombieNew, state.Dependency);
        state.Dependency.Complete();
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
            _entityPlayerInfo = SystemAPI.GetSingletonEntity<PlayerInfo>();
            _entityManager = state.EntityManager;
            _layerStore = SystemAPI.GetSingleton<LayerStoreComponent>();
            _collisionFilter = new CollisionFilter()
            {
                BelongsTo = _layerStore.enemyLayer,
                CollidesWith = _layerStore.characterLayer,
                GroupIndex = 0
            };
            _zombieProperty = SystemAPI.GetSingleton<ZombieProperty>();
        }
    }

    #region Animation Event

    [BurstCompile]
    private void CheckAnimationEvent(ref SystemState state)
    {
        _physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        // Check event boss
        foreach (var (zombieInfo, entity) in SystemAPI.Query<RefRO<ZombieInfo>>()
                     .WithEntityAccess().WithAll<BossInfo>().WithNone<Disabled, AddToBuffer>())
        {
            if (_entityManager.HasBuffer<AnimationEventComponent>(entity))
            {
                HandleEvent(ref state, ref ecb, entity, zombieInfo.ValueRO);
            }
        }

        ecb.Playback(_entityManager);
        ecb.Dispose();
    }

    private void HandleEvent(ref SystemState state, ref EntityCommandBuffer ecb, Entity entity, ZombieInfo zombieInfo)
    {
        NativeList<ColliderCastHit> hits = new NativeList<ColliderCastHit>(Allocator.Persistent);
        foreach (var b in _entityManager.GetBuffer<AnimationEventComponent>(entity))
        {
            var stringHash = b.stringParamHash;
            if (stringHash.CompareTo(_bossOgreAttackHash) == 0)
            {
                var entityNew = _entityManager.CreateEntity();
                var lt = _entityManager.GetComponentData<LocalToWorld>(entity);
                var attackPosition = math.transform(lt.Value, zombieInfo.offsetAttackPosition);
                ecb.AddComponent(entityNew, new EffectComponent()
                {
                    position = attackPosition,
                    rotation = lt.Rotation,
                    effectID = EffectID.GroundCrack
                });
                if (_physicsWorld.SphereCastAll(attackPosition, zombieInfo.radiusDamage, float3.zero, 0, ref hits,
                        _collisionFilter))
                {
                    foreach (var hit in hits)
                    {
                        TakeDamage takeDamage = new TakeDamage();
                        if (_entityManager.HasComponent<TakeDamage>(hit.Entity))
                        {
                            takeDamage = _entityManager.GetComponentData<TakeDamage>(hit.Entity);
                        }

                        takeDamage.value += zombieInfo.damage;
                        ecb.AddComponent(hit.Entity, takeDamage);
                    }
                }
            }
            else if (stringHash.CompareTo(_finishAttackHash) == 0)
            {
                ecb.AddComponent(entity, new SetAnimationSP()
                {
                    state = StateID.Idle,
                    timeDelay = 0,
                });
            }
        }

        hits.Dispose();
    }

    #endregion

    #region Attack Character

    [BurstCompile]
    private void CheckAttackPlayer(ref SystemState state)
    {
        _takeDamageQueue.Clear();
        _characterSetTakeDamages.Clear();

        foreach (var (ltw, entity) in SystemAPI.Query<RefRO<LocalToWorld>>().WithEntityAccess().WithAll<CharacterInfo>()
                     .WithNone<Disabled, AddToBuffer, SetActiveSP>())
        {
            _characterSetTakeDamages.Add(new CharacterSetTakeDamage()
            {
                entity = entity,
                position = ltw.ValueRO.Position
            });
        }

        if (_characterSetTakeDamages.Length == 0) return;
        var ecb = new EntityCommandBuffer(Allocator.Persistent);
        var playerPosition = SystemAPI.GetComponentRO<LocalToWorld>(_entityPlayerInfo).ValueRO.Position;

        BossAttack(ref state, ref ecb, playerPosition);

        ZombieNormalAttack(ref state, ref _takeDamageQueue, playerPosition);

        HandleAttackedCharacter(ref state, ref ecb);

        ecb.Playback(_entityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    private void HandleAttackedCharacter(ref SystemState state, ref EntityCommandBuffer ecb)
    {
        NativeHashMap<int, float> characterTakeDamageMap =
            new NativeHashMap<int, float>(_takeDamageQueue.Count, Allocator.Temp);
        while (_takeDamageQueue.TryDequeue(out var queue))
        {
            if (characterTakeDamageMap.ContainsKey(queue.index))
            {
                characterTakeDamageMap[queue.index] += queue.damage;
            }
            else
            {
                characterTakeDamageMap.Add(queue.index, queue.damage);
            }
        }

        foreach (var map in characterTakeDamageMap)
        {
            if (map.Value == 0) continue;
            Entity entity = _characterSetTakeDamages[map.Key].entity;
            ecb.AddComponent(entity, new TakeDamage()
            {
                value = map.Value,
            });
        }

        characterTakeDamageMap.Dispose();
    }

    [BurstCompile]
    private void ZombieNormalAttack(ref SystemState state, ref NativeQueue<TakeDamageItem> takeDamageQueue,
        float3 playerPosition)
    {
        var job = new CheckAttackPlayerJOB()
        {
            characterSetTakeDamages = _characterSetTakeDamages,
            localToWorldTypeHandle = _ltwTypeHandle,
            time = (float)SystemAPI.Time.ElapsedTime,
            zombieInfoTypeHandle = _zombieInfoTypeHandle,
            zombieRuntimeTypeHandle = _zombieRunTimeTypeHandle,
            takeDamageQueues = takeDamageQueue.AsParallelWriter(),
            playerPosition = playerPosition,
            distanceCheck = 10,
        };

        state.Dependency = job.ScheduleParallel(_enQueryZombieNormal, state.Dependency);
        state.Dependency.Complete();
    }

    [BurstCompile]
    private void BossAttack(ref SystemState state, ref EntityCommandBuffer ecb, float3 playerPosition)
    {
        _ltwTypeHandle.Update(ref state);
        _zombieInfoTypeHandle.Update(ref state);
        _zombieRunTimeTypeHandle.Update(ref state);
        _entityTypeHandle.Update(ref state);

        var jobBoss = new CheckZombieBossAttackPlayerJOB()
        {
            characterSetTakeDamages = _characterSetTakeDamages,
            ecb = ecb.AsParallelWriter(),
            entityTypeHandle = _entityTypeHandle,
            localToWorldTypeHandle = _ltwTypeHandle,
            time = (float)SystemAPI.Time.ElapsedTime,
            timeDelay = 2.2f,
            zombieInfoTypeHandle = _zombieInfoTypeHandle,
            zombieRuntimeTypeHandle = _zombieRunTimeTypeHandle
        };
        state.Dependency = jobBoss.ScheduleParallel(_enQueryZombieBoss, state.Dependency);
        state.Dependency.Complete();
    }

    #endregion

    #region MOVE

    [BurstCompile]
    private void Move(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        UpdateCharacterLTWList(ref state);
        float deltaTime = SystemAPI.Time.DeltaTime;
        _zombieInfoTypeHandle.Update(ref state);
        _ltwTypeHandle.Update(ref state);
        _ltTypeHandle.Update(ref state);
        _entityTypeHandle.Update(ref state);
        _zombieRunTimeTypeHandle.Update(ref state);
        ZombieMovementJOB job = new ZombieMovementJOB()
        {
            deltaTime = deltaTime,
            ltTypeHandle = _ltTypeHandle,
            zombieInfoTypeHandle = _zombieInfoTypeHandle,
            characterLtws = _characterLtws,
            entityTypeHandle = _entityTypeHandle,
            zombieRunTimeTypeHandle = _zombieRunTimeTypeHandle,
            ecb = ecb.AsParallelWriter(),
        };
        state.Dependency = job.ScheduleParallel(_enQueryZombie, state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(_entityManager);
        ecb.Dispose();
    }

    [BurstCompile]
    private void UpdateCharacterLTWList(ref SystemState state)
    {
        _characterLtws.Clear();
        foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<CharacterInfo>()
                     .WithNone<Disabled, SetActiveSP, AddToBuffer>())
        {
            _characterLtws.Add(ltw.ValueRO.Position);
        }
    }

    #endregion

    [BurstCompile]
    private void CheckDeadZone(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        _entityTypeHandle.Update(ref state);
        _ltwTypeHandle.Update(ref state);
        _zombieInfoTypeHandle.Update(ref state);
        var chunkJob = new CheckDeadZoneJOB
        {
            ecb = ecb.AsParallelWriter(),
            entityTypeHandle = _entityTypeHandle,
            ltwTypeHandle = _ltwTypeHandle,
            zombieInfoTypeHandle = _zombieInfoTypeHandle,
            minPointRange = _pointZoneMin,
            maxPointRange = _pointZoneMax,
        };
        state.Dependency = chunkJob.ScheduleParallel(_enQueryZombie, state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(_entityManager);
        ecb.Dispose();
    }

    #region JOB

    [BurstCompile]
    partial struct ZombieMovementJOB : IJobChunk
    {
        public ComponentTypeHandle<LocalTransform> ltTypeHandle;
        public EntityCommandBuffer.ParallelWriter ecb;
        public EntityTypeHandle entityTypeHandle;
        public ComponentTypeHandle<ZombieInfo> zombieInfoTypeHandle;
        [ReadOnly] public ComponentTypeHandle<ZombieRuntime> zombieRunTimeTypeHandle;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public NativeList<float3> characterLtws;


        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var lts = chunk.GetNativeArray(ref ltTypeHandle);
            var zombieRunTimes = chunk.GetNativeArray(ref zombieRunTimeTypeHandle);
            var zombieInfos = chunk.GetNativeArray(ref zombieInfoTypeHandle);
            var entities = chunk.GetNativeArray(entityTypeHandle);
            for (int i = 0; i < chunk.Count; i++)
            {
                var lt = lts[i];
                var info = zombieInfos[i];
                var direct = GetDirect(lt.Position, info.directNormal, info.chasingRange, info.attackRange);

                lt.Rotation = MathExt.MoveTowards(lt.Rotation, quaternion.LookRotationSafe(direct, math.up()),
                    250 * deltaTime);

                lt.Position += direct * info.speed * deltaTime;


                info.currentDirect = direct;
                lts[i] = lt;
                zombieInfos[i] = info;
                if (zombieRunTimes[i].latestAnimState != StateID.Run)
                {
                    ecb.AddComponent(unfilteredChunkIndex, entities[i], new SetAnimationSP()
                    {
                        state = StateID.Run,
                    });
                }
            }
        }

        private float3 GetDirect(float3 position, float3 defaultDirect, float chasingRange, float attackRange)
        {
            float3 nearestPosition = default;
            float distanceNearest = float.MaxValue;

            foreach (var characterLtw in characterLtws)
            {
                float distance = math.distance(characterLtw, position);
                if (distance <= chasingRange && distance < distanceNearest)
                {
                    distanceNearest = distance;
                    nearestPosition = characterLtw;
                }
            }

            if (distanceNearest < float.MaxValue)
            {
                if (math.all(position == nearestPosition))
                {
                    return float3.zero;
                }

                var normalDir = math.normalize(nearestPosition - position);
                
                if (distanceNearest <= (attackRange / 2f))
                {
                    normalDir *= 0.01f;
                }

                return normalDir;
            }

            return defaultDirect;
        }
    }
    
    
    [BurstCompile]
    partial struct ZombieAvoidJOB : IJobParallelFor
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public ZombieProperty zombieProperty;
        [ReadOnly] public NativeArray<AvoidData> avoidDatas;
        [ReadOnly] public float deltaTime;
        public void Execute(int index)
        {
            var directPushed = float3.zero;
            var avoidData = avoidDatas[index];
            bool check = false;
            for(int i = 0;i < avoidDatas.Length; i++)
            {
                var dataSet = avoidDatas[i];
                if (dataSet.info.priority > avoidData.info.priority || i == index) continue;
                var distance = math.distance(dataSet.lt.Position, avoidData.lt.Position);
                var overlappingDistance = (dataSet.info.radius + avoidData.info.radius) - distance;
                if (overlappingDistance <= zombieProperty.minDistanceAvoid) continue;
                directPushed += (avoidData.lt.Position - dataSet.lt.Position);
                check = true;
            }

            if (check)
            {
                Validate(ref directPushed,index);
            }

            if (!directPushed.Equals(float3.zero))
            {
                directPushed = math.normalize(directPushed);
                directPushed.y = 0;
                var lt = avoidData.lt;
                lt.Position += directPushed * deltaTime * zombieProperty.speedAvoid;
                ecb.SetComponent(index,avoidData.entity,lt);
            }
        }

        private void Validate(ref float3 directPushed, int index)
        {
            if (directPushed.Equals(float3.zero) || math.length(directPushed) < zombieProperty.minDistanceAvoid)
            {
                Random random = new Random((uint)(index + 1));
                directPushed = new float3(random.NextFloat(-.3f, .3f), 0, random.NextFloat(-.3f, .3f));

                int ik = 0;

                while (directPushed is { z: 0, x: 0 })
                {
                    if (ik == 0)
                    {
                        directPushed.z = random.NextFloat(-.3f, .3f);
                    }
                    else if (ik == 1)
                    {
                        directPushed.x = random.NextFloat(-.3f, .3f);
                    }
                    else
                    {
                        directPushed.x = random.NextFloat(-.3f, .3f);
                        directPushed.z = random.NextFloat(-.3f, .3f);
                    }

                    ik++;
                    if (ik > 3)
                    {
                        ik = 0;
                    }
                }
            } 
        }
    }
    
    [BurstCompile]
    partial struct UpdatePriorityJOB : IJobChunk
    {
        [ReadOnly] public float3 playerPos;
        public ComponentTypeHandle<ZombieInfo> zombieInfoTypeHandle;
        [ReadOnly] public ComponentTypeHandle<LocalTransform> ltTypeHandle;
        
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var lts = chunk.GetNativeArray(ref ltTypeHandle);
            var infos = chunk.GetNativeArray(ref zombieInfoTypeHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                var info = infos[i];
                var lt = lts[i];

                info.priority = (int)(math.distance(lt.Position, playerPos) * 1000) + (int)info.priorityKey * 10000;

                infos[i] = info;
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
                    state = DisableID.Disable,
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

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var zombieInfos = chunk.GetNativeArray(ref zombieInfoTypeHandle);
            var ltws = chunk.GetNativeArray(ref localToWorldTypeHandle);
            var zombieRuntimes = chunk.GetNativeArray(ref zombieRuntimeTypeHandle);
            for (int i = 0; i < chunk.Count; i++)
            {
                var info = zombieInfos[i];
                var ltw = ltws[i];
                var runtime = zombieRuntimes[i];

                if (time - runtime.latestTimeAttack < info.delayAttack) continue;
                
                if (math.distance(playerPosition, ltw.Position) > distanceCheck) continue;

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
    
    [BurstCompile]
    partial struct SetUpNewZombieJOB : IJobChunk
    {
        public ComponentTypeHandle<LocalTransform> ltTypeHandle;
        [ReadOnly] public ComponentTypeHandle<ZombieInfo> zombieInfoTypeHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var lts = chunk.GetNativeArray(ref ltTypeHandle);
            var zombieInfos = chunk.GetNativeArray(ref zombieInfoTypeHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                var info = zombieInfos[i];
                var lt = lts[i];

                lt.Rotation = quaternion.LookRotationSafe(info.directNormal, math.up());
                lts[i] = lt;
            }
        }
    }
    
    
    [BurstCompile]
    partial struct CheckZombieBossAttackPlayerJOB : IJobChunk
    {
        public ComponentTypeHandle<ZombieRuntime> zombieRuntimeTypeHandle;
        public EntityCommandBuffer.ParallelWriter ecb;
        public EntityTypeHandle entityTypeHandle;
        [ReadOnly] public NativeList<CharacterSetTakeDamage> characterSetTakeDamages;
        [ReadOnly] public ComponentTypeHandle<ZombieInfo> zombieInfoTypeHandle;
        [ReadOnly] public ComponentTypeHandle<LocalToWorld> localToWorldTypeHandle;
        [ReadOnly] public float3 playerPosition;
        [ReadOnly] public float timeDelay;
        [ReadOnly] public float time;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var zombieInfos = chunk.GetNativeArray(ref zombieInfoTypeHandle);
            var ltws = chunk.GetNativeArray(ref localToWorldTypeHandle);
            var zombieRuntimes = chunk.GetNativeArray(ref zombieRuntimeTypeHandle);
            var entities = chunk.GetNativeArray(entityTypeHandle);
            for (int i = 0; i < chunk.Count; i++)
            {
                var info = zombieInfos[i];
                var ltw = ltws[i];
                var runtime = zombieRuntimes[i];

                var distance = info.attackRange - GetDistanceToCharacterNearest(ltw.Position);;
                if (distance >= 0)
                {
                    var timeCheck = time - runtime.latestTimeAttack;
                    if (timeCheck < info.delayAttack)
                    {
                        if (runtime.latestAnimState != StateID.Idle)
                        {
                            ecb.AddComponent(unfilteredChunkIndex, entities[i], new SetAnimationSP()
                            {
                                state = StateID.Idle,
                                timeDelay = info.delayAttack - timeCheck,
                            });
                        }

                        return;
                    }

                    if (MathExt.CalculateAngle(ltw.Forward, playerPosition - ltw.Position) > 45)
                    {
                        return;
                    }

                    ecb.AddComponent(unfilteredChunkIndex, entities[i], new SetAnimationSP()
                    {
                        state = StateID.Attack,
                        timeDelay = 999,
                    });
                    runtime.latestTimeAttack = time + timeDelay;
                    zombieRuntimes[i] = runtime;
                    break;
                }
            }
        }

        private double GetDistanceToCharacterNearest(float3 ltwPosition)
        {
            var distanceNearest = float.MaxValue;

            foreach (var i in characterSetTakeDamages)
            {
                var dis = math.distance(ltwPosition, i.position);

                if (dis < distanceNearest)
                {
                    distanceNearest = dis;
                }
                
            }
            
            return distanceNearest;
        }
    }

    #endregion


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
    
    private struct AvoidData
    {
        public Entity entity;
        public LocalTransform lt;
        public ZombieInfo info;
    }

}