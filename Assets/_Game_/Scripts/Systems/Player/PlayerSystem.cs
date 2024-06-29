using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct PlayerSystem : ISystem
{
    private EntityManager _entityManager;
    private PlayerProperty _playerProperty;
    private PlayerInput _playerMoveInput;
    private PlayerInfo _playerInfo;
    private Entity _playerEntity;
    private bool _init;
    private PlayerAspect _playerAspect;
    //
    private int _maxXGridCharacter;
    private int _maxYGridCharacter;
    private float2 _spaceGrid;
    private float _characterRadius;
    private LocalToWorld _ltwPlayer;
    private PhysicsWorldSingleton _physicsWorld;
    private NativeList<ColliderCastHit> _arrHitItem;
    private CollisionFilter _filterItem;
    private LayerStoreComponent _layerStore;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<LayerStoreComponent>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<PlayerProperty>();
        state.RequireForUpdate<PlayerInput>();
        state.RequireForUpdate<PlayerInfo>();
        state.RequireForUpdate<CharacterInfo>();
        _arrHitItem = new NativeList<ColliderCastHit>(Allocator.Persistent);
    }


    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_arrHitItem.IsCreated)
            _arrHitItem.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!_init)
        {
            _playerProperty = SystemAPI.GetSingleton<PlayerProperty>();
            _playerEntity = SystemAPI.GetSingletonEntity<PlayerInfo>();
            _characterRadius = _playerProperty.characterRadius;
            _layerStore = SystemAPI.GetSingleton<LayerStoreComponent>();
            _filterItem = new CollisionFilter()
            {
                BelongsTo = _layerStore.playerLayer,
                CollidesWith = _layerStore.itemLayer,
                GroupIndex = 0,
            };
            _spaceGrid = _playerProperty.spaceGrid;
            _characterRadius = _playerProperty.characterRadius;
            _init = true;
            _entityManager = state.EntityManager;
        }
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        Move(ref state);
        CheckCollider(ref state,ref ecb);
        state.Dependency.Complete();
        ecb.Playback(_entityManager);
        ecb.Dispose();
    }

    private void Move(ref SystemState state)
    {
        _playerMoveInput = SystemAPI.GetSingleton<PlayerInput>();
        _playerAspect = SystemAPI.GetAspect<PlayerAspect>(_playerEntity);
        float2 direct = _playerMoveInput.directMove;
        _playerAspect.Position += new float3(direct.x, 0, direct.y) * _playerProperty.speed * SystemAPI.Time.DeltaTime;
    }

    private void CheckCollider(ref SystemState state, ref EntityCommandBuffer ecb)
    {
        _physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        _playerInfo = SystemAPI.GetComponentRO<PlayerInfo>(_playerEntity).ValueRO;

        _maxXGridCharacter = math.max(1,_playerInfo.maxXGridCharacter);
        _maxYGridCharacter = math.max(1,_playerInfo.maxYGridCharacter);
        
        _ltwPlayer = SystemAPI.GetComponentRO<LocalToWorld>(_playerEntity).ValueRO;
        var halfX = (_maxXGridCharacter - 1) * _spaceGrid.x / 2f + _characterRadius;
        var halfZ = (_maxYGridCharacter - 1) * _spaceGrid.y / 2f + _characterRadius;
        var halfSizeBox = new float3(halfX, 1, halfZ);
        _arrHitItem.Clear();
        if (_physicsWorld.BoxCastAll(_ltwPlayer.Position, quaternion.identity, halfSizeBox, float3.zero, 0,
                ref _arrHitItem, _filterItem))
        {
            foreach (var t in _arrHitItem)
            {
                Entity entityItem = t.Entity;
                
                if(!_entityManager.HasComponent<ItemInfo>(entityItem))continue;
                
                var itemInfo = _entityManager.GetComponentData<ItemInfo>(entityItem);

                var entityCollectionNew = _entityManager.CreateEntity();

                if (_entityManager.HasBuffer<BufferSpawnPoint>(entityItem))
                {
                    var buffer = ecb.AddBuffer<BufferSpawnPoint>(entityCollectionNew);
                    buffer.CopyFrom(_entityManager.GetBuffer<BufferSpawnPoint>(entityItem));
                }
                
                ecb.AddComponent(entityCollectionNew,new ItemCollection()
                {
                    type = itemInfo.type,
                    count = itemInfo.count,
                    id = itemInfo.id,
                    entityItem = entityCollectionNew,
                });
                ecb.AddComponent(entityItem,new SetActiveSP()
                {
                    state = StateID.DestroyAll,
                });
            }
        }

    }
}

public enum AimType
{
    TeamAim,
    IndividualAim
}
