using Rukhanka;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class HandleAnimationSystem : SystemBase
{
    private FastAnimatorParameter _dyingAnimatorParameter = new FastAnimatorParameter("Die");
    
    protected override void OnUpdate()
    {
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
    void Execute(AnimatorParametersAspect parametersAspect, ref SetActiveSP disableSp, in ZombieInfo zombieInfo)
    {
        switch (disableSp.state)
        {
            case StateID.CanEnable:
                parametersAspect.SetBoolParameter(dyingAnimatorParameter,false);
                break;
            case StateID.Wait:
                parametersAspect.SetBoolParameter(dyingAnimatorParameter,true);
                disableSp.state = StateID.WaitAnimation;
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


[BurstCompile]
public partial struct HandleSetActiveSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency.Complete();
        
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        var active = new HandleSetActiveJob
        {
            ecb = ecb.AsParallelWriter(),
            linkedGroupBufferFromEntity = state.GetBufferLookup<LinkedEntityGroup>(true)
        };

        state.Dependency = active.ScheduleParallel(state.Dependency);

        state.Dependency.Complete(); // Chờ job hoàn thành trước khi phát lại ECB
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

[BurstCompile]
public partial struct HandleSetActiveJob : IJobEntity
{
    [WriteOnly] public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public BufferLookup<LinkedEntityGroup> linkedGroupBufferFromEntity;

    private void Execute(in SetActiveSP setActiveSp, [EntityIndexInQuery] int entityInQueryIndex, in Entity entity)
    {
        bool check = false;

        switch (setActiveSp.state)
        {
            case StateID.CanDisable:
                check = true;
                ecb.RemoveComponent<SetActiveSP>(entityInQueryIndex, entity);
                ecb.AddComponent<Disabled>(entityInQueryIndex, entity);

                if (linkedGroupBufferFromEntity.HasBuffer(entity))
                {
                    var buffer = linkedGroupBufferFromEntity[entity];
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        ecb.AddComponent<Disabled>(entityInQueryIndex, buffer[i].Value);
                    }
                }
                break;
            case StateID.CanEnable:
                check = true;
                ecb.RemoveComponent<SetActiveSP>(entityInQueryIndex, entity);

                if (linkedGroupBufferFromEntity.HasBuffer(entity))
                {
                    var buffer = linkedGroupBufferFromEntity[entity];
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        ecb.RemoveComponent<Disabled>(entityInQueryIndex, buffer[i].Value);
                    }
                }
                break;
        }

        if (check)
        {
            ecb.RemoveComponent<SetActiveSP>(entityInQueryIndex, entity);
        }
    }
}