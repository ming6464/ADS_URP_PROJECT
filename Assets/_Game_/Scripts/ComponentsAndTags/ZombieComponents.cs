using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct ZombieProperty : IComponentData
{
    public Entity entity;
}

public struct ZombieSpawner : IComponentData
{
    public bool spawnInfinity;
    public bool allowRespawn;
    public float cooldown;
    public int totalNumber;
    public float2 spawnAmountRange;
    public float2 timeRange;
}

public struct ZombieSpawnRuntime : IComponentData
{
    public int zombieAlive;
    // public int zombieDie;
}


public struct BufferZombieNormalSpawnID : IBufferElementData
{
    public int id;
}

public struct BufferZombieSpawnRange : IBufferElementData
{
    public float3 directNormal;
    public float3 posMin;
    public float3 posMax;
}

public struct BufferZombieBossSpawn : IBufferElementData
{
    public int id;
    public float timeDelay;
    public float3 position;
    public float3 directNormal;
}


public struct ZombieInfo : IComponentData
{
    public int id;
    public float hp;
    public float radius;
    public float speed;
    public float damage;
    public float attackRange;
    public float delayAttack;
    public float chasingRange;
    public float3 directNormal;
    public float radiusDamage;
    public float3 offsetAttackPosition;
}

public struct BossInfo : IComponentData
{
    
}

public struct ZombieRuntime : IComponentData
{
    public float latestTimeAttack;
    public StateID latestAnimState;
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
    public float radiusDamage;
    public float3 offsetAttackPosition;
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
