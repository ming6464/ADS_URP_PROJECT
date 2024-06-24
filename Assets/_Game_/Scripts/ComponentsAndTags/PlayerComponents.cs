using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct PlayerProperty : IComponentData
{
    public int idWeaponDefault;
    public Entity characterEntity;
    public float characterRadius;
    public float speed;
    public float3 spawnPosition;
    public int numberSpawnDefault;
    public float2 spaceGrid;
    public int countOfCol;
    public bool aimNearestEnemy;
    public float moveToWard;
}

public struct PlayerInfo : IComponentData
{
    public int idWeapon;
    public int maxXGridCharacter;
    public int maxYGridCharacter;
}

public struct CharacterNewBuffer : IBufferElementData
{
    public Entity entity;
}

public readonly partial struct PlayerAspect : IAspect
{
    public readonly Entity entity;
    private readonly RefRW<LocalTransform> _localTransform;
    private readonly RefRO<PlayerInfo> _playerInfo;
    private readonly RefRO<LocalToWorld> _localToWorld;

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
    
    public float3 PositionWorld => _localToWorld.ValueRO.Position;

    public quaternion RotationWorld => new quaternion(_localToWorld.ValueRO.Value);

    public float ScaleWorld => math.length(_localToWorld.ValueRO.Value.c0.xyz); // Assuming uniform scale

    public LocalTransform LocalTransform => _localTransform.ValueRO;
    public LocalToWorld LocalToWorld => _localToWorld.ValueRO;
}




public struct PlayerInput : IComponentData
{
    public bool pullTrigger;
    public float2 directMove;
    public float2 mousePos;
}

// Character

public struct ParentCharacter : IComponentData
{
    
}

public struct CharacterInfo : IComponentData
{
    public int index;
}

public readonly partial struct CharacterAspect : IAspect
{
    public readonly Entity entity;
    private readonly RefRW<LocalTransform> _localTransform;
    private readonly RefRO<CharacterInfo> _characterInfo;
    private readonly RefRO<LocalToWorld> _localToWorld;

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
    
    public float3 PositionWorld => _localToWorld.ValueRO.Position;

    public quaternion RotationWorld => new quaternion(_localToWorld.ValueRO.Value);

    public float ScaleWorld => math.length(_localToWorld.ValueRO.Value.c0.xyz); // Assuming uniform scale

    public LocalTransform LocalTransform => _localTransform.ValueRO;
    public LocalToWorld LocalToWorld => _localToWorld.ValueRO;
}