using Rukhanka;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

//
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class AnimationStateSystem : SystemBase
{
    private readonly FastAnimatorParameter _dieAnimatorParameter = new("Die");
    private readonly FastAnimatorParameter _runAnimatorParameter = new("Run");
    private readonly FastAnimatorParameter _attackAnimatorParameter = new("Attack");
    private LayerStoreComponent _layerStoreComponent;
    private bool _isInit;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<LayerStoreComponent>();
    }

    protected override void OnUpdate()
    {
        CheckAndInit();
        AnimationZombieHandle();
        AnimationPlayerHandle();
    }

    private void CheckAndInit()
    {
        if (!_isInit)
        {
            _isInit = true;
            _layerStoreComponent = SystemAPI.GetSingleton<LayerStoreComponent>();
        }
    }

    private void AnimationPlayerHandle()
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        var characterAnimJob = new ProcessAnimCharacter()
        {
            runAnimatorParameter = _runAnimatorParameter,
            ecb = ecb.AsParallelWriter(),
            timeDelta = (float)SystemAPI.Time.DeltaTime,
            dieAnimatorParameter = _dieAnimatorParameter,
            
        };
        Dependency = characterAnimJob.ScheduleParallel(Dependency);
        Dependency.Complete();
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private void AnimationZombieHandle()
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        var zombieAnimatorJob = new ProcessAnimZombie()
        {
            dieAnimatorParameter = _dieAnimatorParameter,
            timeDelta = (float)SystemAPI.Time.DeltaTime,
            enemyLayer = _layerStoreComponent.enemyLayer,
            enemyDieLayer = _layerStoreComponent.enemyDieLayer,
            attackAnimatorParameter = _attackAnimatorParameter,
            ecb = ecb.AsParallelWriter(),
            runAnimatorParameter = _runAnimatorParameter
        };
        Dependency = zombieAnimatorJob.ScheduleParallel(Dependency);
        Dependency.Complete();
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }


    [BurstCompile]
    partial struct ProcessAnimCharacter : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public float timeDelta;
        [ReadOnly] public FastAnimatorParameter runAnimatorParameter;
        [ReadOnly] public FastAnimatorParameter dieAnimatorParameter;

        void Execute(in CharacterInfo characterInfo, ref SetAnimationSP setAnimation, Entity entity,
            [EntityIndexInQuery] int indexQuery, AnimatorParametersAspect parametersAspect)
        {
            setAnimation.timeDelay -= timeDelta;
            switch (setAnimation.state)
            {
                case StateID.Enable:
                    parametersAspect.SetBoolParameter(dieAnimatorParameter, false);
                    setAnimation.state = StateID.WaitRemove;
                    break;
                case StateID.None:
                    parametersAspect.SetBoolParameter(runAnimatorParameter, false);
                    setAnimation.state = StateID.WaitRemove;
                    break;
                case StateID.Run:
                    parametersAspect.SetBoolParameter(runAnimatorParameter, true);
                    setAnimation.state = StateID.WaitRemove;
                    break;
                case StateID.Die:
                    parametersAspect.SetBoolParameter(dieAnimatorParameter, true);
                    setAnimation.state = StateID.WaitToPool;
                    break;
                case StateID.WaitToPool:
                    if (setAnimation.timeDelay <= 0)
                    {
                        setAnimation.state = StateID.WaitRemove;
                        ecb.AddComponent(indexQuery, entity, new SetActiveSP()
                        {
                            state = DisableID.Disable,
                        });
                    }
                    break;
                case StateID.Idle:
                    setAnimation.state = StateID.WaitRemove;
                    break;
            }

            if (setAnimation is { state: StateID.WaitRemove, timeDelay: <= 0 })
            {
                ecb.RemoveComponent<SetAnimationSP>(indexQuery,entity);
            }
            
        }
    }

    [BurstCompile]
    partial struct ProcessAnimZombie : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [ReadOnly] public FastAnimatorParameter dieAnimatorParameter;
        [ReadOnly] public FastAnimatorParameter attackAnimatorParameter;
        [ReadOnly] public FastAnimatorParameter runAnimatorParameter;
        [ReadOnly] public float timeDelta;
        [ReadOnly] public uint enemyLayer;
        [ReadOnly] public uint enemyDieLayer;

        void Execute(in ZombieInfo zombieInfo,ref ZombieRuntime runtime, ref SetAnimationSP setAnimation,Entity entity,[EntityIndexInQuery]int indexQuery  , AnimatorParametersAspect parametersAspect,
            ref PhysicsCollider physicsCollider)
        {
            setAnimation.timeDelay -= timeDelta;
            var colliderFilter = physicsCollider.Value.Value.GetCollisionFilter();
            runtime.latestAnimState = setAnimation.state;
            switch (setAnimation.state)
            {
                case StateID.Enable:
                    parametersAspect.SetBoolParameter(dieAnimatorParameter, false);
                    parametersAspect.SetBoolParameter(attackAnimatorParameter, false);
                    parametersAspect.SetBoolParameter(runAnimatorParameter, false);
                    colliderFilter.BelongsTo = enemyLayer;
                    physicsCollider.Value.Value.SetCollisionFilter(colliderFilter);
                    setAnimation.state = StateID.WaitRemove;
                    break;
                case StateID.Die:
                    parametersAspect.SetBoolParameter(dieAnimatorParameter, true);
                    setAnimation.state = StateID.WaitToPool;
                    colliderFilter.BelongsTo = enemyDieLayer;
                    physicsCollider.Value.Value.SetCollisionFilter(colliderFilter);
                    break;
                case StateID.WaitToPool:
                    if (setAnimation.timeDelay <= 0)
                    {
                        setAnimation.state = StateID.WaitRemove;
                        ecb.AddComponent(indexQuery, entity, new SetActiveSP()
                        {
                            state = DisableID.Disable,
                        });
                    }
                    break;
                case StateID.Attack:
                    parametersAspect.SetBoolParameter(attackAnimatorParameter, true);
                    if (setAnimation.timeDelay <= 0)
                    {
                        setAnimation.state = StateID.WaitRemove;
                        parametersAspect.SetBoolParameter(attackAnimatorParameter, false);
                    }
                    break;
                case StateID.Run:
                    parametersAspect.SetBoolParameter(runAnimatorParameter,true);
                    setAnimation.state = StateID.WaitRemove;
                    break;
                case StateID.Idle:
                    parametersAspect.SetBoolParameter(dieAnimatorParameter, false);
                    parametersAspect.SetBoolParameter(attackAnimatorParameter, false);
                    parametersAspect.SetBoolParameter(runAnimatorParameter, false);
                    setAnimation.state = StateID.WaitRemove;
                    break;
            }
            
            if (setAnimation is { state: StateID.WaitRemove, timeDelay: <= 0 })
            {
                ecb.RemoveComponent<SetAnimationSP>(indexQuery,entity);
            }

        }
    }
}
//

[BurstCompile, UpdateInGroup(typeof(PresentationSystemGroup))]
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
        ecb.AddComponent(entityCamFirst, new CameraComponent { type = CameraType.FirstPersonCamera });
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

//
[BurstCompile, UpdateInGroup(typeof(PresentationSystemGroup))]
public partial struct HandleSetActiveSystem : ISystem
{
    private EntityTypeHandle _entityTypeHandle;
    private EntityQuery _enQuerySetActive;
    private ComponentTypeHandle<SetActiveSP> _setActiveSPTypeHandle;
    private BufferLookup<LinkedEntityGroup> _linkedBufferLookup;
    private BufferLookup<Child> _childBufferLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SetActiveSP>();
        _setActiveSPTypeHandle = state.GetComponentTypeHandle<SetActiveSP>();
        _linkedBufferLookup = state.GetBufferLookup<LinkedEntityGroup>();
        _childBufferLookup = state.GetBufferLookup<Child>();
        _entityTypeHandle = state.GetEntityTypeHandle();
        _enQuerySetActive = SystemAPI.QueryBuilder().WithAll<SetActiveSP>().Build();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        _entityTypeHandle.Update(ref state);
        _linkedBufferLookup.Update(ref state);
        _childBufferLookup.Update(ref state);
        _setActiveSPTypeHandle.Update(ref state);
        var active = new HandleSetActiveJob
        {
            ecb = ecb.AsParallelWriter(),
            linkedGroupBufferLookup = _linkedBufferLookup,
            childBufferLookup = _childBufferLookup,
            entityTypeHandle = _entityTypeHandle,
            setActiveSpTypeHandle = _setActiveSPTypeHandle
        };
        state.Dependency = active.ScheduleParallel(_enQuerySetActive, state.Dependency);
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
        [ReadOnly] public BufferLookup<LinkedEntityGroup> linkedGroupBufferLookup;
        [ReadOnly] public BufferLookup<Child> childBufferLookup;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var setActiveSps = chunk.GetNativeArray(ref setActiveSpTypeHandle);
            var entities = chunk.GetNativeArray(entityTypeHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                var setActiveSp = setActiveSps[i];
                var entity = entities[i];
                bool stateHandled = HandleState(setActiveSp.state, entity, unfilteredChunkIndex);

                if (stateHandled)
                {
                    ecb.RemoveComponent<SetActiveSP>(unfilteredChunkIndex, entity);
                }
            }
        }

        private bool HandleState(DisableID state, Entity entity, int chunkIndex)
        {
            switch (state)
            {
                case DisableID.Disable:
                    ecb.SetEnabled(chunkIndex, entity, false);
                    return true;
                case DisableID.Enable:
                    if (linkedGroupBufferLookup.HasBuffer(entity))
                    {
                        var buffer = linkedGroupBufferLookup[entity];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            ecb.RemoveComponent<Disabled>(chunkIndex, buffer[i].Value);
                        }
                    }

                    return true;
                case DisableID.Destroy:
                    ecb.DestroyEntity(chunkIndex, entity);
                    return true;
                case DisableID.DestroyAll:
                    DestroyAllChildren(entity, chunkIndex);
                    return true;
                default:
                    return false;
            }
        }

        private void DestroyAllChildren(Entity entity, int chunkIndex)
        {
            if (childBufferLookup.HasBuffer(entity))
            {
                var buffer = childBufferLookup[entity];
                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    DestroyAllChildren(buffer[i].Value, chunkIndex);
                }
            }

            ecb.DestroyEntity(chunkIndex, entity);
        }
    }
}

//
[BurstCompile, UpdateInGroup(typeof(PresentationSystemGroup)), UpdateAfter(typeof(HandleSetActiveSystem))]
public partial struct HandlePoolZombie : ISystem
{
    private NativeList<BufferZombieDie> _zombieDieToPoolList;
    private EntityQuery _entityQuery;
    private EntityTypeHandle _entityTypeHandle;
    private EntityManager _entityManager;
    private ZombieProperty _zombieProperty;
    private ComponentTypeHandle<ZombieInfo> _zombieInfoTypeHandle;
    private bool _isInit;

    private int _currentCountZombieDie;
    private int _passCountZombieDie;
    private int _countCheck;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        RequireNecessaryComponents(ref state);
        Init(ref state);
    }

    [BurstCompile]
    private void Init(ref SystemState state)
    {
        _countCheck = 200;
        _entityQuery = SystemAPI.QueryBuilder().WithAll<ZombieInfo, AddToBuffer, Disabled>().Build();
        _entityTypeHandle = state.GetEntityTypeHandle();
        _zombieInfoTypeHandle = state.GetComponentTypeHandle<ZombieInfo>();
    }

    [BurstCompile]
    private void RequireNecessaryComponents(ref SystemState state)
    {
        state.RequireForUpdate<ZombieProperty>();
        state.RequireForUpdate<AddToBuffer>();
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
        CheckAndInit();
        LoadZombieToPool(ref state);
    }

    [BurstCompile]
    private void LoadZombieToPool(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        _entityTypeHandle.Update(ref state);
        _zombieInfoTypeHandle.Update(ref state);
        var job = new GetListZombieDataToPool()
        {
            zombieDieToPoolList = _zombieDieToPoolList.AsParallelWriter(),
            entityTypeHandle = _entityTypeHandle,
            zombieInfoTypeHandle = _zombieInfoTypeHandle,
            ecb = ecb.AsParallelWriter(),
        };
        state.Dependency = job.ScheduleParallel(_entityQuery, state.Dependency);
        state.Dependency.Complete();
        ecb.Playback(_entityManager);
        ecb.Dispose();
        if (_countCheck < (_zombieDieToPoolList.Length - _passCountZombieDie))
        {
            _countCheck = _zombieDieToPoolList.Length - _passCountZombieDie + 100;
        }

        _passCountZombieDie = _zombieDieToPoolList.Length;
        _entityManager.GetBuffer<BufferZombieDie>(_zombieProperty.entity).AddRange(_zombieDieToPoolList);
        if (_passCountZombieDie > 0)
        {
            var runtime = _entityManager.GetComponentData<ZombieSpawnRuntime>(_zombieProperty.entity);
            runtime.zombieAlive -= _passCountZombieDie;
            _entityManager.SetComponentData(_zombieProperty.entity, runtime);
        }
    }

    [BurstCompile]
    private void CheckAndInit()
    {
        if (!_isInit)
        {
            _isInit = true;
            _zombieProperty = SystemAPI.GetSingleton<ZombieProperty>();
            _currentCountZombieDie = 500;
            _zombieDieToPoolList = new NativeList<BufferZombieDie>(_currentCountZombieDie, Allocator.Persistent);
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
    }

    [BurstCompile]
    partial struct GetListZombieDataToPool : IJobChunk
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [WriteOnly] public NativeList<BufferZombieDie>.ParallelWriter zombieDieToPoolList;
        [ReadOnly] public EntityTypeHandle entityTypeHandle;
        [ReadOnly] public ComponentTypeHandle<ZombieInfo> zombieInfoTypeHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(entityTypeHandle);
            var zombieInfos = chunk.GetNativeArray(ref zombieInfoTypeHandle);

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

            ecb.RemoveComponent<AddToBuffer>(unfilteredChunkIndex, entities);
        }
    }
}
//
//

// [BurstCompile, UpdateInGroup(typeof(PresentationSystemGroup)), UpdateAfter(typeof(HandleSetActiveSystem))]
// public partial struct HandlePoolCharacter : ISystem
// {
//     private NativeList<BufferCharacterDie> _characterDieToPoolList;
//     private Entity _entityPlayerInfo;
//     private EntityQuery _entityQuery;
//     private EntityTypeHandle _entityTypeHandle;
//     private EntityManager _entityManager;
//     private bool _isInit;
//
//     private int _currentCountCharacterDie;
//     private int _passCountCharacterDie;
//     private int _countCheck;
//
//     [BurstCompile]
//     public void OnCreate(ref SystemState state)
//     {
//         state.RequireForUpdate<CharacterInfo>();
//         state.RequireForUpdate<AddToBuffer>();
//         state.RequireForUpdate<Disabled>();
//         _countCheck = 20;
//         _entityQuery = SystemAPI.QueryBuilder().WithAll<CharacterInfo, AddToBuffer, Disabled>().Build();
//         _entityTypeHandle = state.GetEntityTypeHandle();
//     }
//
//     [BurstCompile]
//     public void OnDestroy(ref SystemState state)
//     {
//         if (_characterDieToPoolList.IsCreated)
//             _characterDieToPoolList.Dispose();
//     }
//
//     [BurstCompile]
//     public void OnUpdate(ref SystemState state)
//     {
//         return;
//         _entityManager = state.EntityManager;
//         if (!_isInit)
//         {
//             _isInit = true;
//             _entityPlayerInfo = SystemAPI.GetSingletonEntity<PlayerInfo>();
//             _currentCountCharacterDie = 0;
//             _characterDieToPoolList =
//                 new NativeList<BufferCharacterDie>(_currentCountCharacterDie, Allocator.Persistent);
//         }
//
//         if (_currentCountCharacterDie - _passCountCharacterDie < _countCheck)
//         {
//             _characterDieToPoolList.Dispose();
//             _currentCountCharacterDie = _passCountCharacterDie + _countCheck;
//             _characterDieToPoolList =
//                 new NativeList<BufferCharacterDie>(_currentCountCharacterDie, Allocator.Persistent);
//             return;
//         }
//         else
//         {
//             _characterDieToPoolList.Clear();
//         }
//
//         _entityTypeHandle.Update(ref state);
//         EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
//         var job = new GetListCharacterDataToPool()
//         {
//             characterDieToPoolList = _characterDieToPoolList.AsParallelWriter(),
//             entityTypeHandle = _entityTypeHandle,
//             ecb = ecb.AsParallelWriter(),
//         };
//         state.Dependency = job.ScheduleParallel(_entityQuery, state.Dependency);
//         state.Dependency.Complete();
//         ecb.Playback(_entityManager);
//         ecb.Dispose();
//
//         if (_countCheck < (_characterDieToPoolList.Length - _passCountCharacterDie))
//         {
//             _countCheck = _characterDieToPoolList.Length - _passCountCharacterDie + 10;
//         }
//
//         _passCountCharacterDie = _characterDieToPoolList.Length;
//         _entityManager.GetBuffer<BufferCharacterDie>(_entityPlayerInfo).AddRange(_characterDieToPoolList);
//     }
//
//     [BurstCompile]
//     partial struct GetListCharacterDataToPool : IJobChunk
//     {
//         public EntityCommandBuffer.ParallelWriter ecb;
//         [WriteOnly] public NativeList<BufferCharacterDie>.ParallelWriter characterDieToPoolList;
//         [ReadOnly] public EntityTypeHandle entityTypeHandle;
//
//         public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
//             in v128 chunkEnabledMask)
//         {
//             var entities = chunk.GetNativeArray(entityTypeHandle);
//
//             for (int i = 0; i < chunk.Count; i++)
//             {
//                 var entity = entities[i];
//                 characterDieToPoolList.AddNoResize(new BufferCharacterDie()
//                 {
//                     entity = entity,
//                 });
//             }
//
//             ecb.RemoveComponent<AddToBuffer>(unfilteredChunkIndex, entities);
//         }
//     }
// }

//
[BurstCompile, UpdateInGroup(typeof(PresentationSystemGroup))]
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
        state.RequireForUpdate<WeaponProperty>();
        state.RequireForUpdate<AddToBuffer>();
        _countCheck = 300;
        _entityQuery = SystemAPI.QueryBuilder().WithAll<BulletInfo, AddToBuffer, Disabled>().Build();
        _bufferBulletDisables = new NativeList<BufferBulletDisable>(_countCheck, Allocator.Persistent);
        _currentCountWeaponDisable = _countCheck;
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
            _entityWeaponProperty = SystemAPI.GetSingletonEntity<WeaponProperty>();
        }


        if (_currentCountWeaponDisable - _passCountWeaponDisable < _countCheck)
        {
            _bufferBulletDisables.Dispose();
            _currentCountWeaponDisable = _passCountWeaponDisable + _countCheck;
            _bufferBulletDisables =
                new NativeList<BufferBulletDisable>(_currentCountWeaponDisable, Allocator.Persistent);
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

        if (_countCheck < (_bufferBulletDisables.Length - _passCountWeaponDisable))
        {
            _countCheck = _bufferBulletDisables.Length - _passCountWeaponDisable + 200;
        }

        _passCountWeaponDisable = _bufferBulletDisables.Length;
        _entityManager.GetBuffer<BufferBulletDisable>(_entityWeaponProperty).AddRange(_bufferBulletDisables);
    }


    partial struct GetListDataToPool : IJobChunk
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        [WriteOnly] public NativeList<BufferBulletDisable>.ParallelWriter bulletToPoolList;
        [ReadOnly] public EntityTypeHandle entityTypeHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
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

            ecb.RemoveComponent<AddToBuffer>(unfilteredChunkIndex, entities);
        }
    }
}

//
[UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
public partial class UpdateHybrid : SystemBase
{
    // Camera {
    public delegate void EventCamera(Vector3 positionWorld, Quaternion rotationWorld, CameraType type);

    public delegate void EventHitFlashEffect(Vector3 position, Quaternion rotation,EffectID effectID);

    public delegate void EventChangText(int idText, int value);

    public EventCamera UpdateCamera;
    // Camera }

    //Effect {

    public EventHitFlashEffect UpdateHitFlashEff;
    //Effect }

    //Change text {
    public EventChangText UpdateText;

    // Change Text}
    private ComponentTypeHandle<EffectComponent> _effectTypeHandle;

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        RequireForUpdate<CameraComponent>();
        _effectTypeHandle = GetComponentTypeHandle<EffectComponent>();
    }


    protected override void OnUpdate()
    {
        UpdateCameraEvent();
        UpdateEffectEvent();
        UpdateChangeText();
    }

    private void UpdateChangeText()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.TempJob);
        Entities.ForEach((ref ChangeTextNumberMesh changeText, ref Entity entity) =>
        {
            UpdateText?.Invoke(changeText.id, changeText.value);
            if (changeText.value <= 0)
            {
                ecb.AddComponent(entity, new SetActiveSP()
                {
                    state = DisableID.Destroy,
                });
            }
            else
            {
                ecb.RemoveComponent<ChangeTextMesh>(entity);
            }
        }).WithoutBurst().Run();
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private void UpdateEffectEvent()
    {
        // return;
        _effectTypeHandle.Update(this);
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        Entities.ForEach((EffectComponent eff,Entity entity) =>
        {
            UpdateHitFlashEff?.Invoke(eff.position, eff.rotation, eff.effectID);
            ecb.RemoveComponent<EffectComponent>(entity);
        }).WithoutBurst().Run();
        Dependency.Complete();
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    private void UpdateCameraEvent()
    {
        Entities.WithoutBurst().WithAll<CameraComponent>()
            .ForEach((in LocalToWorld ltw, in CameraComponent camComponent) =>
            {
                UpdateCamera?.Invoke(ltw.Position, ltw.Rotation, camComponent.type);
            }).Run();
    }


    //JOB
    //JOB
    
    //
    //
}