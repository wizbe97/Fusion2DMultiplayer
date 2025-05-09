using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    private GameplayInputActions _actions;
    private InputAction _move, _jump, _dash, _ladderGrab;

    private void Awake()
    {
        _actions = new GameplayInputActions();
        _move = _actions.Player.Move;
        _jump = _actions.Player.Jump;
        _dash = _actions.Player.Dash;
        _ladderGrab = _actions.Player.LadderGrab;
    }

    private void OnEnable() => _actions.Enable();
    private void OnDisable() => _actions.Disable();

    public FrameInput Gather()
    {
        return new FrameInput
        {
            Move = _move.ReadValue<Vector2>(),
            JumpDown = _jump.WasPressedThisFrame(),
            JumpHeld = _jump.IsPressed(),
            DashDown = _dash.WasPressedThisFrame(),
            LadderHeld = _ladderGrab.IsPressed()
        };
    }
}

public struct FrameInput
{
    public Vector2 Move;
    public bool JumpDown;
    public bool JumpHeld;
    public bool DashDown;
    public bool LadderHeld;
}
