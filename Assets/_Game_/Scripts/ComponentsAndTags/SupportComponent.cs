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
    public uint characterLayer;
    public uint zombieLayer;
    public uint bulletLayer;
    public uint itemLayer;
}

public struct EffectComponent : IComponentData
{
    
}

public struct SetActiveSP : IComponentData
{
    public StateID state;
    public float startTime;
}

//Enum
public enum StateID
{
    None = 0,
    Wait = 1,
    WaitAnimation = 2,
    CanDisable = 3,
    CanEnable = 4,
}

public enum CameraType
{
    FirstPersonCamera,
    ThirstPersonCamera,
}

//Enum


