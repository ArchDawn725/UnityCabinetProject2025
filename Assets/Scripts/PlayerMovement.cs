using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[DisallowMultipleComponent]
public sealed class PlayerMovement : MonoBehaviour, IAsyncStep
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float _moveSpeed = 6f;     // target speed (X/Y)
    [SerializeField, Min(0f)] private float _acceleration = 30f; // how fast we reach target speed
    [SerializeField] public bool movementEnabled;                 // exposed for debugging

    [Header("Facing / Rotation")]
    [SerializeField] private bool _faceMoveDirection = true;      // rotate to face velocity
    [SerializeField, Min(0f)] private float _rotateSpeedDegPerSec = 720f;
    [SerializeField] private float _facingAngleOffset = 0f;       // e.g., -90 if sprite faces up

    private Rigidbody2D _rb;
    private PlayerInput _playerInput;
    private InputAction _moveAction;

    private Vector2 _move;        // input vector (x,y)
    private bool _initialized;

    public float GetMoveSpeed() => _moveSpeed;
    public void SetMoveSpeed(float v) => _moveSpeed = Mathf.Max(0f, v);

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _playerInput = GetComponent<PlayerInput>();

        _moveAction = _playerInput.actions?.FindAction("Move", throwIfNotFound: false);
        if (_moveAction == null)
            Debug.LogWarning($"{nameof(PlayerMovement)}: Could not find an InputAction named 'Move' in the PlayerInput actions.", this);

        // Top-down RB2D setup
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rb.gravityScale = 0f;       // no gravity in top-down
        _rb.freezeRotation = false;  // we rotate to face move dir
        // (No drag/damping control here by request)

        StartScreenTest.Singleton?.players.Add(this);
    }

    public async Task SetupAsync(CancellationToken ct, Initializer initializer)
    {
        // kept for API parity; no-op
    }

    /// <summary>Prepare the component. Subscribes to game ready, disables physics until then.</summary>
    public async Task SetupAsync(CancellationToken ct)
    {
        if (_initialized) return;
        _initialized = true;

        _rb.bodyType = RigidbodyType2D.Kinematic; // hold until world ready
        movementEnabled = false;

        if (GameInitializer.singleton != null)
            GameInitializer.singleton.Ready += HandleReady;
        else
            EnableMovementNow();

        if (!ct.IsCancellationRequested)
            await Awaitable.NextFrameAsync(ct);
    }

    public void Setup()
    {
        if (_initialized) return;
        _initialized = true;

        _rb.bodyType = RigidbodyType2D.Kinematic;
        movementEnabled = false;

        if (GameInitializer.singleton != null)
            GameInitializer.singleton.Ready += HandleReady;
        else
            EnableMovementNow();
    }

    private void HandleReady()
    {
        EnableMovementNow();
        if (GameInitializer.singleton != null)
            GameInitializer.singleton.Ready -= HandleReady;
    }

    private void EnableMovementNow()
    {
        _rb.bodyType = RigidbodyType2D.Dynamic;
        movementEnabled = true;
    }

    private void OnEnable()
    {
        if (_moveAction != null)
        {
            _moveAction.performed += OnMove;
            _moveAction.canceled += OnMove;
            if (!_moveAction.enabled) _moveAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (_moveAction != null)
        {
            _moveAction.performed -= OnMove;
            _moveAction.canceled -= OnMove;
        }

        if (GameInitializer.singleton != null)
            GameInitializer.singleton.Ready -= HandleReady;
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        _move = ctx.ReadValue<Vector2>(); // [-1,1] per axis typically
    }

    private void FixedUpdate()
    {
        if (!movementEnabled) return;

        float dt = Time.fixedDeltaTime;
        Vector2 v = _rb.linearVelocity;

        // Desired velocity is world-relative, no camera involvement
        Vector2 desiredVel = _move * _moveSpeed;

        // Move our velocity toward the target with an acceleration budget
        Vector2 target = Vector2.MoveTowards(v, desiredVel, _acceleration * dt);

        // Apply acceleration to reach 'target' this frame (no damping)
        Vector2 neededA = (target - v) / Mathf.Max(dt, 0.0001f);
        _rb.AddForce(neededA, ForceMode2D.Force);

        // Face movement direction (optional)
        if (_faceMoveDirection && desiredVel.sqrMagnitude > 0.0004f)
        {
            float targetAngle = Mathf.Atan2(desiredVel.y, desiredVel.x) * Mathf.Rad2Deg + _facingAngleOffset;
            float newAngle = Mathf.MoveTowardsAngle(_rb.rotation, targetAngle, _rotateSpeedDegPerSec * dt);
            _rb.MoveRotation(newAngle);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 p = transform.position;
        Gizmos.DrawLine(p + Vector3.left * 0.25f, p + Vector3.right * 0.25f);
        Gizmos.DrawLine(p + Vector3.down * 0.25f, p + Vector3.up * 0.25f);
    }
#endif
}
