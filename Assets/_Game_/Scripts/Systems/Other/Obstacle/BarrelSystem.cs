using _Game_.Scripts.Systems.Weapon;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game_.Scripts.Systems.Other.Obstacle
{
    [BurstCompile,UpdateInGroup(typeof(InitializationSystemGroup)),UpdateAfter(typeof(BulletSpawnerSystem)),UpdateBefore(typeof(BulletSpawnerSystem))]
    public partial struct BarrelSystem : ISystem
    {
        private NativeList<LocalTransform> _ltEnemy;
        private NativeQueue<BufferBulletSpawner> _bulletSpawnQueue;
        private EntityQuery _entityQuery;
        private Entity _entityWeaponAuthoring;
        private bool _isInit;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BarrelInfo>();
            state.RequireForUpdate<WeaponProperty>();
            _ltEnemy = new NativeList<LocalTransform>(Allocator.Persistent);
            _entityQuery = SystemAPI.QueryBuilder().WithAll<BarrelInfo>().WithNone<Disabled, SetActiveSP>().Build();
            _bulletSpawnQueue = new NativeQueue<BufferBulletSpawner>(Allocator.Persistent);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!_isInit)
            {
                _entityWeaponAuthoring = SystemAPI.GetSingletonEntity<WeaponProperty>();
                _isInit = true;
            }

            PutEventSpawnBullet(ref state);
        }

        private void PutEventSpawnBullet(ref SystemState state
        )
        {
            _ltEnemy.Clear();
            _bulletSpawnQueue.Clear();
            foreach(var lt in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<ZombieInfo>().WithNone<Disabled,SetActiveSP>())
            {
                _ltEnemy.Add(lt.ValueRO);
            }
            var job = new BarrelOB()
            {
                ltComponentType = state.GetComponentTypeHandle<LocalTransform>(),
                ltEnemy = _ltEnemy,
                barrelInfoComponentType = state.GetComponentTypeHandle<BarrelInfo>(),
                deltaTime = SystemAPI.Time.DeltaTime,
                barrelRunTimeComponentType = state.GetComponentTypeHandle<BarrelRunTime>(),
                time = (float)SystemAPI.Time.ElapsedTime,
                ltwComponentTypeHandle = state.GetComponentTypeHandle<LocalToWorld>(),
                bulletSpawnQueue = _bulletSpawnQueue.AsParallelWriter(),
            };
            state.Dependency = job.ScheduleParallel(_entityQuery, state.Dependency);
            state.Dependency.Complete();
            if (_bulletSpawnQueue.Count > 0)
            {
                var bufferSpawnBullet = state.EntityManager.GetBuffer<BufferBulletSpawner>(_entityWeaponAuthoring);
                while(_bulletSpawnQueue.TryDequeue(out var queue))
                {
                    bufferSpawnBullet.Add(queue);
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_ltEnemy.IsCreated)
                _ltEnemy.Dispose();
            if (_bulletSpawnQueue.IsCreated)
                _bulletSpawnQueue.Dispose();
        }
        
        partial struct BarrelOB : IJobChunk
        {
            public ComponentTypeHandle<LocalTransform> ltComponentType;
            public ComponentTypeHandle<BarrelRunTime> barrelRunTimeComponentType;
            public NativeQueue<BufferBulletSpawner>.ParallelWriter bulletSpawnQueue;
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> ltwComponentTypeHandle;
            [ReadOnly] public NativeList<LocalTransform> ltEnemy;
            [ReadOnly] public ComponentTypeHandle<BarrelInfo> barrelInfoComponentType;
            [ReadOnly] public float deltaTime;
            [ReadOnly] public float time;
            
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var lts = chunk.GetNativeArray(ltComponentType);
                var barrelInfos = chunk.GetNativeArray(barrelInfoComponentType);
                var barrelRunTimes = chunk.GetNativeArray(barrelRunTimeComponentType);
                var ltws = chunk.GetNativeArray(ltwComponentTypeHandle);
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
                    var ltEnemy = GetLocalTransformNearestEnemy(ltw.Position);
                    var direct = lt.Forward();
                    if (ltEnemy.Scale > 0)
                    {
                        var distance = math.distance(ltw.Position, ltEnemy.Position);
                        direct = ltEnemy.Position - ltw.Position;
                        var ratio = 0f;
                        if (distance < info.distanceSetChangeRota)
                        {
                            ratio = 1 - (distance * 1.0f / info.distanceSetChangeRota);
                        }
                        moveToWard = math.lerp(info.moveToWardMin, info.moveToWardMax,ratio);
                    }
                    lt.Rotation = MathExt.MoveTowards(lt.Rotation, quaternion.LookRotationSafe(direct, math.up()),
                        moveToWard * deltaTime);
                    lts[i] = lt;
                    //
                    var barrelRunTime = barrelRunTimes[i];
                    if ((time - barrelRunTime.value) > info.cooldown)
                    {
                        lt_bullet.Position = ltws[i].Position + info.pivotFireOffset;
                        lt_bullet.Rotation = lt.Rotation;
                        bulletSpawnQueue.Enqueue(new BufferBulletSpawner()
                        {
                            bulletPerShot = info.bulletPerShot,
                            damage = info.damage,
                            lt = lt_bullet,
                            parallelOrbit = info.parallelOrbit,
                            speed = info.speed,
                            spaceAnglePerBullet = info.spaceAnglePerBullet,
                        });
                        barrelRunTime.value = time;
                        
                        barrelRunTimes[i] = barrelRunTime;
                    }
                    
                }
                
            }

            private LocalTransform GetLocalTransformNearestEnemy(float3 pos)
            {
                float distanceNearest = 999;

                LocalTransform lt = new LocalTransform()
                {
                    Scale = -1,
                };
                
                foreach (var enemyLt in ltEnemy)
                {
                    if (math.distance(pos, enemyLt.Position) < distanceNearest)
                    {
                        distanceNearest = math.distance(pos, enemyLt.Position);
                        lt = enemyLt;
                    }
                }

                return lt;
            }
        }

        
        
    }
}