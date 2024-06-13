using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct PlayerProperty : IComponentData
{
    public Entity entity;
    public float speed;
}

public struct PlayerInfo : IComponentData
{
    
}

public readonly partial struct PlayerAspect : IAspect
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

public struct PlayerMoveInput : IComponentData
{
    public float2 directMove;
    public bool shot;
    public float2 mousePos;
}

//Bullet
public struct BulletProperty : IComponentData
{
    public Entity entity;
    public float speed;
    public float damage;
}

public struct BulletRunTime : IComponentData
{
    public float timeLatest;
    public float cooldown;
}

public struct BulletInfo : IComponentData
{
    
}

public readonly partial struct BulletAspect : IAspect
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
