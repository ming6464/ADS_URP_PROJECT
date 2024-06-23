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
    None,
    Wait,
    WaitAnimation,
    Disable,
    Enable,
    DisableAll,
    EnableAll,
    Destroy,
    DestroyAll
}

public enum CameraType
{
    FirstPersonCamera,
    ThirstPersonCamera,
}

//Enum


//other components

    

//

