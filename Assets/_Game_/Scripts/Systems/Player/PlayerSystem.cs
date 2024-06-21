using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct PlayerSystem : ISystem
{
    private PlayerProperty _playerProperty;
    private PlayerInput _playerMoveInput;
    private Entity _playerEntity;
    private bool _init;
    private bool _aimNearestEnemy;
    private PlayerAspect _playerAspect;
    private float _moveToWard;
    
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProperty>();
        state.RequireForUpdate<PlayerInput>();
        state.RequireForUpdate<PlayerInfo>();
        state.RequireForUpdate<CharacterInfo>();
    }


    public void OnUpdate(ref SystemState state)
    {
        state.Dependency.Complete();
        if (!_init)
        {
            _playerProperty = SystemAPI.GetSingleton<PlayerProperty>();
            _playerEntity = SystemAPI.GetSingletonEntity<PlayerInfo>();
            _aimNearestEnemy = _playerProperty.aimNearestEnemy;
            _moveToWard = _playerProperty.moveToWard;
            _init = true;
        }
        _playerMoveInput = SystemAPI.GetSingleton<PlayerInput>();
        _playerAspect = SystemAPI.GetAspect<PlayerAspect>(_playerEntity);
        float2 direct = _playerMoveInput.directMove;
        _playerAspect.Position += new float3(direct.x, 0, direct.y) * _playerProperty.speed * SystemAPI.Time.DeltaTime;
        
        state.Dependency = new CharacterRotate()
        {
            ltComponentTypeHandle = state.GetComponentTypeHandle<LocalTransform>(),
            directRota = GetDirectRota(ref state),
            moveToWard = _moveToWard,
            deltaTime = SystemAPI.Time.DeltaTime,
        }.ScheduleParallel(SystemAPI.QueryBuilder().WithAll<CharacterInfo>().Build(), state.Dependency);
    }

    private float3 GetDirectRota(ref SystemState state)
    {
        float3 dirRota;

        if (_aimNearestEnemy)
        {
            float3 playerPosWorld = _playerAspect.PositionWorld;
            float3 positionNearest = float3.zero;
            float spaceNearest = 99999f;
            foreach (var ltw in SystemAPI.Query<RefRO<LocalToWorld>>().WithAll<ZombieInfo>()
                         .WithNone<Disabled, SetActiveSP>())
            {
                float space = math.distance(playerPosWorld, ltw.ValueRO.Position);
                if (space < spaceNearest)
                {
                    positionNearest = ltw.ValueRO.Position;
                    spaceNearest = space;
                }
            }

            dirRota = positionNearest - playerPosWorld;
            if (!dirRota.Equals(float3.zero))
            {
                dirRota = math.normalize(dirRota);
            }
        }
        else
        {
            float x = math.remap(0, 1920, -1, 1, _playerMoveInput.mousePos.x);
            float y = 1 - math.abs(x);
            dirRota = math.normalize(new float3(x, 0, y));
        }
        return dirRota;
    }

    private partial struct CharacterRotate : IJobChunk
    {
        public ComponentTypeHandle<LocalTransform> ltComponentTypeHandle;
        [ReadOnly] public float3 directRota;
        [ReadOnly] public float moveToWard;
        [ReadOnly] public float deltaTime;
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var lts = chunk.GetNativeArray(ltComponentTypeHandle);
            if(chunk.Count == 0) return;
            var targetRota = quaternion.LookRotationSafe(directRota,math.up());
            var curRota = lts[0].Rotation;
            var nextRota = MathExt.MoveTowards(curRota, targetRota, moveToWard * deltaTime);
            for (int i = 0; i < chunk.Count; i++)
            {
                var lt = lts[i];
                lt.Rotation = nextRota;
                lts[i] = lt;
            }
        }
    }
    
}
