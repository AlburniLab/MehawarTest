#nullable enable
using System;
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Generic boss brain driven by a BossDefinition (a boss is data, never a code fork):
    /// HP-threshold phases, per-phase telegraphed attack cycles, readable phase transitions
    /// (pause + signal), SUPER-ARMOR (damage lands, but no knockback and no stagger — its slow
    /// recoveries are the counterplay instead). Dormant until the player enters the arena;
    /// resets fully if the player dies or leaves. Reuses the TrainingDummy damage pipeline and
    /// the telegraph/hitbox tech proven on the Fante.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class BossController : TrainingDummy
    {
        private enum BState { Dormant, Cooldown, Telegraph, Active, Recovery, Evade, Transition, Dead }

        /// <summary>Visual-state projection for the animation driver.</summary>
        public enum BossAnim { Idle, Move, Telegraph, Strike, Recover, Evade, Transition, Dead }

        [Header("Boss body — movement")]
        [Tooltip("Backstep speed when the player crowds it (anti-corner, phase-gated).")]
        [SerializeField] private float backstepSpeed = 8f;
        [SerializeField] private float backstepDuration = 0.25f;
        [Tooltip("Player distance that triggers the backstep (u).")]
        [SerializeField] private float crowdDistance = 2.5f;

        [Header("Attack Hitbox — units (Slash)")]
        [SerializeField] private Vector2 slashHitboxSize = new Vector2(2.2f, 1.8f);
        [SerializeField] private Vector2 slashHitboxOffset = new Vector2(1.6f, 0f);
        [Tooltip("Hitstun inflicted on the player by boss hits (s).")]
        [SerializeField] private float hitstunInflicted = 0.25f;

        [Header("Death")]
        [Tooltip("Death fade length before the corpse is removed (s).")]
        [SerializeField] private float deathFade = 2f;

        private BossDefinition _def = null!;
        private LayerMask _playerMask;
        private float _arenaMinX;
        private float _arenaMaxX;
        private Transform? _player;
        private PlayerHealth? _playerHealth;
        private BoxCollider2D _bodyCollider = null!;
        private BoxCollider2D _slashHitbox = null!;

        private BState _state = BState.Dormant;
        private int _phaseIndex;
        private int _pendingPhase;
        private int _attackCursor;      // deterministic cycle inside the phase's attack list
        private int _chainLeft;
        private float _timer;
        private float _stateMax;        // initial value of _timer (for elapsed-time checks)
        private float _backstepTimer;
        private float _evadeCooldown;   // rate limit: no evade-kiting loops
        private bool _counterArmed;     // next attack after an evade comes out FASTER
        private BossAttackDef? _attack;
        private float _currentTelegraph;
        private int _facing = -1;
        private bool _hitThisSwing;
        private Vector2 _origin;

        /// <summary>Raised once when the boss dies (level flow: activates the goal).</summary>
        public event Action? BossDefeated;

        public string BossName => _def.Name;
        public string PhaseName => _def.Phases[_phaseIndex].Name;
        public int PhaseNumber => _phaseIndex + 1;
        public int PendingPhaseNumber => _pendingPhase + 1;
        public bool HudVisible => _state != BState.Dormant;
        public int Facing => _facing;
        public float CurrentTelegraph => _currentTelegraph;
        public float CurrentActive => _attack?.Active ?? 0.2f;
        public float CurrentRecovery => _attack?.Recovery ?? 0.5f;
        public float DeathFade => deathFade;

        public BossAnim VisualState
        {
            get
            {
                switch (_state)
                {
                    case BState.Dead: return BossAnim.Dead;
                    case BState.Transition: return BossAnim.Transition;
                    case BState.Telegraph: return BossAnim.Telegraph;
                    case BState.Active: return BossAnim.Strike;
                    case BState.Recovery: return BossAnim.Recover;
                    case BState.Evade: return BossAnim.Evade;
                    case BState.Cooldown: return _backstepTimer > 0f ? BossAnim.Move : BossAnim.Idle;
                    default: return BossAnim.Idle;
                }
            }
        }

        /// <summary>Duration of the current evade vault (visuals stretch to this).</summary>
        public float EvadeDuration => _stateMax;

        /// <summary>Inject the boss data and arena bounds (called by the level builder).</summary>
        public void Configure(BossDefinition def, LayerMask playerMask, float arenaMinX, float arenaMaxX)
        {
            _def = def;
            _playerMask = playerMask;
            _arenaMinX = arenaMinX;
            _arenaMaxX = arenaMaxX;
            SetMaxHealth(def.MaxHealth);

            // Per-boss slash hitbox (Awake already built the child collider).
            if (def.SlashHitboxSize != Vector2.zero)
            {
                slashHitboxSize = def.SlashHitboxSize;
                slashHitboxOffset = def.SlashHitboxOffset;
                _slashHitbox.size = slashHitboxSize;
            }
        }

        /// <summary>Lazily resolved (and re-resolved after rebuilds): spawn order never matters.</summary>
        private Transform? Player
        {
            get
            {
                if (_player == null)
                    ResolvePlayer();
                return _player;
            }
        }

        private PlayerHealth? PlayerHp
        {
            get
            {
                if (_playerHealth == null)
                    ResolvePlayer();
                return _playerHealth;
            }
        }

        private void ResolvePlayer()
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            _player = pm != null ? pm.transform : null;
            _playerHealth = pm != null ? pm.GetComponent<PlayerHealth>() : null;
        }

        protected override void Awake()
        {
            base.Awake();
            _bodyCollider = GetComponent<BoxCollider2D>();
            _origin = transform.position;
            _slashHitbox = HitboxFactory.CreateChildTrigger(transform, "BossSlashHitbox", slashHitboxSize);
        }

        /// <summary>Gaull (SuperArmor): damage lands, stagger never does. Xardast family: a hit
        /// during Telegraph or Cooldown INTERRUPTS her — but she escapes with a vault and her
        /// counter comes out faster. Recovery is the honest damage window for both archetypes.</summary>
        public override HitResult TakeHit(in HitInfo hit)
        {
            if (_state == BState.Dead)
                return HitResult.Survived;

            // A first strike from range wakes it: no free damage on a passive boss.
            if (_state == BState.Dormant)
            {
                _state = BState.Cooldown;
                _timer = 0.6f;
                Debug.Log($"[Boss] {_def.Name} — «{PhaseName}» ha inizio.");
            }

            HitResult result = ApplyDamage(hit.Damage);
            if (result == HitResult.Defeated)
                return result;

            // Phase check on damage: crossing a threshold interrupts everything, readably.
            int target = TargetPhaseIndex();
            if (target > _phaseIndex && _state != BState.Transition && _state != BState.Dormant)
            {
                StartTransition(target);
                return result;
            }

            // Interruptible archetype: the hit cancels her wind-up, but she slips away.
            if (!_def.SuperArmor && (_state == BState.Telegraph || _state == BState.Cooldown))
                StartEvade();
            return result;
        }

        protected override void OnDefeated()
        {
            _state = BState.Dead;
            _timer = deathFade;
            _slashHitbox.enabled = false;
            _bodyCollider.enabled = false;
            Body.linearVelocity = Vector2.zero;
            Body.simulated = false;
            Debug.Log($"[Boss] {_def.Name} sconfitto.");
            BossDefeated?.Invoke();
        }

        protected override void Update()
        {
            TickBoss(Time.deltaTime);
            base.Update();
        }

        protected override void FixedUpdate()
        {
            if (_state != BState.Dead)
            {
                bool ballistic = _state == BState.Evade
                    || (_state == BState.Active && _attack != null && _attack.Kind == BossAttackKind.Leap);
                if (!ballistic)   // vaults and leaps fly on the velocity set at launch
                {
                    float vx = 0f;
                    if (_state == BState.Active && _attack != null
                        && (_attack.Kind == BossAttackKind.Charge || _attack.Kind == BossAttackKind.Lunge))
                        vx = _facing * DashSpeed(_attack);
                    else if (_state == BState.Cooldown && _backstepTimer > 0f)
                        vx = -_facing * backstepSpeed;
                    Body.linearVelocity = new Vector2(vx, Body.linearVelocity.y);
                }
            }
            base.FixedUpdate();   // super-armor never buffers knockback, but keep the contract
        }

        private float DashSpeed(BossAttackDef attack)
            => attack.MoveSpeed > 0f ? attack.MoveSpeed : _def.ChargeSpeed;

        private void TickBoss(float dt)
        {
            if (_state == BState.Dead)
            {
                _timer -= dt;
                if (_timer <= 0f)
                    gameObject.SetActive(false);   // fade done: corpse removed, kill permanent
                return;
            }

            // Full reset if the player dies or leaves the arena (respawn = fresh fight).
            if (_state != BState.Dormant && ShouldReset())
            {
                ResetBoss();
                return;
            }

            float dx = Player != null ? Player.position.x - transform.position.x : float.PositiveInfinity;

            switch (_state)
            {
                case BState.Dormant:
                    if (Player != null && PlayerHp != null && !PlayerHp.IsDead
                        && Player.position.x >= _arenaMinX - 2f)
                    {
                        _facing = dx >= 0f ? 1 : -1;
                        _state = BState.Cooldown;
                        _timer = 1.0f;   // a breath before the first telegraph
                        Debug.Log($"[Boss] {_def.Name} — «{PhaseName}» ha inizio.");
                    }
                    break;

                case BState.Cooldown:
                    _facing = dx >= 0f ? 1 : -1;
                    if (_backstepTimer > 0f)
                        _backstepTimer -= dt;
                    if (_evadeCooldown > 0f)
                        _evadeCooldown -= dt;
                    // Elusive archetype: crowding her while she waits triggers the vault.
                    if (!_def.SuperArmor && _def.EvadeCrowdDistance > 0f && _evadeCooldown <= 0f
                        && Mathf.Abs(dx) < _def.EvadeCrowdDistance)
                    {
                        StartEvade();
                        break;
                    }
                    _timer -= dt;
                    if (_timer <= 0f)
                        StartAttack();
                    break;

                case BState.Evade:
                    _timer -= dt;
                    // Land when the arc is done: minimum airtime, then grounded (vertical rest).
                    if (_timer <= 0f || (_stateMax - _timer > 0.25f && Mathf.Abs(Body.linearVelocity.y) < 0.05f))
                    {
                        _state = BState.Cooldown;
                        _timer = 0.35f;   // short breath: the armed counter comes out fast
                    }
                    break;

                case BState.Telegraph:
                    _timer -= dt;
                    if (_timer <= 0f)
                        BeginActive();
                    break;

                case BState.Active:
                    TickActive(dt);
                    break;

                case BState.Recovery:
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        if (_chainLeft > 0)
                            StartAttack();          // chained: no cooldown between
                        else
                            EnterCooldown();
                    }
                    break;

                case BState.Transition:
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        _phaseIndex = _pendingPhase;
                        _attackCursor = 0;
                        Debug.Log($"[Boss] {_def.Name} — fase {PhaseNumber}: «{PhaseName}».");
                        EnterCooldown();
                    }
                    break;
            }
        }

        private bool ShouldReset()
        {
            if (PlayerHp != null && PlayerHp.IsDead)
                return true;
            return Player != null && Player.position.x < _arenaMinX - 10f;   // leash
        }

        private void ResetBoss()
        {
            SetMaxHealth(_def.MaxHealth);
            _phaseIndex = 0;
            _attackCursor = 0;
            _chainLeft = 0;
            _backstepTimer = 0f;
            _evadeCooldown = 0f;
            _counterArmed = false;
            _slashHitbox.enabled = false;
            transform.position = _origin;
            Body.linearVelocity = Vector2.zero;
            _state = BState.Dormant;
            Debug.Log($"[Boss] {_def.Name} si ricompone: lo scontro ricomincia da capo.");
        }

        private void EnterCooldown()
        {
            BossPhaseDef phase = _def.Phases[_phaseIndex];
            _state = BState.Cooldown;
            _timer = phase.Cooldown;
            _chainLeft = Mathf.Max(0, phase.ChainLength - 1);
            if (phase.BackstepWhenCrowded && Player != null
                && Mathf.Abs(Player.position.x - transform.position.x) < crowdDistance)
                _backstepTimer = backstepDuration;
        }

        private void StartAttack()
        {
            BossPhaseDef phase = _def.Phases[_phaseIndex];
            BossAttackKind kind = phase.Attacks[_attackCursor % phase.Attacks.Length];
            _attackCursor++;
            if (_state == BState.Recovery)
                _chainLeft--;

            _attack = _def.GetAttack(kind);
            _facing = Player != null && Player.position.x >= transform.position.x ? 1 : -1;
            _hitThisSwing = false;
            _currentTelegraph = _attack.Telegraph * phase.TelegraphScale;
            if (_counterArmed)
            {
                _currentTelegraph *= _def.CounterTelegraphScale;   // the punish for blind chasing
                _counterArmed = false;
            }
            _timer = _currentTelegraph;
            _state = BState.Telegraph;
        }

        private void StartEvade()
        {
            _slashHitbox.enabled = false;
            _counterArmed = true;
            _evadeCooldown = 1.2f;   // rate limit: no infinite kiting loops

            // Vault away from the player; if the wall is too close on that side, vault OVER him.
            float dir = Player != null && Player.position.x >= transform.position.x ? -1f : 1f;
            float landingX = transform.position.x + dir * 6f;
            if (landingX < _arenaMinX + 1.5f || landingX > _arenaMaxX - 1.5f)
                dir = -dir;
            _facing = (int)-dir;   // keep facing the threat while flying away
            Body.linearVelocity = new Vector2(dir * _def.EvadeSpeed, 9f);
            _state = BState.Evade;
            _stateMax = 1.0f;
            _timer = _stateMax;
        }

        private void BeginActive()
        {
            _state = BState.Active;
            _timer = _attack!.Active;
            _stateMax = _timer;

            switch (_attack.Kind)
            {
                case BossAttackKind.Slash:
                    _slashHitbox.offset = new Vector2(_facing * slashHitboxOffset.x, slashHitboxOffset.y);
                    _slashHitbox.enabled = true;
                    break;

                case BossAttackKind.Shockwave:
                    float feetY = _bodyCollider.bounds.min.y;
                    BossShockwave.Spawn(transform.position.x, feetY, 1, _attack.Damage, hitstunInflicted,
                        _playerMask, _def.ShockwaveSpeed, _arenaMaxX, transform);
                    BossShockwave.Spawn(transform.position.x, feetY, -1, _attack.Damage, hitstunInflicted,
                        _playerMask, _def.ShockwaveSpeed, _arenaMinX, transform);
                    break;

                case BossAttackKind.Leap:
                    // Ballistic arc onto (slightly past) the player: the danger is the LANDING.
                    float targetX = Player != null ? Player.position.x + _facing * 2f : transform.position.x;
                    targetX = Mathf.Clamp(targetX, _arenaMinX + 1.5f, _arenaMaxX - 1.5f);
                    const float leapVy = 10f;
                    float gravity = Mathf.Abs(Physics2D.gravity.y) * Body.gravityScale;
                    float flight = 2f * leapVy / gravity;   // same-height flight time
                    Body.linearVelocity = new Vector2((targetX - transform.position.x) / flight, leapVy);
                    break;
            }
        }

        private void TickActive(float dt)
        {
            _timer -= dt;
            BossAttackKind kind = _attack!.Kind;

            if (kind == BossAttackKind.Slash)
            {
                ScanOverlap(_slashHitbox.bounds.center, _slashHitbox.bounds.size);
            }
            else if (kind == BossAttackKind.Charge || kind == BossAttackKind.Lunge)
            {
                // The body itself is the weapon while rushing.
                ScanOverlap(_bodyCollider.bounds.center, _bodyCollider.bounds.size * 1.1f);
                float x = transform.position.x;
                if ((_facing > 0 && x >= _arenaMaxX - 1.5f) || (_facing < 0 && x <= _arenaMinX + 1.5f))
                    _timer = 0f;   // wall reached: stop early
            }
            else if (kind == BossAttackKind.Leap)
            {
                // Airborne = harmless; the strike happens once, on landing.
                bool landed = _stateMax - _timer > 0.25f && Mathf.Abs(Body.linearVelocity.y) < 0.05f;
                if (landed)
                {
                    _facing = Player != null && Player.position.x >= transform.position.x ? 1 : -1;
                    ScanOverlap(_bodyCollider.bounds.center, new Vector2(3.2f, 2.2f));
                    _timer = 0f;   // straight to recovery: the punish window opens
                }
            }

            if (_timer <= 0f)
            {
                _slashHitbox.enabled = false;
                _state = BState.Recovery;
                _timer = _attack.Recovery;
            }
        }

        private void ScanOverlap(Vector2 center, Vector2 size)
        {
            if (_hitThisSwing)
                return;
            Collider2D hit = Physics2D.OverlapBox(center, size, 0f, _playerMask);
            if (hit == null)
                return;
            var target = hit.GetComponentInParent<IHittable>();
            if (target == null)
                return;
            _hitThisSwing = true;
            Vector2 knockback = new Vector2(_facing, 0.25f);   // direction intent; receiver owns shape
            target.TakeHit(new HitInfo(_attack!.Damage, knockback, hitstunInflicted,
                transform, _attack.Unblockable));
        }

        private int TargetPhaseIndex()
        {
            float fraction = HealthMax > 0 ? (float)Health / HealthMax : 0f;
            for (int i = _def.Phases.Length - 1; i >= 0; i--)
            {
                if (fraction <= _def.Phases[i].EnterAtFraction)
                    return i;
            }
            return 0;
        }

        private void StartTransition(int targetPhase)
        {
            _pendingPhase = targetPhase;
            _slashHitbox.enabled = false;
            Body.linearVelocity = Vector2.zero;
            _state = BState.Transition;
            _timer = _def.TransitionPause;
        }
    }
}
