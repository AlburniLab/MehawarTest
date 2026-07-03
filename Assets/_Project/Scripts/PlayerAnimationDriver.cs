#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Maps the player's gameplay state to SpriteAnimator states, every frame, with a fixed
    /// priority: death > hitstun > attack phase > airborne > run > idle. One-shot durations are
    /// read from the owning components' tunables AT the transition — never hardcoded — so the
    /// visual sweep always matches the gameplay window. Fury is an aura overlay, not a state.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        [Header("Read thresholds")]
        [Tooltip("Horizontal speed (u/s) above which the run loop plays.")]
        [SerializeField] private float runSpeedThreshold = 0.5f;
        [Tooltip("Vertical speed (u/s) separating jump from fall while airborne.")]
        [SerializeField] private float jumpFallThreshold = 0.05f;

        private SpriteAnimator _anim = null!;
        private Rigidbody2D _rb = null!;
        private GroundSensor? _ground;
        private PlayerCombat? _combat;
        private PlayerHitReceiver? _receiver;
        private PlayerHealth? _health;
        private IAvatarResource? _resource;
        private int _lastHitVersion;

        private void Awake()
        {
            // Real art lives on a counter-scaled "Visual" child; placeholders sit on the root.
            _anim = GetComponentInChildren<SpriteAnimator>();
            _rb = GetComponent<Rigidbody2D>();
            _ground = GetComponent<GroundSensor>();
            _combat = GetComponent<PlayerCombat>();
            _receiver = GetComponent<PlayerHitReceiver>();
            _health = GetComponent<PlayerHealth>();
            _resource = GetComponent<IAvatarResource>();
        }

        private void Update()
        {
            if (_combat != null)
                _anim.SetFacing(_combat.Facing);

            // Empowered aura (Furia/Bastione) survives every state change (overlay, not a state).
            if (_resource != null && _resource.IsEmpowered)
                _anim.SetAura(_resource.AuraColor);
            else
                _anim.ClearAura();

            if (_health != null && _health.IsDead)
            {
                _anim.Play(AnimState.Death, _health.RespawnDelay);
                return;
            }

            if (_receiver != null && _receiver.IsParryFlashing)
            {
                _anim.Play(AnimState.Parry, _receiver.ParryFlashDuration);
                return;
            }

            if (_receiver != null && _receiver.InHitstun)
            {
                bool freshHit = _receiver.HitVersion != _lastHitVersion;
                _lastHitVersion = _receiver.HitVersion;
                _anim.Play(AnimState.Hurt, _receiver.LastHitstunSeconds, freshHit);
                return;
            }
            if (_receiver != null)
                _lastHitVersion = _receiver.HitVersion;

            if (_combat != null && _combat.Phase != PlayerCombat.AttackPhase.Idle)
            {
                AnimState state = _combat.Phase switch
                {
                    PlayerCombat.AttackPhase.Windup => AnimState.Windup,
                    PlayerCombat.AttackPhase.Active => AnimState.Active,
                    _ => AnimState.Recovery,
                };
                _anim.Play(state, _combat.PhaseDuration);
                return;
            }

            bool grounded = _ground != null && _ground.IsGrounded;
            if (!grounded)
            {
                _anim.PlayLoop(_rb.linearVelocity.y > jumpFallThreshold ? AnimState.Jump : AnimState.Fall);
                return;
            }

            _anim.PlayLoop(Mathf.Abs(_rb.linearVelocity.x) > runSpeedThreshold ? AnimState.Run : AnimState.Idle);
        }
    }
}
