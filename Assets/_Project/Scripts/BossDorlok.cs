#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Dorlok, "il sicario" — boss of "La Chiamata" (Via Romana liv. 1, Docs/10 §4b). A giant
    /// mercenary in black armor studded with sapphires, one per life taken; the spear of Denab:
    /// pitchfork on one side, obsidian blade on the other. Pure melee, no ranged.
    /// THE BASTIONE TEACHER — third archetype: a READABLE wall. SuperArmor like Gaull, but slower
    /// and more telegraphed; difficulty sits BELOW Gaull and Xardast (it is a level-1 boss).
    /// Grammar: Affondo is PARRYABLE (the lesson), Spazzata is low (jump it, Gaull grammar),
    /// Presa punishes hugging and bypasses the parry (the Bastione is not universal). Phase 3
    /// chains double Affondi: parry the first (-40 Favore, below threshold) and the second MUST
    /// be spaced — the resource economy itself teaches the final lesson.
    /// </summary>
    public static class BossDorlok
    {
        public static BossDefinition Create() => new BossDefinition
        {
            Name = "Dorlok",
            MaxHealth = 200,
            ChargeSpeed = 7f,             // Affondo step-in speed
            TransitionPause = 1.2f,
            SuperArmor = true,            // a wall — Gaull's family, not Xardast's
            AttackDefs = new[]
            {
                new BossAttackDef
                {
                    Label = "Affondo", Kind = BossAttackKind.Lunge,
                    Telegraph = 0.80f, Active = 0.35f, Recovery = 1.10f,
                    Damage = 18, Unblockable = false, MoveSpeed = 7f
                },
                new BossAttackDef
                {
                    Label = "Spazzata", Kind = BossAttackKind.Slash,
                    Telegraph = 0.70f, Active = 0.25f, Recovery = 1.20f,
                    Damage = 20, Unblockable = true,
                    HitboxSize = new Vector2(3.2f, 0.8f), HitboxOffset = new Vector2(1.6f, -0.7f)
                },
                new BossAttackDef
                {
                    Label = "Presa", Kind = BossAttackKind.Grab,
                    Telegraph = 0.90f, Active = 0.20f, Recovery = 1.40f,
                    Damage = 28, Unblockable = true,
                    HitboxSize = new Vector2(1.4f, 1.6f), HitboxOffset = new Vector2(1.0f, 0f)
                },
            },
            Phases = new[]
            {
                new BossPhaseDef
                {
                    Name = "Il Contratto", EnterAtFraction = 1.00f,
                    TelegraphScale = 1.0f, Cooldown = 1.6f, ChainLength = 1,
                    Attacks = new[] { BossAttackKind.Lunge, BossAttackKind.Slash }
                },
                new BossPhaseDef
                {
                    Name = "Il Prezzo", EnterAtFraction = 0.66f,
                    TelegraphScale = 0.85f, Cooldown = 1.3f, ChainLength = 1,
                    Attacks = new[] { BossAttackKind.Lunge, BossAttackKind.Grab, BossAttackKind.Slash }
                },
                new BossPhaseDef
                {
                    Name = "L'Ultimo Zaffiro", EnterAtFraction = 0.33f,
                    TelegraphScale = 0.75f, Cooldown = 1.0f, ChainLength = 2,
                    Attacks = new[] { BossAttackKind.Lunge, BossAttackKind.Lunge, BossAttackKind.Slash, BossAttackKind.Grab }
                },
            }
        };
    }
}
