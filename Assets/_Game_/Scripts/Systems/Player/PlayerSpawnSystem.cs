using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile,UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct PlayerSpawnSystem : ISystem
{
    private byte _spawnPlayerState;
    private Entity _playerEntity;
    private PlayerProperty _playerProperty;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProperty>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        if (_spawnPlayerState != 2)
        {
            if (_spawnPlayerState == 0)
            {
                _playerProperty = SystemAPI.GetSingleton<PlayerProperty>();
                var entityPlayer = ecb.CreateEntity();
                ecb.AddComponent<LocalToWorld>(entityPlayer);
                ecb.AddComponent(entityPlayer,new LocalTransform()
                {
                    Position = _playerProperty.spawnPosition,
                    Rotation = quaternion.identity,
                    Scale = 1,
                });
                ecb.AddComponent(entityPlayer,new PlayerInfo());
                ecb.Playback(state.EntityManager);
                _spawnPlayerState = 1;
                return;
            }
            _playerEntity = SystemAPI.GetSingletonEntity<PlayerInfo>();
            _spawnPlayerState = 2;
        }

        SpawnCharacter(ref state,ref ecb);
        
        ecb.Playback(state.EntityManager);
        
        state.Enabled = false;
    }

    private void SpawnCharacter(ref SystemState state,ref EntityCommandBuffer ecb)
    {
        Entity entityIns = _playerProperty.entity;
        Entity entityPlayer = _playerEntity;
        
        var lt = new LocalTransform
        {
            Rotation = quaternion.identity,
            Scale = 1,
        };

        int totalNumber = _playerProperty.numberSpawn;
        int countOfCol = _playerProperty.countOfCol;
        float2 space = _playerProperty.spaceGrid;
        int maxX = totalNumber;
        int maxY = 1;
        if (maxX > countOfCol)
        {
            maxX = countOfCol;
            maxY = (int)math.ceil(totalNumber* 1.0f / countOfCol);
        }
        for (int i = 0; i < maxY; i++)
        {
            float z = -getPos(maxY, i,space.y);

            int countJ = maxX;
            if (maxY - i == 1)
            {
                countJ = totalNumber - ((maxY - 1) * countOfCol);
            }
            
            for (int j = 0; j < countJ; j++)
            {
                float x = getPos(maxX, j,space.x);
                lt.Position = new float3(x, 0, z);
                Spawn_c(lt, ref ecb);
            }
        }
        
        float getPos(int number, int index,float space)
        {
            index++;
            int halfNumber = (int)math.ceil(number / 2f);
            float subtractIndex = 0.5f;

            if (number % 2 != 0)
            {
                if (index == halfNumber) return 0;
                subtractIndex = 0f;
            }

            int i = index - halfNumber;
            
            return (i - subtractIndex) * space;
        }
        void Spawn_c(LocalTransform lt,ref EntityCommandBuffer ecb)
        {
            Entity entityNew = ecb.Instantiate(entityIns);
            ecb.AddComponent<LocalToWorld>(entityNew);
            ecb.AddComponent(entityNew,lt);
            ecb.AddComponent(entityNew,new CharacterInfo());
            ecb.AddComponent(entityNew,new Parent()
            {
                Value = entityPlayer,
            });
        }
        
    }
}