using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct GenericZombieProperties : IComponentData
{
    public Entity entity;
    public float speed;
    public float3 targetPosition;
    public ZombieSpawner spawner;
    public float3 directNormal;
}

public struct ZombieSpawner
{
    public float timeDelay;
    public byte spawnInfinity;
    public int numberSpawn;
    public float3 posMin;
    public float3 posMax;
}

public struct Zombie : IComponentData
{
    public float3 directNormal;
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