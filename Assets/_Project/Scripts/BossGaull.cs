#nullable enable
namespace Mehawar.Greybox
{
    /// <summary>
    /// Gaull — first boss of "Il Risveglio" (greybox). The anti-Fante: super-armor, slow,
    /// unavoidable presence; the player wins by reading telegraphs and punishing recoveries.
    /// Three phases: "Il Muro" teaches the vocabulary with long telegraphs; "L'Assedio" adds
    /// the shockwave and tightens timings; "L'Ira" chains attacks and steps out of corners.
    /// Charge and Shockwave are unblockable (they bypass Cesare's Bastione); the Fendente can
    /// be parried. NOTE: designed from the phase brief — Docs/04 is not in the project.
    /// </summary>
    public static class BossGaull
    {
        public static BossDefinition Create() => new BossDefinition
        {
            Name = "Gaull",
            MaxHealth = 260,
            ChargeSpeed = 10f,
            ShockwaveSpeed = 8f,
            TransitionPause = 1.2f,
            AttackDefs = new[]
            {
                new BossAttackDef
                {
                    Label = "Fendente", Kind = BossAttackKind.Slash,
                    Telegraph = 0.50f, Active = 0.15f, Recovery = 0.90f,
                    Damage = 25, Unblockable = false
                },
                new BossAttackDef
                {
                    Label = "Carica", Kind = BossAttackKind.Charge,
                    Telegraph = 0.60f, Active = 2.50f, Recovery = 1.00f,
                    Damage = 30, Unblockable = true
                },
                new BossAttackDef
                {
                    Label = "Onda d'urto", Kind = BossAttackKind.Shockwave,
                    Telegraph = 0.55f, Active = 0.30f, Recovery = 1.10f,
                    Damage = 20, Unblockable = true
                },
            },
            Phases = new[]
            {
                new BossPhaseDef
                {
                    Name = "Il Muro", EnterAtFraction = 1.00f,
                    TelegraphScale = 1.0f, Cooldown = 1.2f, ChainLength = 1,
                    Attacks = new[] { BossAttackKind.Slash, BossAttackKind.Charge }
                },
                new BossPhaseDef
                {
                    Name = "L'Assedio", EnterAtFraction = 0.66f,
                    TelegraphScale = 0.7f, Cooldown = 0.9f, ChainLength = 1,
                    Attacks = new[] { BossAttackKind.Slash, BossAttackKind.Shockwave, BossAttackKind.Charge }
                },
                new BossPhaseDef
                {
                    Name = "L'Ira", EnterAtFraction = 0.33f,
                    TelegraphScale = 0.6f, Cooldown = 0.7f, ChainLength = 2, BackstepWhenCrowded = true,
                    Attacks = new[] { BossAttackKind.Shockwave, BossAttackKind.Slash, BossAttackKind.Charge, BossAttackKind.Slash }
                },
            }
        };
    }
}
