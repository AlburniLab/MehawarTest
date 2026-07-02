#nullable enable
using System;
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// A TrainingDummy that BREAKS: anchored in place (no knockback — it must read as a fixture,
    /// not a creature), dies permanently on the killing hit and raises <see cref="Broken"/> so the
    /// level can react (e.g. open a gate). "Il Risveglio" zone 3 uses it to teach the attack on a
    /// harmless target — and to give a safe first taste of the Bloodlust kill payout.
    /// </summary>
    public sealed class BreakableDummy : TrainingDummy
    {
        /// <summary>Raised once, on the killing hit.</summary>
        public event Action? Broken;

        protected override void Awake()
        {
            base.Awake();
            Body.constraints = RigidbodyConstraints2D.FreezeAll;
            Body.gravityScale = 0f;
        }

        protected override void OnDefeated()
        {
            Debug.Log($"[BreakableDummy] '{name}' broken.");
            Broken?.Invoke();
            gameObject.SetActive(false);   // permanent: no reset, no respawn
        }
    }
}
