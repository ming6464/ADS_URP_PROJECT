using Unity.Entities;
using Unity.Mathematics;


public struct ItemProperty : IComponentData
{
    public Entity entity;
}

public struct ItemInfo : IComponentData
{
    public int id;
    public ItemType type;
    public int count;
}

public struct ItemCollection : IComponentData
{
    public ItemType type;
    public Entity entityItem;
    public int count;
    public int id;
}

public struct BufferSpawnPoint : IBufferElementData
{
    public float3 value;
}


//obstacle
public struct BufferObstacle : IBufferElementData
{
    public int id;
    public Entity entity;
}

public struct TurretInfo : IComponentData
{
    public int id;
}

public struct BarrelRunTime : IComponentData
{
    public float value;
}

public struct BarrelInfo : IComponentData
{
    public Entity entityBullet;
    public float speed;
    public float damage;
    public float cooldown;
    public float distanceSetChangeRota;
    public float moveToWardMax;
    public float moveToWardMin;
    public float3 pivotFireOffset;
    public int bulletPerShot;
    public float spaceAnglePerBullet;
    public bool parallelOrbit;
}


//Enum
public enum ItemType
{
    Character,
    Weapon,
    Obstacle
}