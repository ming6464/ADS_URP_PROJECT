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
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _spawnNumber = 0;
        _latestSpawnTime = 0;
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
        Entity entity = SystemAPI.GetSingletonEntity<GenericZombieProperties>();
        GenericZombieProperties zombieProperties = SystemAPI.GetComponentRO<GenericZombieProperties>(entity).ValueRO;
        if((zombieProperties.spawner.spawnInfinity <= 0) && _spawnNumber >= zombieProperties.spawner.numberSpawn) return;
        if (_latestSpawnTime == 0)
        {
            _localTransform = SystemAPI.GetComponentRO<LocalTransform>(entity).ValueRO;
            _latestSpawnTime = -zombieProperties.spawner.timeDelay;
        }
        if((SystemAPI.Time.ElapsedTime - _latestSpawnTime) < zombieProperties.spawner.timeDelay) return;

        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        Entity entityNew = ecb.Instantiate(zombieProperties.entity);

        LocalTransform lt = _localTransform;

        float3 posMin = lt.InverseTransformPoint(zombieProperties.spawner.posMin);
        float3 posMax = lt.InverseTransformPoint(zombieProperties.spawner.posMax);
        uint seed = math.max(2, (uint)(math.round(SystemAPI.Time.ElapsedTime / SystemAPI.Time.DeltaTime)));
        lt.Position = Random.CreateFromIndex(seed).NextFloat3(posMin,posMax);
        ecb.AddComponent(entityNew,lt);
        ecb.AddComponent(entityNew, new Zombie()
        {
            directNormal = zombieProperties.directNormal,
        });
        
        ecb.Playback(state.EntityManager);
        
        _spawnNumber++;
    }
}