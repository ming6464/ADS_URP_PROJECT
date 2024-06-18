using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[BurstCompile]
public partial struct ZombieMovermentSystem : ISystem
{

    private float3 _pointZoneMin;
    private float3 _pointZoneMax;
    private bool _init;
    private ZombieProperty _zombieProperty;
    private EntityTypeHandle _entityTypeHandle;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ZombieProperty>();
        state.RequireForUpdate<ActiveZoneProperty>();
        state.RequireForUpdate<ZombieInfo>();
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
            _zombieProperty = SystemAPI
                .GetComponentRO<ZombieProperty>(SystemAPI.GetSingletonEntity<ZombieProperty>()).ValueRO;
        }
        state.Dependency.Complete();
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        Move(ref state);
        CheckZombieToDeadZone(ref state,ref ecb);
        state.Dependency.Complete();
        ecb.Playback(state.EntityManager);
    }

    private void Move(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        ZombieMovementJOB job = new ZombieMovementJOB()
        {
            speed = _zombieProperty.speed,
            deltaTime = deltaTime,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    private void CheckZombieToDeadZone(ref SystemState state,ref EntityCommandBuffer ecb)
    {
        _entityTypeHandle.Update(ref state);
        
         EntityQuery entityQuery = SystemAPI.QueryBuilder().WithAll<ZombieInfo>().WithNone<Disabled, SetActiveSP>().Build();
         
         var chunkJob = new CheckDeadZoneJOB
         {
             ecb = ecb.AsParallelWriter(),
             entityTypeHandle = _entityTypeHandle,
             ltwTypeHandle = state.GetComponentTypeHandle<LocalToWorld>(true),
             minPointRange = _pointZoneMin,
             maxPointRange = _pointZoneMax,
         };
        state.Dependency = chunkJob.ScheduleParallel(entityQuery, state.Dependency);
    }
    
    [BurstCompile]
    public partial struct ZombieMovementJOB : IJobEntity
    {
        [ReadOnly] public float speed;
        [ReadOnly] public float deltaTime;
        
        public void Execute(ref LocalTransform lt,in ZombieInfo zombie)
        {
            lt.Position += zombie.directNormal * speed * deltaTime;
            lt.Rotation = quaternion.LookRotation(zombie.directNormal, math.up());
        }
    }
    
    
    [BurstCompile]
    private partial struct CheckDeadZoneJOB : IJobChunk
{
    public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public EntityTypeHandle entityTypeHandle;
    [ReadOnly] public ComponentTypeHandle<LocalToWorld> ltwTypeHandle;
    [ReadOnly] public float3 minPointRange;
    [ReadOnly] public float3 maxPointRange;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var ltwArr = chunk.GetNativeArray(ref ltwTypeHandle);
        var entities = chunk.GetNativeArray(entityTypeHandle);

        for (int i = 0; i < chunk.Count; i++)
        {
            if (CheckInRange(ltwArr[i].Position, minPointRange, maxPointRange)) continue;
            ecb.AddComponent(unfilteredChunkIndex, entities[i], new SetActiveSP
            {
                state = StateID.CanDisable,
            });
        }

        bool CheckInRange(float3 value, float3 min, float3 max)
        {
            if ((value.x - min.x) * (max.x - value.x) < 0) return false;
            if ((value.y - min.y) * (max.y - value.y) < 0) return false;
            if ((value.z - min.z) * (max.z - value.z) < 0) return false;
            return true;
        }
    }
}

}