﻿using Unity.Burst;
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
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ZombieProperty>();
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
        ZombieProperty genericZombieProperties = SystemAPI
            .GetComponentRO<ZombieProperty>(SystemAPI.GetSingletonEntity<ZombieProperty>()).ValueRO;
        
        float deltaTime = SystemAPI.Time.DeltaTime;
        CheckZombieToDeadZone(ref state, ref ecb);
        ZombieMoveJOB job = new ZombieMoveJOB()
        {
            speed = genericZombieProperties.speed,
            deltaTime = deltaTime,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
        ecb.Playback(state.EntityManager);
    }

    private void CheckZombieToDeadZone(ref SystemState state,ref EntityCommandBuffer ecb)
    {
        EntityQuery entityQuery = SystemAPI.QueryBuilder().WithAll<ZombieInfo>().WithNone<Disabled>()
            .WithNone<SetActiveSP>().Build();
        EntityManager entityManager = state.EntityManager;
        var arrayZomSet = entityQuery.ToEntityArray(Allocator.Temp);
        foreach (Entity entity in arrayZomSet)
        {
            LocalToWorld ltw = entityManager.GetComponentData<LocalToWorld>(entity);
            if (!CheckInRange(ltw.Position, _pointZoneMin, _pointZoneMax))
            {
                ecb.AddComponent(entity,new SetActiveSP
                {
                    status = 3,
                    startTime = 0,
                });
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
        
        public void Execute(ref LocalTransform lt,in ZombieInfo zombie)
        {
            lt.Position += zombie.directNormal * speed * deltaTime;
            lt.Rotation = quaternion.LookRotation(zombie.directNormal, math.up());
        }
    }
}