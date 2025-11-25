using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System;
using static UnityEditor.Experimental.GraphView.GraphView;
using UnityEditor.EditorTools;
using Unity.VisualScripting;

public class PlayerAttackAndTarget : MonoBehaviour
{
    private enum TargettingType { Closest, Farthest, MostHP, LeastHP, Strongest, Weakest, BiggestGroup }

    /// <summary>
    /// Time (in seconds) between consecutive basic attacks.
    /// </summary>
    [Header("Attack Settings")]
    [SerializeField, Min(0.1f)] private float basicAttackInterval = 0.5f;
    private float nextBasicAttackTimer = 0.0f;
    /// <summary>
    /// Allows basic attacks to fire automatically if input is held down when true, otherwise if false the input must be pressed again to attack again.
    /// </summary>
    [SerializeField] private bool basicAttackAutomaticFire = true;
    /// <summary>
    /// Time (in seconds) after using Class Ability A until you can use it again.
    /// </summary>
    [SerializeField] private float classAbilityACooldownTime = 15.0f;
    private float nextClassAbilityATimer = 0.0f;
    /// <summary>
    /// Time (in seconds) after using Class Ability B until you can use it again.
    /// </summary>
    [SerializeField] private float classAbilityBCooldownTime = 45.0f;
    private float nextClassAbilityBTimer = 0.0f;

    /// <summary>
    /// Type of targetting system to use.
    /// </summary>
    [Header("Targetting Settings")]
    [SerializeField] private TargettingType targettingType = TargettingType.Closest;
    /// <summary>
    /// Radius around player in which the targetting system detects enemies.
    /// </summary>
    [SerializeField, Min(0.1f)] private float detectionRadius = 10.0f;
    private string enemyTag = "Enemy";

    private List<Collider2D> targets = new();

    private IPlayer player;
    private PlayerInput playerInput;
    private InputAction attackAction;
    private InputAction classAbilityAAction;
    private InputAction classAbilityBAction;

    //private PoolManager poolManager;

    [SerializeField] private CircleCollider2D _trigger;

    private void Awake()
    {
        player = GetComponent<IPlayer>();

        // Set up input
        playerInput = GetComponent<PlayerInput>();
        if(playerInput == null)
        {
            Debug.LogError($"{nameof(PlayerAttackAndTarget)} ERROR: Player {this.gameObject.name}; attack/targetting script failed to grab input controller!");
            return;
        }
        attackAction = playerInput.actions.FindAction("Attack", throwIfNotFound: false);
        if (attackAction == null) Debug.LogError($"{nameof(PlayerAttackAndTarget)} ERROR: Attack action is null on {this.gameObject.name}");
        classAbilityAAction = playerInput.actions.FindAction("ClassAbilityA", throwIfNotFound: false);
        if (classAbilityAAction == null) Debug.LogError($"{nameof(PlayerAttackAndTarget)} ERROR: ClassAbilityA action is null on {this.gameObject.name}");
        classAbilityBAction = playerInput.actions.FindAction("ClassAbilityB", throwIfNotFound: false);
        if (classAbilityBAction == null) Debug.LogError($"{nameof(PlayerAttackAndTarget)} ERROR: ClassAbilityB action is null on {this.gameObject.name}");

        // Set up targetting
        if(_trigger == null) _trigger = GetComponent<CircleCollider2D>();
        _trigger.isTrigger = true;
        _trigger.radius = detectionRadius;
    }

    private void OnEnable()
    {
        //poolManager = FindAnyObjectByType<PoolManager>();
        
        if(classAbilityAAction != null)
        {
            Debug.Log($"{this.gameObject.name}: AttachA");
            classAbilityAAction.performed += UseClassAbilityA;
            if (!classAbilityAAction.enabled) classAbilityAAction.Enable();
        }

        if(classAbilityBAction != null)
        {
            Debug.Log($"{this.gameObject.name}: AttachB");
            classAbilityBAction.performed += UseClassAbilityB;
            if (!classAbilityBAction.enabled) classAbilityBAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (classAbilityAAction != null)
        {
            classAbilityAAction.performed -= UseClassAbilityA;
        }

        if (classAbilityBAction != null)
        {
            classAbilityBAction.performed -= UseClassAbilityB;
        }
    }

    private void Update()
    {
        // Decrease timers
        if (nextBasicAttackTimer > 0.0f) nextBasicAttackTimer -= Time.deltaTime;
        if (nextClassAbilityATimer > 0.0f) nextClassAbilityATimer -= Time.deltaTime;
        if (nextClassAbilityBTimer > 0.0f) nextClassAbilityBTimer -= Time.deltaTime;

        // Basic attack check
        if (nextBasicAttackTimer <= 0.0f)
        {
            if(
                (basicAttackAutomaticFire == true && attackAction.IsPressed()) ||
                (basicAttackAutomaticFire == false && attackAction.triggered)
                )
            {
                player.BasicAttack();
                nextBasicAttackTimer = basicAttackInterval;
            }
        }
    }

    private void UseClassAbilityA(InputAction.CallbackContext ctx)
    {
        if (classAbilityAAction == null)
        { Debug.LogError($"ERROR: {this.gameObject.name} class ability A input is null!"); return; }

        if (nextClassAbilityATimer > 0.0f)
        { Debug.LogAssertion($"{this.gameObject.name} Can't use Class A; cooldown {nextClassAbilityATimer} s"); return; }

        nextClassAbilityATimer = classAbilityACooldownTime;

        player.ClassAbilityA();
    }

    private void UseClassAbilityB(InputAction.CallbackContext ctx)
    {
        if (classAbilityBAction == null)
        { Debug.LogError($"ERROR: {this.gameObject.name} class ability B input is null!"); return; }

        if (nextClassAbilityBTimer > 0.0f)
        { Debug.LogAssertion($"{this.gameObject.name} Can't use Class B; cooldown {nextClassAbilityBTimer} s"); return; }

        nextClassAbilityBTimer = classAbilityBCooldownTime;

        player.ClassAbilityB();
    }

    public void IncrementBasicAttackInterval(float value)
    {
        if (value < 0.0f && basicAttackInterval - value < 0.1f) return;
        basicAttackInterval += value;
    }

    public void ResetBasicAttackTimer()
    {
        nextBasicAttackTimer = 0.0f;
    }

    public float GetBasicAttackInterval()
    {
        return basicAttackInterval;
    }

    public void SetBasicAttackInterval(float value)
    {
        basicAttackInterval = value;
    }

    public Vector3 GetAimPoint(Collider2D col)
    {
        // 1) Try Health anchor on this object or its parents (handles multi-collider rigs)
        var health = col.GetComponentInParent<Health>();

        // 2) Fallback: “chest” from combined bounds across all colliders on the same rigidbody2D
        Bounds b = col.bounds;
        var rb = col.attachedRigidbody;
        if (rb)
        {
            var cols = rb.GetComponentsInChildren<Collider2D>();
            if (cols.Length > 0)
            {
                b = cols[0].bounds;
                for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            }
        }

        float y = Mathf.Lerp(b.min.y, b.max.y, Mathf.Clamp01(0.65f));
        return new Vector3(b.center.x, y, transform.position.z); // z stays on shooter’s plane
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other || !other.gameObject.CompareTag(enemyTag)) return;

        if (!targets.Contains(other))
        {
            targets.Add(other);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other || !other.gameObject.CompareTag(enemyTag)) return;
        targets.Remove(other);
    }

    public Collider2D GetClosestTarget()
    {
        Collider2D best = null;
        float bestSqr = float.PositiveInfinity;
        Vector3 origin = transform.position;

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            var col = targets[i];
            if (!IsValid(col)) { targets.RemoveAt(i); continue; }

            float d2 = ((Vector2)col.bounds.center - (Vector2)origin).sqrMagnitude;
            if (d2 < bestSqr) { bestSqr = d2; best = col; }
        }
        return best;
    }

    public bool IsValid(Collider2D col) =>
        col && col.gameObject.activeInHierarchy;
}
