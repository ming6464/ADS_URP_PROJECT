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
        
        Entity entityNew = ecb.Instantiate(_playerProperty.entity);
        ecb.AddComponent<LocalTransform>(entityNew);
        ecb.AddComponent<LocalToWorld>(entityNew);
        ecb.AddComponent(entityNew,new CharacterInfo());
        ecb.AddComponent(entityNew,new Parent()
        {
            Value = _playerEntity,
        });
        
        ecb.Playback(state.EntityManager);
        
        state.Enabled = false;
    }
}