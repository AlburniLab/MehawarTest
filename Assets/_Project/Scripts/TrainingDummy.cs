#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>Payload of a single melee hit (Docs/01_Combat_Movement.md §3).</summary>
    public readonly struct HitInfo
    {
        public readonly int Damage;
        public readonly Vector2 Knockback;   // impulse velocity in Unity units/s
        public readonly float HitstunSeconds;
        public readonly Transform? Source;   // attacker, so parries can answer (optional)
        public readonly bool Unblockable;    // heavy hits bypass resource parries (Bastione)

        public HitInfo(int damage, Vector2 knockback, float hitstunSeconds,
            Transform? source = null, bool unblockable = false)
        {
            Damage = damage;
            Knockback = knockback;
            HitstunSeconds = hitstunSeconds;
            Source = source;
            Unblockable = unblockable;
        }
    }

    /// <summary>Outcome of a hit, so attackers can react (e.g. Bloodlust kill gain).</summary>
    public enum HitResult
    {
        Survived,
        Defeated
    }

    /// <summary>Anything that can receive a melee hit. Returns whether the hit defeated it.</summary>
    public interface IHittable
    {
        HitResult TakeHit(in HitInfo hit);
    }

    /// <summary>
    /// Passive punching bag to validate combat feel: receives damage, knockback and hitstun.
    /// No AI. Designed to be extended (see <see cref="EnemyFante"/>): the reaction pipeline
    /// (TakeHit, hitstun, defeat) lives here so an enemy is just "a TrainingDummy that reacts".
    /// Visual feedback (impact flash, stun pose) is owned by SpriteAnimator + a driver.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class TrainingDummy : MonoBehaviour, IHittable
    {
        [Header("Health")]
        [SerializeField] private int maxHealth = 50;

        [Header("Knockback — px/s (PPU 16), mostly horizontal")]
        [Tooltip("Horizontal launch speed on a hit (px/s). Direction comes from the incoming hit.")]
        [SerializeField] private float knockbackHorizontal = 240f;
        [Tooltip("Vertical launch speed on a hit (px/s). Keep small: ~4:1 horizontal:vertical.")]
        [SerializeField] private float knockbackVertical = 60f;

        [Header("Physics")]
        [SerializeField] private float gravityScale = 2f;

        protected SpriteRenderer Sprite = null!;
        protected Rigidbody2D Body = null!;

        private int _health;
        private float _hitstunTimer;
        private Vector2? _pendingKnockback;   // applied in FixedUpdate: physics writes stay in the physics step

        /// <summary>True while reeling from a hit (used by subclasses to interrupt behaviour).</summary>
        public bool IsStunned => _hitstunTimer > 0f;

        /// <summary>Hitstun of the LAST received hit (animation drivers stretch Hurt to this).</summary>
        public float LastHitstunSeconds { get; private set; }

        /// <summary>Increments on every received hit, so observers can detect re-hits mid-stun.</summary>
        public int HitVersion { get; private set; }

        public int Health => _health;
        public int HealthMax => maxHealth;

        protected int MaxHealth => maxHealth;

        /// <summary>For code-configured actors (e.g. bosses) whose HP is not the serialized default.</summary>
        protected void SetMaxHealth(int value)
        {
            maxHealth = value;
            _health = value;
        }

        protected virtual void Awake()
        {
            Sprite = GetComponent<SpriteRenderer>();
            Body = GetComponent<Rigidbody2D>();
            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.gravityScale = gravityScale;
            Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _health = maxHealth;
        }

        public virtual HitResult TakeHit(in HitInfo hit)
        {
            // Target owns the knockback shape for readability: keep the incoming direction, but use
            // this target's own magnitudes (mostly horizontal) so it flies AWAY, not into the sky.
            // Buffered here, applied in FixedUpdate (convention: physics only in the physics step).
            float dir = hit.Knockback.x >= 0f ? 1f : -1f;
            _pendingKnockback = new Vector2(
                dir * Units.PxToUnits(knockbackHorizontal),
                Units.PxToUnits(knockbackVertical));
            _hitstunTimer = hit.HitstunSeconds;
            LastHitstunSeconds = hit.HitstunSeconds;
            HitVersion++;

            return ApplyDamage(hit.Damage);
        }

        /// <summary>Damage-only path (super-armor actors skip knockback/stagger but not this).</summary>
        protected HitResult ApplyDamage(int damage)
        {
            _health -= damage;
            if (_health <= 0)
            {
                OnDefeated();
                return HitResult.Defeated;
            }
            return HitResult.Survived;
        }

        /// <summary>Default: keep the bag alive by resetting. Enemies override to die/respawn.</summary>
        protected virtual void OnDefeated()
        {
            _health = maxHealth;
            Debug.Log($"[TrainingDummy] '{name}' knocked out — health reset for continued testing.");
        }

        protected void RestoreFullHealth() => _health = maxHealth;

        /// <summary>Clear transient hit reactions (respawn/reset paths): stun and buffered knockback.</summary>
        protected void ClearReactionState()
        {
            _hitstunTimer = 0f;
            _pendingKnockback = null;
        }

        protected virtual void Update()
        {
            // Visuals are owned by SpriteAnimator + drivers; only the gameplay timer ticks here.
            if (_hitstunTimer > 0f)
                _hitstunTimer -= Time.deltaTime; // scaled: hitstun pauses during hitstop
        }

        protected virtual void FixedUpdate()
        {
            // Subclasses drive their own velocity first and call base LAST: a fresh knockback
            // always wins the step it lands on.
            if (_pendingKnockback.HasValue)
            {
                if (Body.simulated)
                    Body.linearVelocity = _pendingKnockback.Value;
                _pendingKnockback = null;
            }
        }
    }
}
