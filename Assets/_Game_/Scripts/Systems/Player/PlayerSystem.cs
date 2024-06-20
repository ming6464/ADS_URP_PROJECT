using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct PlayerSystem : ISystem
{
    private PlayerProperty _playerProperty;
    private bool _checkGetEntity;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProperty>();
        state.RequireForUpdate<PlayerMoveInput>();
        state.RequireForUpdate<PlayerInfo>();
        state.RequireForUpdate<CharacterInfo>();
    }


    public void OnUpdate(ref SystemState state)
    {
        if (!_checkGetEntity)
        {
            _playerProperty = SystemAPI.GetSingleton<PlayerProperty>();
            _checkGetEntity = true;
        }
        
        state.Dependency.Complete();
        
        PlayerMoveInput playerMoveInput = SystemAPI.GetSingleton<PlayerMoveInput>();

        float2 direct = playerMoveInput.directMove;
        float3 dir = new float3(direct.x, 0, direct.y);
        float x = math.remap(0, 1920, -1, 1, playerMoveInput.mousePos.x);
        float y = 1 - math.abs(x);
        float3 dirRota =math.normalize(new float3(x, 0, y));
        foreach (var aspect in SystemAPI.Query<PlayerAspect>())
        {
            aspect.Position += dir * _playerProperty.speed * SystemAPI.Time.DeltaTime;
        }

        state.Dependency = new CharacterRotate()
        {
            ltComponentTypeHandle = state.GetComponentTypeHandle<LocalTransform>(),
            directRota = dirRota,
        }.ScheduleParallel(SystemAPI.QueryBuilder().WithAll<CharacterInfo>().Build(), state.Dependency);

    }

    private partial struct CharacterRotate : IJobChunk
    {
        public ComponentTypeHandle<LocalTransform> ltComponentTypeHandle;
        [ReadOnly] public float3 directRota;
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var lts = chunk.GetNativeArray(ltComponentTypeHandle);
            for (int i = 0; i < chunk.Count; i++)
            {
                var lt = lts[i];
                lt.Rotation = quaternion.LookRotationSafe(directRota,math.up());
                lts[i] = lt;
            }
        }
    }
    
}
