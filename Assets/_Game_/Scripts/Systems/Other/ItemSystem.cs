using _Game_.Scripts.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace _Game_.Scripts.Systems.Other
{
    [BurstCompile,UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ItemSystem : ISystem
    {
        private NativeArray<BufferTurretObstacle> _buffetObstacle;
        private EntityManager _entityManager;
        private bool _isInit;
        private float _time; 
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInfo>();
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
            if (!_isInit)
            {
                _entityManager = state.EntityManager;
                _isInit = true;
                _buffetObstacle = SystemAPI.GetSingletonBuffer<BufferTurretObstacle>().ToNativeArray(Allocator.Persistent);
                _time = (float)SystemAPI.Time.ElapsedTime;
                return;
            }

            _time += SystemAPI.Time.DeltaTime;
            
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            CheckObstacleItem(ref state, ref ecb);
            CheckItemShooting(ref state, ref ecb);
            ecb.Playback(_entityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private void CheckItemShooting(ref SystemState state, ref EntityCommandBuffer ecb) 
        {
            foreach (var (itemInfo, takeDamage, entity) in SystemAPI.Query<RefRW<ItemInfo>, RefRO<TakeDamage>>()
                         .WithEntityAccess())
            {
                itemInfo.ValueRW.hp -= (int)takeDamage.ValueRO.value;
                ecb.RemoveComponent<TakeDamage>(entity);
                ecb.AddComponent(entity,new ChangeTextNumberMesh
                {
                    id = itemInfo.ValueRO.idTextHp,
                    value = itemInfo.ValueRW.hp,
                });
                Debug.Log("Hellvoealks2222222222222222");
                if (itemInfo.ValueRO.hp <= 0)
                {
                    var entityNEw = _entityManager.CreateEntity();
                    ecb.AddComponent(entityNEw,new ItemCollection()
                    {
                        count = itemInfo.ValueRO.count,
                        entityItem = entityNEw,
                        id = itemInfo.ValueRO.id,
                        type = itemInfo.ValueRO.type,
                        operation = itemInfo.ValueRO.operation,
                    });
                    ecb.AddComponent(entity,new SetActiveSP()
                    {
                        state = StateID.DestroyAll
                    });
                }
            }
        }

        private void CheckObstacleItem(ref SystemState state, ref EntityCommandBuffer ecb)
        {
            foreach (var (collection,entity) in SystemAPI.Query<RefRO<ItemCollection>>().WithEntityAccess()
                         .WithNone<Disabled, SetActiveSP>())
            {
                switch (collection.ValueRO.type)
                {
                    case ItemType.ObstacleTurret:
                        Debug.Log("m_ log ");
                        SpawnTurret(ref state,ref ecb,collection.ValueRO);
                        ecb.AddComponent(entity,new SetActiveSP()
                        {
                            state = StateID.Disable
                        });
                        break;
                }
                
            }
        }
        [BurstCompile]
        private BufferTurretObstacle GetTurret(int id)
        {
            foreach (var i in _buffetObstacle)
            {
                if (i.id == id) return i;
            }

            return new BufferTurretObstacle()
            {
                id = -1,
            };
        }
        [BurstCompile]
        private void SpawnTurret(ref SystemState state,ref EntityCommandBuffer ecb,ItemCollection itemCollection)
        {
            Debug.Log("m_ log 1");
            BufferTurretObstacle buffetObstacle = default;
            bool check = false;
            foreach (var obs in _buffetObstacle)
            {
                if(obs.id != itemCollection.id) continue;
                buffetObstacle = obs;
                check = true;
                break;
            }
            Debug.Log("m_ log 2");
            if(!check) return;
            Debug.Log("m_ log 3");
            var turret = GetTurret(itemCollection.id);
            if(turret.id == -1) return;
            Debug.Log("m_ log 4");
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
                    type = ObstacleType.Turret,
                    timeLife = turret.timeLife,
                });
            }
            Debug.Log("m_ log 5");
            points.Clear();
        }
    }
}