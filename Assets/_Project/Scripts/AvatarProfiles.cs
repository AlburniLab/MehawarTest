#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Melee configurations for the two avatars (asymmetric mapping, CLAUDE.md). Movement and
    /// jump are IDENTICAL by design — only the strike and the signature resource differ.
    /// Lucius (Via Oscura): fast, short, snowballs through Bloodlust/Fury.
    /// Cesare (Via Romana): slower, heavier, LONGER reach (legionary spacing) — he outranges the
    /// Fante (reach 2.0u vs attack range 1.5u) and turns defense into control via Favore/Bastione.
    /// </summary>
    public static class AvatarProfiles
    {
        public readonly struct MeleeProfile
        {
            public readonly float Windup;
            public readonly float Active;
            public readonly float Recovery;
            public readonly int Damage;
            public readonly Vector2 HitboxSize;
            public readonly Vector2 HitboxOffset;
            public readonly float KnockbackSpeed;   // px/s (direction intent; receiver owns shape)
            public readonly float KnockbackUp;      // px/s
            public readonly float Hitstun;

            public MeleeProfile(float windup, float active, float recovery, int damage,
                Vector2 hitboxSize, Vector2 hitboxOffset,
                float knockbackSpeed, float knockbackUp, float hitstun)
            {
                Windup = windup;
                Active = active;
                Recovery = recovery;
                Damage = damage;
                HitboxSize = hitboxSize;
                HitboxOffset = hitboxOffset;
                KnockbackSpeed = knockbackSpeed;
                KnockbackUp = knockbackUp;
                Hitstun = hitstun;
            }
        }

        /// <summary>Fast cut: 0.36s full cycle, 5 hits to kill a Fante — pressure and chains.</summary>
        public static readonly MeleeProfile Lucius = new MeleeProfile(
            0.08f, 0.10f, 0.18f, 10,
            new Vector2(1.1f, 1.0f), new Vector2(0.8f, 0f),
            240f, 120f, 0.15f);

        /// <summary>Legion thrust: 0.60s full cycle, 18 damage (3 hits per Fante), reach 2.0u —
        /// deliberate spacing; whiffing the slow strike is the risk.</summary>
        public static readonly MeleeProfile Cesare = new MeleeProfile(
            0.18f, 0.12f, 0.30f, 18,
            new Vector2(1.7f, 0.9f), new Vector2(1.15f, 0f),
            240f, 120f, 0.20f);
    }
}
