using Unity.Entities;

public struct SetActiveSP : IComponentData
{
    public StateID state;
    public float startTime;
}
public enum StateID
{
    None = 0,
    Wait = 1,
    WaitAnimation = 2,
    CanDisable = 3,
    CanEnable = 4,
}