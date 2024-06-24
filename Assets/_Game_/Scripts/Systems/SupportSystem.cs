using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;


//

[BurstCompile,UpdateInGroup(typeof(PresentationSystemGroup))]
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
            ecb.AddComponent(entityCamFirst, new CameraComponent { type = CameraType.FirstPersonCamera});
            var entityCamThirst = ecb.CreateEntity();
            ecb.AddComponent(entityCamThirst, new Parent { Value = entityParent });
            ecb.AddComponent<LocalToWorld>(entityCamThirst);
            ecb.AddComponent(entityCamThirst, new LocalTransform
            {
                Position = camProperty.offsetCamThirst,
                Rotation = camProperty.offsetRotationCamThirst,
                Scale = 1,
            });
            ecb.AddComponent(entityCamThirst, new CameraComponent { type = CameraType.ThirstPersonCamera });
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            state.Enabled = false;
        }
    }
}

//
[BurstCompile,UpdateInGroup(typeof(PresentationSystemGroup))]
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
        EntityQuery entityQuery = SystemAPI.QueryBuilder().WithAll<SetActiveSP>().Build();
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        _entityTypeHandle.Update(ref state);
        var active = new HandleSetActiveJob
        {
            ecb = ecb.AsParallelWriter(),
            linkedGroupBufferLockUp = state.GetBufferLookup<LinkedEntityGroup>(true),
            childBufferLockUp = state.GetBufferLookup<Child>(true),
            entityTypeHandle = _entityTypeHandle,
            setActiveSpTypeHandle = state.GetComponentTypeHandle<SetActiveSP>(true)
        };
        state.Dependency = active.ScheduleParallel(entityQuery,state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
    
     
    [BurstCompile]
    partial struct HandleSetActiveJob : IJobChunk
    {
        [WriteOnly] public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public EntityTypeHandle entityTypeHandle;
        [ReadOnly] public ComponentTypeHandle<SetActiveSP> setActiveSpTypeHandle;
        [ReadOnly] public BufferLookup<LinkedEntityGroup> linkedGroupBufferLockUp;
        [ReadOnly] public BufferLookup<Child> childBufferLockUp;
    

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
                    case StateID.Disable:
                        check = true;
                        ecb.SetEnabled(unfilteredChunkIndex,entity,false);
                        break;
                    case StateID.Enable:
                        check = true;
                        if (linkedGroupBufferLockUp.HasBuffer(entity))
                        {
                            var buffer = linkedGroupBufferLockUp[entity];
                            for (int j = 0; j < buffer.Length; j++)
                            {
                                ecb.RemoveComponent<Disabled>(unfilteredChunkIndex, buffer[j].Value);
                            }
                        }
                        break;
                    case StateID.Destroy:
                        check = true;
                        ecb.DestroyEntity(unfilteredChunkIndex,entity);
                        break;
                    case StateID.DestroyAll:
                        check = true;
                        DestroyAllChildren(entity,unfilteredChunkIndex);
                        break;
                }
                if (check)
                {
                    ecb.RemoveComponent<SetActiveSP>(unfilteredChunkIndex, entity);
                }
            }
            
        }
        void DestroyAllChildren(Entity entity,int index)
        {
            if (childBufferLockUp.HasBuffer(entity))
            {
                var buffer = childBufferLockUp[entity];
                for (int j = buffer.Length - 1; j >= 0; j--)
                {
                    DestroyAllChildren(buffer[j].Value, index);
                }
            }
            else
            {
                ecb.DestroyEntity(index,entity);
            }
        }
    }
    
}
//
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class UpdateHybrid : SystemBase
{
    // Camera {
    public delegate void EventCamera(Vector3 positionWorld,Quaternion rotationWorld, CameraType type);
    public delegate void EventHitFlashEffect(Vector3 position,Quaternion rotation);
    public EventCamera UpdateCamera;
    // Camera }
    
    //Effect {
    
    public EventHitFlashEffect UpdateHitFlashEff;
    private NativeQueue<LocalTransform> _hitFlashQueue;
    
    //Effect }
    
    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        RequireForUpdate<CameraComponent>();
        _hitFlashQueue = new NativeQueue<LocalTransform>(Allocator.Persistent);
    }

    protected override void OnStopRunning()
    {
        base.OnStopRunning();
        _hitFlashQueue.Dispose();
    }

    protected override void OnUpdate()
    {
        UpdateCameraEvent();
        UpdateEffectEvent();
    }

    private void UpdateEffectEvent()
    {
        Dependency.Complete();
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        var hitFlashEff = new HandleHitFlashEffectEventJOB()
        {
            ltwTypeHandle = GetComponentTypeHandle<LocalTransform>(),
            hitFlashQueue = _hitFlashQueue.AsParallelWriter(),
            entityTypeHandle = GetEntityTypeHandle(),
            ecb = ecb.AsParallelWriter(),
        };
        var enQuery = GetEntityQuery(ComponentType.ReadOnly<EffectComponent>());
        Dependency = hitFlashEff.ScheduleParallel(enQuery, Dependency); 
        Dependency.Complete();
        ecb.Playback(EntityManager);
        ecb.Dispose();
        while (_hitFlashQueue.TryDequeue(out var lt))
        {
            UpdateHitFlashEff?.Invoke(lt.Position,lt.Rotation);
        }
    }
    private void UpdateCameraEvent()
    {
        Entities.WithoutBurst().WithAll<CameraComponent>().ForEach((in LocalToWorld ltw, in CameraComponent camComponent) =>
        {
            UpdateCamera(ltw.Position,ltw.Rotation, camComponent.type);
        }).Run();
    }
    
    
    //JOB
    partial struct HandleHitFlashEffectEventJOB : IJobChunk
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        public EntityTypeHandle entityTypeHandle;
        public NativeQueue<LocalTransform>.ParallelWriter hitFlashQueue;
        [ReadOnly] public ComponentTypeHandle<LocalTransform> ltwTypeHandle;
        
        public void Execute(in ArchetypeChunk chunk, int indexQuery, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var lts = chunk.GetNativeArray(ltwTypeHandle);
            var entities = chunk.GetNativeArray(entityTypeHandle);
            for (int i = 0; i < chunk.Count; i++)
            {
                ecb.AddComponent(indexQuery,entities[i],new SetActiveSP()
                {
                    state = StateID.Disable,
                });
                hitFlashQueue.Enqueue(lts[i]);
            }
            ecb.RemoveComponent<EffectComponent>(indexQuery,entities);
        }
    }
    //JOB
}


