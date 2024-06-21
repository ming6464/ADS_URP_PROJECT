using System.ComponentModel;
using Rukhanka;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;

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
    
    
    [BurstCompile]
    public partial struct ProcessAnimZombie : IJobEntity
    {
        [ReadOnly(true)] public FastAnimatorParameter dyingAnimatorParameter;
        [ReadOnly(true)] public float time;
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
                    if ((time - disableSp.startTime) > 4)
                    {
                        disableSp.state = StateID.CanDisable;
                    }
                    break;
            }
        
        }
    }
    
}

