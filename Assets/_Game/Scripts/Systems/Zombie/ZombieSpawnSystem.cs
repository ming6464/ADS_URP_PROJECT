using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[BurstCompile]
public partial struct ZombieSpawnSystem : ISystem
{
    private int _seedCount;
    private float _latestSpawnTime;
    private NativeList<float3> _zombiePositions;
    private LocalTransform _localTransform;
    private float3 _pointRandomMin;
    private float3 _pointRandomMax;
    private bool _isInit;
    private ZombieInfo _zombieComponent;
    private ZombieProperty _zombieProperties;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _isInit = false;
        _zombiePositions = new NativeList<float3>(Allocator.Persistent);
        state.RequireForUpdate<ZombieProperty>();
        
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
            Entity entity = SystemAPI.GetSingletonEntity<ZombieProperty>();
            _zombieProperties = SystemAPI.GetComponentRO<ZombieProperty>(entity).ValueRO;
            _localTransform = SystemAPI.GetComponentRO<LocalTransform>(entity).ValueRO;
            _pointRandomMin = _localTransform.InverseTransformPoint(_zombieProperties.spawner.posMin);
            _pointRandomMax = _localTransform.InverseTransformPoint(_zombieProperties.spawner.posMax);
            _latestSpawnTime = -_zombieProperties.spawner.timeDelay;
            _zombieComponent = new ZombieInfo
            {
                directNormal = _zombieProperties.directNormal,
            };
            _isInit = true;
        }
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        SpawnZombie(ref state,ref ecb);
        ecb.Playback(state.EntityManager);
    }

    private void SpawnZombie(ref SystemState state, ref EntityCommandBuffer ecb)
    {
        int spawnNumber = SystemAPI.QueryBuilder().WithNone<Disabled>().WithNone<SetActiveSP>().WithAll<ZombieInfo>().Build()
            .ToEntityArray(Allocator.Temp).Length;
        if((_zombieProperties.spawner.spawnInfinity < 1) && spawnNumber >= _zombieProperties.spawner.numberSpawn) return;
        if((SystemAPI.Time.ElapsedTime - _latestSpawnTime) < _zombieProperties.spawner.timeDelay) return;
        EntityManager entityManager = state.EntityManager;
        var zombiesDisable = SystemAPI.QueryBuilder().WithAll<Disabled>().WithAll<ZombieInfo>().Build()
            .ToEntityArray(Allocator.Temp);
        Entity entityNew;
        for (int i = 0; i < _zombieProperties.spawner.numberSpawnPerFrame; i++)
        {
            if (i < zombiesDisable.Length)
            {
                entityNew = zombiesDisable[i];
                ecb.RemoveComponent<Disabled>(entityNew);
                ecb.AddComponent(entityNew, new SetActiveSP()
                {
                    state = StateID.CanEnable,
                });
            }
            else
            {
                entityNew = ecb.Instantiate(_zombieProperties.entity);
                ecb.AddComponent(entityNew,_zombieComponent);
            }
            
            Random random = Random.CreateFromIndex((uint)(_seedCount + 1));
            _localTransform.Position = random.GetRandomRange(_pointRandomMin, _pointRandomMax);
            ecb.AddComponent(entityNew,_localTransform);
            spawnNumber++;
            _seedCount++;
            if ((_zombieProperties.spawner.spawnInfinity < 1) &&
                spawnNumber >= _zombieProperties.spawner.numberSpawn) return;
        
        }
    }
}