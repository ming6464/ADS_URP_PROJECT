using Unity.Entities;


public struct ItemProperty : IComponentData
{
    public Entity entity;
}

public struct ItemInfo : IComponentData
{
    public ItemType type;
    public int count;
}

public struct ItemCollection : IComponentData
{
    public ItemType type;
    public int count;
}

public enum ItemType
{
    Character,
}