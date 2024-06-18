// using Unity.Burst;
// using Unity.Burst.Intrinsics;
// using Unity.Collections;
// using Unity.Entities;
// using Unity.Mathematics;
// using Unity.Transforms;
//
// [BurstCompile]
// public partial struct SystemTest : ISystem
// {
//     private bool _init;
//     private float3 _pointZoneMin;
//     private float3 _pointZoneMax;
//     private EntityTypeHandle _entityTypeHandle;
//
//     [BurstCompile]
//     public void OnCreate(ref SystemState state)
//     {
//         // Khởi tạo EntityTypeHandle
//         _entityTypeHandle = state.GetEntityTypeHandle();
//         state.RequireForUpdate<ZombieInfo>();
//     }
//
//     [BurstCompile]
//     public void OnUpdate(ref SystemState state)
//     {
//         if (!_init)
//         {
//             _init = true;
//
//             var zone = SystemAPI.GetSingleton<ActiveZoneProperty>();
//             _pointZoneMin = zone.pointRangeMin;
//             _pointZoneMax = zone.pointRangeMax;
//         }
//
//         // Cập nhật EntityTypeHandle trước khi sử dụng
//         _entityTypeHandle.Update(ref state);
//
//         var ecb = new EntityCommandBuffer(Allocator.TempJob);
//
//         EntityQuery entityQuery = SystemAPI.QueryBuilder().WithAll<ZombieInfo>().WithNone<Disabled, SetActiveSP>().Build();
//         
//         var chunkJob = new CheckDeadZoneJOB
//         {
//             ecb = ecb.AsParallelWriter(),
//             _entityTypeHandle = _entityTypeHandle,
//             ltwTypeHandle = state.GetComponentTypeHandle<LocalToWorld>(true),
//             minPointRange = _pointZoneMin,
//             maxPointRange = _pointZoneMax,
//         };
//
//         state.Dependency = chunkJob.ScheduleParallel(entityQuery, state.Dependency);
//         state.Dependency.Complete();
//
//         ecb.Playback(state.EntityManager);
//     }
// }
//
// [BurstCompile]
// public partial struct CheckDeadZoneJOB : IJobChunk
// {
//     public EntityCommandBuffer.ParallelWriter ecb;
//     [ReadOnly] public EntityTypeHandle _entityTypeHandle;
//     [ReadOnly] public ComponentTypeHandle<LocalToWorld> ltwTypeHandle;
//     [ReadOnly] public float3 minPointRange;
//     [ReadOnly] public float3 maxPointRange;
//
//     public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
//     {
//         var ltwArr = chunk.GetNativeArray(ref ltwTypeHandle);
//         var entities = chunk.GetNativeArray(_entityTypeHandle);
//
//         for (int i = 0; i < chunk.Count; i++)
//         {
//             if (CheckInRange(ltwArr[i].Position, minPointRange, maxPointRange)) continue;
//             ecb.AddComponent(unfilteredChunkIndex, entities[i], new SetActiveSP
//             {
//                 state = StateID.CanDisable,
//             });
//         }
//
//         bool CheckInRange(float3 value, float3 min, float3 max)
//         {
//             if ((value.x - min.x) * (max.x - value.x) < 0) return false;
//             if ((value.y - min.y) * (max.y - value.y) < 0) return false;
//             if ((value.z - min.z) * (max.z - value.z) < 0) return false;
//             return true;
//         }
//     }
// }
