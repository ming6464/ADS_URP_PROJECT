using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct ZombieProperty : IComponentData
{
    public Entity entity;
    public float speed;
    public ZombieSpawner spawner;
    public float3 directNormal;
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
    public float hp;
    public float3 directNormal;
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