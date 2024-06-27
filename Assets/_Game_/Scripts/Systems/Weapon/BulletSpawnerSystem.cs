using _Game_.Scripts.Systems.Other.Obstacle;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace _Game_.Scripts.Systems.Weapon
{
    [BurstCompile, UpdateInGroup(typeof(PresentationSystemGroup)),UpdateAfter(typeof(BarrelSystem))]
    public partial struct BulletSpawnerSystem : ISystem
    {
        private EntityManager _entityManager;
        private Entity _entityWeaponAuthoring;
        private Entity _entityBulletInstantiate;
        private bool _isInit;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponProperty>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _entityManager = state.EntityManager;
            if (!_isInit)
            {
                _isInit = true;
                var weaponProperty = SystemAPI.GetSingleton<WeaponProperty>();
                _entityBulletInstantiate = weaponProperty.entityBullet;
                _entityWeaponAuthoring = SystemAPI.GetSingletonEntity<WeaponProperty>();
            }

            SpawnBullet(ref state);
        }

        private void SpawnBullet(ref SystemState state)
        {
            var bufferBulletSpawn = _entityManager.GetBuffer<BufferBulletSpawner>(_entityWeaponAuthoring);
            if (bufferBulletSpawn.Length == 0) return;
            var bulletSpawnerArr = bufferBulletSpawn.ToNativeArray(Allocator.TempJob);
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var bufferBulletDisables = _entityManager.GetBuffer<BufferBulletDisable>(_entityWeaponAuthoring);
            float time = (float)SystemAPI.Time.ElapsedTime;

            for (int index = 0; index < bulletSpawnerArr.Length; index++)
            {
                var bulletSpawn = bulletSpawnerArr[index];
                float subtractIndex = 0.5f;
                int halfNumberPreShot = (int)math.ceil(bulletSpawn.bulletPerShot / 2f);
                var lt = bulletSpawn.lt;
                var angleRota = MathExt.QuaternionToFloat3(lt.Rotation);
                float damage = bulletSpawn.damage;
                float speed = bulletSpawn.speed;

                if (halfNumberPreShot % 2 != 0)
                {
                    InstantiateBullet_L( lt, damage, speed, _entityBulletInstantiate);
                    --halfNumberPreShot;
                    subtractIndex = 0;
                }

                for (int i = 1; i <= halfNumberPreShot; i++)
                {
                    float3 angleRotaNew = angleRota;
                    float angle = (i - subtractIndex) * bulletSpawn.spaceAnglePerBullet;
                    float angle1 = angleRotaNew.y + angle;
                    float angle2 = angleRotaNew.y - angle;
                    angleRotaNew.y = angle1;
                    lt.Rotation = MathExt.Float3ToQuaternion(angleRotaNew);
                    InstantiateBullet_L( lt, damage, speed, _entityBulletInstantiate);
                    angleRotaNew.y = angle2;
                    lt.Rotation = MathExt.Float3ToQuaternion(angleRotaNew);
                    InstantiateBullet_L( lt, damage, speed, _entityBulletInstantiate);
                }
            }

            ecb.Playback(_entityManager);
            bulletSpawnerArr.Dispose();
            _entityManager.GetBuffer<BufferBulletSpawner>(_entityWeaponAuthoring).Clear();
            ecb.Dispose();
            void InstantiateBullet_L(LocalTransform lt, float damage, float speed, Entity entityBullet)
            {
                Entity entity;
                if (bufferBulletDisables.Length > 0)
                {
                    entity = bufferBulletDisables[0].entity;
                    ecb.RemoveComponent<Disabled>(entity);
                    ecb.AddComponent(entity, new SetActiveSP { state = StateID.Enable });
                    bufferBulletDisables.RemoveAt(0);
                }
                else
                {
                    entity = ecb.Instantiate(entityBullet);
                }
                ecb.AddComponent(entity, lt);
                ecb.AddComponent(entity, new BulletInfo { damage = damage, speed = speed, startTime = time });
            }
            
        }

        

        [BurstCompile]
        private struct SpawnBulletJob : IJobParallelFor
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            [ReadOnly] public NativeArray<BufferBulletSpawner> bulletSpawnerArr;
            [ReadOnly] public NativeArray<BufferBulletDisable> bulletDisableArr;
            [ReadOnly] public Entity entityBullet;
            [ReadOnly] public float time;

            public void Execute(int index)
            {
                var bulletSpawn = bulletSpawnerArr[index];
                float subtractIndex = 0.5f;
                int halfNumberPreShot = (int)math.ceil(bulletSpawn.bulletPerShot / 2f);
                var lt = bulletSpawn.lt;
                var angleRota = MathExt.QuaternionToFloat3(lt.Rotation);
                float damage = bulletSpawn.damage;
                float speed = bulletSpawn.speed;

                if (halfNumberPreShot % 2 != 0)
                {
                    Instantiate(index, lt, damage, speed);
                    --halfNumberPreShot;
                    subtractIndex = 0;
                }

                for (int i = 1; i <= halfNumberPreShot; i++)
                {
                    float3 angleRotaNew = angleRota;
                    float angle = (i - subtractIndex) * bulletSpawn.spaceAnglePerBullet;
                    float angle1 = angleRotaNew.y + angle;
                    float angle2 = angleRotaNew.y - angle;
                    angleRotaNew.y = angle1;
                    lt.Rotation = MathExt.Float3ToQuaternion(angleRotaNew);
                    Instantiate(index, lt, damage, speed);
                    angleRotaNew.y = angle2;
                    lt.Rotation = MathExt.Float3ToQuaternion(angleRotaNew);
                    Instantiate(index, lt, damage, speed);
                }
            }

            private void Instantiate(int index, LocalTransform lt, float damage, float speed)
            {
                Entity entity;
                if (bulletDisableArr.Length > index)
                {
                    entity = bulletDisableArr[index].entity;
                    ecb.AddComponent(index, entity, new SetActiveSP { state = StateID.Enable });
                    ecb.RemoveComponent<Disabled>(index, entity);
                }
                else
                {
                    entity = ecb.Instantiate(index, entityBullet);
                }

                ecb.AddComponent(index, entity, lt);
                ecb.AddComponent(index, entity, new BulletInfo { damage = damage, speed = speed, startTime = time });
            }
        }
    }
}
