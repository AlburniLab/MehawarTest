#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// The avatar's signature-resource module. An avatar is a CONFIGURATION, not a code fork:
    /// shared movement + a melee profile (AvatarProfiles) + one implementation of this interface
    /// (Bloodlust for Lucius, Favore for Cesare). PlayerCombat, PlayerHitReceiver, PlayerHealth,
    /// the animation driver and the debug HUD consume ONLY this surface.
    /// </summary>
    public interface IAvatarResource
    {
        /// <summary>Resource name shown in the debug HUD (Italian).</summary>
        string DisplayName { get; }

        /// <summary>Name of the empowered state, e.g. "FURIA" / "BASTIONE".</summary>
        string EmpoweredLabel { get; }

        float Current { get; }
        float Max { get; }

        /// <summary>Threshold that arms the empowered state (HUD marker).</summary>
        float Threshold { get; }

        bool IsEmpowered { get; }

        /// <summary>Persistent tint applied to the avatar while empowered.</summary>
        Color AuraColor { get; }

        // Offensive multipliers consumed by PlayerCombat (neutral = 1).
        float DamageMultiplier { get; }
        float AttackSpeedMultiplier { get; }
        float HitstopMultiplier { get; }

        /// <summary>One Italian line describing the empowered state (debug HUD).</summary>
        string EmpoweredLine { get; }

        /// <summary>One Italian context line (decay, rules...) for the debug HUD.</summary>
        string StatusLine { get; }

        void RegisterHitLanded();
        void RegisterKill();

        /// <summary>The avatar is about to take a hit. Returns true if the resource NEGATES it
        /// (e.g. Favore's Bastione parry); the receiver then skips damage/knockback/hitstun.
        /// <paramref name="parryable"/> is false for unblockable hits (boss heavies): the resource
        /// must treat those as ordinary hits taken (no parry, penalties apply).</summary>
        bool NotifyDamageTaken(bool parryable);

        /// <summary>Death penalty: losing the resource must burn (risk/reward).</summary>
        void ResetFromDeath();
    }
}
