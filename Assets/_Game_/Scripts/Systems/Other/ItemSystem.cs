﻿using _Game_.Scripts.Data;
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
        private Entity _entityPlayerAuthoring;
        private EntityManager _entityManager;
        private bool _isInit;
        
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
                _entityPlayerAuthoring = SystemAPI.GetSingletonEntity<PlayerInfo>();

            }
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            CheckObstacleItem(ref state, ref ecb);
            CheckItemShooting(ref state, ref ecb);
            ecb.Playback(_entityManager);
            ecb.Dispose();
        }

        private void CheckItemShooting(ref SystemState state, ref EntityCommandBuffer ecb) 
        {
            var query = SystemAPI.QueryBuilder().WithAll<TakeDamage>().Build();
            var query2 = SystemAPI.QueryBuilder().WithAll<TakeDamage,ItemInfo>().Build();

            var a = query.ToEntityArray(Allocator.Temp);
            var b = query2.ToEntityArray(Allocator.Temp);
            
            a.Dispose();
            b.Dispose();
            
            
            foreach (var (itemInfo, takeDamage, entity) in SystemAPI.Query<RefRW<ItemInfo>, RefRO<TakeDamage>>()
                         .WithEntityAccess())
            {
                itemInfo.ValueRW.hp -= takeDamage.ValueRO.value;
                ecb.RemoveComponent<TakeDamage>(entity);
                if (itemInfo.ValueRO.hp <= 0)
                {
                    var entityNEw = _entityManager.CreateEntity();
                    ecb.AddComponent(entityNEw,new ItemCollection()
                    {
                        count = itemInfo.ValueRO.count,
                        entityItem = entityNEw,
                        id = itemInfo.ValueRO.id,
                        type = itemInfo.ValueRO.type,
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
                        SpawnTurret(ref state,ref ecb,collection.ValueRO);
                        ecb.AddComponent(entity,new SetActiveSP()
                        {
                            state = StateID.Disable
                        });
                        break;
                }
                
            }
        }

        private void SpawnTurret(ref SystemState state,ref EntityCommandBuffer ecb,ItemCollection itemCollection)
        {
            BufferTurretObstacle buffetObstacle = default;
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
                    type = ObstacleType.Turret,
                });
            }
            points.Clear();
        }
    }
}