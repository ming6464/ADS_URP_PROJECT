using Rukhanka;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

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
    public FastAnimatorParameter dyingAnimatorParameter;
    public float time;
    void Execute(AnimatorParametersAspect parametersAspect, ref SetActiveSP disableSp, in ZombieInfo zombieInfo)
    {
        switch (disableSp.status)
        {
            case 4:
                parametersAspect.SetBoolParameter(dyingAnimatorParameter,false);
                break;
            case 1:
                parametersAspect.SetBoolParameter(dyingAnimatorParameter,true);
                disableSp.status = 2;
                break;
            case 2:
                if ((time - disableSp.startTime) > 1)
                {
                    disableSp.status = 3;
                }
                break;
        }
        
    }
}


[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct HandleDeadSystem : ISystem
{
    private EntityManager _entityManager;
    private EntityCommandBuffer _ecb;


    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SetActiveSP>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _entityManager = state.EntityManager;
        EntityQuery queryDisable = SystemAPI.QueryBuilder().WithAll<SetActiveSP>().Build();
        using (var disabledEntities = queryDisable.ToEntityArray(Allocator.Temp))
        {
            _ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (Entity entity in disabledEntities)
            {
                var key = _entityManager.GetComponentData<SetActiveSP>(entity).status;
                switch (key)
                {
                    case 3:
                        Debug.Log("--------------- 1");
                        _ecb.RemoveComponent<SetActiveSP>(entity);
                        _ecb.AddComponent<Disabled>(entity);
                        foreach (LinkedEntityGroup linked in _entityManager.GetBuffer<LinkedEntityGroup>(entity))
                        {
                            Entity entity2 = linked.Value;
                            _ecb.AddComponent<Disabled>(entity2);
                        }
                        break;
                    case 4:
                        Debug.Log("--------------- 2");
                        _ecb.RemoveComponent<SetActiveSP>(entity);
                        // _ecb.RemoveComponent<Disabled>(entity);
                        foreach (LinkedEntityGroup linked in _entityManager.GetBuffer<LinkedEntityGroup>(entity))
                        {
                            Entity entity2 = linked.Value;
                            _ecb.RemoveComponent<Disabled>(entity2);
                        }
                        break;
                }
            }
            _ecb.Playback(_entityManager);
            _ecb.Dispose();
        }
        
    }
}