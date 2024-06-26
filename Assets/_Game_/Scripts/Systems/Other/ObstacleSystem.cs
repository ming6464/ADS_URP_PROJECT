using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace _Game_.Scripts.Systems.Other
{
    [BurstCompile,UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ObstacleSystem : ISystem
    {
        private NativeArray<BufferObstacle> _buffetObstacle;
        private EntityManager _entityManager;
        private bool _isInit;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ItemCollection>();
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_buffetObstacle.IsCreated)
                _buffetObstacle.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _entityManager = state.EntityManager;
            if (!_isInit)
            {
                _isInit = true;
                _buffetObstacle = SystemAPI.GetSingletonBuffer<BufferObstacle>().ToNativeArray(Allocator.Persistent);

            }
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (collection,entity) in SystemAPI.Query<RefRO<ItemCollection>>().WithEntityAccess()
                         .WithNone<Disabled, SetActiveSP>())
            {
                switch (collection.ValueRO.type)
                {
                    case ItemType.Obstacle:
                        Spawn(ref state,collection.ValueRO, ref ecb);
                        ecb.AddComponent(entity,new SetActiveSP()
                        {
                            state = StateID.Disable
                        });
                        break;
                }
            }
            
            ecb.Playback(_entityManager);
            ecb.Dispose();
        }

        private void Spawn(ref SystemState state,ItemCollection itemCollection, ref EntityCommandBuffer ecb)
        {
            BufferObstacle buffetObstacle = default;
            bool check = false;
            foreach (var obs in _buffetObstacle)
            {
                if(obs.id != itemCollection.id) continue;
                buffetObstacle = obs;
                check = true;
                break;
            }
            if(!check) return;
            var points = _entityManager.GetBuffer<BufferSpawnPoint>(itemCollection.entityItem);
            LocalTransform lt = new LocalTransform()
            {
                Scale = 1,
                Rotation = quaternion.identity
            };
            foreach (var point in points)
            {
                var newObs = ecb.Instantiate(buffetObstacle.entity);
                lt.Position = point.value;
                ecb.AddComponent(newObs,lt);
                ecb.AddComponent(newObs,new TurretInfo()
                {
                    id = itemCollection.id,
                });
            }
            points.Clear();
        }
        
    }
}