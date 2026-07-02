#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Minimal animation mapping for passive targets (TrainingDummy / BreakableDummy):
    /// hitstun -> Hurt one-shot (duration from the hit), otherwise Idle.
    /// </summary>
    [RequireComponent(typeof(SpriteAnimator))]
    [RequireComponent(typeof(TrainingDummy))]
    public sealed class DummyAnimationDriver : MonoBehaviour
    {
        private SpriteAnimator _anim = null!;
        private TrainingDummy _dummy = null!;
        private int _lastHitVersion;

        private void Awake()
        {
            _anim = GetComponent<SpriteAnimator>();
            _dummy = GetComponent<TrainingDummy>();
        }

        private void Update()
        {
            if (_dummy.IsStunned)
            {
                bool freshHit = _dummy.HitVersion != _lastHitVersion;
                _lastHitVersion = _dummy.HitVersion;
                _anim.Play(AnimState.Hurt, _dummy.LastHitstunSeconds, freshHit);
                return;
            }
            _lastHitVersion = _dummy.HitVersion;
            _anim.PlayLoop(AnimState.Idle);
        }
    }
}
