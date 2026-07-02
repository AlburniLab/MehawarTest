#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Minimal melee enemy to test the combat LOOP (not just impact). It is a TrainingDummy that
    /// reacts: it reuses the base damage/knockback/hitstun/flash pipeline and adds a readable
    /// 4-state brain — Patrol, Chase, Attack (telegraphed), Hurt/Dead. No pathfinding: it just
    /// walks on X toward the player. Its telegraph is deliberately longer than the player's windup,
    /// so the attack is dodgeable, and a hit during windup interrupts it (Hurt).
    /// Anti-stunlock: after eating a chain of hits it PARRIES the next one (no damage, no stagger),
    /// shoves the player back and retaliates immediately — mashing one button stops being a win.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class EnemyFante : TrainingDummy
    {
        private enum State { Patrol, Chase, Attack, Hurt, Dead }
        private enum AtkPhase { Windup, Active, Recovery }

        [Header("Perception — units")]
        [SerializeField] private float aggroRange = 6f;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float patrolRange = 2f;
        [Tooltip("Leash hysteresis: gives up the chase beyond aggroRange * this.")]
        [SerializeField] private float leashMultiplier = 1.25f;

        // Horizontal speed (u/s) below which the visual state reads Idle instead of Move.
        private const float MoveAnimThreshold = 0.05f;

        [Header("Movement — px/s (PPU 16)")]
        [SerializeField] private float moveSpeed = 110f;   // ~6.9 u/s chase: presses the player
        [SerializeField] private float patrolSpeed = 40f;  // ~2.5 u/s idle patrol

        [Header("Attack (telegraphed) — seconds")]
        [Tooltip("Readable wind-up BEFORE the hit. Longer than the player's windup on purpose.")]
        [SerializeField] private float telegraphTime = 0.28f;
        [SerializeField] private float activeTime = 0.10f;
        [Tooltip("Cooldown between attacks. Short: the Fante keeps the pressure up.")]
        [SerializeField] private float recoveryTime = 0.35f;

        [Header("Attack Hitbox — units")]
        [SerializeField] private Vector2 atkHitboxSize = new Vector2(1.2f, 1.0f);
        [SerializeField] private Vector2 atkHitboxOffset = new Vector2(1.0f, 0f); // reach 1.6u >= attackRange
        [Tooltip("Dedicated Player layer (never Everything).")]
        [SerializeField] private LayerMask playerMask;

        [Header("Attack Hit — damage / knockback / hitstun")]
        [Tooltip("Sized so the player dies in 4-5 hits once PlayerHealth exists (100 HP).")]
        [SerializeField] private int atkDamage = 25;
        [SerializeField] private float atkKnockbackSpeed = 200f; // px/s
        [SerializeField] private float atkKnockbackUp = 140f;    // px/s
        [SerializeField] private float atkHitstun = 0.2f;

        [Header("Parry (anti-stunlock)")]
        [Tooltip("Consecutive hits eaten before the NEXT one is parried (0 = never parry).")]
        [SerializeField] private int hitsBeforeParry = 3;
        [Tooltip("Max seconds between hits to count as a chain; an armed parry also expires after this.")]
        [SerializeField] private float parryComboWindow = 1.5f;
        [Tooltip("Duration of the parry pose (visual state, shown by the animation driver).")]
        [SerializeField] private float parryFlashDuration = 0.12f;
        [Tooltip("Hitstun applied to the player by the parry shove (zero damage).")]
        [SerializeField] private float parryShoveStun = 0.15f;

        [Header("Death")]
        [SerializeField] private float respawnDelay = 1.5f;
        [Tooltip("Respawn after death (training scene). Off: death is permanent (level mode).")]
        [SerializeField] private bool respawns = true;

        private State _state = State.Patrol;
        private AtkPhase _atk;
        private float _atkTimer;
        private float _deadTimer;
        private int _facing = -1;
        private float _desiredVelX;
        private bool _hitPlayerThisSwing;
        private int _patrolDir = 1;
        private Vector2 _origin;
        private int _consecutiveHits;
        private float _lastHitTime;
        private bool _parryArmed;
        private float _parryFlashTimer;
        private Transform? _player;
        private BoxCollider2D _bodyCollider = null!;
        private BoxCollider2D _atkHitbox = null!;

        /// <summary>Exposed so a bootstrap can wire the dedicated Player mask at runtime.</summary>
        public LayerMask PlayerMask
        {
            get => playerMask;
            set => playerMask = value;
        }

        /// <summary>Level mode: set false so a kill is permanent progression.</summary>
        public bool Respawns
        {
            get => respawns;
            set => respawns = value;
        }

        // Per-spawn composition knobs (sentinels on ledges, staggered columns...): the Fante
        // stays ONE enemy type, encounters differ by configuration only.
        public float AggroRange
        {
            get => aggroRange;
            set => aggroRange = value;
        }

        /// <summary>Chase speed in px/s (PPU 16), like the serialized field.</summary>
        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = value;
        }

        public float PatrolRange
        {
            get => patrolRange;
            set => patrolRange = value;
        }

        /// <summary>Visual-state projection for the animation driver (read-only, no AI leakage).</summary>
        public enum FanteAnim { Idle, Move, Telegraph, Strike, Recover, Hurt, Parry, Dead }

        public FanteAnim VisualState
        {
            get
            {
                if (_state == State.Dead) return FanteAnim.Dead;
                if (_parryFlashTimer > 0f) return FanteAnim.Parry;
                if (_state == State.Hurt) return FanteAnim.Hurt;
                if (_state == State.Attack)
                {
                    return _atk switch
                    {
                        AtkPhase.Windup => FanteAnim.Telegraph,
                        AtkPhase.Active => FanteAnim.Strike,
                        _ => FanteAnim.Recover,
                    };
                }
                return Mathf.Abs(_desiredVelX) > MoveAnimThreshold ? FanteAnim.Move : FanteAnim.Idle;
            }
        }

        // Durations for the animation driver — always the live tunables, never copies.
        public int Facing => _facing;
        public float TelegraphTime => telegraphTime;
        public float ActiveTime => activeTime;
        public float RecoveryTime => recoveryTime;
        public float RespawnDelay => respawnDelay;
        public float ParryFlashDuration => parryFlashDuration;


        /// <summary>Lazily resolved (and re-resolved after scene rebuilds): spawn order never matters.</summary>
        private Transform? Player
        {
            get
            {
                if (_player == null)
                {
                    var pm = FindFirstObjectByType<PlayerMovement>();
                    _player = pm != null ? pm.transform : null;
                }
                return _player;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _bodyCollider = GetComponent<BoxCollider2D>();
            _origin = transform.position;
            _atkHitbox = HitboxFactory.CreateChildTrigger(transform, "FanteHitbox", atkHitboxSize);
        }

        protected override void Update()
        {
            // Unscaled so the flash reads during hitstop, but parked under a full pause.
            if (_parryFlashTimer > 0f && !PauseManager.IsPaused)
                _parryFlashTimer -= Time.unscaledDeltaTime;
            TickAI(Time.deltaTime);
            base.Update(); // ticks the hitstun timer
        }

        protected override void FixedUpdate()
        {
            // Drive horizontal velocity only while actively moving; otherwise leave the body to
            // physics/knockback (so Hurt/Dead knockback flies, and Attack stays planted).
            if (_state == State.Patrol || _state == State.Chase)
                Body.linearVelocity = new Vector2(_desiredVelX, Body.linearVelocity.y);
            else if (_state == State.Attack)
                Body.linearVelocity = new Vector2(0f, Body.linearVelocity.y);

            base.FixedUpdate();   // buffered knockback last: a fresh hit wins the step
        }

        public override HitResult TakeHit(in HitInfo hit)
        {
            // Anti-stunlock: an armed parry absorbs this hit and turns the exchange around.
            if (_state != State.Dead && TryParry())
                return HitResult.Survived;

            HitResult result = base.TakeHit(hit); // damage + knockback + flash + hitstun (may -> OnDefeated)
            if (_state == State.Dead)
                return result;

            // Interrupt any wind-up: the attack is cancelled and the enemy reels.
            _atkHitbox.enabled = false;
            _hitPlayerThisSwing = false;
            _desiredVelX = 0f;
            _state = State.Hurt;

            // Chain bookkeeping: the hit after `hitsBeforeParry` chained hits gets parried.
            bool chained = Time.time - _lastHitTime <= parryComboWindow;
            _consecutiveHits = chained ? _consecutiveHits + 1 : 1;
            _lastHitTime = Time.time;
            if (hitsBeforeParry > 0 && _consecutiveHits >= hitsBeforeParry)
            {
                _parryArmed = true;
                _consecutiveHits = 0;
            }
            return result;
        }

        private bool TryParry()
        {
            if (!_parryArmed)
                return false;
            if (Time.time - _lastHitTime > parryComboWindow)
            {
                _parryArmed = false; // armed too long ago: treat this as a fresh exchange
                return false;
            }
            _parryArmed = false;

            // No damage, no stagger: flash, plant, shove the player out of mash range, retaliate NOW.
            _parryFlashTimer = parryFlashDuration;
            Body.linearVelocity = Vector2.zero;
            ShovePlayer();
            StartAttack();
            return true;
        }

        private void ShovePlayer()
        {
            if (Player == null)
                return;
            var target = Player.GetComponent<IHittable>();
            if (target == null)
                return;
            // Zero damage: the receiver owns the shove magnitudes; we only send direction + stun.
            float dir = Player.position.x >= transform.position.x ? 1f : -1f;
            target.TakeHit(new HitInfo(0, new Vector2(dir, 0f), parryShoveStun, transform));
        }

        protected override void OnDefeated()
        {
            // Die, drop collision, stop moving. The death animation (fade) plays over _deadTimer;
            // the sprite stays enabled so the fade is visible.
            _state = State.Dead;
            _deadTimer = respawnDelay;
            _atkHitbox.enabled = false;
            _bodyCollider.enabled = false;
            Body.linearVelocity = Vector2.zero;
            Debug.Log($"[EnemyFante] '{name}' defeated" + (respawns ? $" — respawning in {respawnDelay:0.0}s." : "."));
        }

        private void Respawn()
        {
            RestoreFullHealth();
            transform.position = _origin;
            _bodyCollider.enabled = true;
            _desiredVelX = 0f;
            _consecutiveHits = 0;
            _parryArmed = false;
            _parryFlashTimer = 0f;
            ClearReactionState();   // stale hitstun/knockback must never leak into a fresh life
            _state = State.Patrol;
        }

        private void TickAI(float dt)
        {
            switch (_state)
            {
                case State.Dead:
                    _deadTimer -= dt;
                    if (_deadTimer > 0f)
                        return;           // death fade still playing
                    if (respawns)
                        Respawn();
                    else
                        gameObject.SetActive(false);   // level mode: corpse removed, kill permanent
                    return;

                case State.Hurt:
                    _desiredVelX = 0f;
                    if (!IsStunned)
                        _state = State.Patrol; // re-evaluate ranges next frame
                    return;
            }

            float dx = Player != null ? Player.position.x - transform.position.x : float.PositiveInfinity;
            float adx = Mathf.Abs(dx);

            switch (_state)
            {
                case State.Patrol:
                    Patrol();
                    if (Player != null && adx <= aggroRange)
                        _state = State.Chase;
                    break;

                case State.Chase:
                    _facing = dx >= 0f ? 1 : -1;
                    _desiredVelX = _facing * Units.PxToUnits(moveSpeed);
                    if (adx <= attackRange)
                        StartAttack();
                    else if (adx > aggroRange * leashMultiplier)
                        _state = State.Patrol;
                    break;

                case State.Attack:
                    _desiredVelX = 0f;
                    TickAttack(dt);
                    break;
            }
        }

        private void Patrol()
        {
            float offset = transform.position.x - _origin.x;
            if (offset > patrolRange) _patrolDir = -1;
            else if (offset < -patrolRange) _patrolDir = 1;
            _facing = _patrolDir;
            _desiredVelX = _patrolDir * Units.PxToUnits(patrolSpeed);
        }

        private void StartAttack()
        {
            _state = State.Attack;
            _atk = AtkPhase.Windup;
            _atkTimer = telegraphTime;
            _facing = (Player != null && Player.position.x >= transform.position.x) ? 1 : -1;
            _hitPlayerThisSwing = false;
            _atkHitbox.enabled = false;
        }

        private void TickAttack(float dt)
        {
            _atkTimer -= dt;
            switch (_atk)
            {
                case AtkPhase.Windup:
                    if (_atkTimer <= 0f)
                    {
                        _atk = AtkPhase.Active;
                        _atkTimer = activeTime;
                        _atkHitbox.offset = new Vector2(_facing * atkHitboxOffset.x, atkHitboxOffset.y);
                        _atkHitbox.enabled = true;
                    }
                    break;

                case AtkPhase.Active:
                    ScanForPlayer();
                    if (_atkTimer <= 0f)
                    {
                        _atk = AtkPhase.Recovery;
                        _atkTimer = recoveryTime;
                        _atkHitbox.enabled = false;
                    }
                    break;

                case AtkPhase.Recovery:
                    if (_atkTimer <= 0f)
                        _state = State.Chase; // re-evaluate: chase again or leash back to patrol
                    break;
            }
        }

        private void ScanForPlayer()
        {
            if (_hitPlayerThisSwing)
                return;

            Collider2D hit = Physics2D.OverlapBox(_atkHitbox.bounds.center, _atkHitbox.bounds.size, 0f, playerMask);
            if (hit == null)
                return;

            var target = hit.GetComponentInParent<IHittable>();
            if (target == null)
                return;

            _hitPlayerThisSwing = true;
            // Magnitudes here are intent only: the receiver (PlayerHitReceiver) owns the
            // actual knockback shape and keeps the ~4:1 ratio. Direction is what matters.
            Vector2 knockback = new Vector2(
                _facing * Units.PxToUnits(atkKnockbackSpeed),
                Units.PxToUnits(atkKnockbackUp));
            target.TakeHit(new HitInfo(atkDamage, knockback, atkHitstun, transform));
        }

        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && _state == State.Attack && _atk == AtkPhase.Active && _atkHitbox != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(_atkHitbox.bounds.center, _atkHitbox.bounds.size);
            }
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, aggroRange);
        }
    }
}
