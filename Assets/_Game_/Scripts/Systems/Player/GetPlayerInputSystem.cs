using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup),OrderLast = true)]
public partial class GetPlayerInputSystem : SystemBase
{
    private Inputs _inputs;
    
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<PlayerMoveInput>();
        _inputs = new Inputs();
    }

    protected override void OnStartRunning()
    {
        base.OnStartRunning();
        _inputs.Enable();
        
    }

    protected override void OnStopRunning()
    {
        base.OnStopRunning();
        _inputs.Disable();
    }

    protected override void OnUpdate()
    {
        float2 curMoveInput = _inputs.Player.PlayerMovement.ReadValue<Vector2>();
        float2 mousePos = _inputs.Player.Mouse.ReadValue<Vector2>();
        bool isShot = _inputs.Player.Shot.ReadValue<float>() > 0;
        SystemAPI.SetSingleton(new PlayerMoveInput()
        {
            directMove = curMoveInput,
            mousePos = mousePos,
            shot = isShot,
        });
    }
}