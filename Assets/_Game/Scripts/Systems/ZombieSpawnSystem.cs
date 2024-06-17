using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct ZombieSpawnSystem : ISystem
{
    private int _spawnNumber;
    private int seedCount;
    private float _latestSpawnTime;
    private NativeList<float3> _zombiePositions;
    private LocalTransform _localTransform;
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
            _isInit = true;
        }
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        SpawnZombie(ref state,ref ecb);
        ecb.Playback(state.EntityManager);
    }

    private void SpawnZombie(ref SystemState state, ref EntityCommandBuffer ecb)
    {
        var zombiesDisable = SystemAPI.QueryBuilder().WithAll<Disabled>().WithAll<ZombieInfo>().Build()
            .ToEntityArray(Allocator.Temp);
        _spawnNumber -= zombiesDisable.Length;
        if((_zombieProperties.spawner.spawnInfinity < 1) && _spawnNumber >= _zombieProperties.spawner.numberSpawn) return;
        if((SystemAPI.Time.ElapsedTime - _latestSpawnTime) < _zombieProperties.spawner.timeDelay) return;
        EntityManager entityManager = state.EntityManager;
        int countUsing = 0;
        
        
        for (int i = 0; i < _zombieProperties.spawner.numberSpawnPerFrame; i++)
        {
            Entity entityNew;
            
            if (countUsing + 1 < zombiesDisable.Length)
            {
                entityNew = zombiesDisable[countUsing];
                foreach (LinkedEntityGroup linked in entityManager.GetBuffer<LinkedEntityGroup>(entityNew))
                {
                    Entity entity2 = linked.Value;
                    ecb.RemoveComponent<Disabled>(entity2);
                }
                countUsing++;
            }
            else
            {
                entityNew = ecb.Instantiate(_zombieProperties.entity);
                ecb.AddComponent<LocalTransform>(entityNew);
                ecb.AddComponent(entityNew,_zombieComponent);
            }
            
            
            LocalTransform lt = _localTransform;
            Random random = Random.CreateFromIndex((uint)(seedCount + 1));
            lt.Position = random.GetRandomRange(_pointRandomMin, _pointRandomMax);
            ecb.SetComponent(entityNew,lt);
            _spawnNumber++;
            seedCount++;
        }
    }
}