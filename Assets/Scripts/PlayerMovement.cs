using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[DisallowMultipleComponent]
public sealed class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float _moveSpeed = 5f;    
    [SerializeField, Min(0f)] private float _acceleration = 30f;
    [SerializeField] public bool movementEnabled;  

    [Header("Facing / Rotation")]
    [SerializeField] private bool _faceMoveDirection = true;      // rotate to face velocity
    [SerializeField, Min(0f)] private float _rotateSpeedDegPerSec = 720f;
    [SerializeField] private float _facingAngleOffset = -90f;       // e.g., -90 if sprite faces up

    private Rigidbody2D _rb;
    private PlayerInput _playerInput;
    private InputAction _moveAction;

    private Vector2 _move;        // input vector (x,y)

    public void IncreaseMoveSpeed(float amount) => _moveSpeed += amount;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _playerInput = GetComponent<PlayerInput>();

        _moveAction = _playerInput.actions?.FindAction("Move", throwIfNotFound: false);
        if (_moveAction == null)
            Debug.LogWarning($"{nameof(PlayerMovement)}: Could not find an InputAction named 'Move' in the PlayerInput actions.", this);

        // Top-down RB2D setup
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rb.gravityScale = 0f;  
        _rb.freezeRotation = false;

        _rb.bodyType = RigidbodyType2D.Kinematic;
        movementEnabled = false;
    }
    public void EnableMovementNow()
    {
        _rb.bodyType = RigidbodyType2D.Dynamic;
        movementEnabled = true;
    }
    public void DisableMovementNow()
    {
        movementEnabled = false;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
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

        // Face movement direction
        if (_faceMoveDirection && desiredVel.sqrMagnitude > 0.0004f)
        {
            float targetAngle = Mathf.Atan2(desiredVel.y, desiredVel.x) * Mathf.Rad2Deg + _facingAngleOffset;
            float newAngle = Mathf.MoveTowardsAngle(_rb.rotation, targetAngle, _rotateSpeedDegPerSec * dt);
            _rb.MoveRotation(newAngle);
        }
    }
}
