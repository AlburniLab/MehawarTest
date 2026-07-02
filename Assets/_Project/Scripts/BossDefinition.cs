#nullable enable
using System;
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>The greybox boss attack archetypes, all telegraphed and dodge-on-sight.</summary>
    public enum BossAttackKind
    {
        Slash,      // frontal melee — dodge by stepping back or jumping; usually parryable
        Charge,     // horizontal rush across the arena — dodge by jumping OVER; unblockable
        Shockwave,  // ground wave on both sides — dodge by jumping; unblockable
        Lunge,      // short fast dash strike (dagger) — dodge sideways/jump; parryable
        Leap        // ballistic arc onto the player, strike on LANDING — move away; unblockable
    }

    /// <summary>One boss attack as data: timings, damage, parry rules. Phases scale telegraphs.</summary>
    public sealed class BossAttackDef
    {
        public string Label = "";          // Italian, shown by the floating state label
        public BossAttackKind Kind;
        public float Telegraph;            // base seconds, multiplied by the phase's TelegraphScale
        public float Active;               // strike window (Charge/Lunge/Leap: max duration cap)
        public float Recovery;             // punish window
        public int Damage;
        public bool Unblockable;           // bypasses resource parries (Bastione)
        public float MoveSpeed;            // u/s for Charge/Lunge (0 = use BossDefinition.ChargeSpeed)
    }

    /// <summary>One boss phase: entered when HP fraction drops to EnterAtFraction.</summary>
    public sealed class BossPhaseDef
    {
        public string Name = "";           // Italian
        public float EnterAtFraction = 1f; // phase is active while hpFraction <= this (checked descending)
        public float TelegraphScale = 1f;
        public float Cooldown = 1f;        // pause between attack chains
        public int ChainLength = 1;        // attacks back-to-back before the cooldown
        public bool BackstepWhenCrowded;   // anti-corner: steps back if the player hugs it
        public BossAttackKind[] Attacks = Array.Empty<BossAttackKind>();  // deterministic cycle
    }

    /// <summary>A whole boss as configuration for BossController — bosses are data, not forks.</summary>
    public sealed class BossDefinition
    {
        public string Name = "";
        public int MaxHealth = 200;
        public float ChargeSpeed = 10f;      // u/s
        public float ShockwaveSpeed = 8f;    // u/s
        public float TransitionPause = 1.2f; // readable stop between phases

        // --- Mobility archetype (Xardast family). SuperArmor=true = the Gaull wall. ---
        public bool SuperArmor = true;       // false: hits during Telegraph/Cooldown interrupt AND trigger an evade
        public float EvadeSpeed = 12f;       // horizontal launch of the escape vault (u/s)
        public float EvadeCrowdDistance;     // 0 = never evade proximity; >0 = evades when crowded in Cooldown
        public float CounterTelegraphScale = 1f; // telegraph multiplier for the attack right after an evade

        // Optional slash hitbox override (zero = keep the controller's serialized default).
        public Vector2 SlashHitboxSize = Vector2.zero;
        public Vector2 SlashHitboxOffset = Vector2.zero;

        public BossAttackDef[] AttackDefs = Array.Empty<BossAttackDef>();
        public BossPhaseDef[] Phases = Array.Empty<BossPhaseDef>();

        public BossAttackDef GetAttack(BossAttackKind kind)
            => Array.Find(AttackDefs, a => a.Kind == kind)
               ?? throw new InvalidOperationException($"Boss '{Name}' has no attack def for {kind}.");
    }
}
