using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile, UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct PlayerSpawnSystem : ISystem
{
    private byte _spawnPlayerState;
    private Entity _characterEntityInstantiate;
    private Entity _entityPlayerInfo;
    private Entity _parentCharacterEntity;
    private bool _spawnInit;
    private EntityManager _entityManager;
    private float2 _spaceGrid;
    private PlayerProperty _playerProperty;
    private int _passCountOfCol;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProperty>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        if (_spawnPlayerState < 2)
        {
            if (_spawnPlayerState == 0)
            {
                _playerProperty = SystemAPI.GetSingleton<PlayerProperty>();
                var entityPlayerAuthoring =
                    SystemAPI.GetSingletonEntity<PlayerProperty>();
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
                ecb.AddBuffer<BufferCharacterNew>(entityPlayer);
                ecb.AddBuffer<BufferCharacterDie>(entityPlayer);
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
            _entityPlayerInfo = SystemAPI.GetSingletonEntity<PlayerInfo>();
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

        foreach (var (collection,entity) in SystemAPI.Query<RefRO<ItemCollection>>().WithEntityAccess()
                     .WithNone<Disabled, SetActiveSP>())
        {
            switch (collection.ValueRO.type)
            {
                case ItemType.Character:
                    spawnChange += collection.ValueRO.count;
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

        int countOfCol = math.max(2,(int)math.ceil(math.sqrt(totalNumber)));
        bool changeSizeCol = false;

        if (countOfCol != _passCountOfCol)
        {
            changeSizeCol = true;
            _passCountOfCol = countOfCol;
        }

        var bufferDisable = _entityManager.GetBuffer<BufferCharacterDie>(_entityPlayerInfo);
        
        if (count > 0)
        {
            var characterBuffer = _entityManager.GetBuffer<BufferCharacterNew>(_entityPlayerInfo);
            int i = changeSizeCol ? 0 : characterAliveCount;
            for (;i < totalNumber; i++)
            {
                lt.Position = GetPositionLocal_L(i, countOfCol, _spaceGrid);
                Entity entitySet;
                
                if (i < characterAliveCount)
                {
                    entitySet = characterAlive[i];
                }
                else
                {
                    if (bufferDisable.Length > 1)
                    {
                        entitySet = bufferDisable[0].entity;
                        bufferDisable.RemoveAt(0);
                        ecb.RemoveComponent<Disabled>(entitySet);
                        ecb.AddComponent(entitySet,new SetActiveSP()
                        {
                            state = StateID.Enable,
                        });
                    }
                    else
                    {
                        entitySet = _entityManager.Instantiate(_characterEntityInstantiate);
                        ecb.AddComponent<LocalToWorld>(entitySet);
                        ecb.AddComponent(entitySet, new Parent()
                        {
                            Value = _parentCharacterEntity,
                        });
                    }
                    characterBuffer.Add(new BufferCharacterNew()
                    {
                        entity = entitySet,
                    });
                    ecb.AddComponent(entitySet, new CharacterInfo()
                    {
                        index = i
                    });
                }
                ecb.AddComponent(entitySet, lt);
            }
        }
        else
        {
            int maxIndex = totalNumber - 1;
            int condition = totalNumber;
            if (changeSizeCol)
            {
                condition = 0;
            }
            for (int i = characterAliveCount - 1; i >= condition; i--)
            {
                if (i > maxIndex)
                {
                    var characterInfo = _entityManager.GetComponentData<CharacterInfo>(characterAlive[i]);
                    bufferDisable.Add(new BufferCharacterDie()
                    {
                        entity = characterAlive[i],
                    });
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
                else
                {
                    if(!changeSizeCol) break;
                    lt.Position = GetPositionLocal_L(i, countOfCol, _spaceGrid);
                    ecb.AddComponent(characterAlive[i], lt);
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

        var playInfo = SystemAPI.GetComponentRW<PlayerInfo>(_entityPlayerInfo);
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
