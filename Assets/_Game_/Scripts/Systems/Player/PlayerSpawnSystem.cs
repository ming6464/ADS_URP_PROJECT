using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile, UpdateInGroup(typeof(InitializationSystemGroup)),UpdateAfter(typeof(PlayerSystem))]
public partial struct PlayerSpawnSystem : ISystem
{
    private byte _spawnPlayerState;
    private Entity _characterEntityInstantiate;
    private Entity _playerEntity;
    private Entity _parentCharacterEntity;
    private PlayerProperty _playerProperty;
    private bool _spawnInit;
    private EntityManager _entityManager;
    private float2 _spaceGrid;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProperty>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        if (_spawnPlayerState < 2)
        {
            if (_spawnPlayerState == 0)
            {
                _playerProperty = SystemAPI.GetSingleton<PlayerProperty>();
                var entityPlayer = ecb.CreateEntity();
                ecb.AddComponent(entityPlayer, new PlayerInfo()
                {
                    idWeapon = _playerProperty.idWeaponDefault,
                });
                ecb.AddComponent<LocalToWorld>(entityPlayer);
                ecb.AddComponent(entityPlayer, new LocalTransform()
                {
                    Position = _playerProperty.spawnPosition,
                    Rotation = quaternion.identity,
                    Scale = 1,
                });
                ecb.AddBuffer<CharacterNewBuffer>(entityPlayer);
                
                var parentCharacter = ecb.CreateEntity();
                ecb.AddComponent<ParentCharacter>(parentCharacter);
                ecb.AddComponent(parentCharacter, new Parent()
                {
                    Value = entityPlayer,
                });
                DotsEX.AddTransformDefault(ref ecb,parentCharacter);
                
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
                _spawnPlayerState = 1;
                return;
            }
            _parentCharacterEntity = SystemAPI.GetSingletonEntity<ParentCharacter>();
            _playerEntity = SystemAPI.GetSingletonEntity<PlayerInfo>();
            _characterEntityInstantiate = _playerProperty.characterEntity;
            _entityManager = state.EntityManager;
            _spawnPlayerState = 2;
            _spaceGrid = _playerProperty.spaceGrid;
        }
        
        UpdateCharacter(ref state, ref ecb);
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    private void UpdateCharacter(ref SystemState state, ref EntityCommandBuffer ecb)
    {
        int spawnChange = 0;
        if (!_spawnInit)
        {
            _spawnInit = true;
            spawnChange = _playerProperty.numberSpawnDefault;
        }

        foreach(var (collect,entity) in SystemAPI.Query<RefRO<ItemCollection>>().WithEntityAccess().WithNone<Disabled,SetActiveSP>())
        {
            switch (collect.ValueRO.type)
            {
                case ItemType.Character:
                    spawnChange += collect.ValueRO.count;
                    ecb.AddComponent(entity,new SetActiveSP()
                    {
                        state = StateID.Disable,
                    });
                    break;
            }
           
        }
        
        Spawn(ref state, ref ecb, spawnChange);
    }

    private void Spawn(ref SystemState state, ref EntityCommandBuffer ecb, int count)
    {
        if(count == 0) return;
        var lt = DotsEX.LocalTransformDefault();
        int countOfCol = _playerProperty.countOfCol;
        NativeArray<Entity> characterAlive = SystemAPI.QueryBuilder().WithAll<CharacterInfo>().WithNone<Disabled>().Build().ToEntityArray(Allocator.Temp);
        int characterAliveCount = characterAlive.Length;
        var totalNumber = count + characterAliveCount;
        if (totalNumber < 0)
        {
            if (characterAlive.Length == 0)
            {
                characterAlive.Dispose();
                return;
            }
            totalNumber = 0;
        }

        if (count > 0)
        {
            var characterBuffer = _entityManager.GetBuffer<CharacterNewBuffer>(_playerEntity);
            NativeArray<Entity> characterDisable = SystemAPI.QueryBuilder().WithAll<CharacterInfo>().WithAll<Disabled>().Build().ToEntityArray(Allocator.Temp);
            int maxIndexUsing = -1;
            int maxIndexCharacterDisable = characterDisable.Length - 1;

            for (int i = characterAliveCount; i < totalNumber; i++)
            {
                maxIndexUsing++;
                lt.Position = GetPositionLocal_L(i, countOfCol, _spaceGrid);

                Entity entityNew;
                if (maxIndexUsing <= maxIndexCharacterDisable)
                {
                    entityNew = characterDisable[maxIndexUsing];
                    ecb.RemoveComponent<Disabled>(entityNew);
                    ecb.AddComponent(entityNew,new SetActiveSP()
                    {
                        state = StateID.Enable,
                    });
                    
                }
                else
                {
                    entityNew = _entityManager.Instantiate(_characterEntityInstantiate);
                    ecb.AddComponent<LocalToWorld>(entityNew);
                    ecb.AddComponent(entityNew, new Parent()
                    {
                        Value = _parentCharacterEntity,
                    });
                }
                characterBuffer.Add(new CharacterNewBuffer()
                {
                    entity = entityNew,
                });
                ecb.AddComponent(entityNew, lt);
                ecb.AddComponent(entityNew, new CharacterInfo()
                {
                    index = i
                });
            }
            characterDisable.Dispose();
        }
        else
        {
            int maxIndex = totalNumber - 1;
            int numberDisable = 0;
            for (int i = characterAliveCount - 1; i >= 0; i--)
            {
                if (numberDisable + count == 0) break;
                var characterInfo = _entityManager.GetComponentData<CharacterInfo>(characterAlive[i]);
                if (characterInfo.index > maxIndex)
                {
                    numberDisable++;
                    ecb.AddComponent(characterAlive[i], new SetActiveSP()
                    {
                        state = StateID.Disable
                    });

                    if (!characterInfo.weaponEntity.Equals(default))
                    {
                        ecb.RemoveComponent<Parent>(characterInfo.weaponEntity);
                        ecb.AddComponent(characterInfo.weaponEntity,new SetActiveSP()
                        {
                            state = StateID.Disable,
                        });
                    }
                }
            }
        }

        int maxX = totalNumber;
        int maxY = 1;

        if (maxX > countOfCol)
        {
            maxX = countOfCol;
            maxY = (int)math.ceil(totalNumber * 1.0f / maxX);
        }

        float maxWidthCharacters = _spaceGrid.x * (maxX - 1);
        float maxHeightCharacters = _spaceGrid.y * (maxY - 1);
        ecb.SetComponent(_parentCharacterEntity, new LocalTransform()
        {
            Position = new float3(-maxWidthCharacters / 2f, 0, maxHeightCharacters / 2f),
            Scale = 1,
            Rotation = quaternion.identity,
        });

        var playInfo = SystemAPI.GetComponentRW<PlayerInfo>(_playerEntity);
        playInfo.ValueRW.maxXGridCharacter = maxX;
        playInfo.ValueRW.maxYGridCharacter = maxY;
        characterAlive.Dispose();

        // local function
        float3 GetPositionLocal_L(int index, int maxCol, float2 space)
        {
            float3 grid = GetGridPos_L(index, maxCol);
            grid.z *= -space.y;
            grid.x *= space.x;

            return grid;
        }

        float3 GetGridPos_L(int index, int maxCol)
        {
            var grid = new float3(0, 0, 0);

            if (index < 0)
            {
                grid.x = -1;
            }
            else if (index < countOfCol)
            {
                grid.x = index;
            }
            else
            {
                grid.x = index % maxCol;
                grid.z = index / maxCol;
            }

            return grid;
        }
    }
}
