using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[DisallowMultipleComponent]
public sealed class PlayerMovement : MonoBehaviour
{
    private enum MovementState
    {
        Free, Rails, Off
    }

    [Header("Movement")]
    [SerializeField, Min(0f)] private float _moveSpeed = 5f;    
    [SerializeField, Min(0f)] private float _acceleration = 30f;
    [SerializeField] public bool movementEnabled;
    [SerializeField] MovementState _movementState;

    [Header("Facing / Rotation")]
    [SerializeField] private bool _faceMoveDirection = true;      // rotate to face velocity
    [SerializeField, Min(0f)] private float _rotateSpeedDegPerSec = 720f;
    [SerializeField] private float _facingAngleOffset = -90f;       // e.g., -90 if sprite faces up

    private Rigidbody2D _rb;
    private PlayerInput _playerInput;
    private InputAction _moveAction;

    private float moveFreeTime = 0.0f;

    private Vector2 _move;        // input vector (x,y)
    private Vector2 _finalMove;   // actual movement direction

    public void IncreaseMoveSpeed(float amount) => _moveSpeed += amount;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _playerInput = GetComponent<PlayerInput>();

        if(_playerInput == null)
            Debug.LogError($"ERROR: Player {this.gameObject.name} does not have an input controller set!");

        _moveAction = _playerInput.actions.FindAction("Move", throwIfNotFound: false);
        if (_moveAction == null)
            Debug.LogWarning($"{nameof(PlayerMovement)}: Could not find an InputAction named 'Move' in the PlayerInput actions.", this);

        // Top-down RB2D setup
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _rb.gravityScale = 0f;  
        _rb.freezeRotation = false;

        _rb.bodyType = RigidbodyType2D.Kinematic;
        //movementEnabled = false;
        _movementState = MovementState.Off;
    }
    public void EnableMovementNow()
    {
        _rb.bodyType = RigidbodyType2D.Dynamic;
        //movementEnabled = true;
        _movementState = MovementState.Free;
    }
    public void DisableMovementNow()
    {
        //movementEnabled = false;
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
        _movementState = MovementState.Off;
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

    public Vector2 GetInputDir()
    {
        return _move;
    }

    public void Dash(Vector2 dirMagnitude)
    {
        if (_movementState != MovementState.Free) return;

        _movementState = MovementState.Rails;
        _finalMove = dirMagnitude;
        moveFreeTime = Time.time + 0.25f;
    }

    private void FixedUpdate()
    {
        if (_movementState == MovementState.Off) return;

        if (_movementState == MovementState.Rails)
        {
            if (Time.time >= moveFreeTime)
            {
                _movementState = MovementState.Free;
                _finalMove = _move;
            }
        } else _finalMove = _move;

        float dt = Time.fixedDeltaTime;
        Vector2 v = _rb.linearVelocity;

        // Desired velocity is world-relative, no camera involvement
        Vector2 desiredVel = _finalMove * _moveSpeed;

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
