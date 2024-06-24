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
    private bool _isAllowRespawn;
    private int _numberSpawn;
    private int _numberSpawnPerFrame;
    private float _timeDelay;
    private LocalTransform _localTransform;
    private float3 _pointRandomMin;
    private float3 _pointRandomMax;
    private ZombieInfo _zombieComponent;
    private ZombieProperty _zombieProperties;
    private EntityTypeHandle _entityTypeHandle;
    private int _totalSpawnCount;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _isInit = false;
        state.RequireForUpdate<ZombieProperty>();
        _totalSpawnCount = 0;
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
            _isAllowRespawn = _zombieProperties.spawner.allowRespawn;
            _numberSpawn = _zombieProperties.spawner.numberSpawn;
            _numberSpawnPerFrame = _zombieProperties.spawner.numberSpawnPerFrame;
            _timeDelay = _zombieProperties.spawner.timeDelay;
            _entityZombieSpawn = _zombieProperties.entity;
            _zombieComponent = new ZombieInfo
            {
                directNormal = _zombieProperties.directNormal,
                hp = _zombieProperties.hp,
            };
            _isInit = true;
        }
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
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
        int zombieAlive = 0;

        if (_isAllowRespawn)
        {
            NativeArray<Entity> zombieAliveArr = SystemAPI.QueryBuilder().WithNone<Disabled,SetActiveSP>().WithAll<ZombieInfo>()
                .Build()
                .ToEntityArray(Allocator.TempJob);
            zombieAlive = zombieAliveArr.Length;
            zombieAliveArr.Dispose();
        }
        else
        {
            zombieAlive = _totalSpawnCount;
        }
        
        
        if(!_isSpawnInfinity && zombieAlive >= _numberSpawn) return;
        if((SystemAPI.Time.ElapsedTime - _latestSpawnTime) < _timeDelay) return;
        var zombiesDisable = SystemAPI.QueryBuilder().WithAll<Disabled>().WithAll<ZombieInfo>().Build()
            .ToEntityArray(Allocator.TempJob);
        int numberZombieCanSpawn = _isSpawnInfinity ? _numberSpawnPerFrame : (math.min(_numberSpawn - zombieAlive,_numberSpawnPerFrame));
        int i = 0;
        while (i < numberZombieCanSpawn)
        {
            
            Random random = Random.CreateFromIndex((uint)(++_seedCount));
            _localTransform.Position = random.GetRandomRange(_pointRandomMin, _pointRandomMax);
            
            Entity entityNew;
            if (i < zombiesDisable.Length)
            {
                entityNew = zombiesDisable[i];
                ecb.AddComponent(entityNew,_localTransform);
                ecb.RemoveComponent<Disabled>(entityNew);
                ecb.AddComponent(entityNew, new SetActiveSP()
                {
                    state = StateID.Enable,
                });
            }
            else
            {
                entityNew = ecb.Instantiate(_entityZombieSpawn);
                ecb.AddComponent<NotUnique>(entityNew);
                ecb.AddComponent(entityNew,_localTransform);
                hasNewEntity = true;
            }
            ecb.AddComponent(entityNew,_zombieComponent);
            i++;
        }
        _latestSpawnTime = (float)SystemAPI.Time.ElapsedTime;
        _totalSpawnCount += numberZombieCanSpawn;
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
        var physicColliders = chunk.GetNativeArray(physicColliderTypeHandle);
        for (int i = 0; i < chunk.Count; i++)
        {
            var collider = physicColliders[i];
            collider.Value = collider.Value.Value.Clone();
            physicColliders[i] = collider;
            
        }
        ecb.RemoveComponent<NotUnique>(unfilteredChunkIndex,chunk.GetNativeArray(entityTypeHandle));
    }
}