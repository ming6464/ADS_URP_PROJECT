using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game_.Scripts.Systems.Player
{
    [BurstCompile,UpdateInGroup(typeof(InitializationSystemGroup)),UpdateAfter(typeof(PlayerSpawnSystem))]
    public partial struct CharacterSystem : ISystem
    {
        private EntityQuery _enQueryCharacterTakeDamage;
        private EntityTypeHandle _entityTypeHandle;
        private EntityManager _entityManager;
        private NativeQueue<Entity> _characterDieQueue;
        private EntityQuery _enQueryMove;
        private EntityQuery _enQueryCharacterMove;
        private Entity _entityPlayerInfo;
        private bool _isInit;
        private float _characterMoveToWardChangePos;
        private PlayerProperty _playerProperty;
        private NativeList<TargetInfo> _targetNears;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInfo>();
            state.RequireForUpdate<CharacterInfo>();
            _entityTypeHandle = state.GetEntityTypeHandle();
            _enQueryCharacterTakeDamage = SystemAPI.QueryBuilder().WithAll<CharacterInfo,TakeDamage>()
                .WithNone<Disabled, SetActiveSP>().Build();
            _characterDieQueue = new NativeQueue<Entity>(Allocator.Persistent);
            _enQueryMove = SystemAPI.QueryBuilder().WithAll<CharacterInfo, NextPoint>().WithNone<Disabled, SetActiveSP>()
                .Build();
            _enQueryCharacterMove = SystemAPI.QueryBuilder().WithAll<CharacterInfo>().WithNone<Disabled, SetActiveSP>()
                .Build();
            _targetNears = new NativeList<TargetInfo>(Allocator.Persistent);
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_targetNears.IsCreated) _targetNears.Dispose();
            if (_characterDieQueue.IsCreated) _characterDieQueue.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!_isInit)
            {
                _isInit = true;
                _entityManager = state.EntityManager;
                _entityPlayerInfo = SystemAPI.GetSingletonEntity<PlayerInfo>();
                _playerProperty = SystemAPI.GetSingleton<PlayerProperty>();
                _characterMoveToWardChangePos = _playerProperty.speedMoveToNextPoint;
            }
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            Rota(ref state);
            Move(ref state, ref ecb);
            CheckTakeDamage(ref state, ref ecb); 
            ecb.Playback(_entityManager);
            ecb.Dispose();
        }

        private void Rota(ref SystemState state)
        {
            _targetNears.Clear();
            var playerPosition = SystemAPI.GetComponentRO<LocalTransform>(SystemAPI.GetSingletonEntity<PlayerInfo>()).ValueRO.Position;
            var directRota = math.forward();
            var distanceNearest = _playerProperty.distanceAim;
            var positionNearest = float3.zero;
            var moveToWard = _playerProperty.moveToWardMax;

            bool check = false;
            foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAny<ZombieInfo,ItemCanShoot>()
                         .WithNone<Disabled, SetActiveSP>())
            {
                var posTarget = ltw.ValueRO.Position;
                float distance = math.distance(playerPosition, posTarget);
                if (distance < distanceNearest)
                {
                    if (_playerProperty.aimType == AimType.TeamAim)
                    {
                        if (MathExt.CalculateAngle(posTarget - playerPosition, new float3(0, 0, 1)) < _playerProperty.rotaAngleMax)
                        {
                            positionNearest = posTarget;
                            distanceNearest = distance;
                            check = true;
                        }
                        continue;
                    }
                    
                    distanceNearest = distance;
                    _targetNears.Add(new TargetInfo()
                    {
                        position = ltw.ValueRO.Position,
                        distance = distance
                    });
                }
            }

            if (_playerProperty.aimType == AimType.IndividualAim && _targetNears.Length > 20)
            {
                _targetNears.Sort(new TargetInfoComparer());
                _targetNears.Resize(20,NativeArrayOptions.ClearMemory);
            }
            
            if(check)
            {
                directRota = positionNearest - playerPosition;
                var ratio = 1 - math.clamp((distanceNearest * 1.0f / _playerProperty.distanceAim), 0, 1);
                moveToWard = math.lerp(_playerProperty.moveToWardMin, _playerProperty.moveToWardMax,ratio);
            }

            var job = new CharacterRotaJOB()
            {
                ltComponentType = state.GetComponentTypeHandle<LocalTransform>(),
                ltwComponentType = state.GetComponentTypeHandle<LocalToWorld>(),
                targetNears = _targetNears,
                playerProperty = _playerProperty,
                deltaTime = SystemAPI.Time.DeltaTime,
                directRota = directRota,
                moveToWard = moveToWard,
            };
            state.Dependency = job.ScheduleParallel(_enQueryCharacterMove, state.Dependency);
            state.Dependency.Complete();
        }

        private void Move(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            _entityTypeHandle.Update(ref state);

            var listNextPointEntity = _enQueryMove.ToEntityArray(Allocator.Temp);
            listNextPointEntity.Dispose();
            
            var job = new CharacterMoveNextPointJOB()
            {
                deltaTime = SystemAPI.Time.DeltaTime,
                ecb = ecb.AsParallelWriter(),
                entityTypeHandle = _entityTypeHandle,
                ltComponentType = state.GetComponentTypeHandle<LocalTransform>(),
                moveToWardValue = _characterMoveToWardChangePos,
                nextPointComponentType = state.GetComponentTypeHandle<NextPoint>()
            };

            state.Dependency = job.ScheduleParallel(_enQueryMove, state.Dependency);
            state.Dependency.Complete();

        }

        private void CheckTakeDamage(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            _characterDieQueue.Clear();
            float time = (float) SystemAPI.Time.ElapsedTime;
            _entityTypeHandle.Update(ref state);
            var job = new CharacterHandleTakeDamageJOB()
            {
                ecb = ecb.AsParallelWriter(),
                characterInfoTypeHandle = state.GetComponentTypeHandle<CharacterInfo>(),
                entityTypeHandle = _entityTypeHandle,
                time = time,
                takeDamageTypeHandle = state.GetComponentTypeHandle<TakeDamage>(),
                characterDieQueue = _characterDieQueue.AsParallelWriter(),
            };
            state.Dependency = job.ScheduleParallel(_enQueryCharacterTakeDamage, state.Dependency);
            state.Dependency.Complete();
            var bufferCharacterDie = _entityManager.GetBuffer<BufferCharacterDie>(_entityPlayerInfo);
            while (_characterDieQueue.TryDequeue(out var queue))
            {
                ecb.AddComponent(queue, new SetActiveSP()
                {
                    state = StateID.Wait,
                    startTime = time,
                });
                
                bufferCharacterDie.Add(new BufferCharacterDie()
                {
                    entity = queue,
                });
            }
        }
        
        private struct TargetInfoComparer : IComparer<TargetInfo>
        {
            public int Compare(TargetInfo x, TargetInfo y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }
        
        [BurstCompile]
        partial struct CharacterRotaJOB : IJobChunk
        {
            public ComponentTypeHandle<LocalTransform> ltComponentType;
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> ltwComponentType;
            [ReadOnly] public NativeList<TargetInfo> targetNears;
            [ReadOnly] public PlayerProperty playerProperty;
            [ReadOnly] public float deltaTime;
            [ReadOnly] public float3 directRota;
            [ReadOnly] public float moveToWard;
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var lts = chunk.GetNativeArray(ref ltComponentType);
                var ltws = chunk.GetNativeArray(ref ltwComponentType);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var directRota_ = directRota;
                    var moveToWard_ = moveToWard;
                    var lt = lts[i];
                    if (playerProperty.aimType == AimType.IndividualAim)
                    {
                        LoadDirectRota(ref directRota_, ref moveToWard_, ltws[i].Position);
                    }
                    lt.Rotation = MathExt.MoveTowards(lt.Rotation, quaternion.LookRotationSafe(directRota_,math.up()),
                        deltaTime * moveToWard_);
                    lts[i] = lt;
                }
                
            }

            private void LoadDirectRota(ref float3 directRef, ref float moveToWardRef, float3 characterPos)
            {
                var disNearest = playerProperty.distanceAim;
                var nearestEnemyPosition = float3.zero;
                bool check = false;
                foreach (var enemyPos in targetNears)
                {
                    var distance = math.distance(enemyPos.position, characterPos);
                    if (distance < disNearest && MathExt.CalculateAngle(enemyPos.position - characterPos, math.forward()) < playerProperty.rotaAngleMax)
                    {
                        nearestEnemyPosition = enemyPos.position;
                        disNearest = distance;
                        check = true;
                    }   
                }

                if (check)
                {
                    directRef = nearestEnemyPosition - characterPos;
                    var ratio = 1 - math.clamp((disNearest - playerProperty.distanceAim), 0, 1);
                    moveToWardRef = math.lerp(playerProperty.moveToWardMin, playerProperty.moveToWardMax, ratio);
                }
            }
        }

        [BurstCompile]
        partial struct CharacterHandleTakeDamageJOB : IJobChunk
        {
            public NativeQueue<Entity>.ParallelWriter characterDieQueue;
            public EntityCommandBuffer.ParallelWriter ecb;
            public ComponentTypeHandle<CharacterInfo> characterInfoTypeHandle;
            public EntityTypeHandle entityTypeHandle;
            [ReadOnly] public float time;
            [ReadOnly] public ComponentTypeHandle<TakeDamage> takeDamageTypeHandle;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var characterInfos = chunk.GetNativeArray(characterInfoTypeHandle);
                var takeDamages = chunk.GetNativeArray(takeDamageTypeHandle);
                var entities = chunk.GetNativeArray(entityTypeHandle);
                
                
                for (int i = 0; i < chunk.Count; i++)
                {
                    var info = characterInfos[i];
                    var takeDamage = takeDamages[i];
                    var entity = entities[i];
                    info.hp -= takeDamage.value;
                    characterInfos[i] = info;
                    ecb.RemoveComponent<TakeDamage>(unfilteredChunkIndex,entity);
                    if (info.hp <= 0)
                    {
                        characterDieQueue.Enqueue(entity);
                        if (!info.weaponEntity.Equals(default))
                        {
                            ecb.RemoveComponent<Parent>(unfilteredChunkIndex,info.weaponEntity);
                            ecb.AddComponent(unfilteredChunkIndex,info.weaponEntity,new SetActiveSP()
                            {
                                state = StateID.Disable,
                            });
                        }
                        
                    }

                }
            }
        }
        
        [BurstCompile]
        partial struct CharacterMoveNextPointJOB : IJobChunk
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            public EntityTypeHandle entityTypeHandle;
            public ComponentTypeHandle<LocalTransform> ltComponentType;
            [ReadOnly] public float deltaTime;
            [ReadOnly] public float moveToWardValue;
            [ReadOnly] public ComponentTypeHandle<NextPoint> nextPointComponentType;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var lts = chunk.GetNativeArray(ltComponentType);
                var nextPoints = chunk.GetNativeArray(nextPointComponentType);
                var entities = chunk.GetNativeArray(entityTypeHandle);
                
                for (int i = 0; i < chunk.Count; i++)
                {
                    var lt = lts[i];
                    var nextPoint = nextPoints[i].value;
                    if (lt.Position.ComparisionEqual(nextPoint))
                    {
                        ecb.RemoveComponent<NextPoint>(unfilteredChunkIndex,entities[i]);
                        return;
                    }

                    lt.Position =MathExt.MoveTowards(lt.Position, nextPoint, deltaTime * moveToWardValue);
                    lts[i] = lt;
                }
                
            }
        }
        
        private struct TargetInfo
        {
            public float3 position;
            public float distance;
        }
        
    }
}