using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct PlayerSystem : ISystem
{
    private PlayerProperty _playerProperty;
    private PlayerMoveInput _playerMoveInput;
    private Entity _playerEntity;
    private bool _checkGetEntity;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _checkGetEntity = false;
        state.RequireForUpdate<PlayerProperty>();
        state.RequireForUpdate<PlayerMoveInput>();
    }
    

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!_checkGetEntity)
        {
            _checkGetEntity = true;
            _playerEntity = SystemAPI.GetSingletonEntity<PlayerProperty>();
            _playerProperty = SystemAPI.GetComponentRW<PlayerProperty>(_playerEntity).ValueRW;
        }

        state.Dependency.Complete();
        _playerMoveInput = SystemAPI.GetComponentRO<PlayerMoveInput>(_playerEntity).ValueRO;
        Move(ref state);
    }
    private void Move(ref SystemState state)
    {
        foreach (var aspect in SystemAPI.Query<PlayerAspect>())
        {
            float2 direct = _playerMoveInput.directMove;
            float3 dir = new float3(direct.x, 0, direct.y);

            float x = math.remap(0, 1920, -1, 1, _playerMoveInput.mousePos.x);
            float y = 1 - math.abs(x);
            
            float3 dirRota =math.normalize(new float3(x, 0, y));
            dir.y = 0;
            aspect.Position += dir * _playerProperty.speed * SystemAPI.Time.DeltaTime;
            aspect.Rotation = quaternion.LookRotationSafe(dirRota,math.up());
            _playerProperty.worldPosition = aspect.Position;
            _playerProperty.rotation = aspect.Rotation;
            _playerProperty.isShot = _playerMoveInput.shot;
            SystemAPI.SetSingleton(_playerProperty);
        }
    }
}