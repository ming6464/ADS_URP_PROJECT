using Rukhanka;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

[UpdateBefore(typeof(ZombieAnimationSystem))]
public partial class ZombieAnimationSystem : SystemBase
{
    private FastAnimatorParameter _dyingAnimatorParameter = new FastAnimatorParameter("Die");
    
    protected override void OnUpdate()
    {
        Dependency.Complete();
        var zombieAnimatorJob = new ProcessAnimZombie()
        {
            dyingAnimatorParameter = _dyingAnimatorParameter,
            time = (float)SystemAPI.Time.ElapsedTime,
        };
        Dependency = zombieAnimatorJob.ScheduleParallel(Dependency);
    }
}

[BurstCompile]
public partial struct ProcessAnimZombie : IJobEntity
{
    [ReadOnly] public FastAnimatorParameter dyingAnimatorParameter;
    [ReadOnly] public float time;
    void Execute( in ZombieInfo zombieInfo, ref SetActiveSP disableSp, AnimatorParametersAspect parametersAspect,ref PhysicsCollider physicsCollider)
    {
        var colliderFilter = physicsCollider.Value.Value.GetCollisionFilter();
        switch (disableSp.state)
        {
            case StateID.CanEnable:
                parametersAspect.SetBoolParameter(dyingAnimatorParameter,false);
                colliderFilter.BelongsTo = 1u << 7;
                physicsCollider.Value.Value.SetCollisionFilter(colliderFilter);
                break;
            case StateID.Wait:
                parametersAspect.SetBoolParameter(dyingAnimatorParameter,true);
                disableSp.state = StateID.WaitAnimation;
                colliderFilter.BelongsTo = 1u << 9;
                physicsCollider.Value.Value.SetCollisionFilter(colliderFilter);
                break;
            case StateID.WaitAnimation:
                if ((time - disableSp.startTime) > 1)
                {
                    disableSp.state = StateID.CanDisable;
                }
                break;
        }
        
    }
}

[UpdateAfter(typeof(ZombieAnimationSystem))]
[BurstCompile]
public partial struct HandleSetActiveSystem : ISystem
{

    private EntityTypeHandle _entityTypeHandle;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _entityTypeHandle = state.GetEntityTypeHandle();
        state.RequireForUpdate<SetActiveSP>();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency.Complete();
        EntityQuery entityQuery = SystemAPI.QueryBuilder().WithAll<SetActiveSP>().Build();
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        _entityTypeHandle.Update(ref state);
        var active = new HandleSetActiveJob
        {
            ecb = ecb.AsParallelWriter(),
            linkedGroupBufferFromEntity = state.GetBufferLookup<LinkedEntityGroup>(true),
            entityTypeHandle = _entityTypeHandle,
            setActiveSpTypeHandle = state.GetComponentTypeHandle<SetActiveSP>(true)
        };
        state.Dependency = active.ScheduleParallel(entityQuery,state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

[BurstCompile]
public partial struct HandleSetActiveJob : IJobChunk
{
    [WriteOnly] public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public EntityTypeHandle entityTypeHandle;
    [ReadOnly] public ComponentTypeHandle<SetActiveSP> setActiveSpTypeHandle;
    [ReadOnly] public BufferLookup<LinkedEntityGroup> linkedGroupBufferFromEntity;
    

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {

        var setActiveSps = chunk.GetNativeArray(setActiveSpTypeHandle);
        var entities = chunk.GetNativeArray(entityTypeHandle);

        for (int i = 0; i < chunk.Count; i++)
        {
            var setActiveSp = setActiveSps[i];
            var entity = entities[i];
            bool check = false;
            switch (setActiveSp.state)
            {
                case StateID.CanDisable:
                    check = true;
                    ecb.SetEnabled(unfilteredChunkIndex,entity,false);
                    break;
                case StateID.CanEnable:
                    check = true;
                    if (linkedGroupBufferFromEntity.HasBuffer(entity))
                    {
                        var buffer = linkedGroupBufferFromEntity[entity];
                        for (int j = 0; j < buffer.Length; j++)
                        {
                            ecb.RemoveComponent<Disabled>(unfilteredChunkIndex, buffer[j].Value);
                        }
                    }
                    break;
            }
            if (check)
            {
                ecb.RemoveComponent<SetActiveSP>(unfilteredChunkIndex, entity);
            }
            
        }
    }
}