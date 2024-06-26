using _Game_.Scripts.Systems.Other.Obstacle;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

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
            
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var bufferBulletSpawn = _entityManager.GetBuffer<BufferBulletSpawner>(_entityWeaponAuthoring);
            var bulletSpawnerArr = bufferBulletSpawn.ToNativeArray(Allocator.TempJob);
            if (bulletSpawnerArr.Length == 0)
            {
                bulletSpawnerArr.Dispose();
                ecb.Dispose();
                return;
            }

            var bufferBulletDisables = _entityManager.GetBuffer<BufferBulletDisable>(_entityWeaponAuthoring);
            var bulletDisableArr = bufferBulletDisables.ToNativeArray(Allocator.TempJob);
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
                    InstantiateBullet(index, lt, damage, speed, _entityBulletInstantiate, ref ecb, bulletDisableArr, time);
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
                    InstantiateBullet(index, lt, damage, speed, _entityBulletInstantiate, ref ecb, bulletDisableArr, time);
                    angleRotaNew.y = angle2;
                    lt.Rotation = MathExt.Float3ToQuaternion(angleRotaNew);
                    InstantiateBullet(index, lt, damage, speed, _entityBulletInstantiate, ref ecb, bulletDisableArr, time);
                }
            }

            ecb.Playback(_entityManager);

            // Clear the buffers to avoid conflicts in the next update
            bufferBulletDisables.Clear();
            bufferBulletSpawn.Clear();

            bulletDisableArr.Dispose();
            bulletSpawnerArr.Dispose();
            ecb.Dispose();
        }

        private static void InstantiateBullet(int index, LocalTransform lt, float damage, float speed, Entity entityBullet, ref EntityCommandBuffer ecb, NativeArray<BufferBulletDisable> bulletDisableArr, float time)
        {
            Entity entity;
            if (bulletDisableArr.Length > index)
            {
                entity = bulletDisableArr[index].entity;
                ecb.AddComponent(entity, new SetActiveSP { state = StateID.Enable });
                ecb.RemoveComponent<Disabled>(entity);
            }
            else
            {
                entity = ecb.Instantiate(entityBullet);
            }

            ecb.AddComponent(entity, lt);
            ecb.AddComponent(entity, new BulletInfo { damage = damage, speed = speed, startTime = time });
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
