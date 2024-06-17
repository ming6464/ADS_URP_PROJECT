using Unity.Entities;

public struct DisableSP : IComponentData
{
    public DisableKEY key;
    public float startTime;
}