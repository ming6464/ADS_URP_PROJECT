using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace _Game.Scripts.Systems.Zombie
{
    [UpdateInGroup(typeof(SimulationSystemGroup)),UpdateAfter(typeof(BulletMovementSystem)),UpdateBefore(typeof(AnimationSystem))]
    public partial struct ZombieHandleDamageSystem : ISystem
    {
        private EntityTypeHandle _entityTypeHandle;
        private EntityQuery _queryZombieTakeDamage;
        private ComponentTypeHandle<ZombieInfo> _zombieInfoComponentType;
        private ComponentTypeHandle<TakeDamage> _takeDamageComponentType;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _entityTypeHandle = state.GetEntityTypeHandle();
            _zombieInfoComponentType = state.GetComponentTypeHandle<ZombieInfo>();
            _takeDamageComponentType = state.GetComponentTypeHandle<TakeDamage>();
            state.RequireForUpdate<ZombieInfo>();
            state.RequireForUpdate<TakeDamage>();
            
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            HandleTakeDamage(ref state,ref ecb);
            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void HandleTakeDamage(ref SystemState state,ref EntityCommandBuffer ecb)
        {
            _queryZombieTakeDamage = SystemAPI.QueryBuilder().WithAll<ZombieInfo, TakeDamage>().Build();
            _entityTypeHandle.Update(ref state);
            _zombieInfoComponentType.Update(ref state);
            _takeDamageComponentType.Update(ref state);
            var job = new ZombieHandleDamageTakeJOB()
            {
                ecb = ecb.AsParallelWriter(),
                zombieInfoComponentType = _zombieInfoComponentType,
                entityTypeHandle = _entityTypeHandle,
                takeDamageComponentType = _takeDamageComponentType,
                time = (float)SystemAPI.Time.ElapsedTime
            };
            state.Dependency = job.ScheduleParallel(_queryZombieTakeDamage, state.Dependency);
        }

        [BurstCompile]
        private partial struct ZombieHandleDamageTakeJOB : IJobChunk
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            public ComponentTypeHandle<ZombieInfo> zombieInfoComponentType;
            public EntityTypeHandle entityTypeHandle;
            [ReadOnly] public float time;
            [ReadOnly] public ComponentTypeHandle<TakeDamage> takeDamageComponentType;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var zombieInfos = chunk.GetNativeArray(zombieInfoComponentType);
                var takeDamages = chunk.GetNativeArray(takeDamageComponentType);
                var entities = chunk.GetNativeArray(entityTypeHandle);
                var setActive = new SetActiveSP()
                {
                    state = StateID.Wait,
                    startTime = time,
                };
                var addToBuffer = new AddToBuffer();
                
                for (int i = 0; i < chunk.Count; i++)
                {
                    var entity = entities[i];
                    var zombieInfo = zombieInfos[i];
                    var takeDamage = takeDamages[i];
                    zombieInfo.hp -= takeDamage.value;
                    zombieInfos[i] = zombieInfo;
                    if (zombieInfo.hp <= 0)
                    {
                        addToBuffer.id = zombieInfo.id;
                        addToBuffer.entity = entity;
                        ecb.RemoveComponent<LocalTransform>(unfilteredChunkIndex,entity);
                        ecb.AddComponent(unfilteredChunkIndex,entity,setActive);
                        ecb.AddComponent(unfilteredChunkIndex,entity,addToBuffer);
                    }
                    
                }
                ecb.RemoveComponent<TakeDamage>(unfilteredChunkIndex,entities);
            }
        }
        
    }
}