using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct ZombieMovermentSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GenericZombieProperties>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        GenericZombieProperties genericZombieProperties = SystemAPI
            .GetComponentRO<GenericZombieProperties>(SystemAPI.GetSingletonEntity<GenericZombieProperties>()).ValueRO;
        ZombieMoveJOB job = new ZombieMoveJOB()
        {
            speed = genericZombieProperties.speed,
            deltaTime = deltaTime,
            targetPosition = genericZombieProperties.targetPosition,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }
    
    
    [BurstCompile]
    public partial struct ZombieMoveJOB : IJobEntity
    {
        [ReadOnly] public float speed;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float3 targetPosition;
        
        public void Execute(ZombieAspect aspect,in Zombie zombie)
        {
            if(targetPosition.Equals(aspect.Position)) return;
            aspect.Position += zombie.directNormal * speed * deltaTime;
            aspect.Rotation = quaternion.LookRotation(zombie.directNormal, math.up());
        }
    }
}