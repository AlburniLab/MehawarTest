#nullable enable
using System;
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// End-of-level trigger: fires once when the player enters. Placeholder goal until the boss
    /// (STEP 4) becomes the real gate of level 1. Lives on the Default layer so GroundSensor
    /// never mistakes it for ground.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class GoalTrigger : MonoBehaviour
    {
        /// <summary>Raised once, on first player contact.</summary>
        public event Action? Reached;

        private bool _fired;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_fired)
                return;
            if (other.GetComponentInParent<PlayerMovement>() == null)
                return;
            _fired = true;
            Reached?.Invoke();
        }
    }
}
