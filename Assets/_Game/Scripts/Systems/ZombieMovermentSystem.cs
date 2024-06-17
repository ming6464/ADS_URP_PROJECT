using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct ZombieMovermentSystem : ISystem
{

    private float3 _pointZoneMin;
    private float3 _pointZoneMax;
    private bool _init;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GenericZombieProperties>();
        state.RequireForUpdate<ActiveZoneProperty>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {

        if (!_init)
        {
            _init = true;

            var zone = SystemAPI.GetComponent<ActiveZoneProperty>(SystemAPI.GetSingletonEntity<ActiveZoneProperty>());
            _pointZoneMin = zone.pointRangeMin;
            _pointZoneMax = zone.pointRangeMax;
        }

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        GenericZombieProperties genericZombieProperties = SystemAPI
            .GetComponentRO<GenericZombieProperties>(SystemAPI.GetSingletonEntity<GenericZombieProperties>()).ValueRO;
        
        float deltaTime = SystemAPI.Time.DeltaTime;
        CheckZombieToDeadZone(ref state, ref ecb);
        ZombieMoveJOB job = new ZombieMoveJOB()
        {
            speed = genericZombieProperties.speed,
            deltaTime = deltaTime,
            targetPosition = genericZombieProperties.targetPosition,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
        ecb.Playback(state.EntityManager);
    }

    private void CheckZombieToDeadZone(ref SystemState state,ref EntityCommandBuffer ecb)
    {
        EntityQuery entityQuery = SystemAPI.QueryBuilder().WithAll<ZombieInfo>().WithNone<Disabled>()
            .WithNone<DisableSP>().Build();
        EntityManager entityManager = state.EntityManager;
        var arrayZomSet = entityQuery.ToEntityArray(Allocator.Temp);
        foreach (Entity entity in arrayZomSet)
        {
            LocalToWorld ltw = entityManager.GetComponentData<LocalToWorld>(entity);
            if (!CheckInRange(ltw.Position, _pointZoneMin, _pointZoneMax))
            {
                ecb.AddComponent<DisableSP>(entity);
            }
        }

        bool CheckInRange(float3 value, float3 min, float3 max)
        {
            if ((value.x - min.x) * (max.x - value.x) < 0) return false;
            if ((value.y - min.y) * (max.y - value.y) < 0) return false;
            if ((value.z - min.z) * (max.z - value.z) < 0) return false;
            return true;
        }
    }
    
    [BurstCompile]
    public partial struct ZombieMoveJOB : IJobEntity
    {
        [ReadOnly] public float speed;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float3 targetPosition;
        
        public void Execute(ZombieAspect aspect,in ZombieInfo zombie)
        {
            if(targetPosition.Equals(aspect.Position)) return;
            aspect.Position += zombie.directNormal * speed * deltaTime;
            aspect.Rotation = quaternion.LookRotation(zombie.directNormal, math.up());
        }
    }
}