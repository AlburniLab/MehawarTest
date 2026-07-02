#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Lethal trigger under R4 chasms: the player dies through the EXISTING death rule
    /// (PlayerHealth.Kill -> respawn, resource burned), enemies die through their own defeat
    /// pipeline (kills stay permanent). Lives on the Default layer so the Player/Hittable
    /// collision-ignore matrix never suppresses the trigger contact.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class KillZone : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            var health = other.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                health.Kill();
                return;
            }

            var dummy = other.GetComponentInParent<TrainingDummy>();
            if (dummy != null)
            {
                // Overkill damage: the fall is always lethal, whatever the actor's HP.
                ((IHittable)dummy).TakeHit(new HitInfo(int.MaxValue / 2, Vector2.right, 0f, transform, true));
            }
        }
    }
}
