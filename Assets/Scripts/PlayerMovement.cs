using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
[DisallowMultipleComponent]
public sealed class PlayerMovement : MonoBehaviour, IAsyncStep
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float _moveSpeed = 6f;          // target ground speed
    [SerializeField, Min(0f)] private float _acceleration = 30f;      // how fast we reach target speed
    [SerializeField, Range(0f, 1f)] private float _airControl = 0.4f; // % of accel allowed in air
    [SerializeField] public bool movementEnabled; // exposed for debugging

    [Header("Rotation")]
    [SerializeField] private bool _faceMoveDirection = true;
    [SerializeField, Min(0f)] private float _rotateSpeedDegPerSec = 720f;

    [Header("Camera Relative")]
    [SerializeField] private bool _cameraRelative = true;
    [SerializeField] private Transform _cameraTransform; // optional; falls back to Camera.main

    [Header("Grounding & Drag")]
    [SerializeField] private LayerMask _groundMask = ~0;
    [SerializeField, Min(0f)] private float _groundCheckDistance = 0.25f;
    [SerializeField] private Vector3 _groundCheckOffset = new(0f, 0.1f, 0f);
    [SerializeField, Min(0f)] private float _groundDrag = 4f;
    [SerializeField, Min(0f)] private float _airDrag = 0.1f;

    private Rigidbody _rb;
    private PlayerInput _playerInput;
    private InputAction _moveAction;

    private Vector2 _move;
    private bool _grounded;
    private bool _initialized;
    private Transform _cachedCam;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _playerInput = GetComponent<PlayerInput>();

        // Try to find a "Move" action safely (no throw)
        _moveAction = _playerInput.actions?.FindAction("Move", throwIfNotFound: false);
        if (_moveAction == null)
        {
            Debug.LogWarning($"{nameof(PlayerMovement)}: Could not find an InputAction named 'Move' in the PlayerInput actions.", this);
        }

        // Rigidbody best practices for a character-like body
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Cache camera if provided; otherwise we’ll try Camera.main at runtime
        _cachedCam = _cameraTransform != null ? _cameraTransform : Camera.main?.transform;
    }

    /// <summary>
    /// Prepare the component. Subscribes to game ready, disables physics until then.
    /// </summary>
    public async Task SetupAsync(CancellationToken ct)
    {
        if (_initialized) return;
        _initialized = true;

        // Don’t let physics kick in until the world is ready
        _rb.isKinematic = true;
        movementEnabled = false;

        if (GameInitializer.singleton != null)
        {
            GameInitializer.singleton.Ready += HandleReady;
        }
        else
        {
            // If there’s no GameInitializer, enable immediately so this works in isolation.
            EnableMovementNow();
        }

        // give one frame for other systems to wire up
        if (!ct.IsCancellationRequested)
            await Awaitable.NextFrameAsync(ct);
    }

    private void HandleReady()
    {
        EnableMovementNow();

        if (GameInitializer.singleton != null)
            GameInitializer.singleton.Ready -= HandleReady;
    }

    private void EnableMovementNow()
    {
        _rb.isKinematic = false;
        movementEnabled = true;
    }

    private void OnEnable()
    {
        if (_moveAction != null)
        {
            _moveAction.performed += OnMove;
            _moveAction.canceled += OnMove;
            // Make sure it’s enabled (PlayerInput usually handles this, but safe to ensure)
            if (!_moveAction.enabled) _moveAction.Enable();
        }

        // If camera wasn’t available earlier, try again now
        if (_cachedCam == null) _cachedCam = _cameraTransform != null ? _cameraTransform : Camera.main?.transform;
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
        _move = ctx.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        if (!movementEnabled) return;

        // Ground check
        var origin = transform.position + _groundCheckOffset;
        _grounded = Physics.Raycast(origin, Vector3.down, _groundCheckDistance, _groundMask, QueryTriggerInteraction.Ignore);

        // Choose frame of reference
        Vector3 fwd, right;
        if (_cameraRelative)
        {
            // Recover camera if it went missing (scene swaps, etc.)
            if (_cachedCam == null) _cachedCam = _cameraTransform != null ? _cameraTransform : Camera.main?.transform;

            if (_cachedCam != null)
            {
                fwd = Vector3.ProjectOnPlane(_cachedCam.forward, Vector3.up).normalized;
                right = Vector3.Cross(Vector3.up, fwd);
            }
            else
            {
                fwd = Vector3.forward; right = Vector3.right;
            }
        }
        else
        {
            fwd = Vector3.forward; right = Vector3.right;
        }

        // Desired horizontal velocity from left stick
        Vector3 desiredVel = (right * _move.x + fwd * _move.y) * _moveSpeed;

        // Current velocities
        float dt = Time.fixedDeltaTime;
        Vector3 v = _rb.linearVelocity;
        Vector3 vH = new(v.x, 0f, v.z);

        // Accel budget (less in air)
        float accel = _acceleration * (_grounded ? 1f : _airControl);

        // Move our horizontal velocity toward the target
        Vector3 targetH = Vector3.MoveTowards(vH, desiredVel, accel * dt);

        // Apply the acceleration needed to reach targetH this frame
        Vector3 neededAccel = (targetH - vH) / Mathf.Max(dt, 0.0001f);
        _rb.AddForce(neededAccel, ForceMode.Acceleration);

        // Drag helps snappy stops (ground vs air)
        _rb.linearDamping = _grounded ? _groundDrag : _airDrag;

        // Preserve vertical velocity (gravity/jumps handled elsewhere)
        Vector3 cur = _rb.linearVelocity;
        _rb.linearVelocity = new Vector3(cur.x, v.y, cur.z);

        // Face movement direction (optional)
        if (_faceMoveDirection && desiredVel.sqrMagnitude > 0.0004f)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredVel, Vector3.up);
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetRot, _rotateSpeedDegPerSec * dt));
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _grounded ? Color.green : Color.red;
        var origin = transform.position + _groundCheckOffset;
        Gizmos.DrawLine(origin, origin + Vector3.down * _groundCheckDistance);
    }
#endif
}
