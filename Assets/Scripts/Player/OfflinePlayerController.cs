using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class GamePlayerControllerOffline : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;

    private Rigidbody2D _rb;
    private GameplayInputActions _input;

    private Vector2 _moveInput;
    private bool _jumpToConsume;
    private float _timeJumpPressed;
    private bool _jumpHeld;
    private bool _endedJumpEarly;

    private bool _isGrounded;
    private int _groundContacts = 0;
    private float _timeLeftGrounded;
    private bool _coyoteUsable;
    private int _airJumpsRemaining;

    private const float JUMP_BUFFER_TIME = 0.15f;

    private void OnEnable() { }

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();

        if (stats == null)
            Debug.LogError($"[PlayerController] Stats is NULL on {gameObject.name}!");
        else
            Debug.Log($"[PlayerController] Stats loaded: {stats.name} on {gameObject.name}");

        _input = new GameplayInputActions();
        _input.Enable();
    }

    private void OnDisable()
    {
        _input?.Disable();
    }

    private void Update()
    {
        _moveInput = _input.Player.Move.ReadValue<Vector2>();

        if (_input.Player.Jump.triggered)
        {
            _jumpToConsume = true;
            _timeJumpPressed = Time.time;
            Debug.Log($"[{gameObject.name}] Jump input triggered");
        }

        _jumpHeld = _input.Player.Jump.ReadValue<float>() > 0.1f;
    }

    private void FixedUpdate()
    {
        Move();

        bool hasBufferedJump = _jumpToConsume && Time.time < _timeJumpPressed + JUMP_BUFFER_TIME;
        bool canUseCoyote = _coyoteUsable && !_isGrounded && Time.time < _timeLeftGrounded + stats.CoyoteTime;
        bool canAirJump = !_isGrounded && stats.AllowAirJumps && _airJumpsRemaining > 0;

        if (hasBufferedJump && (_isGrounded || canUseCoyote || canAirJump))
        {
            if (canAirJump)
            {
                _airJumpsRemaining--;
                Debug.Log($"[{gameObject.name}] Executing AIR jump. Air jumps remaining: {_airJumpsRemaining}");
                ExecuteJump();
            }
            else if (_isGrounded)
            {
                Debug.Log($"[{gameObject.name}] Executing GROUNDED jump");
                ExecuteJump();
            }
            else if (canUseCoyote)
            {
                _coyoteUsable = false;
                Debug.Log($"[{gameObject.name}] Executing COYOTE jump");
                ExecuteJump();
            }

            _jumpToConsume = false;
        }

        if (!_jumpHeld && !_isGrounded && _rb.velocity.y > 0 && !_endedJumpEarly)
        {
            _rb.velocity = new Vector2(_rb.velocity.x, _rb.velocity.y * 0.5f);
            _endedJumpEarly = true;
            Debug.Log($"[{gameObject.name}] Early jump release - cutting jump short");
        }

        if (!_isGrounded)
            _coyoteUsable = true;
    }

    private void Move()
    {
        float targetSpeed = _moveInput.x * stats.BaseSpeed;
        float speedDif = targetSpeed - _rb.velocity.x;

        float accelRate = _isGrounded ? stats.Acceleration : stats.AirAcceleration;
        _rb.AddForce(Vector2.right * (accelRate * speedDif));

        if (!_isGrounded && Mathf.Abs(_rb.velocity.x) > stats.MaxAirSpeed)
        {
            _rb.velocity = new Vector2(Mathf.Sign(_rb.velocity.x) * stats.MaxAirSpeed, _rb.velocity.y);
        }
    }

    private void ExecuteJump()
    {
        _rb.velocity = new Vector2(_rb.velocity.x, 0);
        _rb.AddForce(Vector2.up * stats.JumpPower, ForceMode2D.Impulse);
        _endedJumpEarly = false;
        _isGrounded = false;
        _coyoteUsable = false;
    }

    private void UpdateGrounded(bool grounded)
    {
        if (grounded && !_isGrounded)
        {
            _isGrounded = true;
            _coyoteUsable = true;
            _endedJumpEarly = false;
            _airJumpsRemaining = stats.AllowAirJumps ? stats.MaxAirJumps : 0;
            Debug.Log($"[{gameObject.name}] Landed - Reset air jumps");
        }
        else if (!grounded && _isGrounded)
        {
            _isGrounded = false;
            _timeLeftGrounded = Time.time;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & stats.CollisionLayers) != 0)
        {
            _groundContacts++;
            UpdateGrounded(true);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (((1 << collision.gameObject.layer) & stats.CollisionLayers) != 0)
        {
            _groundContacts = Mathf.Max(0, _groundContacts - 1);
            if (_groundContacts == 0) UpdateGrounded(false);
        }
    }
}
