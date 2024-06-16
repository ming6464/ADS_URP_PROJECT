using Rukhanka;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

[BurstCompile]
public partial struct SupportSystem : ISystem
{
    private EntityManager _entityManager;
    private EntityCommandBuffer _ecb;
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _entityManager = state.EntityManager;
        EntityQuery queryDisable = SystemAPI.QueryBuilder().WithAll<DisableSP>().Build();
        
        using (var disabledEntities = queryDisable.ToEntityArray(Allocator.Temp))
        {

            _ecb = new EntityCommandBuffer(Allocator.Temp);
            
            
            
            foreach (Entity entity in disabledEntities)
            {
                _ecb.AddComponent<Disabled>(entity);
                _ecb.RemoveComponent<DisableSP>(entity);
                foreach (LinkedEntityGroup linked in _entityManager.GetBuffer<LinkedEntityGroup>(entity))
                {
                    Entity entity2 = linked.Value;
                    _ecb.AddComponent<Disabled>(entity2);
                }
            }
            
            _ecb.Playback(_entityManager);
            _ecb.Dispose();
        }
        
    }
}