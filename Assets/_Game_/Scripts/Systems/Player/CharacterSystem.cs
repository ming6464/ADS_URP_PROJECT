using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
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
        private Entity _entityPlayerInfo;
        private bool _isInit;
        private float _characterMoveToWardChangePos;
        
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
        }

        public void OnDestroy(ref SystemState state)
        {
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
                var playerProperty = SystemAPI.GetSingleton<PlayerProperty>();
                _characterMoveToWardChangePos = playerProperty.characterMoveToWardChangePos;
            }
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            Move(ref state, ref ecb);
            CheckTakeDamage(ref state, ref ecb); 
            ecb.Playback(_entityManager);
            ecb.Dispose();
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
    }
}