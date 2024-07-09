﻿using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game_.Scripts.Systems.Other.Obstacle
{
    [BurstCompile, UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct TurretSystem : ISystem
    {
        private NativeArray<BufferTurretObstacle> _bufferTurretObstacles;
        private NativeList<float3> _enemyPositions;
        private NativeQueue<BufferBulletSpawner> _bulletSpawnQueue;
        private EntityQuery _enQueryBarrelInfo;
        private Entity _entityWeaponAuthoring;
        private EntityManager _entityManager;
        private bool _isInit;
        private ComponentTypeHandle<LocalTransform> _ltTypeHandle;
        private ComponentTypeHandle<BarrelInfo> _barrelInfoTypeHandle;
        private ComponentTypeHandle<BarrelRunTime> _barrelRunTimeTypeHandle;
        private ComponentTypeHandle<LocalToWorld> _ltwTypeHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            RequireNecessaryComponents(ref state);
            Init(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            CheckAndInitRunTime(ref state);
            CheckSetupBarrel(ref state);
            PutEventSpawnBullet(ref state);
            CheckAndHandleTimeLifeExpired(ref state);
        }

        [BurstCompile]
        private void CheckAndHandleTimeLifeExpired(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            float time = (float)SystemAPI.Time.ElapsedTime;
            var setActiveComponent = new SetActiveSP()
            {
                state = DisableID.Disable,
            };
            foreach (var (turret,entity) in SystemAPI.Query<RefRO<TurretInfo>>().WithEntityAccess().WithNone<Disabled, SetActiveSP>())
            {
                if (time - turret.ValueRO.startTime > turret.ValueRO.timeLife)
                {
                    ecb.AddComponent(entity,setActiveComponent);
                }
            }
            ecb.Playback(_entityManager);
            ecb.Dispose();
        }


        [BurstCompile]
        private void CheckSetupBarrel(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (barrelSetup, entity) in SystemAPI.Query<RefRO<BarrelCanSetup>>().WithEntityAccess()
                         .WithNone<Disabled>())
            {
                SetUpBarrel(ref ecb, entity, barrelSetup.ValueRO.id);
            }

            ecb.Playback(_entityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private BufferTurretObstacle GetTurret(int id)
        {
            BufferTurretObstacle turret = default;

            foreach (var t in _bufferTurretObstacles)
            {
                if (t.id == id) return t;
            }

            return turret;
        }
        [BurstCompile]
        private void PutEventSpawnBullet(ref SystemState state)
        {
            _enemyPositions.Clear();
            _bulletSpawnQueue.Clear();
            foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<ZombieInfo>()
                         .WithNone<Disabled, SetActiveSP>())
            {
                _enemyPositions.Add(ltw.ValueRO.Position);
            }
            
            _ltTypeHandle.Update(ref state);
            _ltwTypeHandle.Update(ref state);
            _barrelInfoTypeHandle.Update(ref state);
            _barrelRunTimeTypeHandle.Update(ref state);
            
            var job = new BarreJOB()
            {
                ltComponentType = _ltTypeHandle,
                enemyPositions = _enemyPositions,
                barrelInfoComponentType = _barrelInfoTypeHandle,
                deltaTime = SystemAPI.Time.DeltaTime,
                barrelRunTimeComponentType = _barrelRunTimeTypeHandle,
                time = (float)SystemAPI.Time.ElapsedTime,
                ltwComponentTypeHandle = _ltwTypeHandle,
                bulletSpawnQueue = _bulletSpawnQueue.AsParallelWriter(),
            };
            state.Dependency = job.ScheduleParallel(_enQueryBarrelInfo, state.Dependency);
            state.Dependency.Complete();
            if (_bulletSpawnQueue.Count <= 0) return;
            var bufferSpawnBullet = state.EntityManager.GetBuffer<BufferBulletSpawner>(_entityWeaponAuthoring);
            while (_bulletSpawnQueue.TryDequeue(out var queue))
            {
                bufferSpawnBullet.Add(queue);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_enemyPositions.IsCreated)
                _enemyPositions.Dispose();
            if (_bulletSpawnQueue.IsCreated)
                _bulletSpawnQueue.Dispose();
            if (_bufferTurretObstacles.IsCreated)
                _bufferTurretObstacles.Dispose();
        }

        [BurstCompile]
        private void RequireNecessaryComponents(ref SystemState state)
        {
            state.RequireForUpdate<TurretInfo>();
            state.RequireForUpdate<WeaponProperty>();
        }

        [BurstCompile]
        private void Init(ref SystemState state)
        {
            _enemyPositions = new NativeList<float3>(Allocator.Persistent);
            _enQueryBarrelInfo = SystemAPI.QueryBuilder().WithAll<BarrelInfo, BarrelRunTime>()
                .WithNone<Disabled, SetActiveSP>().Build();
            _bulletSpawnQueue = new NativeQueue<BufferBulletSpawner>(Allocator.Persistent);
            _ltTypeHandle = state.GetComponentTypeHandle<LocalTransform>();
            _ltwTypeHandle = state.GetComponentTypeHandle<LocalToWorld>();
            _barrelInfoTypeHandle = state.GetComponentTypeHandle<BarrelInfo>();
            _barrelRunTimeTypeHandle = state.GetComponentTypeHandle<BarrelRunTime>();
        }
        
        [BurstCompile]
        private void CheckAndInitRunTime(ref SystemState state)
        {
            _entityManager = state.EntityManager;
            if (!_isInit)
            {
                _isInit = true;
                _entityWeaponAuthoring = SystemAPI.GetSingletonEntity<WeaponProperty>();
                _bufferTurretObstacles = SystemAPI.GetSingletonBuffer<BufferTurretObstacle>()
                    .ToNativeArray(Allocator.Persistent);
            }
        }
        
        [BurstCompile]
        private BarrelInfo GetBarrelInfoFromBuffer(int id)
        {
            var turret = GetTurret(id);
            var barrel = new BarrelInfo()
            {
                bulletPerShot = turret.bulletPerShot,
                cooldown = turret.cooldown,
                damage = turret.damage,
                distanceAim = turret.distanceAim,
                moveToWardMax = turret.moveToWardMax,
                moveToWardMin = turret.moveToWardMin,
                parallelOrbit = turret.parallelOrbit,
                pivotFireOffset = turret.pivotFireOffset,
                speed = turret.speed,
                spaceAnglePerBullet = turret.spaceAnglePerBullet,
                spacePerBullet = turret.spacePerBullet,
            };
            return barrel;
        }

        [BurstCompile]
        private void SetUpBarrel(ref EntityCommandBuffer ecb,Entity entity, int id)
        {
            ecb.AddComponent(entity, GetBarrelInfoFromBuffer(id));
            ecb.AddComponent<BarrelRunTime>(entity);
            ecb.RemoveComponent<BarrelCanSetup>(entity);
        }
        #region JOB

        partial struct BarreJOB : IJobChunk
        {
            public ComponentTypeHandle<LocalTransform> ltComponentType;
            public ComponentTypeHandle<BarrelRunTime> barrelRunTimeComponentType;
            public NativeQueue<BufferBulletSpawner>.ParallelWriter bulletSpawnQueue;
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> ltwComponentTypeHandle;
            [ReadOnly] public NativeList<float3> enemyPositions;
            [ReadOnly] public ComponentTypeHandle<BarrelInfo> barrelInfoComponentType;
            [ReadOnly] public float deltaTime;
            [ReadOnly] public float time;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var lts = chunk.GetNativeArray(ref ltComponentType);
                var barrelInfos = chunk.GetNativeArray(ref barrelInfoComponentType);
                var barrelRunTimes = chunk.GetNativeArray(ref barrelRunTimeComponentType);
                var ltws = chunk.GetNativeArray(ref ltwComponentTypeHandle);
                var lt_bullet = new LocalTransform()
                {
                    Scale = 1
                };
                for (int i = 0; i < chunk.Count; i++)
                {
                    var lt = lts[i];
                    var ltw = ltws[i];
                    var info = barrelInfos[i];
                    var moveToWard = info.moveToWardMax;
                    var direct = math.forward();
                    GetLocalTransformNearestEnemy(ltw.Position, info, ref direct, ref moveToWard);
                    lt.Rotation = MathExt.MoveTowards(ltw.Rotation, quaternion.LookRotationSafe(direct, math.up()),
                        moveToWard * deltaTime);
                    lts[i] = lt;

                    var barrelRunTime = barrelRunTimes[i];
                    if ((time - barrelRunTime.value) > info.cooldown)
                    {
                        lt_bullet.Position = lt.TransformPoint(info.pivotFireOffset) + ltw.Position;
                        lt_bullet.Rotation = lt.Rotation;
                        bulletSpawnQueue.Enqueue(new BufferBulletSpawner()
                        {
                            bulletPerShot = info.bulletPerShot,
                            damage = info.damage,
                            lt = lt_bullet,
                            parallelOrbit = info.parallelOrbit,
                            speed = info.speed,
                            spaceAnglePerBullet = info.spaceAnglePerBullet,
                            spacePerBullet = info.spacePerBullet,
                        });
                        barrelRunTime.value = time;
                        barrelRunTimes[i] = barrelRunTime;
                    }
                }
            }

            private void GetLocalTransformNearestEnemy(float3 pos, BarrelInfo info, ref float3 direct,
                ref float moveToWard)
            {
                float distanceNearest = info.distanceAim;
                float3 positionNearest = float3.zero;
                bool check = false;
                foreach (var enemyPos in enemyPositions)
                {
                    var distance = math.distance(pos, enemyPos);
                    // var distance = math.abs(pos.z - enemyPos.z);

                    if (distance < distanceNearest)
                    {
                        if (MathExt.CalculateAngle(enemyPos - pos, math.forward()) < 120)
                        {
                            distanceNearest = distance;
                            positionNearest = enemyPos;
                            check = true;
                        }
                    }
                }

                if (check)
                {
                    direct = MathExt.RotateVector(positionNearest - pos, new float3(0, -(70.0f / distanceNearest), 0));
                    var ratio = 1 - math.clamp(distanceNearest * 1.0f / info.distanceAim, 0, 1);
                    
                    
                    moveToWard = math.lerp(info.moveToWardMin, info.moveToWardMax, ratio);
                }
                else
                {
                    moveToWard = info.moveToWardMax;
                    direct = math.forward();
                }
            }
        }

        #endregion
    }
}