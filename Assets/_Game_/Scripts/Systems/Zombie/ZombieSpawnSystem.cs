using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[BurstCompile]
public partial struct ZombieSpawnSystem : ISystem
{
    private Entity _entityZombieSpawn;
    private int _seedCount;
    private float _latestSpawnTime;
    private bool _isInit;
    private bool _isSpawnInfinity;
    private int _numberSpawn;
    private int _numberSpawnPerFrame;
    private float _timeDelay;
    private LocalTransform _localTransform;
    private float3 _pointRandomMin;
    private float3 _pointRandomMax;
    private ZombieInfo _zombieComponent;
    private ZombieProperty _zombieProperties;
    private EntityTypeHandle _entityTypeHandle;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _isInit = false;
        state.RequireForUpdate<ZombieProperty>();
        _entityTypeHandle = state.GetEntityTypeHandle();
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
            _isSpawnInfinity = _zombieProperties.spawner.spawnInfinity;
            _numberSpawn = _zombieProperties.spawner.numberSpawn;
            _numberSpawnPerFrame = _zombieProperties.spawner.numberSpawnPerFrame;
            _timeDelay = _zombieProperties.spawner.timeDelay;
            _entityZombieSpawn = _zombieProperties.entity;
            _zombieComponent = new ZombieInfo
            {
                directNormal = _zombieProperties.directNormal,
            };
            _isInit = true;
        }
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        bool hasEntityNew = false;
        SpawnZombie(ref state,ref ecb,ref hasEntityNew);
        var entityManager = state.EntityManager;
        ecb.Playback(entityManager);
        ecb.Dispose();
        if (hasEntityNew)
        {
            EntityQuery entityQuery = SystemAPI.QueryBuilder().WithAll<ZombieInfo, NotUnique>().WithNone<Disabled>()
                .WithNone<SetActiveSP>().Build();
            _entityTypeHandle.Update(ref state);
            ecb = new EntityCommandBuffer(Allocator.TempJob);
            var uniquePhysic = new UniquePhysicColliderJOB()
            {
                ecb = ecb.AsParallelWriter(),
                entityTypeHandle = _entityTypeHandle,
                physicColliderTypeHandle = state.GetComponentTypeHandle<PhysicsCollider>(),
            };
            state.Dependency = uniquePhysic.ScheduleParallel(entityQuery, state.Dependency);
            state.Dependency.Complete();
            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }

    private void SpawnZombie(ref SystemState state,ref EntityCommandBuffer ecb,ref bool hasNewEntity)
    {
        var zombieAliveArr = SystemAPI.QueryBuilder().WithNone<Disabled>().WithNone<SetActiveSP>().WithAll<ZombieInfo>()
            .Build()
            .ToEntityArray(Allocator.Temp);
        int zombieAlive = zombieAliveArr.Length;
        zombieAliveArr.Dispose();
        if(!_isSpawnInfinity && zombieAlive >= _numberSpawn) return;
        if((SystemAPI.Time.ElapsedTime - _latestSpawnTime) < _timeDelay) return;
        var zombiesDisable = SystemAPI.QueryBuilder().WithAll<Disabled>().WithAll<ZombieInfo>().Build()
            .ToEntityArray(Allocator.Temp);
        int numberRunningJob = _isSpawnInfinity ? _numberSpawnPerFrame : (math.min(_numberSpawn - zombieAlive,_numberSpawnPerFrame));
        int i = 0;
        while (i < numberRunningJob)
        {
            Entity entityNew;
            if (i < zombiesDisable.Length)
            {
                entityNew = zombiesDisable[i];
                ecb.RemoveComponent<Disabled>(entityNew);
                ecb.AddComponent(entityNew, new SetActiveSP()
                {
                    state = StateID.Enable,
                });
            }
            else
            {
                entityNew = ecb.Instantiate(_entityZombieSpawn);
                ecb.AddComponent(entityNew,_zombieComponent);
                ecb.AddComponent<NotUnique>(entityNew);
                hasNewEntity = true;
            }
            
            Random random = Random.CreateFromIndex((uint)(++_seedCount));
            _localTransform.Position = random.GetRandomRange(_pointRandomMin, _pointRandomMax);
            ecb.AddComponent(entityNew,_localTransform);
            i++;
        }
        _latestSpawnTime = (float)SystemAPI.Time.ElapsedTime;
        zombiesDisable.Dispose();
    }
}

[BurstCompile]
public partial struct UniquePhysicColliderJOB : IJobChunk
{
    [WriteOnly] public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public EntityTypeHandle entityTypeHandle;
    public ComponentTypeHandle<PhysicsCollider> physicColliderTypeHandle;
    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var entites = chunk.GetNativeArray(entityTypeHandle);
        var physicColliders = chunk.GetNativeArray(physicColliderTypeHandle);
        for (int i = 0; i < chunk.Count; i++)
        {
            var entity = entites[i];
            var collider = physicColliders[i];

            collider.Value = collider.Value.Value.Clone();
            physicColliders[i] = collider;
            ecb.RemoveComponent<NotUnique>(unfilteredChunkIndex,entity);
        }
        
    }
}

