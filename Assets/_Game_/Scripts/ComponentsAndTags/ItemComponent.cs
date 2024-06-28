﻿using _Game_.Scripts.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


public struct ItemProperty : IComponentData
{
    public Entity entity;
}

public struct ItemInfo : IComponentData
{
    public int id;
    public ItemType type;
    public int count;
    public float hp;
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

public struct ItemCanShoot : IComponentData
{
    
}

//obstacle
public struct BufferTurretObstacle : IBufferElementData
{
    public int id;
    public Entity entity;
    public float3 pivotFireOffset;
    public int bulletPerShot;
    public float spaceAnglePerBullet;
    public bool parallelOrbit;
    public float speed;
    public float damage;
    public float cooldown;
    public float distanceSetChangeRota;
    public float moveToWardMax;
    public float moveToWardMin;
}

public struct TurretInfo : IComponentData
{
    public int id;
    public ObstacleType type;
}

public struct BarrelRunTime : IComponentData
{
    public float value;
}

public struct BarrelInfo : IComponentData
{
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

public struct BarrelCanSetup : IComponentData
{
    public int id;
}

//Enum
public enum ItemType
{
    Character,
    Weapon,
    ObstacleTurret
}