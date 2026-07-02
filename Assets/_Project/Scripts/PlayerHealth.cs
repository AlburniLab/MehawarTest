#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Player hit points, death and respawn (no checkpoints, no game-over screen: death is a
    /// hard reset to the spawn point after a short pause). Dying dumps the whole Bloodlust
    /// resource — losing Fury has to BURN (risk/reward core of the signature mechanic).
    /// Damage arrives routed by PlayerHitReceiver; this component owns only HP + death flow.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Header("Health")]
        [Tooltip("Sized against the Fante's 25 damage: the player dies in 4 hits.")]
        [SerializeField] private int maxHealth = 100;

        [Header("Death / Respawn")]
        [Tooltip("Pause between death and respawn (s). Long enough to register the failure.")]
        [SerializeField] private float respawnDelay = 1.0f;

        private Rigidbody2D _rb = null!;
        private Collider2D? _bodyCollider;
        private PlayerMovement? _movement;
        private PlayerCombat? _combat;
        private IAvatarResource? _resource;
        private Vector2 _spawnPoint;
        private float _respawnTimer;

        public int Current { get; private set; }
        public int Max => maxHealth;
        public bool IsDead { get; private set; }
        public float RespawnTimeLeft => IsDead ? Mathf.Max(0f, _respawnTimer) : 0f;

        /// <summary>Death pause length (the death animation stretches to this).</summary>
        public float RespawnDelay => respawnDelay;

        /// <summary>Respawn location; defaults to the starting position, settable by a level builder.</summary>
        public Vector2 SpawnPoint
        {
            get => _spawnPoint;
            set => _spawnPoint = value;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _bodyCollider = GetComponent<Collider2D>();
            _movement = GetComponent<PlayerMovement>();
            _combat = GetComponent<PlayerCombat>();
            _resource = GetComponent<IAvatarResource>();
            _spawnPoint = transform.position;
            Current = maxHealth;
        }

        /// <summary>External kill switch (pause menu "Ricomincia livello"): EXACTLY the death
        /// path — same penalties (resource burned), same respawn, same enemy-kill persistence.</summary>
        public void Kill()
        {
            if (!IsDead)
                Die();
        }

        /// <summary>Apply incoming damage. Returns true when this damage killed the player.</summary>
        public bool ApplyDamage(int damage)
        {
            if (IsDead)
                return false;
            Current = Mathf.Max(0, Current - damage);
            if (Current > 0)
                return false;
            Die();
            return true;
        }

        private void Die()
        {
            IsDead = true;
            _respawnTimer = respawnDelay;

            // The signature resource lost on death must burn (Fury or Favore alike).
            _resource?.ResetFromDeath();

            if (_movement != null) _movement.enabled = false;
            if (_combat != null) _combat.enabled = false;

            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;              // no gravity/collisions while "gone"
            if (_bodyCollider != null) _bodyCollider.enabled = false;
            // Sprite stays enabled: the death animation (fade-out) covers the respawn delay.
            Debug.Log($"[PlayerHealth] Player died — respawning in {respawnDelay:0.0}s.");
        }

        private void Update()
        {
            if (!IsDead)
                return;
            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f)
                Respawn();
        }

        private void Respawn()
        {
            transform.position = _spawnPoint;
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = true;
            if (_bodyCollider != null) _bodyCollider.enabled = true;
            Current = maxHealth;
            IsDead = false;
            if (_movement != null) _movement.enabled = true;
            if (_combat != null) _combat.enabled = true;
        }
    }
}
