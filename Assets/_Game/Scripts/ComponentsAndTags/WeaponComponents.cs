using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct WeaponProperties : IComponentData
{
    public Entity entityWeapon;
    public Entity entityBullet;
    public float3 offset;
    public float bulletSpeed;
    public float bulletDamage;
    public float length;
}


public struct WeaponRunTime : IComponentData
{
    public float timeLatest;
    public float cooldown;
}

public struct WeaponInfo : IComponentData
{
    
}


public readonly partial struct WeaponAspect : IAspect
{
    public readonly Entity entity;
    private readonly RefRO<WeaponInfo> _weaponInfo;
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

//Bullet
public struct BulletInfo : IComponentData
{
    
}

public readonly partial struct BulletAspect : IAspect
{
    public readonly Entity entity;
    private readonly RefRO<BulletInfo> _bulletInfo;
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

    public LocalTransform LocalTransform => _localTransform.ValueRO;
}
