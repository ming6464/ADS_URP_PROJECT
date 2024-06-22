﻿using Unity.Entities;

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