#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Maps the Fante's AI state to SpriteAnimator states. Durations come from the Fante's own
    /// tunables at the transition (telegraph MUST last exactly telegraphTime on screen, or the
    /// dodge window lies to the player).
    /// </summary>
    [RequireComponent(typeof(SpriteAnimator))]
    [RequireComponent(typeof(EnemyFante))]
    public sealed class FanteAnimationDriver : MonoBehaviour
    {
        private SpriteAnimator _anim = null!;
        private EnemyFante _fante = null!;
        private int _lastHitVersion;

        private void Awake()
        {
            _anim = GetComponent<SpriteAnimator>();
            _fante = GetComponent<EnemyFante>();
        }

        private void Update()
        {
            _anim.SetFacing(_fante.Facing);

            switch (_fante.VisualState)
            {
                case EnemyFante.FanteAnim.Dead:
                    _anim.Play(AnimState.Death, _fante.RespawnDelay);
                    break;
                case EnemyFante.FanteAnim.Parry:
                    _anim.Play(AnimState.Parry, _fante.ParryFlashDuration);
                    break;
                case EnemyFante.FanteAnim.Hurt:
                    bool freshHit = _fante.HitVersion != _lastHitVersion;
                    _lastHitVersion = _fante.HitVersion;
                    _anim.Play(AnimState.Hurt, _fante.LastHitstunSeconds, freshHit);
                    break;
                case EnemyFante.FanteAnim.Telegraph:
                    _anim.Play(AnimState.Telegraph, _fante.TelegraphTime);
                    break;
                case EnemyFante.FanteAnim.Strike:
                    _anim.Play(AnimState.Active, _fante.ActiveTime);
                    break;
                case EnemyFante.FanteAnim.Recover:
                    _anim.Play(AnimState.Recovery, _fante.RecoveryTime);
                    break;
                case EnemyFante.FanteAnim.Move:
                    _anim.PlayLoop(AnimState.Run);
                    break;
                default:
                    _anim.PlayLoop(AnimState.Idle);
                    break;
            }

            if (_fante.VisualState != EnemyFante.FanteAnim.Hurt)
                _lastHitVersion = _fante.HitVersion;
        }
    }
}
