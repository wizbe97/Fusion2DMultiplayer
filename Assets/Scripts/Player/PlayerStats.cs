using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[CreateAssetMenu]
public class PlayerStats : ScriptableObject
{
    // Setup
    [Header("Setup")]
    [Tooltip("Layer the player is on (used to exclude self in checks)")]
    public LayerMask PlayerLayer;

    [Tooltip("Layers considered solid for collisions, ground, walls, etc.")]
    public LayerMask CollisionLayers;

    [Tooltip("Size and collider shape settings for the character")]
    public CharacterSize CharacterSize;

    // Controller Setup
    [Header("Controller Setup"), Space]
    [Tooltip("Minimum vertical input value required to trigger up/down movement or interaction (e.g. climbing ladders, crouching). Prevents unintentional movement.")]
    public float VerticalDeadZoneThreshold = 0.3f;

    [Tooltip("Minimum horizontal input value required before considering movement left/right. Useful for analog sticks.")]
    public double HorizontalDeadZoneThreshold = 0.1;

    [Tooltip("Controls how the character sticks to the ground on slopes and drops.\nVelocity: smooth but can be unreliable on sharp edges.\nImmediate: stable but might jitter.")]
    public PositionCorrectionMode PositionCorrectionMode = PositionCorrectionMode.Velocity;


    // Movement
    [Header("Movement"), Space]
    [Tooltip("Maximum ground speed when moving.")]
    public float BaseSpeed = 9;

    [Tooltip("Rate of horizontal acceleration when input is held.")]
    public float Acceleration = 50;

    [Tooltip("Rate of deceleration when no input is held (on ground).")]
    public float Friction = 30;
    [Tooltip("Max rate of deceleration when falling).")]
    public float MaxFallSpeed = 20f;

    [Tooltip("Multiplier applied to acceleration and friction while airborne.")]
    public float AirFrictionMultiplier = 0.5f;

    [Tooltip("Extra force applied when changing direction quickly to make turning snappier.")]
    public float DirectionCorrectionMultiplier = 3f;

    [Tooltip("Maximum ground slope (in degrees) the player can walk on. Slopes steeper than this are considered walls.")]
    public float MaxWalkableSlope = 50;

    // Jump
    [Header("Jump"), Space]
    [Tooltip("Constant downward force applied while airborne to improve jump feel")]
    public float ExtraConstantGravity = 40;

    [Tooltip("How long a jump input can be buffered before landing")]
    public float BufferedJumpTime = 0.15f;

    [Tooltip("How long after falling off a ledge a jump can still be performed")]
    public float CoyoteTime = 0.15f;

    [Tooltip("Force applied when jumping from the ground")]
    public float JumpPower = 20;

    [Tooltip("Multiplier for gravity when jump is ended early")]
    public float EndJumpEarlyExtraForceMultiplier = 3;

    [Tooltip("Maximum number of air jumps (excluding initial and coyote)")]
    public int MaxAirJumps = 1;

    [Tooltip("If true, gives the player more control at the apex of a jump")]
    public bool UseApexControl = true;

    [Tooltip("Multiplier applied to horizontal control at the apex")]
    [Range(0f, 5f)] public float ApexModifier = 1.5f;

    [Tooltip("Only triggers ApexControl when velocity.y is below this value (near true apex)")]
    [Range(0f, 10f)] public float ApexDetectionThreshold = 2f;


    // Dash
    [Header("Dash"), Space]
    [Tooltip("Enables dash functionality.")]
    public bool AllowDash = true;

    [Tooltip("Initial speed of the dash in the chosen direction.")]
    public float DashVelocity = 50;

    [Tooltip("How long the dash lasts in seconds.")]
    public float DashDuration = 0.2f;

    [Tooltip("Cooldown time before the player can dash again.")]
    public float DashCooldown = 1.5f;

    [Tooltip("Multiplier for horizontal speed at the end of a dash.")]
    public float DashEndHorizontalMultiplier = 0.5f;

    // Crouch
    [Header("Crouch"), Space]
    [Tooltip("Enables crouching.")]
    public bool AllowCrouching;

    [Tooltip("Time it takes to fully enter crouching speed state.")]
    public float CrouchSlowDownTime = 0.5f;

    [Tooltip("Speed multiplier applied while crouching.")]
    public float CrouchSpeedModifier = 0.5f;

    // Walls
    [Header("Walls"), Space]
    [Tooltip("Enables wall grabs and wall jumps.")]
    public bool AllowWalls;

    [Tooltip("Which layers are climbable (for wall interactions).")]
    public LayerMask ClimbableLayer;

    [Tooltip("Time after jumping off a wall during which input is temporarily ignored.")]
    public float WallJumpTotalInputLossTime = 0.2f;

    [Tooltip("Time it takes for horizontal control to fully return after a wall jump.")]
    public float WallJumpInputLossReturnTime = 0.5f;

    [Tooltip("If true, player must be pushing into the wall to stick or grab it.")]
    public bool RequireInputPush;

    [Tooltip("Jump force vector away from the wall when grabbing it.")]
    public Vector2 WallJumpPower = new(25, 15);

    [Tooltip("Jump force vector when pushing off a wall without grabbing.")]
    public Vector2 WallPushPower = new(15, 10);

    [Tooltip("Vertical climb speed while holding onto a wall.")]
    public float WallClimbSpeed = 5;

    [Tooltip("Vertical acceleration downward while sliding down a wall.")]
    public float WallFallAcceleration = 20;

    [Tooltip("Small vertical force applied when letting go of a wall to pop player off slightly.")]
    public float WallPopForce = 10;

    [Tooltip("Time window after leaving a wall where a wall jump is still allowed.")]
    public float WallCoyoteTime = 0.3f;

    [Tooltip("Distance from the player to check for climbable walls.")]
    public float WallDetectorRange = 0.1f;

    // Ladders
    [Header("Ladders"), Space]
    [Tooltip("Enables climbing ladders.")]
    public bool AllowLadders;

    [Tooltip("Cooldown time after leaving a ladder before reattaching is allowed.")]
    public double LadderCooldownTime = 0.15f;

    [Tooltip("Automatically attach to ladder when moving into it (no grab input needed).")]
    public bool AutoAttachToLadders = true;

    [Tooltip("If true, player snaps to the center of the ladder.")]
    public bool SnapToLadders = true;

    [Tooltip("Which layers are considered ladders.")]
    public LayerMask LadderLayer;

    [Tooltip("Smoothing time for snapping to ladder center.")]
    public float LadderSnapTime = 0.02f;

    [Tooltip("Distance from the ladder to check for a valid snap.")]
    [Range(0f, 1f)]
    public float MaxLadderSnapDistance = 0.25f;


    [Tooltip("Small vertical boost applied when jumping off a ladder.")]
    public float LadderPopForce = 10;

    [Tooltip("Upward climbing speed on ladders.")]
    public float LadderClimbSpeed = 8;

    [Tooltip("Downward sliding speed on ladders.")]
    public float LadderSlideSpeed = 12;

    [Tooltip("Multiplier for shimmying left/right on a ladder while holding grab.")]
    public float LadderShimmySpeedMultiplier = 0.5f;

    // Moving Platforms
    [Header("Moving Platforms"), Space]
    [Tooltip("Multiplier to reduce negative Y velocity from platform movement when stepping off.")]
    public float NegativeYVelocityNegation = 0.2f;

    [Tooltip("Rate at which externally applied velocity decays over time.")]
    public float ExternalVelocityDecayRate = 0.1f;

    private void OnValidate()
    {
        var potentialPlayer = FindObjectsOfType<PlayerController>();
        foreach (var player in potentialPlayer)
        {
            player.OnValidate();
        }
    }
}



[Serializable]
public class CharacterSize
{
    public const float STEP_BUFFER = 0.05f;
    public const float COLLIDER_EDGE_RADIUS = 0.05f;

    [Range(0.1f, 10), Tooltip("How tall you are. This includes a collider and your step height.")]
    public float Height = 1.8f;

    [Range(0.1f, 10), Tooltip("The width of your collider")]
    public float Width = 0.6f;

    [Range(STEP_BUFFER, 15), Tooltip("Step height allows you to step over rough terrain like steps and rocks.")]
    public float StepHeight = 0.5f;

    [Range(0.1f, 10), Tooltip("A percentage of your height stat which determines your height while crouching. A smaller crouch requires more step height sacrifice")]
    public float CrouchHeight = 0.6f;

    [Range(0.01f, 0.2f), Tooltip("The outer buffer distance of the grounder rays. Reducing this too much can cause problems on slopes, too big and you can get stuck on the sides of drops.")]
    public float RayInset = 0.1f;

    public GeneratedCharacterSize GenerateCharacterSize()
    {
        ValidateHeights();

        var s = new GeneratedCharacterSize
        {
            Height = Height,
            Width = Width,
            StepHeight = StepHeight,
            RayInset = RayInset
        };

        s.StandingColliderSize = new Vector2(s.Width - COLLIDER_EDGE_RADIUS * 2, s.Height - s.StepHeight - COLLIDER_EDGE_RADIUS * 2);
        s.StandingColliderCenter = new Vector2(0, s.Height - s.StandingColliderSize.y / 2 - COLLIDER_EDGE_RADIUS);

        s.CrouchingHeight = CrouchHeight;
        s.CrouchColliderSize = new Vector2(s.Width - COLLIDER_EDGE_RADIUS * 2, s.CrouchingHeight - s.StepHeight);
        s.CrouchingColliderCenter = new Vector2(0, s.CrouchingHeight - s.CrouchColliderSize.y / 2 - COLLIDER_EDGE_RADIUS);

        return s;
    }

    private static double _lastDebugLogTime;
    private const double TIME_BETWEEN_LOGS = 1f;

    private void ValidateHeights()
    {
#if UNITY_EDITOR
        var maxStepHeight = Height - STEP_BUFFER;
        if (StepHeight > maxStepHeight)
        {
            StepHeight = maxStepHeight;
            Log("Step height cannot be larger than height");
        }

        var minCrouchHeight = StepHeight + STEP_BUFFER;

        if (CrouchHeight < minCrouchHeight)
        {
            CrouchHeight = minCrouchHeight;
            Log("Crouch height must be larger than step height");
        }

        void Log(string text)
        {
            var time = EditorApplication.timeSinceStartup;
            if (_lastDebugLogTime + TIME_BETWEEN_LOGS > time) return;
            _lastDebugLogTime = time;
            Debug.LogWarning(text);
        }
#endif
    }
}

public struct GeneratedCharacterSize
{
    // Standing
    public float Height;
    public float Width;
    public float StepHeight;
    public float RayInset;
    public Vector2 StandingColliderSize;
    public Vector2 StandingColliderCenter;

    // Crouching
    public Vector2 CrouchColliderSize;
    public float CrouchingHeight;
    public Vector2 CrouchingColliderCenter;
}

[Serializable]
public enum PositionCorrectionMode
{
    Velocity,
    Immediate
}
