#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Maps the boss brain to SpriteAnimator states. Telegraph durations come from the live
    /// phase-scaled value, so the visible wind-up always equals the dodge window. Phase
    /// transitions show the Fury frames with a "FASE N" label override — a readable stop.
    /// </summary>
    [RequireComponent(typeof(SpriteAnimator))]
    [RequireComponent(typeof(BossController))]
    public sealed class BossAnimationDriver : MonoBehaviour
    {
        private SpriteAnimator _anim = null!;
        private BossController _boss = null!;

        private void Awake()
        {
            _anim = GetComponent<SpriteAnimator>();
            _boss = GetComponent<BossController>();
        }

        private void Update()
        {
            _anim.SetFacing(_boss.Facing);

            switch (_boss.VisualState)
            {
                case BossController.BossAnim.Dead:
                    _anim.Play(AnimState.Death, _boss.DeathFade);
                    break;
                case BossController.BossAnim.Transition:
                    _anim.PlayLoop(AnimState.Fury);
                    _anim.OverrideLabel($"FASE {_boss.PendingPhaseNumber}");
                    break;
                case BossController.BossAnim.Telegraph:
                    _anim.Play(AnimState.Telegraph, _boss.CurrentTelegraph);
                    break;
                case BossController.BossAnim.Strike:
                    _anim.Play(AnimState.Active, _boss.CurrentActive);
                    break;
                case BossController.BossAnim.Recover:
                    _anim.Play(AnimState.Recovery, _boss.CurrentRecovery);
                    break;
                case BossController.BossAnim.Move:
                    _anim.PlayLoop(AnimState.Run);
                    break;
                default:
                    _anim.PlayLoop(AnimState.Idle);
                    break;
            }
        }
    }
}
