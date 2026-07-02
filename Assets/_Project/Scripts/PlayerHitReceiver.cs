#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Lets the player receive hits with the same IHittable/HitInfo pattern as the dummy:
    /// knockback + brief hitstun (visual feedback is owned by SpriteAnimator + driver). Hitstun is
    /// modeled by disabling PlayerMovement/PlayerCombat for the stun window, so the knockback
    /// actually flies before control resumes. Damage is routed to PlayerHealth (optional), which
    /// owns death/respawn; while dead, incoming hits are ignored and control hand-back is deferred
    /// to the respawn.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerHitReceiver : MonoBehaviour, IHittable
    {
        [Header("Hitstun")]
        [Tooltip("Disable movement/attack for the hitstun window so knockback reads.")]
        [SerializeField] private bool freezeControlDuringHitstun = true;

        [Header("Knockback received — px/s (PPU 16), mostly horizontal")]
        [Tooltip("Horizontal launch speed when hit (px/s). Direction comes from the incoming hit.")]
        [SerializeField] private float knockbackHorizontal = 240f;
        [Tooltip("Vertical launch speed when hit (px/s). Keep small: ~4:1 horizontal:vertical.")]
        [SerializeField] private float knockbackVertical = 60f;

        [Header("Resource parry (e.g. Favore/Bastione)")]
        [Tooltip("Stagger applied to the ATTACKER when the resource parries a hit (s).")]
        [SerializeField] private float parryShoveStun = 0.3f;
        [Tooltip("Duration of the avatar's parry pose (visual only, s).")]
        [SerializeField] private float parryFlashDuration = 0.25f;

        private Rigidbody2D _rb = null!;
        private PlayerMovement? _movement;
        private PlayerCombat? _combat;
        private IAvatarResource? _resource;   // optional: may negate hits (Bastione parry)
        private PlayerHealth? _health;        // optional: owns HP / death / respawn
        private float _hitstunTimer;
        private float _parryFlashTimer;
        private bool _controlFrozen;
        private Vector2? _pendingKnockback;   // applied in FixedUpdate (physics stays in the physics step)

        /// <summary>True while reeling (read by the animation driver).</summary>
        public bool InHitstun => _hitstunTimer > 0f;

        /// <summary>True right after a resource parry (animation driver shows the Parry pose).</summary>
        public bool IsParryFlashing => _parryFlashTimer > 0f;

        /// <summary>Duration of the parry pose, for the animation one-shot.</summary>
        public float ParryFlashDuration => parryFlashDuration;

        /// <summary>Hitstun of the LAST received hit (animation drivers stretch Hurt to this).</summary>
        public float LastHitstunSeconds { get; private set; }

        /// <summary>Increments on every received hit, so observers can detect re-hits mid-stun.</summary>
        public int HitVersion { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _movement = GetComponent<PlayerMovement>();
            _combat = GetComponent<PlayerCombat>();
            _resource = GetComponent<IAvatarResource>();
            _health = GetComponent<PlayerHealth>();
        }

        public HitResult TakeHit(in HitInfo hit)
        {
            if (_health != null && _health.IsDead)
                return HitResult.Survived;   // already gone: ignore late hits

            // The resource gets first say: an armed Bastione negates the hit and answers it.
            // Zero-damage hits (parry shoves) never touch the resource; unblockable hits (boss
            // heavies) reach the resource as plain hits taken (no parry, penalties apply).
            if (hit.Damage > 0 && _resource != null && _resource.NotifyDamageTaken(!hit.Unblockable))
            {
                _parryFlashTimer = parryFlashDuration;
                ShoveAttacker(hit.Source);
                return HitResult.Survived;
            }

            // Receiver owns the knockback shape (same pattern as TrainingDummy): keep the incoming
            // direction, use this actor's own magnitudes so the ~4:1 ratio holds for every actor.
            // Buffered, applied in FixedUpdate; safe because hitstun disables PlayerMovement first.
            float dir = hit.Knockback.x >= 0f ? 1f : -1f;
            _pendingKnockback = new Vector2(
                dir * Units.PxToUnits(knockbackHorizontal),
                Units.PxToUnits(knockbackVertical));
            _hitstunTimer = hit.HitstunSeconds;
            LastHitstunSeconds = hit.HitstunSeconds;
            HitVersion++;

            if (freezeControlDuringHitstun && !_controlFrozen)
            {
                _controlFrozen = true;
                if (_movement != null) _movement.enabled = false;
                if (_combat != null) _combat.enabled = false;
            }

            bool lethal = _health != null && _health.ApplyDamage(hit.Damage);
            Debug.Log($"[Player] took {hit.Damage} damage (HP {(_health != null ? _health.Current : -1)}, " +
                      $"hitstun {hit.HitstunSeconds:0.00}s).");
            return lethal ? HitResult.Defeated : HitResult.Survived;
        }

        private void ShoveAttacker(Transform? source)
        {
            if (source == null)
                return;
            var attacker = source.GetComponentInParent<IHittable>();
            if (attacker == null)
                return;
            // Zero damage: the attacker's receiver owns the shove magnitudes; direction pushes it away.
            float dir = source.position.x >= transform.position.x ? 1f : -1f;
            attacker.TakeHit(new HitInfo(0, new Vector2(dir, 0f), parryShoveStun, transform));
        }

        private void Update()
        {
            // Visuals are owned by SpriteAnimator + PlayerAnimationDriver; only timers tick here.
            if (_parryFlashTimer > 0f)
                _parryFlashTimer -= Time.deltaTime;

            if (_hitstunTimer > 0f)
            {
                _hitstunTimer -= Time.deltaTime;
                if (_hitstunTimer <= 0f)
                    EndHitstun();
            }
        }

        private void FixedUpdate()
        {
            if (_pendingKnockback.HasValue)
            {
                if (_rb.simulated)              // death may have frozen the body meanwhile
                    _rb.linearVelocity = _pendingKnockback.Value;
                _pendingKnockback = null;
            }
        }

        private void EndHitstun()
        {
            if (!_controlFrozen)
                return;
            _controlFrozen = false;
            if (_health != null && _health.IsDead)
                return;   // death owns control now; PlayerHealth.Respawn re-enables
            if (_movement != null) _movement.enabled = true;   // OnEnable re-wires input cleanly
            if (_combat != null) _combat.enabled = true;
        }

        private void OnDisable()
        {
            // Never leave the player permanently frozen if this component is disabled mid-hitstun.
            EndHitstun();
        }
    }
}
