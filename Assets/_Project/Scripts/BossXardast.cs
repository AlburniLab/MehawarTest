#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Xardast, "la selvaggia" — boss of "Il Passo Conteso" (Via Romana liv. 2, Docs/10 §4b).
    /// The mechanical OPPOSITE of Gaull: he is an uninterruptible wall; she is mobile, elusive
    /// and INTERRUPTIBLE — but hitting her wind-up makes her vault away and arms a counter with
    /// a halved telegraph, so blind chasing is punished. Her honest damage window is the recovery
    /// after each attack. Kit: Lame (fast slash, parryable), Affondo (dagger dash, parryable —
    /// Cesare's Bastione answers her mobility), Balzo (arc over the player, landing strike,
    /// unblockable). Phases: La Caccia -> La Trappola -> La Belva.
    /// </summary>
    public static class BossXardast
    {
        public static BossDefinition Create() => new BossDefinition
        {
            Name = "Xardast",
            MaxHealth = 180,
            ChargeSpeed = 14f,            // her dash speed default (Affondo)
            TransitionPause = 1.0f,
            SuperArmor = false,           // interruptible — but interrupting her sets up her counter
            EvadeSpeed = 12f,
            EvadeCrowdDistance = 2.2f,
            CounterTelegraphScale = 0.55f,
            SlashHitboxSize = new Vector2(1.6f, 1.4f),
            SlashHitboxOffset = new Vector2(1.2f, 0f),
            AttackDefs = new[]
            {
                new BossAttackDef
                {
                    Label = "Lame", Kind = BossAttackKind.Slash,
                    Telegraph = 0.45f, Active = 0.12f, Recovery = 0.70f,
                    Damage = 20, Unblockable = false
                },
                new BossAttackDef
                {
                    Label = "Affondo", Kind = BossAttackKind.Lunge,
                    Telegraph = 0.50f, Active = 0.35f, Recovery = 0.85f,
                    Damage = 25, Unblockable = false, MoveSpeed = 14f
                },
                new BossAttackDef
                {
                    Label = "Balzo", Kind = BossAttackKind.Leap,
                    Telegraph = 0.60f, Active = 1.40f, Recovery = 0.90f,
                    Damage = 25, Unblockable = true
                },
            },
            Phases = new[]
            {
                new BossPhaseDef
                {
                    Name = "La Caccia", EnterAtFraction = 1.00f,
                    TelegraphScale = 1.0f, Cooldown = 1.1f, ChainLength = 1,
                    Attacks = new[] { BossAttackKind.Slash, BossAttackKind.Lunge }
                },
                new BossPhaseDef
                {
                    Name = "La Trappola", EnterAtFraction = 0.66f,
                    TelegraphScale = 0.8f, Cooldown = 0.9f, ChainLength = 1,
                    Attacks = new[] { BossAttackKind.Lunge, BossAttackKind.Leap, BossAttackKind.Slash }
                },
                new BossPhaseDef
                {
                    Name = "La Belva", EnterAtFraction = 0.33f,
                    TelegraphScale = 0.7f, Cooldown = 0.6f, ChainLength = 2,
                    Attacks = new[] { BossAttackKind.Leap, BossAttackKind.Slash, BossAttackKind.Lunge, BossAttackKind.Slash }
                },
            }
        };
    }
}
