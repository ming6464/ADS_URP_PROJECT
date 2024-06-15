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
            var myComponentLookup = state.GetComponentLookup<AnimatedSkinnedMeshComponent>();
            _ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (Entity entity in disabledEntities)
            {
                
                // if (_entityManager.HasComponent<RenderMesh>(entity))
                // {
                //     _ecb.RemoveComponent<RenderMesh>(entity);
                // }
                // if (_entityManager.HasComponent<RenderBounds>(entity))
                // {
                //     _ecb.RemoveComponent<RenderBounds>(entity);
                // }
                
                
                if (_entityManager.HasComponent<LinkedEntityGroup>(entity))
                {
                    var linkedEntities = _entityManager.GetBuffer<LinkedEntityGroup>(entity);
                    foreach (var linkedEntity in linkedEntities)
                    {
                        Entity entity1 = linkedEntity.Value;
                        // if (_entityManager.HasComponent<AnimatedSkinnedMeshComponent>(linkedEntity.Value))
                        // {
                        //     // var skinnedMeshRenderer = _entityManager.GetComponentObject<AnimatedSkinnedMeshComponent>(linkedEntity.Value);
                        //     // myComponentLookup.SetComponentEnabled(entity,false);
                        //     _ecb.SetComponentEnabled<AnimatedSkinnedMeshComponent>(entity1,false);
                        // }
                        //
                        // if (_entityManager.HasComponent<RenderMeshArray>(linkedEntity.Value))
                        // {
                        //     // var skinnedMeshRenderer = _entityManager.GetComponentObject<AnimatedSkinnedMeshComponent>(linkedEntity.Value);
                        //     // myComponentLookup.SetComponentEnabled(entity,false);
                        //     // _ecb.SetComponentEnabled<AnimatedSkinnedMeshComponent>(entity1,false);
                        //     _ecb.RemoveComponent<RenderMeshArray>(entity1);
                        // }
                        
                        if (_entityManager.HasComponent<RenderBounds>(entity1))
                        {
                            // var skinnedMeshRenderer = _entityManager.GetComponentObject<AnimatedSkinnedMeshComponent>(linkedEntity.Value);
                            // myComponentLookup.SetComponentEnabled(entity,false);
                            // _ecb.SetComponentEnabled<AnimatedSkinnedMeshComponent>(entity1,false);
                            // _ecb.RemoveComponent<RenderBounds>(entity1);
                            if (!_entityManager.HasComponent<Disabled>(entity1))
                            {
                                _ecb.AddComponent<Disabled>(entity1);
                                // _ecb.AddComponentForLinkedEntityGroup(entity1,entity1.);
                            }
                        }

                        
                        // if (_entityManager.HasComponent<RenderMesh>(entity1))
                        // {
                        //     _ecb.RemoveComponent<RenderMesh>(entity1);
                        // }
                        // if (_entityManager.HasComponent<RenderBounds>(entity1))
                        // {
                        //     _ecb.RemoveComponent<RenderBounds>(entity1);
                        // }
                    }
                }
                
                _ecb.RemoveComponent<DisableSP>(entity);
                _ecb.RemoveComponent<LocalToWorld>(entity);
                if (!_entityManager.HasComponent<Disabled>(entity))
                {
                    _ecb.AddComponent<Disabled>(entity);
                }
            }
            _ecb.Playback(_entityManager);
        }
        
    }
}