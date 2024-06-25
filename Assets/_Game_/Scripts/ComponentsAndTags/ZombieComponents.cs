using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct ZombieProperty : IComponentData
{
    public ZombieSpawner spawner;
    public float3 directNormal;
    public bool applyTotalCount;
}

public struct ZombieSpawner
{
    public float timeDelay;
    public bool spawnInfinity;
    public bool allowRespawn;
    public int numberSpawn;
    public int numberSpawnPerFrame;
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
    public float3 directNormal;
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
    //
    public int numberSpawn;
}

public struct BufferZombieDie : IBufferElementData
{
    public int id;
    public Entity entity;
}


public struct TakeDamage : IComponentData
{
    public float value;
}

public struct NotUnique : IComponentData
{
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
