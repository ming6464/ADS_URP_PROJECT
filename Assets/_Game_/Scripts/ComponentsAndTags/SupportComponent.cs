using Unity.Entities;
using Unity.Mathematics;


public struct CameraProperty : IComponentData
{
    public float3 offsetCamFirst;
    public float3 offsetCamThirst;
    public quaternion offsetRotationCamFirst;
    public quaternion offsetRotationCamThirst;
}

public struct CameraComponent : IComponentData
{
    public CameraType type;
}

public struct LayerStoreComponent : IComponentData
{
    public uint playerLayer;
    public uint characterLayer;
    public uint enemyLayer;
    public uint enemyDieLayer;
    public uint bulletLayer;
    public uint itemLayer;
    public uint itemCanShootLayer;
}

public struct EffectComponent : IComponentData
{
    public float3 position;
    public quaternion rotation;
}

public struct SetActiveSP : IComponentData
{
    public DisableID state;
}


public struct SetAnimationSP : IComponentData
{
    public StateID state;
    public float timeDelay;
}

//Enum
public enum DisableID
{
    Disable,
    Enable,
    DisableAll,
    EnableAll,
    Destroy,
    DestroyAll,
}


public enum StateID
{
    None,
    Wait,
    WaitAnimation,
    Enable,
    Run,
    Attack
}

public enum CameraType
{
    FirstPersonCamera,
    ThirstPersonCamera,
}

//Enum

//Events {

//Events }

//other components

public struct AddToBuffer : IComponentData
{
    public int id;
    public Entity entity;
}

public struct TakeDamage : IComponentData
{
    public float value;
}

public struct NotUnique : IComponentData
{
}

public struct New : IComponentData
{
    
}

public struct DataProperty : IComponentData
{
    
}

//