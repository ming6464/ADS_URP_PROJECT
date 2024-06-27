using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace _Game_.Scripts.Systems.Player
{
    [BurstCompile]
    public partial struct CharacterSystem : ISystem
    {
        private EntityQuery _enQueryCharacterTakeDamage;
        private EntityTypeHandle _entityTypeHandle;
        private EntityManager _entityManager;
        private bool _isInit;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CharacterInfo>();
            _entityTypeHandle = state.GetEntityTypeHandle();
            _enQueryCharacterTakeDamage = SystemAPI.QueryBuilder().WithAll<CharacterInfo,TakeDamage>()
                .WithNone<Disabled, SetActiveSP>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!_isInit)
            {
                _isInit = true;
                _entityManager = state.EntityManager;
            }
            
            float time = (float) SystemAPI.Time.ElapsedTime;
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            _entityTypeHandle.Update(ref state);
            var job = new CharacterHandleTakeDamageJOB()
            {
                ecb = ecb.AsParallelWriter(),
                characterInfoTypeHandle = state.GetComponentTypeHandle<CharacterInfo>(),
                entityTypeHandle = _entityTypeHandle,
                time = time,
                takeDamageTypeHandle = state.GetComponentTypeHandle<TakeDamage>(),
            };
            state.Dependency = job.ScheduleParallel(_enQueryCharacterTakeDamage, state.Dependency);
            state.Dependency.Complete();
            ecb.Playback(_entityManager);
            ecb.Dispose();
        }


        [BurstCompile]
        partial struct CharacterHandleTakeDamageJOB : IJobChunk
        {
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
                        ecb.AddComponent(unfilteredChunkIndex,entity, new SetActiveSP()
                        {
                            state = StateID.Wait,
                            startTime = time,
                        });
                    }

                }
            }
        }
    }
}