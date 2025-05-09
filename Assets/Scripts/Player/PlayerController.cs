using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour, IPlayerController, IPhysicsObject
{
    [SerializeField] private bool _drawGizmos = true; // Toggle for debug gizmo drawing in editor

    #region References

    private BoxCollider2D _groundedCollider; // Used when grounded
    private CapsuleCollider2D _airborneCollider; // Used when airborne
    private ConstantForce2D _constantForce; // Optional constant force (e.g. gravity overrides)
    private Rigidbody2D _rigidbody;
    private PlayerInput _playerInput;

    #endregion

    #region Interface

    [field: SerializeField] public PlayerStats PlayerStats { get; private set; } // Stats/config for movement/physics

    public ControllerState CurrentState { get; private set; } // Last saved position/velocity state

    // Events for external systems to hook into
    public event Action<JumpType> OnJumped;
    public event Action<bool, float> OnGroundedStateChanged;
    public event Action<bool, Vector2> OnDashStateChanged;
    public event Action<bool> OnWallGrabStateChanged;
    public event Action<Vector2> OnRepositioned;
    public event Action<bool> OnPlayerToggled;

    // Public readable state
    public bool IsControllerActive { get; private set; } = true;
    public Vector2 UpDirection { get; private set; }             // Rotated up vector
    public Vector2 RightDirection { get; private set; }          // Rotated right vector
    public bool IsCrouching { get; private set; }                // True when crouching
    public Vector2 MoveInput => _frameInput.Move;                // Input vector
    public Vector2 GroundSurfaceNormal { get; private set; }     // Last grounded surface normal
    public Vector2 CurrentVelocity { get; private set; }         // Cached velocity (set manually)
    public int WallDirection { get; private set; }        // -1 = left wall, 1 = right wall
    public bool IsClimbingLadder { get; private set; }           // True when attached to ladder

    private Vector2 _frameForceToApply; // Force to apply this frame

    public void AddForceThisFrame(Vector2 force, bool resetVelocity = false)
    {
        if (resetVelocity) SetVelocity(Vector2.zero);
        _frameForceToApply += force;
    }

    public void LoadCharacterState(ControllerState state)
    {
        RepositionInstantly(state.Position);
        _rigidbody.rotation = state.Rotation;
        SetVelocity(state.Velocity);

        if (state.Grounded) SetGroundedState(true);
    }

    public void RepositionInstantly(Vector2 position, bool resetVelocity = false)
    {
        _rigidbody.position = position;
        if (resetVelocity) SetVelocity(Vector2.zero);
        OnRepositioned?.Invoke(position);
    }

    public void SetPlayerActive(bool isActive)
    {
        IsControllerActive = isActive;
        _rigidbody.isKinematic = !isActive;
        OnPlayerToggled?.Invoke(isActive);
    }

    private void SetGroundedState(bool isGrounded)
    {
        _isGrounded = isGrounded;
        if (isGrounded)
        {
            OnGroundedStateChanged?.Invoke(true, _lastFrameVerticalVelocity);
            _rigidbody.gravityScale = 0;
            SetVelocity(_horizontalVelocityOnly);
            _constantForce.force = Vector2.zero;
            _stepDownExtension = _character.StepHeight;
            _canDash = true;
            _coyoteUsable = true;
            _bufferedJumpUsable = true;
            ResetAirJumps();
            SetColliderMode(ColliderMode.Standard);
        }
        else
        {
            OnGroundedStateChanged?.Invoke(false, 0);
            _timeLeftGrounded = _timeSinceStart;
            _rigidbody.gravityScale = GRAVITY_SCALE;
            SetColliderMode(ColliderMode.Airborne);
        }
    }

    #endregion
    #region Input

    private FrameInput _frameInput;

    private void GatherInput()
    {
        _frameInput = _playerInput.Gather();

        if (_frameInput.JumpDown)
        {
            _jumpToConsume = true;
            _timeJumpWasPressed = _timeSinceStart;
        }

        if (_frameInput.DashDown)
        {
            _dashToConsume = true;
        }
    }

    #endregion
    #region Lifecycle & Update Loop

    private float _deltaTime;      // Time.deltaTime value this frame
    private float _timeSinceStart; // Elapsed time since game start

    private void Awake()
    {
        if (!TryGetComponent(out _playerInput)) _playerInput = gameObject.AddComponent<PlayerInput>();
        if (!TryGetComponent(out _constantForce)) _constantForce = gameObject.AddComponent<ConstantForce2D>();

        SetupCharacter(); // Setup colliders

        PhysicsSimulator.Instance.AddPlayer(this); // Register with physics manager
    }

    private void OnDestroy() => PhysicsSimulator.Instance.RemovePlayer(this);

    public void TickUpdate(float delta, float time)
    {
        _deltaTime = delta;
        _timeSinceStart = time;
        GatherInput();
    }

    public void TickFixedUpdate(float delta)
    {
        _deltaTime = delta;
        if (!IsControllerActive) return;

        RemoveTransientVelocity();

        SetFrameData();

        CalculateCollisions();
        CalculateDirection();

        CalculateWalls();
        CalculateLadders();
        CalculateJump();
        CalculateDash();

        CalculateExternalModifiers();

        TraceGround();
        Move();

        CalculateCrouch();

        CleanFrameData();

        SaveCharacterState();
    }

    #endregion

    #region Setup

    private const float GRAVITY_SCALE = 1f;     // Gravity override multiplier 

    private bool _originalQueryStartInColliders;       // Caches global Physics2D setting
    private GeneratedCharacterSize _character; // Precomputed collider data

    public void OnValidate() => SetupCharacter();

    private void SetupCharacter()
    {
        // Generate collider sizing from the PlayerStats scriptable object
        _character = PlayerStats.CharacterSize.GenerateCharacterSize();

        // Cache Physics2D settings so we can restore if necessary
        _originalQueryStartInColliders = Physics2D.queriesStartInColliders;

        // Define wall detection bounds based on the character's collider size and wall detection range
        _wallDetectionBounds = new Bounds(
            new Vector3(0, _character.Height / 2f), // Centered halfway up the player
            new Vector3(
                _character.StandingColliderSize.x + CharacterSize.COLLIDER_EDGE_RADIUS * 2f + PlayerStats.WallDetectorRange,
                _character.Height - 0.1f
            )
        );

        // Assign reference to Rigidbody2D
        _rigidbody = GetComponent<Rigidbody2D>();

        // Configure the BoxCollider2D (used when grounded)
        _groundedCollider = GetComponent<BoxCollider2D>();
        _groundedCollider.edgeRadius = CharacterSize.COLLIDER_EDGE_RADIUS;
        _groundedCollider.sharedMaterial = _rigidbody.sharedMaterial;
        _groundedCollider.enabled = true;

        // Configure the CapsuleCollider2D (used in the air)
        _airborneCollider = GetComponent<CapsuleCollider2D>();
        _airborneCollider.size = new Vector2(
            _character.Width - SKIN_WIDTH * 2f,
            _character.Height - SKIN_WIDTH * 2f
        );
        _airborneCollider.offset = new Vector2(0, _character.Height / 2f);
        _airborneCollider.sharedMaterial = _rigidbody.sharedMaterial;

        // Start the player in airborne mode (default state)
        SetColliderMode(ColliderMode.Airborne);
    }

    private void SaveCharacterState()
    {
        CurrentState = new ControllerState
        {
            Position = _positionThisFrame,
            Rotation = _rigidbody.rotation,
            Velocity = CurrentVelocity,
            Grounded = _isGrounded
        };
    }

    #endregion

    #region Frame Data

    private bool _hasInputThisFrame;                // True if horizontal input was pressed this frame
    private Vector2 _horizontalVelocityOnly;        // Copy of current velocity with vertical removed
    private Vector2 _positionThisFrame;             // Position of the Rigidbody at start of frame
    private Bounds _wallDetectionBounds;            // Bounds used for wall collision checks

    private void SetFrameData()
    {
        // Convert Rigidbody rotation to radians
        var rotationInRadians = _rigidbody.rotation * Mathf.Deg2Rad;

        // Compute Up and Right directions based on current rotation
        UpDirection = new Vector2(-Mathf.Sin(rotationInRadians), Mathf.Cos(rotationInRadians));
        RightDirection = new Vector2(UpDirection.y, -UpDirection.x);

        // Store current Rigidbody position
        _positionThisFrame = _rigidbody.position;

        // Detect if horizontal movement input was pressed
        _hasInputThisFrame = _frameInput.Move.x != 0;

        // Cache current velocity
        CurrentVelocity = _rigidbody.velocity;

        // Trim vertical component (used for grounded movement, friction, etc.)
        _horizontalVelocityOnly = new Vector2(CurrentVelocity.x, 0);
    }

    private void RemoveTransientVelocity() // Clear any temporary velocity changes
    {
        var currentVelocity = _rigidbody.velocity;
        var velocityBeforeReduction = currentVelocity;

        // Subtract total impulse applied last frame
        currentVelocity -= _totalImpulseLastFrame;
        SetVelocity(currentVelocity);

        // Clear transient impulse data for this frame
        _frameImpulseVelocity = Vector2.zero;
        _totalImpulseLastFrame = Vector2.zero;

        // Determine decay rate for external forces (e.g. knockback)
        float decay = PlayerStats.Friction * PlayerStats.AirFrictionMultiplier * PlayerStats.ExternalVelocityDecayRate;

        // Increase decay if we're moving against a wall or obstacle
        if ((velocityBeforeReduction.x < 0 && _decayingExternalVelocity.x < velocityBeforeReduction.x) ||
            (velocityBeforeReduction.x > 0 && _decayingExternalVelocity.x > velocityBeforeReduction.x) ||
            (velocityBeforeReduction.y < 0 && _decayingExternalVelocity.y < velocityBeforeReduction.y) ||
            (velocityBeforeReduction.y > 0 && _decayingExternalVelocity.y > velocityBeforeReduction.y))
        {
            decay *= 5;
        }

        // Slowly move knockback/external forces toward zero
        _decayingExternalVelocity = Vector2.MoveTowards(_decayingExternalVelocity, Vector2.zero, decay * _deltaTime);

        // Clear any forced move override (e.g. teleport, platform movement)
        _instantMovementOverride = Vector2.zero;
    }

    private void CleanFrameData()
    {
        _jumpToConsume = false;
        _dashToConsume = false;
        _frameForceToApply = Vector2.zero;

        // Store Y velocity for next-frame comparisons (e.g. fall vs. rise)
        _lastFrameVerticalVelocity = CurrentVelocity.y;
    }

    #endregion

    #region Collisions

    private const float SKIN_WIDTH = 0.02f;                  // Padding to prevent collider sticking
    private const int RAY_SIDE_COUNT = 5;                    // Number of side rays for ground detection
    private RaycastHit2D _groundRayHit;                      // Last successful ground raycast hit
    private bool _isGrounded;                                // Current grounded state
    private float _stepDownExtension;                        // Extra step-down length used for smooth drops

    // Total ground check ray length
    private float GroundRayLength => _character.StepHeight + SKIN_WIDTH;

    // Starting point for center ground ray
    private Vector2 GroundRayStartPoint => _positionThisFrame + UpDirection * GroundRayLength;

    private void CalculateCollisions()
    {
        Physics2D.queriesStartInColliders = false;

        // Is the middle ray good?
        var isGroundedThisFrame = PerformRay(GroundRayStartPoint);

        // If not, zigzag rays from the center outward until we find a hit
        if (!isGroundedThisFrame)
        {
            foreach (var offset in GenerateGroundRayOffsets())
            {
                isGroundedThisFrame = PerformRay(GroundRayStartPoint + RightDirection * offset) || PerformRay(GroundRayStartPoint - RightDirection * offset);
                if (isGroundedThisFrame) break;
            }
        }

        if (isGroundedThisFrame && !_isGrounded) SetGroundedState(true);
        else if (!isGroundedThisFrame && _isGrounded) SetGroundedState(false);

        Physics2D.queriesStartInColliders = _originalQueryStartInColliders;

        bool PerformRay(Vector2 point)
        {
            _groundRayHit = Physics2D.Raycast(point, -UpDirection, GroundRayLength + _stepDownExtension, PlayerStats.CollisionLayers);
            if (!_groundRayHit) return false;

            if (Vector2.Angle(_groundRayHit.normal, UpDirection) > PlayerStats.MaxWalkableSlope)
            {
                return false;
            }

            return true;
        }
    }

    // Generate offset distances for left/right ground check rays
    private IEnumerable<float> GenerateGroundRayOffsets()
    {
        float extent = _character.StandingColliderSize.x / 2f - _character.RayInset;
        float offsetStep = extent / RAY_SIDE_COUNT;

        for (int i = 1; i <= RAY_SIDE_COUNT; i++)
        {
            yield return offsetStep * i;
        }
    }

    // Swap between collider modes (standing/crouch/air)
    private void SetColliderMode(ColliderMode mode)
    {
        _airborneCollider.enabled = mode == ColliderMode.Airborne;

        switch (mode)
        {
            case ColliderMode.Standard:
                _groundedCollider.size = _character.StandingColliderSize;
                _groundedCollider.offset = _character.StandingColliderCenter;
                break;

            case ColliderMode.Crouching:
                _groundedCollider.size = _character.CrouchColliderSize;
                _groundedCollider.offset = _character.CrouchingColliderCenter;
                break;

            case ColliderMode.Airborne:
                // Capsule collider already active; no box collider changes needed
                break;
        }
    }

    private enum ColliderMode
    {
        Standard,
        Crouching,
        Airborne
    }

    #endregion

    #region Direction

    private Vector2 _moveDirectionThisFrame; // Normalized direction vector for movement this frame

    private void CalculateDirection()
    {
        // Start with horizontal input only
        _moveDirectionThisFrame = new Vector2(_frameInput.Move.x, 0f);

        if (_isGrounded)
        {
            GroundSurfaceNormal = _groundRayHit.normal; // Store the current ground normal

            // If we're on a slope that is walkable, adjust direction to follow it
            float slopeAngle = Vector2.Angle(GroundSurfaceNormal, UpDirection);
            if (slopeAngle < PlayerStats.MaxWalkableSlope)
            {
                // Adjust vertical movement to "stick" to the slope
                _moveDirectionThisFrame.y = _moveDirectionThisFrame.x * -GroundSurfaceNormal.x / GroundSurfaceNormal.y;
            }
        }

        // Ensure the direction vector has a magnitude of 1
        _moveDirectionThisFrame = _moveDirectionThisFrame.normalized;
    }

    #endregion

    #region Move

    private Vector2 _frameImpulseVelocity;                     // One-frame force (e.g. platform shift)
    private Vector2 _instantMovementOverride;                  // Direct positional override (e.g. snapping to ground)
    private Vector2 _decayingExternalVelocity;                 // External velocity that decays over time (e.g. knockback)
    private Vector2 _totalImpulseLastFrame;                    // Combined impulse velocity from last frame
    private Vector2 _frameSpeedModifier, _currentFrameSpeedModifier = Vector2.one; // Speed modifiers (crouch, slow, etc.)
    private const float SLOPE_ANGLE_FOR_EXACT_MOVEMENT = 0.7f; // Threshold for slope angle blending
    private IPhysicsMover _lastPlatform;                       // Platform we were on last frame
    private float _lastFrameVerticalVelocity;                  // Vertical speed at end of last frame

    private void SetVelocity(Vector2 velocity)
    {
        _rigidbody.velocity = velocity;
        CurrentVelocity = velocity;
    }

    private void TraceGround()
    {
        IPhysicsMover currentPlatform = null;

        if (_isGrounded && !IsWithinJumpClearance)
        {
            var groundOffset = _character.StepHeight - _groundRayHit.distance; // Distance needed to stay grounded
            if (groundOffset != 0)
            {
                var correction = Vector2.zero;
                correction.y += groundOffset; // Move up to stay grounded

                if (PlayerStats.PositionCorrectionMode == PositionCorrectionMode.Velocity) _frameImpulseVelocity = correction / _deltaTime; // Apply via velocity
                else _instantMovementOverride = correction; // Apply instantly
            }

            if (_groundRayHit.transform.TryGetComponent(out currentPlatform))
                _activatedMovers.Add(currentPlatform); // Register platform
        }

        if (_lastPlatform != currentPlatform)
        {
            if (_lastPlatform is { UsesBounding: false })
            {
                _activatedMovers.Remove(_lastPlatform); // Remove old platform
                ApplyPlatformExitVelocity(_lastPlatform);  // Apply takeoff velocity
            }
            _lastPlatform = currentPlatform;
        }

        foreach (var platform in _activatedMovers)
        {
            if (_positionThisFrame.y < platform.FramePosition.y - SKIN_WIDTH) continue; // Only apply if above platform
            _frameImpulseVelocity += platform.FramePositionDelta / _deltaTime; // Add platform velocity
        }
    }

    private void ApplyPlatformExitVelocity(IPhysicsMover platform)
    {
        var vel = platform.TakeOffVelocity;
        if (vel.y < 0) vel.y *= PlayerStats.NegativeYVelocityNegation; // Reduce fall speed
        _decayingExternalVelocity += vel; // Add exit velocity to decay pool
    }

    private void Move()
    {
        if (_frameForceToApply != Vector2.zero)
        {
            _rigidbody.velocity += AdditionalFrameVelocities(); // Add transient forces
            _rigidbody.AddForce(_frameForceToApply * _rigidbody.mass, ForceMode2D.Impulse); // Apply jump/dash force
            return;
        }

        if (_dashing)
        {
            SetVelocity(_dashVelocity); // Force dash velocity
            return;
        }

        if (_isOnWall)
        {
            _constantForce.force = Vector2.zero;

            float wallVelocity;
            if (_frameInput.Move.y != 0) wallVelocity = _frameInput.Move.y * PlayerStats.WallClimbSpeed;
            else wallVelocity = Mathf.MoveTowards(Mathf.Min(CurrentVelocity.y, 0), -PlayerStats.WallClimbSpeed, PlayerStats.WallFallAcceleration * _deltaTime);

            SetVelocity(new Vector2(_rigidbody.velocity.x, wallVelocity));
            return;
        }

        if (IsClimbingLadder)
        {
            if (!_frameInput.LadderHeld)
            {
                ToggleClimbingLadder(false);
                return;
            }
            _constantForce.force = Vector2.zero;
            _rigidbody.gravityScale = 0;

            var goalVelocity = Vector2.zero;
            goalVelocity.y = _frameInput.Move.y * (_frameInput.Move.y > 0 ? PlayerStats.LadderClimbSpeed : PlayerStats.LadderSlideSpeed);

            // Horizontal
            float goalX;
            if (PlayerStats.SnapToLadders && _frameInput.Move.x == 0)
            {
                var targetX = _ladderHit.transform.position.x;
                goalX = Mathf.SmoothDamp(_positionThisFrame.x, targetX, ref _ladderSnapVelocity, PlayerStats.LadderSnapTime);
            }
            else
            {
                goalX = Mathf.MoveTowards(_positionThisFrame.x, _positionThisFrame.x + _frameInput.Move.x, PlayerStats.Acceleration * PlayerStats.LadderShimmySpeedMultiplier * _deltaTime);
            }

            goalVelocity.x = (goalX - _positionThisFrame.x) / _deltaTime;

            SetVelocity(goalVelocity);

            return;
        }


        var extraForce = new Vector2(0, _isGrounded ? 0 : -PlayerStats.ExtraConstantGravity * (_endedJumpEarly && CurrentVelocity.y > 0 ? PlayerStats.EndJumpEarlyExtraForceMultiplier : 1));
        _constantForce.force = extraForce * _rigidbody.mass;

        var targetSpeed = _hasInputThisFrame ? PlayerStats.BaseSpeed : 0;

        if (IsCrouching)
        {
            var crouchPoint = Mathf.InverseLerp(0, PlayerStats.CrouchSlowDownTime, _timeSinceStart - _timeStartedCrouching);
            targetSpeed *= Mathf.Lerp(1, PlayerStats.CrouchSpeedModifier, crouchPoint);
        }

        var step = _hasInputThisFrame ? PlayerStats.Acceleration : PlayerStats.Friction;

        var xDir = (_hasInputThisFrame ? _moveDirectionThisFrame : CurrentVelocity.normalized);

        // Quicker direction change
        if (Vector3.Dot(_horizontalVelocityOnly, _moveDirectionThisFrame) < 0) step *= PlayerStats.DirectionCorrectionMultiplier;

        Vector2 newVelocity;
        step *= _deltaTime;
        if (_isGrounded)
        {
            var speed = Mathf.MoveTowards(CurrentVelocity.magnitude, targetSpeed, step);

            // Blend the two approaches
            var targetVelocity = xDir * speed;

            // Calculate the new speed based on the current and target speeds
            var newSpeed = Mathf.MoveTowards(CurrentVelocity.magnitude, targetVelocity.magnitude, step);

            // TODO: Lets actually trace the ground direction automatically instead of direct
            var smoothed = Vector2.MoveTowards(CurrentVelocity, targetVelocity, step); // Smooth but potentially inaccurate
            var direct = targetVelocity.normalized * newSpeed; // Accurate but abrupt
            var slopePoint = Mathf.InverseLerp(0, SLOPE_ANGLE_FOR_EXACT_MOVEMENT, Mathf.Abs(_moveDirectionThisFrame.y)); // Blend factor

            // Calculate the blended velocity
            newVelocity = Vector2.Lerp(smoothed, direct, slopePoint);
        }
        else
        {
            step *= PlayerStats.AirFrictionMultiplier;

            if (_wallJumpInputNerfPoint < 1 && (int)Mathf.Sign(xDir.x) == (int)Mathf.Sign(_wallDirectionForJump))
            {
                if (_timeSinceStart < _returnWallInputLossAfter) xDir.x = -_wallDirectionForJump;
                else xDir.x *= _wallJumpInputNerfPoint;
            }

            var targetX = Mathf.MoveTowards(_horizontalVelocityOnly.x, xDir.x * targetSpeed, step);
            newVelocity = new Vector2(targetX, _rigidbody.velocity.y);
        }

        SetVelocity((newVelocity + AdditionalFrameVelocities()) * _currentFrameSpeedModifier);

        Vector2 AdditionalFrameVelocities()
        {
            if (_instantMovementOverride.sqrMagnitude > SKIN_WIDTH)
            {
                _rigidbody.MovePosition(_positionThisFrame + _instantMovementOverride);
            }

            _totalImpulseLastFrame = _frameImpulseVelocity + _decayingExternalVelocity;
            return _totalImpulseLastFrame;
        }
    }

    #endregion

    #region Walls

    private const float WALL_REATTACH_COOLDOWN = 0.2f;

    private float _wallJumpInputNerfPoint;
    private int _wallDirectionForJump;
    private bool _isOnWall;
    private float _timeLeftWall;
    private float _currentWallSpeedVel;
    private float _canGrabWallAfter;
    private int _wallDirThisFrame;

    private bool HorizontalInputPressed => Mathf.Abs(_frameInput.Move.x) > PlayerStats.HorizontalDeadZoneThreshold;
    private bool IsPushingAgainstWall => HorizontalInputPressed && (int)Mathf.Sign(_moveDirectionThisFrame.x) == _wallDirThisFrame;

    private void CalculateWalls()
    {
        if (!PlayerStats.AllowWalls) return;

        var rayDir = _isOnWall ? WallDirection : _moveDirectionThisFrame.x;
        var hasHitWall = DetectWallCast(rayDir);

        _wallDirThisFrame = hasHitWall ? (int)rayDir : 0;

        if (!_isOnWall && ShouldStickToWall() && _timeSinceStart > _canGrabWallAfter && CurrentVelocity.y < 0) ToggleOnWall(true);
        else if (_isOnWall && !ShouldStickToWall()) ToggleOnWall(false);

        // If we're not grabbing a wall, let's check if we're against one for wall-jumping purposes
        if (!_isOnWall)
        {
            if (DetectWallCast(-1)) _wallDirThisFrame = -1;
            else if (DetectWallCast(1)) _wallDirThisFrame = 1;
        }

        bool ShouldStickToWall()
        {
            if (_wallDirThisFrame == 0 || _isGrounded) return false;

            if (HorizontalInputPressed && !IsPushingAgainstWall) return false; // If pushing away
            return !PlayerStats.RequireInputPush || (IsPushingAgainstWall);
        }
    }

    private bool DetectWallCast(float dir)
    {
        return Physics2D.BoxCast(_positionThisFrame + (Vector2)_wallDetectionBounds.center, new Vector2(_character.StandingColliderSize.x - SKIN_WIDTH, _wallDetectionBounds.size.y), 0, new Vector2(dir, 0), PlayerStats.WallDetectorRange,
            PlayerStats.ClimbableLayer);
    }

    private void ToggleOnWall(bool on)
    {
        _isOnWall = on;

        if (on)
        {
            _decayingExternalVelocity = Vector2.zero;
            _bufferedJumpUsable = true;
            _wallJumpCoyoteUsable = true;
            WallDirection = _wallDirThisFrame;
        }
        else
        {
            _timeLeftWall = _timeSinceStart;
            _canGrabWallAfter = _timeSinceStart + WALL_REATTACH_COOLDOWN;
            _rigidbody.gravityScale = GRAVITY_SCALE;
            WallDirection = 0;
            if (CurrentVelocity.y > 0)
            {
                AddForceThisFrame(new Vector2(0, PlayerStats.WallPopForce), true);
            }

            ResetAirJumps(); // so that we can air jump even if we didn't leave via a wall jump
        }

        OnWallGrabStateChanged?.Invoke(on);
    }

    #endregion

    #region Ladders

    private bool CanEnterLadder => _ladderHit && _timeSinceStart > _timeLeftLadder + PlayerStats.LadderCooldownTime;
    private bool ShouldMountLadder => PlayerStats.AutoAttachToLadders || _frameInput.Move.y > PlayerStats.VerticalDeadZoneThreshold || (!_isGrounded && _frameInput.Move.y < -PlayerStats.VerticalDeadZoneThreshold) || _frameInput.LadderHeld && CanEnterLadder;
    private bool ShouldDismountLadder => !PlayerStats.AutoAttachToLadders && _isGrounded && _frameInput.Move.y < -PlayerStats.VerticalDeadZoneThreshold;

    private float _timeLeftLadder;
    private Collider2D _ladderHit;
    private float _ladderSnapVelocity;

    private void CalculateLadders()
    {
        if (!PlayerStats.AllowLadders) return;

        Physics2D.queriesHitTriggers = true; // Ladders are set to Trigger
        _ladderHit = Physics2D.OverlapBox(_positionThisFrame + (Vector2)_wallDetectionBounds.center, _wallDetectionBounds.size, 0, PlayerStats.LadderLayer);

        Physics2D.queriesHitTriggers = _originalQueryStartInColliders;

        if (!IsClimbingLadder && CanEnterLadder && ShouldMountLadder) ToggleClimbingLadder(true);
        else if (IsClimbingLadder && (!_ladderHit || ShouldDismountLadder)) ToggleClimbingLadder(false);
    }

    private void ToggleClimbingLadder(bool on)
    {
        if (IsClimbingLadder == on) return;

        if (on)
        {
            SetVelocity(Vector2.zero);
            _rigidbody.gravityScale = 0;
            _ladderSnapVelocity = 0; // reset damping velocity for consistency
        }
        else
        {
            if (_ladderHit) _timeLeftLadder = _timeSinceStart;

            if (_frameInput.Move.y > 0)
            {
                var force = new Vector2(0, PlayerStats.LadderPopForce);
                Debug.Log($"[LADDER] Leaving ladder with upward pop force: {force}, current velocity: {CurrentVelocity}");
                AddForceThisFrame(force);
            }
            else
            {
                Debug.Log($"[LADDER] Leaving ladder with NO pop. Velocity retained: {CurrentVelocity}");
            }

            _rigidbody.gravityScale = GRAVITY_SCALE;
        }

        IsClimbingLadder = on;
        ResetAirJumps();
    }


    #endregion

    #region Jump

    private const float JUMP_CLEARANCE_TIME = 0.25f;
    private bool IsWithinJumpClearance => _lastJumpExecutedTime + JUMP_CLEARANCE_TIME > _timeSinceStart;
    private float _lastJumpExecutedTime;
    private bool _bufferedJumpUsable;
    private bool _jumpToConsume;
    private float _timeJumpWasPressed;
    private Vector2 _forceToApplyThisFrame;
    private bool _endedJumpEarly;
    private float _endedJumpForce;
    private int _airJumpsRemaining;
    private bool _wallJumpCoyoteUsable;
    private bool _coyoteUsable;
    private float _timeLeftGrounded;
    private float _returnWallInputLossAfter;

    private bool HasBufferedJump => _bufferedJumpUsable && _timeSinceStart < _timeJumpWasPressed + PlayerStats.BufferedJumpTime && !IsWithinJumpClearance;
    private bool CanUseCoyote => _coyoteUsable && !_isGrounded && _timeSinceStart < _timeLeftGrounded + PlayerStats.CoyoteTime;
    private bool CanAirJump => !_isGrounded && _airJumpsRemaining > 0;
    private bool CanWallJump => !_isGrounded && (_isOnWall || _wallDirThisFrame != 0) || (_wallJumpCoyoteUsable && _timeSinceStart < _timeLeftWall + PlayerStats.WallCoyoteTime);

    private void CalculateJump()
    {
        if ((_jumpToConsume || HasBufferedJump) && CanStand)
        {
            if (CanWallJump)
            {
                Debug.Log("Executing Wall Jump");
                ExecuteJump(JumpType.WallJump);
            }
            else if (_isGrounded || IsClimbingLadder)
            {
                Debug.Log("Executing Normal Jump (Grounded or Ladder)");
                ExecuteJump(JumpType.Jump);
            }
            else if (CanUseCoyote)
            {
                Debug.Log("Executing Coyote Jump");
                ExecuteJump(JumpType.Coyote);
            }
            else if (CanAirJump)
            {
                Debug.Log("Executing Air Jump");
                ExecuteJump(JumpType.AirJump);
            }

        }

        if ((!_endedJumpEarly && !_isGrounded && !_frameInput.JumpHeld && CurrentVelocity.y > 0) || CurrentVelocity.y < 0)
        {
            _endedJumpEarly = true;
        }

        if (_timeSinceStart > _returnWallInputLossAfter)
        {
            float prev = _wallJumpInputNerfPoint;
            _wallJumpInputNerfPoint = Mathf.MoveTowards(_wallJumpInputNerfPoint, 1, _deltaTime / PlayerStats.WallJumpInputLossReturnTime);
            if (_wallJumpInputNerfPoint != prev)
                Debug.Log($"Wall jump nerf point updated to {_wallJumpInputNerfPoint}");
        }
    }



    private void ExecuteJump(JumpType jumpType)
    {
        SetVelocity(_horizontalVelocityOnly);
        _endedJumpEarly = false;
        _bufferedJumpUsable = false;
        _lastJumpExecutedTime = _timeSinceStart;
        _stepDownExtension = 0;
        if (IsClimbingLadder) ToggleClimbingLadder(false);

        if (jumpType is JumpType.Jump or JumpType.Coyote)
        {
            _coyoteUsable = false;
            AddForceThisFrame(new Vector2(0, PlayerStats.JumpPower));
        }
        else if (jumpType is JumpType.AirJump)
        {
            _airJumpsRemaining--;
            AddForceThisFrame(new Vector2(0, PlayerStats.JumpPower));
        }
        else if (jumpType is JumpType.WallJump)
        {
            ToggleOnWall(false);

            _wallJumpCoyoteUsable = false;
            _wallJumpInputNerfPoint = 0;
            _returnWallInputLossAfter = _timeSinceStart + PlayerStats.WallJumpTotalInputLossTime;
            _wallDirectionForJump = _wallDirThisFrame;
            if (_isOnWall || IsPushingAgainstWall)
            {
                AddForceThisFrame(new Vector2(-_wallDirThisFrame, 1) * PlayerStats.WallJumpPower);
            }
            else
            {
                AddForceThisFrame(new Vector2(-_wallDirThisFrame, 1) * PlayerStats.WallPushPower);
            }
        }

        OnJumped?.Invoke(jumpType);
    }

    private void ResetAirJumps() => _airJumpsRemaining = PlayerStats.MaxAirJumps;

    #endregion

    #region Dash

    private bool _dashToConsume;
    private bool _canDash;
    private Vector2 _dashVelocity;
    private bool _dashing;
    private float _startedDashing;
    private float _nextDashTime;

    private void CalculateDash()
    {
        if (!PlayerStats.AllowDash) return;

        if (_dashToConsume && _canDash && !IsCrouching && _timeSinceStart > _nextDashTime)
        {
            var dir = new Vector2(_frameInput.Move.x, Mathf.Max(_frameInput.Move.y, 0f)).normalized;
            if (dir == Vector2.zero) return;

            _dashVelocity = dir * PlayerStats.DashVelocity;
            _dashing = true;
            _canDash = false;
            _startedDashing = _timeSinceStart;
            _nextDashTime = _timeSinceStart + PlayerStats.DashCooldown;
            OnDashStateChanged?.Invoke(true, dir);
        }

        if (_dashing)
        {
            if (_timeSinceStart > _startedDashing + PlayerStats.DashDuration)
            {
                _dashing = false;
                OnDashStateChanged?.Invoke(false, Vector2.zero);

                SetVelocity(new Vector2(CurrentVelocity.x * PlayerStats.DashEndHorizontalMultiplier, CurrentVelocity.y));
                if (_isGrounded) _canDash = true;
            }
        }
    }

    #endregion

    #region Crouching

    private float _timeStartedCrouching;
    private bool CrouchPressed => _frameInput.Move.y < -PlayerStats.VerticalDeadZoneThreshold;

    private bool CanStand => IsStandingPosClear(_rigidbody.position + _character.StandingColliderCenter);
    private bool IsStandingPosClear(Vector2 pos) => CheckPos(pos, _character.StandingColliderSize - SKIN_WIDTH * Vector2.one);

    // We handle crouch AFTER frame movements are done to avoid transient velocity issues
    private void CalculateCrouch()
    {
        if (!PlayerStats.AllowCrouching) return;

        if (!IsCrouching && CrouchPressed && _isGrounded) ToggleCrouching(true);
        else if (IsCrouching && (!CrouchPressed || !_isGrounded)) ToggleCrouching(false);
    }

    private void ToggleCrouching(bool shouldCrouch)
    {
        if (shouldCrouch)
        {
            _timeStartedCrouching = _timeSinceStart;
            IsCrouching = true;
        }
        else
        {
            if (!CanStand) return;
            IsCrouching = false;
        }

        SetColliderMode(IsCrouching ? ColliderMode.Crouching : ColliderMode.Standard);
    }

    private bool CheckPos(Vector2 pos, Vector2 size)
    {
        Physics2D.queriesHitTriggers = false;
        var hit = Physics2D.OverlapBox(pos, size, 0, PlayerStats.CollisionLayers);
        //var hit = Physics2D.OverlapCapsule(pos, size - new Vector2(SKIN_WIDTH, 0), _collider.direction, 0, ~Stats.PlayerLayer);
        Physics2D.queriesHitTriggers = _originalQueryStartInColliders;
        return !hit;
    }

    #endregion

    #region External Triggers

    private const int MAX_ACTIVE_MOVERS = 5;
    private readonly HashSet<IPhysicsMover> _activatedMovers = new(MAX_ACTIVE_MOVERS);
    private readonly HashSet<ISpeedModifier> _modifiers = new();
    private Vector2 _frameSpeedModifierVelocity;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent(out ISpeedModifier modifier)) _modifiers.Add(modifier);
        else if (other.TryGetComponent(out IPhysicsMover mover) && !mover.RequireGrounding) _activatedMovers.Add(mover);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent(out ISpeedModifier modifier)) _modifiers.Remove(modifier);
        else if (other.TryGetComponent(out IPhysicsMover mover)) _activatedMovers.Remove(mover);
    }

    private void CalculateExternalModifiers()
    {
        _frameSpeedModifier = Vector2.one;
        foreach (var modifier in _modifiers)
        {
            if ((modifier.OnGround && _isGrounded) || (modifier.InAir && !_isGrounded))
                _frameSpeedModifier += modifier.Modifier;
        }

        _currentFrameSpeedModifier = Vector2.SmoothDamp(_currentFrameSpeedModifier, _frameSpeedModifier, ref _frameSpeedModifierVelocity, 0.1f);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;

        var pos = (Vector2)transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(pos + Vector2.up * _character.Height / 2, new Vector3(_character.Width, _character.Height));
        Gizmos.color = Color.magenta;

        var rayStart = pos + Vector2.up * _character.StepHeight;
        var rayDir = Vector3.down * _character.StepHeight;
        Gizmos.DrawRay(rayStart, rayDir);
        foreach (var offset in GenerateGroundRayOffsets())
        {
            Gizmos.DrawRay(rayStart + Vector2.right * offset, rayDir);
            Gizmos.DrawRay(rayStart + Vector2.left * offset, rayDir);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(pos + (Vector2)_wallDetectionBounds.center, _wallDetectionBounds.size);


        Gizmos.color = Color.black;
        Gizmos.DrawRay(GroundRayStartPoint, Vector3.right);
    }

    #endregion
}


#region Enums, Structs, Interfaces

public interface IPlayerController
{
    public PlayerStats PlayerStats { get; }
    public ControllerState CurrentState { get; }
    public event Action<JumpType> OnJumped;
    public event Action<bool, float> OnGroundedStateChanged;
    public event Action<bool, Vector2> OnDashStateChanged;
    public event Action<bool> OnWallGrabStateChanged;
    public event Action<Vector2> OnRepositioned;
    public event Action<bool> OnPlayerToggled;

    public bool IsControllerActive { get; }
    public Vector2 UpDirection { get; }
    public bool IsCrouching { get; }
    public Vector2 MoveInput { get; }
    public Vector2 GroundSurfaceNormal { get; }
    public Vector2 CurrentVelocity { get; }
    public int WallDirection { get; }
    public bool IsClimbingLadder { get; }

    // External force
    public void AddForceThisFrame(Vector2 force, bool resetVelocity = false);

    // Utility
    public void LoadCharacterState(ControllerState state);
    public void RepositionInstantly(Vector2 position, bool resetVelocity = false);
    public void SetPlayerActive(bool on);
}

public enum JumpType
{
    Jump,
    Coyote,
    AirJump,
    WallJump
}

public struct ControllerState
{
    public Vector2 Position;
    public float Rotation;
    public Vector2 Velocity;
    public bool Grounded;
}
public interface ISpeedModifier
{
    public bool InAir { get; }
    public bool OnGround { get; }
    public Vector2 Modifier { get; }
    #endregion
}

