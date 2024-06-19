using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[UpdateBefore(typeof(ZombieAnimationSystem))]
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
        state.Dependency.Complete();
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
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
        bool hasEntityNew = false;
        var zombiesDisable = SystemAPI.QueryBuilder().WithAll<Disabled>().WithAll<ZombieInfo>().Build()
            .ToEntityArray(Allocator.TempJob);
        SpawnZombie(ref state,ref ecb,ref hasEntityNew,ref zombiesDisable);
        state.Dependency.Complete();
        var entityManager = state.EntityManager;
        ecb.Playback(entityManager);
        ecb.Dispose();
        zombiesDisable.Dispose();
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

    private void SpawnZombie(ref SystemState state,ref EntityCommandBuffer ecb,ref bool hasNewEntity, ref NativeArray<Entity> zombiesDisable)
    {
        var zombieAliveArr = SystemAPI.QueryBuilder().WithNone<Disabled>().WithNone<SetActiveSP>().WithAll<ZombieInfo>()
            .Build()
            .ToEntityArray(Allocator.TempJob);
        int zombieAlive = zombieAliveArr.Length;
        zombieAliveArr.Dispose();
        if(!_isSpawnInfinity && zombieAlive >= _numberSpawn) return;
        if((SystemAPI.Time.ElapsedTime - _latestSpawnTime) < _timeDelay) return;
        
        int numberRunningJob = _isSpawnInfinity ? _numberSpawnPerFrame : (math.min(_numberSpawn - zombieAlive,_numberSpawnPerFrame));
        
        var spawnJob = new SpawnZombieJOB()
        {
            ecb = ecb.AsParallelWriter(),
            entityZombieSpawn = _entityZombieSpawn,
            zombieInfoComponent = _zombieComponent,
            localTransformComponent = _localTransform,
            zombiesDisable = zombiesDisable,
            seedCount = _seedCount,
            pointRandomMax = _pointRandomMax,
            pointRandomMin = _pointRandomMin,
        };
        state.Dependency = spawnJob.Schedule(numberRunningJob, 2,state.Dependency);
        _seedCount += numberRunningJob;
        _latestSpawnTime = (float)SystemAPI.Time.ElapsedTime;
        if (numberRunningJob > zombiesDisable.Length)
        {
            hasNewEntity = true;
        }
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


[BurstCompile]
public struct SpawnZombieJOB : IJobParallelFor
{
    [WriteOnly] public EntityCommandBuffer.ParallelWriter ecb;
    [ReadOnly] public Entity entityZombieSpawn;
    [ReadOnly] public ZombieInfo zombieInfoComponent;
    [ReadOnly] public LocalTransform localTransformComponent;
    [ReadOnly] public NativeArray<Entity> zombiesDisable;
    [ReadOnly] public int seedCount;
    [ReadOnly] public float3 pointRandomMin;
    [ReadOnly] public float3 pointRandomMax;
    
    public void Execute(int index)
    {
        Entity entityNew;
        if (index < zombiesDisable.Length)
        {
            entityNew = zombiesDisable[index];
            ecb.RemoveComponent<Disabled>(index,entityNew);
            ecb.AddComponent(index,entityNew, new SetActiveSP()
            {
                state = StateID.CanEnable,
            });
        }
        else
        {
            entityNew = ecb.Instantiate(index,entityZombieSpawn);
            ecb.AddComponent(index,entityNew,zombieInfoComponent);
            ecb.AddComponent<NotUnique>(index,entityNew);
        }
        Random random = Random.CreateFromIndex((uint)(seedCount + index + 1));
        var lt = localTransformComponent;
        lt.Position = random.GetRandomRange(pointRandomMin, pointRandomMax);
        ecb.AddComponent(index,entityNew,lt);
    }
}