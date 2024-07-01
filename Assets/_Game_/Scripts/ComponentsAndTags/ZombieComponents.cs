using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct ZombieProperty : IComponentData
{
    public ZombieSpawner spawner;
    public float3 directNormal;
}

public struct ZombieSpawner
{
    public float timeDelay;
    public bool spawnInfinity;
    public bool allowRespawn;
    public int numberSpawn;
    public float2 spawnAmountRange;
    public float2 timeRange;
    public float3 posMin;
    public float3 posMax;
}

public struct ZombieInfo : IComponentData
{
    public int id;
    public float hp;    
    public float speed;
    public float damage;
    public float attackRange;
    public float delayAttack;
    public float chasingRange;
    public float3 directNormal;
}

public struct ZombieRuntime : IComponentData
{
    public float latestTimeAttack;
}

public struct BufferZombieStore : IBufferElementData
{
    public int id;
    public Entity entity;
    public float hp;
    public float speed;
    public float damage;
    public float attackRange;
    public float delayAttack;
    public float chasingRange;
    //
}

public struct BufferZombieDie : IBufferElementData
{
    public int id;
    public Entity entity;
}



public readonly partial struct ZombieAspect : IAspect
{
    public readonly Entity entity;
    private readonly RefRW<LocalTransform> _localTransform;

    public float3 Position
    {
        get => _localTransform.ValueRO.Position;
        set => _localTransform.ValueRW.Position = value;
    }

    public quaternion Rotation
    {
        get => _localTransform.ValueRO.Rotation;
        set => _localTransform.ValueRW.Rotation = value;
    }

    public float Scale
    {
        get => _localTransform.ValueRO.Scale;
        set => _localTransform.ValueRW.Scale = value;
    }
    
}

public struct ActiveZoneProperty : IComponentData
{
    public float3 pointRangeMin;
    public float3 pointRangeMax;
}
