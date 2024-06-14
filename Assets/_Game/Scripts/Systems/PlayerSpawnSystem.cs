using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile,UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct PlayerSpawnSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProperty>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        Entity entity = SystemAPI.GetSingletonEntity<PlayerProperty>();
        PlayerProperty playerProperty = SystemAPI.GetComponentRO<PlayerProperty>(entity).ValueRO;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        Entity entityNew = ecb.Instantiate(playerProperty.entity);
        ecb.AddComponent(entityNew,new LocalTransform()
        {
            Position = playerProperty.spawnPosition,
            Rotation = quaternion.identity,
            Scale = 1,
        });
        
        ecb.AddComponent<PlayerInfo>(entityNew);
        
        ecb.Playback(state.EntityManager);
        
        state.Enabled = false;
    }
}