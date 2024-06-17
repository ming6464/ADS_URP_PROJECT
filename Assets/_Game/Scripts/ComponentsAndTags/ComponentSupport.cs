using Unity.Entities;

public struct SetActiveSP : IComponentData
{
    // public StateKEY key;
    public int status;
    public float startTime;
}
