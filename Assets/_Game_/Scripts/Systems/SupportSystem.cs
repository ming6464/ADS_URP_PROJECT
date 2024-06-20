using Rukhanka;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

// 
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
                if ((time - disableSp.startTime) > 4)
                {
                    disableSp.state = StateID.CanDisable;
                }
                break;
        }
        
    }
}

//

[BurstCompile]
public partial struct CameraSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInfo>();
        state.RequireForUpdate<CameraProperty>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityQuery entityQuery = SystemAPI.QueryBuilder().WithAll<PlayerInfo>().Build();
        if (!entityQuery.IsEmpty)
        {
            Entity entityParent = entityQuery.GetSingletonEntity();
            var camProperty = SystemAPI.GetSingleton<CameraProperty>();
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            var entityCamFirst = ecb.CreateEntity();
            ecb.AddComponent(entityCamFirst, new Parent { Value = entityParent });
            ecb.AddComponent<LocalToWorld>(entityCamFirst);
            ecb.AddComponent(entityCamFirst, new LocalTransform
            {
                Position = camProperty.offsetCamFirst,
                Rotation = camProperty.offsetRotationCamFirst,
                Scale = 1,
            });
            ecb.AddComponent(entityCamFirst, new CameraComponent { isFirstPerson = true });
            var entityCamThirst = ecb.CreateEntity();
            ecb.AddComponent(entityCamThirst, new Parent { Value = entityParent });
            ecb.AddComponent<LocalToWorld>(entityCamThirst);
            ecb.AddComponent(entityCamThirst, new LocalTransform
            {
                Position = camProperty.offsetCamThirst,
                Rotation = camProperty.offsetRotationCamThirst,
                Scale = 1,
            });
            ecb.AddComponent(entityCamThirst, new CameraComponent { isFirstPerson = false });
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            state.Enabled = false;
        }
    }
}

//

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

//

public partial class UpdateHybrid : SystemBase
{
    public delegate void EventCamera(LocalToWorld ltw, bool isFirstPerson);
    public EventCamera UpdateCamera;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        RequireForUpdate<CameraComponent>();
    }

    protected override void OnUpdate()
    {
        Debug.Log("Hello -1 ");
        Entities.WithoutBurst().WithAll<CameraComponent>().ForEach((in LocalToWorld ltw, in CameraComponent camComponent) =>
        {
            Debug.Log("Hello -2 ");
            UpdateCamera?.Invoke(ltw, camComponent.isFirstPerson);
        }).Run();
    }
}


//