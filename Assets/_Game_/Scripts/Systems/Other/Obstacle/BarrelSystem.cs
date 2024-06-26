using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Game_.Scripts.Systems.Other.Obstacle
{
    public partial struct BarrelSystem : ISystem
    {
        private NativeList<LocalTransform> _ltEnemy;
        private EntityQuery _entityQuery;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ltEnemy = new NativeList<LocalTransform>(Allocator.Persistent);
            _entityQuery = SystemAPI.QueryBuilder().WithAll<BarrelInfo>().WithNone<Disabled, SetActiveSP>().Build();
            state.RequireForUpdate<BarrelInfo>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _ltEnemy.Clear();
            foreach(var lt in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<ZombieInfo>().WithNone<Disabled,SetActiveSP>())
            {
                _ltEnemy.Add(lt.ValueRO);
            }

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            var job = new BarrelOB()
            {
                ltComponentType = state.GetComponentTypeHandle<LocalTransform>(),
                ltEnemy = _ltEnemy,
                barrelInfoComponentType = state.GetComponentTypeHandle<BarrelInfo>(),
                deltaTime = SystemAPI.Time.DeltaTime,
                barrelRunTimeComponentType = state.GetComponentTypeHandle<BarrelRunTime>(),
                time = (float)SystemAPI.Time.ElapsedTime,
                ecb = ecb.AsParallelWriter(),
                ltwComponentTypeHandle = state.GetComponentTypeHandle<LocalToWorld>()
            };
            state.Dependency = job.ScheduleParallel(_entityQuery, state.Dependency);
            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_ltEnemy.IsCreated)
                _ltEnemy.Dispose();
        }
        
        partial struct BarrelOB : IJobChunk
        {
            public EntityCommandBuffer.ParallelWriter ecb;
            public ComponentTypeHandle<LocalTransform> ltComponentType;
            public ComponentTypeHandle<BarrelRunTime> barrelRunTimeComponentType;
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
               
                for (int i = 0; i < chunk.Count; i++)
                {
                    var lt = lts[i];
                    var ltw = ltws[i];
                    var barrel = barrelInfos[i];
                    var moveToWard = barrel.moveToWardMax;
                    var ltEnemy = GetLocalTransformNearestEnemy(ltw.Position);
                    var direct = lt.Forward();
                    if (ltEnemy.Scale > 0)
                    {
                        var distance = math.distance(ltw.Position, ltEnemy.Position);
                        direct = ltEnemy.Position - ltw.Position;
                        var ratio = 0f;
                        if (distance < barrel.distanceSetChangeRota)
                        {
                            ratio = 1 - (distance * 1.0f / barrel.distanceSetChangeRota);
                        }
                        moveToWard = math.lerp(barrel.moveToWardMin, barrel.moveToWardMax,ratio);
                    }
                    lt.Rotation = MathExt.MoveTowards(lt.Rotation, quaternion.LookRotationSafe(direct, math.up()),
                        moveToWard * deltaTime);
                    lts[i] = lt;
                    //
                    var barrelRunTime = barrelRunTimes[i];
                    if ((time - barrelRunTime.value) > barrel.cooldown)
                    {
                        var bullet = ecb.Instantiate(unfilteredChunkIndex,barrel.entityBullet);
                        var lt_bullet = new LocalTransform()
                        {
                            Position = ltws[i].Position + barrel.pivotFireOffset,
                            Rotation = lt.Rotation,
                            Scale = 1,
                        };
                        ecb.AddComponent(unfilteredChunkIndex,bullet,lt_bullet);
                        ecb.AddComponent(unfilteredChunkIndex,bullet,new BulletInfo()
                        {
                            damage = barrel.damage,
                            speed = barrel.speed,
                            startTime = time,
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