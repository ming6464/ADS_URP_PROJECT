using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct PlayerSystem : ISystem
{
    private EntityManager _entityManager;
    private PlayerProperty _playerProperty;
    private PlayerInput _playerMoveInput;
    private PlayerInfo _playerInfo;
    private Entity _playerEntity;
    private bool _init;
    private bool _aimNearestEnemy;
    private PlayerAspect _playerAspect;
    private float _moveToWardMin;
    private float _moveToWardMax;
    private float _distanceSetChangeRota;

    private EntityQuery _entityQueryPlayer;
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
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<LayerStoreComponent>();
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<PlayerProperty>();
        state.RequireForUpdate<PlayerInput>();
        state.RequireForUpdate<PlayerInfo>();
        state.RequireForUpdate<CharacterInfo>();
        _arrHitItem = new NativeList<ColliderCastHit>(Allocator.Persistent);
        _entityQueryPlayer = SystemAPI.QueryBuilder().WithAll<CharacterInfo>().Build();
    }


    public void OnDestroy(ref SystemState state)
    {
        if (_arrHitItem.IsCreated)
            _arrHitItem.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!_init)
        {
            _playerProperty = SystemAPI.GetSingleton<PlayerProperty>();
            _playerEntity = SystemAPI.GetSingletonEntity<PlayerInfo>();
            _aimNearestEnemy = _playerProperty.aimNearestEnemy;
            
            _moveToWardMin = _playerProperty.moveToWardMin;
            _moveToWardMax = _playerProperty.moveToWardMax;
            _distanceSetChangeRota = _playerProperty.distanceSetChangeRota;
            
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
        Rota(ref state);
        state.Dependency.Complete();
        // CheckCollider(ref state,ref ecb);
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


    private void Rota(ref SystemState state)
    {
        
        float3 dirRota;
        float moveToWard = _moveToWardMax;
        if (_aimNearestEnemy)
        {
            float spaceNearest = 99999f;
            float3 playerPosWorld = _playerAspect.PositionWorld;
            float3 positionNearest = float3.zero;
            bool check = false;
            foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<ZombieInfo>()
                         .WithNone<Disabled, SetActiveSP>())
            {
                check = true;
                float space = math.distance(playerPosWorld, ltw.ValueRO.Position);
                if (space < spaceNearest)
                {
                    positionNearest = ltw.ValueRO.Position;
                    spaceNearest = space;
                }
            }

            if (!check)
            {
                dirRota = _playerAspect.LocalToWorld.Forward;
            }
            else
            {
                dirRota = positionNearest - playerPosWorld;
                if (!dirRota.Equals(float3.zero))
                {
                    dirRota = math.normalize(dirRota);
                }
            }

            float ratio = 0;
            if (spaceNearest < _distanceSetChangeRota)
            {
                ratio = 1 - (spaceNearest * 1.0f / _distanceSetChangeRota);
            }

            moveToWard = math.lerp(_moveToWardMin, _moveToWardMax,ratio);
        }
        else
        {
            float x = math.remap(0, 1920, -1, 1, _playerMoveInput.mousePos.x);
            float y = 1 - math.abs(x);
            dirRota = math.normalize(new float3(x, 0, y));
        }
        
        
        state.Dependency = new CharacterRotate()
        {
            ltComponentTypeHandle = state.GetComponentTypeHandle<LocalTransform>(),
            directRota = dirRota,
            moveToWard = moveToWard,
            deltaTime = SystemAPI.Time.DeltaTime,
        }.ScheduleParallel(_entityQueryPlayer, state.Dependency);
    }

    private void CheckCollider(ref SystemState state, ref EntityCommandBuffer ecb)
    {
        _physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
        _playerInfo = SystemAPI.GetComponentRO<PlayerInfo>(_playerEntity).ValueRO;

        _maxXGridCharacter = _playerInfo.maxXGridCharacter;
        _maxYGridCharacter = _playerInfo.maxYGridCharacter;
        
        
        _ltwPlayer = SystemAPI.GetComponentRO<LocalToWorld>(_playerEntity).ValueRO;
        var halfX = (_maxXGridCharacter - 1) * _spaceGrid.x / 2f + _characterRadius;
        var halfZ = (_maxYGridCharacter - 1) * _spaceGrid.y / 2f + _characterRadius;
        var halfSizeBox = new float3(halfX, 1, halfZ);

        if (_physicsWorld.BoxCastAll(_ltwPlayer.Position, quaternion.identity, halfSizeBox, float3.zero, 0,
                ref _arrHitItem, _filterItem))
        {

            NativeArray<Entity> itemDisable =
                SystemAPI.QueryBuilder().WithAll<ItemCollection, Disabled>().Build().ToEntityArray(Allocator.Temp);
            int maxIndexItemDisable = itemDisable.Length - 1;
            for (int i = 0; i < _arrHitItem.Length; i++)
            {
                Entity entityItem = _arrHitItem[i].Entity;
                var itemInfo = _entityManager.GetComponentData<ItemInfo>(entityItem);
                Entity entityItemCollection;
                if (i <= maxIndexItemDisable)
                {
                    entityItemCollection = itemDisable[i];
                    ecb.RemoveComponent<Disabled>(entityItemCollection);
                    ecb.AddComponent(entityItemCollection,new SetActiveSP()
                    {
                        state = StateID.Enable,
                    });
                }
                else
                {
                    entityItemCollection = _entityManager.CreateEntity();
                }
                ecb.AddComponent(entityItemCollection,new ItemCollection()
                {
                    type = itemInfo.type,
                    count = itemInfo.count,
                    id = itemInfo.id,
                    entityItem = _arrHitItem[i].Entity,
                });
                
                ecb.RemoveComponent<PhysicsCollider>(entityItem);
                
                ecb.RemoveComponent<ItemInfo>(entityItem);
                
                ecb.AddComponent(entityItem,new SetActiveSP()
                {
                    state = StateID.DestroyAll,
                });
            }

            itemDisable.Dispose();
        }

        _arrHitItem.Clear();
    }
    

    private partial struct CharacterRotate : IJobChunk
    {
        public ComponentTypeHandle<LocalTransform> ltComponentTypeHandle;
        [ReadOnly] public float3 directRota;
        [ReadOnly] public float moveToWard;
        [ReadOnly] public float deltaTime;
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var lts = chunk.GetNativeArray(ltComponentTypeHandle);
            if(chunk.Count == 0) return;
            var targetRota = quaternion.LookRotationSafe(directRota,math.up());
            var curRota = lts[0].Rotation;
            var nextRota = MathExt.MoveTowards(curRota, targetRota, moveToWard * deltaTime);
            for (int i = 0; i < chunk.Count; i++)
            {
                var lt = lts[i];
                lt.Rotation = nextRota;
                lts[i] = lt;
            }
        }
    }
    
}
