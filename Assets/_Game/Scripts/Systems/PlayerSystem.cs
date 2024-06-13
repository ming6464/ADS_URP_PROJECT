using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct PlayerSystem : ISystem
{
    private PlayerProperty _playerProperty;
    private PlayerMoveInput _playerMoveInput;
    private BulletProperty _bulletProperty;
    private Entity _playerEntity;
    private bool _checkGetEntity;
    private EntityManager _entityManager;
    private EntityCommandBuffer _ecb;
    
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _checkGetEntity = false;
        state.RequireForUpdate<PlayerProperty>();
        state.RequireForUpdate<PlayerMoveInput>();
        state.RequireForUpdate<BulletProperty>();
    }
    

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency.Complete();
        _entityManager = state.EntityManager;
        if (!_checkGetEntity)
        {
            _checkGetEntity = true;
            _playerEntity = SystemAPI.GetSingletonEntity<PlayerProperty>();
            _bulletProperty = SystemAPI.GetComponentRO<BulletProperty>(_playerEntity).ValueRO;
        }
        
        _playerProperty = SystemAPI.GetComponentRO<PlayerProperty>(_playerEntity).ValueRO;
        _playerMoveInput = SystemAPI.GetComponentRO<PlayerMoveInput>(_playerEntity).ValueRO;

        Move(ref state);

        Shoot(ref state);
    }
    private void Move(ref SystemState state)
    {
        PlayerMoveJOB job = new PlayerMoveJOB()
        {
            deltaTime = SystemAPI.Time.DeltaTime,
            speed = _playerProperty.speed,
            playerMoveInput = _playerMoveInput,
        };
        
        state.Dependency = job.Schedule(state.Dependency);
    }

    private void Shoot(ref SystemState state)
    {
        if(!_playerMoveInput.shot) return;
        _ecb = new EntityCommandBuffer(Allocator.Temp);
        // Entity entity = _ecb.Instantiate(_bulletProperty.entity);
    }
    
    [BurstCompile]
    public partial struct PlayerMoveJOB : IJobEntity
    {
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float speed;
        [ReadOnly] public PlayerMoveInput playerMoveInput;
        
        public void Execute(PlayerAspect aspect,in PlayerInfo playerInfo)
        {
            float2 direct = playerMoveInput.directMove;
            float3 dir = new float3(direct.x, 0, direct.y);

            float x = math.remap(0, 1920, -1, 1, playerMoveInput.mousePos.x);
            float y = 1 - math.abs(x);
            
            float3 dirRota =math.normalize(new float3(x, 0, y));
            dir.y = 0;
            aspect.Position += dir * speed * deltaTime;
            aspect.Rotation = quaternion.LookRotationSafe(dirRota,math.up());
        }
    }
}