#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Player horizontal movement and jump for the greybox (Docs/01_Combat_Movement.md §1, §2).
    /// Tunables are authored in PIXELS (PPU = 16) to match the design spec, then converted
    /// to Unity units at runtime. Jump gravity/velocity are DERIVED from apex height + time,
    /// never hardcoded. Input is read in Update, physics is applied in FixedUpdate.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerInputHub))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        // Target-speed magnitude below which we decelerate instead of accelerating.
        private const float MoveInputEpsilon = 0.01f;

        [Header("Movement (horizontal) — pixels")]
        [Tooltip("Peak run speed (px/s). 200 px/s = 12.5 u/s.")]
        [SerializeField] private float runSpeedMax = 200f;
        [Tooltip("Time from 0 -> max on the ground (s).")]
        [SerializeField] private float groundAccelTime = 0.12f;
        [Tooltip("Time from max -> 0 on the ground (s).")]
        [SerializeField] private float groundDecelTime = 0.08f;
        [Tooltip("Accel/decel scale while airborne.")]
        [SerializeField, Range(0f, 1f)] private float airControl = 0.65f;

        [Header("Jump — pixels")]
        [Tooltip("Max jump height (px). 64 px = 4 u (~2.7x player height; 96 px felt too high in playtest).")]
        [SerializeField] private float jumpApexHeight = 64f;
        [Tooltip("Time to reach the apex (s). Scaled down with the height to keep the rise snappy.")]
        [SerializeField] private float jumpTimeToApex = 0.30f;
        [Tooltip("Gravity multiplier while falling (asymmetric fall).")]
        [SerializeField] private float fallGravityMult = 1.8f;
        [Tooltip("Upward velocity kept when Jump is released early.")]
        [SerializeField, Range(0f, 1f)] private float jumpCutMult = 0.45f;
        [Tooltip("Terminal fall speed (px/s).")]
        [SerializeField] private float maxFallSpeed = 900f;

        [Header("Assist windows — seconds")]
        [Tooltip("Grace period to still jump after leaving a ledge (s).")]
        [SerializeField] private float coyoteTime = 0.10f;
        [Tooltip("Jump input buffered before landing (s).")]
        [SerializeField] private float jumpBuffer = 0.12f;

        [Header("References")]
        [Tooltip("Ground sensor. Auto-found on this GameObject if left empty.")]
        [SerializeField] private GroundSensor? groundSensor;

        // --- Derived values (Unity units), recomputed in Awake/OnValidate. ---
        private float _runSpeedU;      // u/s
        private float _maxFallU;       // u/s
        private float _accelU;         // u/s^2
        private float _decelU;         // u/s^2
        private float _gravity;        // u/s^2 (positive magnitude)
        private float _jumpVelocity;   // u/s

        // --- Runtime state. ---
        private Rigidbody2D _rb = null!;
        private PlayerInputHub _input = null!;
        private PlayerControls? _controls;   // cached from the hub for safe unsubscribe on teardown
        private float _moveInput;          // -1..1, read in Update
        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private bool _jumpCutQueued;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _input = GetComponent<PlayerInputHub>();
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f;                                   // custom gravity below
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (groundSensor == null)
                groundSensor = GetComponent<GroundSensor>();

            RecalculateDerived();
        }

        private void OnValidate()
        {
            // Keep derived values in sync while tuning from the Inspector.
            RecalculateDerived();
        }

        private void RecalculateDerived()
        {
            _runSpeedU = Units.PxToUnits(runSpeedMax);
            _maxFallU = Units.PxToUnits(maxFallSpeed);
            _accelU = groundAccelTime > 0f ? _runSpeedU / groundAccelTime : float.MaxValue;
            _decelU = groundDecelTime > 0f ? _runSpeedU / groundDecelTime : float.MaxValue;

            float apexU = Units.PxToUnits(jumpApexHeight);
            float t = Mathf.Max(0.0001f, jumpTimeToApex);
            _gravity = (2f * apexU) / (t * t);      // gravity = 2h / t^2
            _jumpVelocity = _gravity * t;           // v0 = g * t = 2h / t
        }

        private void OnEnable()
        {
            // Subscribe only: the hub owns the controls' lifecycle and enable state.
            _controls = _input.Controls;
            _controls.Player.Jump.performed += OnJumpPerformed;
            _controls.Player.Jump.canceled += OnJumpCanceled;
        }

        private void OnDisable()
        {
            // Detach callbacks so no input stays wired while disabled (hitstun, death).
            if (_controls != null)
            {
                _controls.Player.Jump.performed -= OnJumpPerformed;
                _controls.Player.Jump.canceled -= OnJumpCanceled;
            }

            // Clear transient intent so a disabled player never keeps a queued jump.
            _jumpBufferCounter = 0f;
            _jumpCutQueued = false;
        }

        private void Update()
        {
            // Input only in Update.
            if (_controls != null)
                _moveInput = _controls.Player.Move.ReadValue<float>();
        }

        private void OnJumpPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            // Arm the buffer; actual jump is resolved in FixedUpdate against coyote time.
            _jumpBufferCounter = jumpBuffer;
        }

        private void OnJumpCanceled(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            // Variable jump height: request a cut, applied in FixedUpdate while rising.
            _jumpCutQueued = true;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool grounded = groundSensor != null && groundSensor.IsGrounded;
            Vector2 v = _rb.linearVelocity;

            // --- Assist timers ---
            if (grounded)
                _coyoteCounter = coyoteTime;
            else
                _coyoteCounter -= dt;

            if (_jumpBufferCounter > 0f)
                _jumpBufferCounter -= dt;

            // --- Horizontal: accel toward target, decel toward zero, air-scaled ---
            float targetX = _moveInput * _runSpeedU;
            bool accelerating = Mathf.Abs(targetX) > MoveInputEpsilon;
            float rate = accelerating ? _accelU : _decelU;
            if (!grounded)
                rate *= airControl;
            v.x = Mathf.MoveTowards(v.x, targetX, rate * dt);

            // --- Jump execution (buffer + coyote) ---
            bool jumpedThisFrame = false;
            if (_jumpBufferCounter > 0f && _coyoteCounter > 0f)
            {
                v.y = _jumpVelocity;
                _jumpBufferCounter = 0f;
                _coyoteCounter = 0f;
                jumpedThisFrame = true;
            }

            // --- Jump cut (variable height) ---
            if (_jumpCutQueued)
            {
                if (v.y > 0f)
                    v.y *= jumpCutMult;
                _jumpCutQueued = false;
            }

            // --- Vertical: planted when grounded, asymmetric gravity otherwise ---
            if (grounded && !jumpedThisFrame && v.y <= 0f)
            {
                v.y = 0f; // stay planted; prevents gravity accumulation on the ground
            }
            else
            {
                float gravMult = v.y < 0f ? fallGravityMult : 1f;
                v.y -= _gravity * gravMult * dt;
                if (v.y < -_maxFallU)
                    v.y = -_maxFallU;
            }

            _rb.linearVelocity = v;
        }
    }
}
