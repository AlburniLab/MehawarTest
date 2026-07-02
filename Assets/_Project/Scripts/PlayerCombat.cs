#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Three-phase melee attack for the greybox (Docs/01_Combat_Movement.md §3, §4).
    /// windup -> active -> recovery. A child trigger hitbox is enabled ONLY during the
    /// active phase; targets are resolved by an explicit OverlapBox so the query is
    /// immune to the player's transform scale. A HashSet guards against double hits per
    /// swing (cleared on windup). On a valid hit: damage + knockback + hitstun on the
    /// target, plus a global hitstop delegated to <see cref="Hitstop"/> (single owner of
    /// Time.timeScale). Not re-triggerable until Idle.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHub))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        public enum AttackPhase { Idle, Windup, Active, Recovery }

        // Horizontal input magnitude below which facing keeps its last value (stick deadzone).
        private const float FacingInputDeadzone = 0.1f;

        [Header("Melee Phases — seconds")]
        [SerializeField] private float windupDuration = 0.08f;
        [SerializeField] private float activeDuration = 0.10f;
        [SerializeField] private float recoveryDuration = 0.18f;

        [Header("Hitbox — units (PPU 16)")]
        [Tooltip("Hitbox size in Unity units.")]
        [SerializeField] private Vector2 hitboxSize = new Vector2(1.1f, 1.0f);
        [Tooltip("Hitbox offset from the player center; X is mirrored by facing.")]
        [SerializeField] private Vector2 hitboxOffset = new Vector2(0.8f, 0f);
        [Tooltip("Dedicated layer(s) of hittable targets (never Everything).")]
        [SerializeField] private LayerMask targetMask;

        [Header("Hit — damage / knockback / hitstun")]
        [SerializeField] private int damage = 10;
        [Tooltip("Horizontal knockback (px/s), mirrored by facing. 240 px/s = 15 u/s.")]
        [SerializeField] private float knockbackSpeed = 240f;
        [Tooltip("Vertical knockback pop (px/s). 120 px/s = 7.5 u/s.")]
        [SerializeField] private float knockbackUp = 120f;
        [Tooltip("Target hitstun on a valid hit (s).")]
        [SerializeField] private float hitstun = 0.15f;

        [Header("Hitstop (combat feel)")]
        [Tooltip("Freeze duration on impact (real seconds).")]
        [SerializeField] private float hitstopDuration = 0.06f;
        [Tooltip("Time scale held during hitstop (0 = full freeze).")]
        [SerializeField, Range(0f, 1f)] private float hitstopTimeScale = 0f;

        // --- Runtime ---
        private PlayerInputHub _inputHub = null!;
        private PlayerControls? _controls;   // cached from the hub for safe unsubscribe on teardown
        private BoxCollider2D _hitbox = null!;
        private readonly HashSet<Collider2D> _alreadyHit = new HashSet<Collider2D>();
        private AttackPhase _state = AttackPhase.Idle;
        private float _phaseTimer;
        private int _facing = 1;
        private IAvatarResource? _resource;   // optional signature resource (Bloodlust/Favore)

        /// <summary>Current attack phase (read by the animation driver).</summary>
        public AttackPhase Phase => _state;

        /// <summary>Duration of the current phase, attack-speed included (visuals stretch to this).</summary>
        public float PhaseDuration { get; private set; }

        /// <summary>Facing sign (+1 right, -1 left), from the last horizontal input.</summary>
        public int Facing => _facing;

        // Fury multipliers captured ONCE per swing (EnterWindup) so Fury entering or expiring
        // mid-swing keeps the whole swing consistent — and nothing lingers past it.
        private float _attackSpeed = 1f;
        private float _damageMult = 1f;
        private float _hitstopMult = 1f;

        /// <summary>Exposed so a bootstrap can wire the dedicated target mask at runtime.</summary>
        public LayerMask TargetMask
        {
            get => targetMask;
            set => targetMask = value;
        }

        private void Awake()
        {
            _inputHub = GetComponent<PlayerInputHub>();
            _hitbox = HitboxFactory.CreateChildTrigger(transform, "Hitbox", hitboxSize);
            _resource = GetComponent<IAvatarResource>(); // may be null -> neutral multipliers
        }

        /// <summary>Configure this melee module from an avatar profile (asymmetric mapping):
        /// the two avatars are configurations of the same state machine, never forks.</summary>
        public void ApplyProfile(in AvatarProfiles.MeleeProfile profile)
        {
            windupDuration = profile.Windup;
            activeDuration = profile.Active;
            recoveryDuration = profile.Recovery;
            damage = profile.Damage;
            hitboxSize = profile.HitboxSize;
            hitboxOffset = profile.HitboxOffset;
            knockbackSpeed = profile.KnockbackSpeed;
            knockbackUp = profile.KnockbackUp;
            hitstun = profile.Hitstun;
            if (_hitbox != null)
                _hitbox.size = hitboxSize;   // Awake already built the child hitbox
        }

        private void OnEnable()
        {
            // Subscribe only: the hub owns the controls' lifecycle and enable state.
            _controls = _inputHub.Controls;
            _controls.Player.Attack.performed += OnAttackPerformed;
        }

        private void OnDisable()
        {
            if (_controls != null)
                _controls.Player.Attack.performed -= OnAttackPerformed;

            ResetToIdle();
        }

        private void OnAttackPerformed(InputAction.CallbackContext ctx)
        {
            // Only start from Idle: not re-triggerable during windup/active/recovery.
            if (_state != AttackPhase.Idle)
                return;
            EnterWindup();
        }

        private void Update()
        {
            // Facing from horizontal input (kept when standing still).
            if (_controls != null)
            {
                float moveX = _controls.Player.Move.ReadValue<float>();
                if (Mathf.Abs(moveX) > FacingInputDeadzone)
                    _facing = moveX > 0f ? 1 : -1;
            }

            TickState(Time.deltaTime); // scaled: attack phases pause during hitstop
        }

        private void TickState(float dt)
        {
            switch (_state)
            {
                case AttackPhase.Windup:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                        EnterActive();
                    break;

                case AttackPhase.Active:
                    ScanForHits();
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                        EnterRecovery();
                    break;

                case AttackPhase.Recovery:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f)
                        ResetToIdle();
                    break;
            }
        }

        private void EnterWindup()
        {
            _state = AttackPhase.Windup;
            // Capture ALL resource multipliers for the whole swing so toggling mid-swing stays consistent.
            _attackSpeed = _resource != null ? _resource.AttackSpeedMultiplier : 1f;
            _damageMult = _resource != null ? _resource.DamageMultiplier : 1f;
            _hitstopMult = _resource != null ? _resource.HitstopMultiplier : 1f;
            _phaseTimer = windupDuration / _attackSpeed;
            PhaseDuration = _phaseTimer;
            _alreadyHit.Clear();         // fresh swing: reset the double-hit guard
            _hitbox.enabled = false;
        }

        private void EnterActive()
        {
            _state = AttackPhase.Active;
            _phaseTimer = activeDuration;
            PhaseDuration = _phaseTimer;
            _hitbox.offset = new Vector2(_facing * hitboxOffset.x, hitboxOffset.y);
            _hitbox.enabled = true;      // hitbox live ONLY during active
        }

        private void EnterRecovery()
        {
            _state = AttackPhase.Recovery;
            _phaseTimer = recoveryDuration / _attackSpeed;   // Fury shortens recovery too (active stays)
            PhaseDuration = _phaseTimer;
            _hitbox.enabled = false;
        }

        private void ResetToIdle()
        {
            _state = AttackPhase.Idle;
            _phaseTimer = 0f;
            PhaseDuration = 0f;
            if (_hitbox != null)             // may already be gone during teardown
                _hitbox.enabled = false;
        }

        private void ScanForHits()
        {
            // Explicit world-space query (independent of parent scale).
            Vector2 center = _hitbox.bounds.center;
            Vector2 size = _hitbox.bounds.size;
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, targetMask);

            foreach (Collider2D col in hits)
            {
                if (!_alreadyHit.Add(col))    // already hit this swing?
                    continue;

                var hittable = col.GetComponentInParent<IHittable>();
                if (hittable == null)
                    continue;

                int dealt = Mathf.RoundToInt(damage * _damageMult);
                Vector2 knockback = new Vector2(
                    _facing * Units.PxToUnits(knockbackSpeed),
                    Units.PxToUnits(knockbackUp));
                HitResult result = hittable.TakeHit(new HitInfo(dealt, knockback, hitstun, transform));

                // Feed the signature resource off the hits already handled here.
                if (_resource != null)
                {
                    if (result == HitResult.Defeated) _resource.RegisterKill();
                    else _resource.RegisterHitLanded();
                }

                // Centralized: concurrent same-frame hits extend the freeze, never stack it.
                Hitstop.Request(hitstopDuration * _hitstopMult, hitstopTimeScale);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || _state != AttackPhase.Active || _hitbox == null)
                return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_hitbox.bounds.center, _hitbox.bounds.size);
        }
    }
}
