using Rukhanka;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;


// public partial class HandleAnimationSystem : SystemBase
// {
//     private FastAnimatorParameter _dyingAnimatorParameter = new FastAnimatorParameter("Die");
//     
//     protected override void OnUpdate()
//     {
//         // var zombieAnimatorJob = new ProcessAnimZombie()
//         // {
//         //     dyingAnimatorParameter = _dyingAnimatorParameter,
//         //     time = (float)SystemAPI.Time.ElapsedTime,
//         // };
//         // Dependency = zombieAnimatorJob.ScheduleParallel(Dependency);
//     }
// }
//
// [BurstCompile]
// public partial struct ProcessAnimZombie : IJobEntity
// {
//     public FastAnimatorParameter dyingAnimatorParameter;
//     public float time;
//     void Execute(AnimatorParametersAspect parametersAspect, in ZombieInfo zombieInfo, ref DisableSP disableSp)
//     {
//         Debug.Log("Hello_1");
//         switch (disableSp.key)
//         {
//             case DisableKEY.Wait:
//                 parametersAspect.SetTrigger(dyingAnimatorParameter);
//                 disableSp.key = DisableKEY.WaitAnimation;
//                 break;
//             case DisableKEY.WaitAnimation:
//                 if ((time - disableSp.startTime) > 1)
//                 {
//                     Debug.Log("Hello_2");
//                     disableSp.key = DisableKEY.CanDisable;
//                 }
//                 break;
//         }
//         
//     }
// }



[BurstCompile]
public partial struct HandleDeadSystem : ISystem
{
    private EntityManager _entityManager;
    private EntityCommandBuffer _ecb;
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _entityManager = state.EntityManager;
        EntityQuery queryDisable = SystemAPI.QueryBuilder().WithAll<DisableSP>().Build();
        
        using (var disabledEntities = queryDisable.ToEntityArray(Allocator.Temp))
        {
            _ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (Entity entity in disabledEntities)
            {
                // if(!_entityManager.GetComponentData<DisableSP>(entity).key.Equals(DisableKEY.CanDisable)) continue;
                _ecb.RemoveComponent<DisableSP>(entity);
                _ecb.AddComponent<Disabled>(entity);
                foreach (LinkedEntityGroup linked in _entityManager.GetBuffer<LinkedEntityGroup>(entity))
                {
                    Entity entity2 = linked.Value;
                    _ecb.AddComponent<Disabled>(entity2);
                }
            }
            
            _ecb.Playback(_entityManager);
            _ecb.Dispose();
        }
        
    }
}