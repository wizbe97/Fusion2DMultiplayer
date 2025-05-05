using UnityEngine;

[CreateAssetMenu(menuName = "Player/PlayerStats")]
public class PlayerStats : ScriptableObject
{
    [Header("Setup")]
    public LayerMask PlayerLayer;
    public LayerMask CollisionLayers;

    [Header("Controller Setup")]
    public float VerticalDeadZoneThreshold = 0.3f;
    public double HorizontalDeadZoneThreshold = 0.1f;

    [Header("Movement")]
    public float BaseSpeed = 9;
    public float Acceleration = 50;
    public float Friction = 30;
    public float AirFrictionMultiplier = 0.5f;
    public float DirectionCorrectionMultiplier = 3f;
    public float MaxWalkableSlope = 50;

    [Header("Jump")]
    public bool AllowAirJumps = true;

    public float AirAcceleration = 10f;
    public float MaxAirSpeed = 5f;

    public float ExtraConstantGravity = 40;
    public float BufferedJumpTime = 0.15f;
    public float CoyoteTime = 0.15f;
    public float JumpPower = 20;
    public float EndJumpEarlyExtraForceMultiplier = 3;
    public int MaxAirJumps = 1;

    [Header("Dash")]
    public bool AllowDash = true;
    public float DashVelocity = 50;
    public float DashDuration = 0.2f;
    public float DashCooldown = 1.5f;
    public float DashEndHorizontalMultiplier = 0.5f;

    [Header("Crouch")]
    public bool AllowCrouching;
    public float CrouchSlowDownTime = 0.5f;
    public float CrouchSpeedModifier = 0.5f;
}
