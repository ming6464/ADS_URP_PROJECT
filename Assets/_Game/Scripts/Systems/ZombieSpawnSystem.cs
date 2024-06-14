using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct ZombieSpawnSystem : ISystem
{
    private int _spawnNumber;
    private float _latestSpawnTime;
    private NativeList<float3> _zombiePositions;
    private LocalTransform _localTransform;
    private float3 _pointActiveMin;
    private float3 _pointActiveMax;
    private float3 _pointRandomMin;
    private float3 _pointRandomMax;
    private bool _isInit;
    private ZombieInfo _zombieComponent;
    private GenericZombieProperties _zombieProperties;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _spawnNumber = 0;
        _isInit = false;
        _zombiePositions = new NativeList<float3>(Allocator.Persistent);
        state.RequireForUpdate<GenericZombieProperties>();
        state.RequireForUpdate<ActiveZoneProperty>();
        
    }
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        _zombiePositions.Dispose();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!_isInit)
        {
            Entity entity = SystemAPI.GetSingletonEntity<GenericZombieProperties>();
            _zombieProperties = SystemAPI.GetComponentRO<GenericZombieProperties>(entity).ValueRO;
            _localTransform = SystemAPI.GetComponentRO<LocalTransform>(entity).ValueRO;
            _pointRandomMin = _localTransform.InverseTransformPoint(_zombieProperties.spawner.posMin);
            _pointRandomMax = _localTransform.InverseTransformPoint(_zombieProperties.spawner.posMax);
            _latestSpawnTime = -_zombieProperties.spawner.timeDelay;
            _zombieComponent = new ZombieInfo
            {
                directNormal = _zombieProperties.directNormal,
                isDead = false,
            };

            ActiveZoneProperty activeZoneProperty = SystemAPI.GetSingleton<ActiveZoneProperty>();
            _pointActiveMin = _localTransform.InverseTransformPoint(activeZoneProperty.pointRangeMin);
            _pointRandomMax = _localTransform.InverseTransformPoint(activeZoneProperty.pointRangeMax);
            
            _isInit = true;
        }
        if((_zombieProperties.spawner.spawnInfinity < 1) && _spawnNumber >= _zombieProperties.spawner.numberSpawn) return;
        if((SystemAPI.Time.ElapsedTime - _latestSpawnTime) < _zombieProperties.spawner.timeDelay) return;
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        for (int i = 0; i < _zombieProperties.spawner.numberSpawnPerFrame; i++)
        {
            Entity entityNew = ecb.Instantiate(_zombieProperties.entity);
            LocalTransform lt = _localTransform;
            Random random = Random.CreateFromIndex((uint)(_spawnNumber + _zombieProperties.speed));
            lt.Position = random.GetRandomRange(_pointRandomMin, _pointRandomMax);
            ecb.AddComponent(entityNew,lt);
            ecb.AddComponent(entityNew,_zombieComponent);
            _spawnNumber++;
        }
        ecb.Playback(state.EntityManager);
    }
}