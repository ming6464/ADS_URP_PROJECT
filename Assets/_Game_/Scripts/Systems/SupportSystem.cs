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
    private EntityQuery _enQueryPlayerInfo;
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInfo>();
        state.RequireForUpdate<CameraProperty>();
        _enQueryPlayerInfo = SystemAPI.QueryBuilder().WithAll<PlayerInfo>().Build();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_enQueryPlayerInfo.IsEmpty)
        {
            Entity entityParent = _enQueryPlayerInfo.GetSingletonEntity();
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
    private EntityQuery _enQuerySetActive;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SetActiveSP>();
        _entityTypeHandle = state.GetEntityTypeHandle();
        _enQuerySetActive = SystemAPI.QueryBuilder().WithAll<SetActiveSP>().Build();
    }
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        
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
        state.Dependency = active.ScheduleParallel(_enQuerySetActive,state.Dependency);
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
[BurstCompile,UpdateInGroup(typeof(PresentationSystemGroup)),UpdateAfter(typeof(HandleSetActiveSystem))]
public partial struct HandlePoolZombie : ISystem
{
    private NativeList<BufferZombieDie> _zombieDieToPoolList;
    private Entity _entityZombieProperty;
    private EntityQuery _entityQuery;
    private EntityTypeHandle _entityTypeHandle;
    private EntityManager _entityManager;
    private bool _isInit;

    private int _currentCountZombieDie;
    private int _passCountZombieDie;
    private int _countCheck;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ZombieProperty>();
        state.RequireForUpdate<AddToBuffer>();
        _countCheck = 100;
        _entityQuery = SystemAPI.QueryBuilder().WithAll<ZombieInfo, AddToBuffer,Disabled>().Build();
        _entityTypeHandle = state.GetEntityTypeHandle();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_zombieDieToPoolList.IsCreated)
            _zombieDieToPoolList.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _entityManager = state.EntityManager;
        if (!_isInit)
        {
            _isInit = true;
            _entityZombieProperty = SystemAPI.GetSingletonEntity<ZombieProperty>();
        }

        if (_currentCountZombieDie - _passCountZombieDie < _countCheck)
        {
            _zombieDieToPoolList.Dispose();
            _currentCountZombieDie = _passCountZombieDie + _countCheck;
            _zombieDieToPoolList = new NativeList<BufferZombieDie>(_currentCountZombieDie, Allocator.Persistent);
        }
        else
        {
            _zombieDieToPoolList.Clear();
        }
        
        
        
        _entityTypeHandle.Update(ref state);
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        var job = new GetListZombieDataToPool()
        {
            zombieDieToPoolList = _zombieDieToPoolList.AsParallelWriter(),
            entityTypeHandle = _entityTypeHandle,
            zombieInfoTypeHandle = state.GetComponentTypeHandle<ZombieInfo>(),
            ecb = ecb.AsParallelWriter(),
        };
        state.Dependency = job.ScheduleParallel(_entityQuery, state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(_entityManager);
        ecb.Dispose();
        _passCountZombieDie = _zombieDieToPoolList.Length;
        _entityManager.GetBuffer<BufferZombieDie>(_entityZombieProperty).AddRange(_zombieDieToPoolList);
    }
    
    
    partial struct GetListZombieDataToPool : IJobChunk
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [WriteOnly] public NativeList<BufferZombieDie>.ParallelWriter zombieDieToPoolList;
        [ReadOnly] public EntityTypeHandle entityTypeHandle;
        [ReadOnly] public ComponentTypeHandle<ZombieInfo> zombieInfoTypeHandle;
        
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(entityTypeHandle);
            var zombieInfos = chunk.GetNativeArray(zombieInfoTypeHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                var entity = entities[i];
                var zombieInfo = zombieInfos[i];
                zombieDieToPoolList.AddNoResize(new BufferZombieDie()
                {
                    id = zombieInfo.id,
                    entity = entity,
                });
            }
            ecb.RemoveComponent<AddToBuffer>(unfilteredChunkIndex,entities);
        }
    }
}
//
public partial struct HandlePoolBullet : ISystem
{
    private NativeList<BufferBulletDisable> _bufferBulletDisables;
    private Entity _entityWeaponProperty;
    private EntityQuery _entityQuery;
    private EntityTypeHandle _entityTypeHandle;
    private EntityManager _entityManager;
    private bool _isInit;

    private int _currentCountWeaponDisable;
    private int _passCountWeaponDisable;
    private int _countCheck;
    
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<WeaponProperties>();
        state.RequireForUpdate<AddToBuffer>();
        _countCheck = 200;
        _entityQuery = SystemAPI.QueryBuilder().WithAll<BulletInfo, AddToBuffer,Disabled>().Build();
        _entityTypeHandle = state.GetEntityTypeHandle();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_bufferBulletDisables.IsCreated)
            _bufferBulletDisables.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _entityManager = state.EntityManager;
        if (!_isInit)
        {
            _isInit = true;
            _entityWeaponProperty = SystemAPI.GetSingletonEntity<WeaponProperties>();
        }

        if (_currentCountWeaponDisable - _passCountWeaponDisable < _countCheck)
        {
            _bufferBulletDisables.Dispose();
            _currentCountWeaponDisable = _passCountWeaponDisable + _countCheck;
            _bufferBulletDisables = new NativeList<BufferBulletDisable>(_currentCountWeaponDisable, Allocator.Persistent);
        }
        else
        {
            _bufferBulletDisables.Clear();
        }
        
        
        
        _entityTypeHandle.Update(ref state);
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        var job = new GetListDataToPool()
        {
            bulletToPoolList = _bufferBulletDisables.AsParallelWriter(),
            entityTypeHandle = _entityTypeHandle,
            ecb = ecb.AsParallelWriter(),
        };
        state.Dependency = job.ScheduleParallel(_entityQuery, state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(_entityManager);
        ecb.Dispose();
        _passCountWeaponDisable = _bufferBulletDisables.Length;
        _entityManager.GetBuffer<BufferBulletDisable>(_entityWeaponProperty).AddRange(_bufferBulletDisables);
    }
    
    
    partial struct GetListDataToPool : IJobChunk
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [WriteOnly] public NativeList<BufferBulletDisable>.ParallelWriter bulletToPoolList;
        [ReadOnly] public EntityTypeHandle entityTypeHandle;
        
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(entityTypeHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                var entity = entities[i];
                bulletToPoolList.AddNoResize(new BufferBulletDisable()
                {
                    entity = entity,
                });
            }
            ecb.RemoveComponent<AddToBuffer>(unfilteredChunkIndex,entities);
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

    private EntityQuery _enQuery;
    //Effect }
    
    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        RequireForUpdate<CameraComponent>();
        _hitFlashQueue = new NativeQueue<LocalTransform>(Allocator.Persistent);
        _enQuery = SystemAPI.QueryBuilder().WithAll<EffectComponent>().Build();
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
        _hitFlashQueue.Clear();
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        var hitFlashEff = new HandleHitFlashEffectEventJOB()
        {
            effTypeHandle = GetComponentTypeHandle<EffectComponent>(),
            hitFlashQueue = _hitFlashQueue.AsParallelWriter(),
            entityTypeHandle = GetEntityTypeHandle(),
            ecb = ecb.AsParallelWriter(),
        };
        Dependency = hitFlashEff.ScheduleParallel(_enQuery, Dependency); 
        Dependency.Complete();
        while (_hitFlashQueue.TryDequeue(out var lt))
        {
            UpdateHitFlashEff?.Invoke(lt.Position,lt.Rotation);
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
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
        [WriteOnly] public NativeQueue<LocalTransform>.ParallelWriter hitFlashQueue;
        [ReadOnly] public ComponentTypeHandle<EffectComponent> effTypeHandle;
        
        public void Execute(in ArchetypeChunk chunk, int indexQuery, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var effs = chunk.GetNativeArray(effTypeHandle);
            var entities = chunk.GetNativeArray(entityTypeHandle);
            LocalTransform lt;
            for (int i = 0; i < chunk.Count; i++)
            {
                lt = new LocalTransform()
                {
                    Position = effs[i].position,
                    Rotation = effs[i].rotation,
                };
                hitFlashQueue.Enqueue(lt);
                ecb.RemoveComponent<EffectComponent>(indexQuery,entities[i]);
            }
        }
    }
    //JOB
}


