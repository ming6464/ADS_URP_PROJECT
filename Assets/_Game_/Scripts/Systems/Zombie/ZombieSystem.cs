using Rukhanka;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup)), UpdateBefore(typeof(AnimationStateSystem))]
[BurstCompile]
public partial struct ZombieSystem : ISystem
{
    private float3 _pointZoneMin;
    private float3 _pointZoneMax;
    private bool _init;
    private Entity _entityPlayerInfo;
    private EntityTypeHandle _entityTypeHandle;
    private EntityQuery _enQueryZombie;
    private EntityQuery _enQueryZombieNormal;
    private EntityQuery _enQueryZombieBoss;
    private EntityQuery _enQueryZombieNew;
    private EntityManager _entityManager;
    private NativeQueue<TakeDamageItem> _takeDamageQueue;
    private NativeList<CharacterSetTakeDamage> _characterSetTakeDamages;
    private NativeList<float3> _characterLtws;
    private ComponentTypeHandle<LocalToWorld> _ltwTypeHandle;
    private ComponentTypeHandle<ZombieRuntime> _zombieRunTimeTypeHandle;
    private ComponentTypeHandle<ZombieInfo> _zombieInfoTypeHandle;
    private ComponentTypeHandle<LocalTransform> _ltTypeHandle;
    private uint _bossOgreAttackHash;
    private uint _finishAttackHash;
    private LayerStoreComponent _layerStore;
    private CollisionFilter _collisionFilter;
    private PhysicsWorldSingleton _physicsWorld;

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
        CheckAttackPlayer(ref state);
        CheckZombieToDeadZone(ref state);
        CheckAnimationEvent(ref state);
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
        }
    }

    #region Animation Event

    [BurstCompile]
    private void CheckAnimationEvent(ref SystemState state)
    {
        _physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        // Check event boss
        foreach (var (zombieInfo, bossInfo, entity) in SystemAPI.Query<RefRO<ZombieInfo>, RefRO<BossInfo>>()
                     .WithEntityAccess().WithNone<Disabled, AddToBuffer>())
        {
            if (_entityManager.HasBuffer<AnimationEventComponent>(entity))
            {
                HandleEvent(ref state, ref ecb, entity, zombieInfo.ValueRO, bossInfo.ValueRO);
            }
        }

        ecb.Playback(_entityManager);
        ecb.Dispose();
    }

    private void HandleEvent(ref SystemState state, ref EntityCommandBuffer ecb, Entity entity, ZombieInfo zombieInfo,
        BossInfo bossInfo)
    {
        NativeList<ColliderCastHit> hits = new NativeList<ColliderCastHit>(Allocator.TempJob);
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
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        var playerPosition = SystemAPI.GetComponentRO<LocalToWorld>(_entityPlayerInfo).ValueRO.Position;

        BossAttack(ref state,ref ecb, playerPosition);

        ZombieNormalAttack(ref state, ref _takeDamageQueue,playerPosition);

        HandleAttackedCharacter(ref state,ref ecb,_takeDamageQueue);
        
        ecb.Playback(_entityManager);
        ecb.Dispose();
    }
    [BurstCompile]
    private void HandleAttackedCharacter(ref SystemState state, ref EntityCommandBuffer ecb, NativeQueue<TakeDamageItem> takeDamageQueue)
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
    private void ZombieNormalAttack(ref SystemState state, ref NativeQueue<TakeDamageItem> takeDamageQueue, float3 playerPosition)
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
            playerPosition = playerPosition,
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
            ltwTypeHandle = _ltwTypeHandle,
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
    private void CheckZombieToDeadZone(ref SystemState state)
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
        [ReadOnly] public ComponentTypeHandle<ZombieRuntime> zombieRunTimeTypeHandle;
        [ReadOnly] public ComponentTypeHandle<LocalToWorld> ltwTypeHandle;
        [ReadOnly] public ComponentTypeHandle<ZombieInfo> zombieInfoTypeHandle;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public NativeList<float3> characterLtws;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var lts = chunk.GetNativeArray(ref ltTypeHandle);
            var ltws = chunk.GetNativeArray(ref ltwTypeHandle);
            var zombieRunTimes = chunk.GetNativeArray(ref zombieRunTimeTypeHandle);
            var zombieInfos = chunk.GetNativeArray(ref zombieInfoTypeHandle);
            var entities = chunk.GetNativeArray(entityTypeHandle);
            for (int i = 0; i < chunk.Count; i++)
            {
                var ltw = ltws[i];
                var lt = lts[i];
                var info = zombieInfos[i];
                var direct = GetDirect(ltw.Position, info.directNormal, info.chasingRange);

                lt.Position += direct * info.speed * deltaTime;
                lt.Rotation = MathExt.MoveTowards(lt.Rotation, quaternion.LookRotationSafe(direct, math.up()),
                    250 * deltaTime);
                lts[i] = lt;
                if (zombieRunTimes[i].latestAnimState != StateID.Run)
                {
                    ecb.AddComponent(unfilteredChunkIndex, entities[i], new SetAnimationSP()
                    {
                        state = StateID.Run,
                    });
                }
            }
        }

        private float3 GetDirect(float3 position, float3 defaultDirect, float chasingRange)
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
                return math.all(position == nearestPosition) ? float3.zero : math.normalize(nearestPosition - position);
            }

            return defaultDirect;
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
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
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


                var distance = info.attackRange - math.distance(playerPosition, ltw.Position);
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
}