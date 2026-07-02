#nullable enable
using System;
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// "Sete di sangue" (Bloodlust) + "Furia" — the signature mechanic (Docs/01_Combat_Movement.md §5).
    /// A resource that rises on landed hits/kills and decays out of combat. Above a threshold it enters
    /// FURY, which exposes damage / attack-speed / hitstop multipliers consumed by PlayerCombat.
    ///
    /// Fury duration choice: RESOURCE-DRIVEN, not a fixed timer. Fury stays on while the resource is high
    /// and ends once it decays below an exit threshold. Rationale: it ties the power spike directly to
    /// sustained aggression (keep hitting to stay furious), and the out-of-combat decay already provides
    /// the "limited duration". Hysteresis (enter >= furyThreshold, exit < furyExitThreshold) avoids flicker
    /// at the boundary. This component holds NO combat logic — it only scores hits and reports multipliers.
    /// </summary>
    public sealed class Bloodlust : MonoBehaviour, IAvatarResource
    {
        [Header("Resource")]
        [SerializeField] private float bloodlustMax = 100f;
        [Tooltip("Gained per landed hit.")]
        [SerializeField] private float gainPerHit = 8f;
        [Tooltip("Gained per kill (on top of the hit that killed).")]
        [SerializeField] private float gainPerKill = 25f;

        [Header("Out-of-combat Decay")]
        [Tooltip("Seconds without hitting/being hit before the resource starts decaying.")]
        [SerializeField] private float outOfCombatDelay = 2f;
        [SerializeField] private float decayPerSecond = 20f;

        [Header("Fury Trigger")]
        [Tooltip("Enter Fury when the resource reaches this.")]
        [SerializeField] private float furyThreshold = 60f;
        [Tooltip("Exit Fury when the resource drops below this (hysteresis).")]
        [SerializeField] private float furyExitThreshold = 45f;

        [Header("Fury Multipliers")]
        [SerializeField] private float furyDamageMult = 1.5f;
        [Tooltip("Shortens PlayerCombat windup/recovery (their durations are divided by this).")]
        [SerializeField] private float furyAttackSpeedMult = 1.4f;
        [Tooltip("Makes hitstop slightly more marcato in Fury (duration multiplier).")]
        [SerializeField] private float furyHitstopMult = 1.5f;

        [Header("Fury Visual (no HUD)")]
        [Tooltip("Player aura tint while in Fury; read by PlayerHitReceiver as the persistent color.")]
        [SerializeField] private Color furyAura = new Color(1f, 0.35f, 0.30f);

        /// <summary>Raised once when Fury turns on / off. For future hooks (VFX, audio, camera).</summary>
        public event Action? OnFuryEnter;
        public event Action? OnFuryExit;

        public bool IsInFury { get; private set; }
        public float Current { get; private set; }
        public Color AuraColor => furyAura;

        // --- IAvatarResource surface ---
        public string DisplayName => "Sete di sangue";
        public string EmpoweredLabel => "FURIA";
        public float Max => bloodlustMax;
        public float Threshold => furyThreshold;
        public bool IsEmpowered => IsInFury;

        // Multipliers consumed by PlayerCombat; neutral (1) when not furious.
        public float DamageMultiplier => IsInFury ? furyDamageMult : 1f;
        public float AttackSpeedMultiplier => IsInFury ? furyAttackSpeedMult : 1f;
        public float HitstopMultiplier => IsInFury ? furyHitstopMult : 1f;

        public string EmpoweredLine => IsInFury
            ? $"FURIA: ON   ~{EstimateFurySecondsLeft():0.0}s residui (se non colpisci)"
            : $"Furia: OFF   colpi alla soglia: ~{HitsToThreshold()}";

        public string StatusLine => _timeSinceCombat >= outOfCombatDelay
            ? $"Decadimento: ATTIVO (-{decayPerSecond:0}/s)"
            : $"In combattimento — decadimento tra {Mathf.Max(0f, outOfCombatDelay - _timeSinceCombat):0.0}s";

        private float _timeSinceCombat;

        private void OnValidate()
        {
            // Hysteresis requires exit < enter, or Fury would flicker off right after entering.
            furyExitThreshold = Mathf.Min(furyExitThreshold, furyThreshold);
        }

        /// <summary>A player hit landed on a target.</summary>
        public void RegisterHitLanded() => AddResource(gainPerHit);

        /// <summary>A player hit defeated a target.</summary>
        public void RegisterKill() => AddResource(gainPerKill);

        /// <summary>The player was hit: stays "in combat" (resets decay timer) but gains nothing.
        /// Bloodlust never negates hits — aggression has no shield.</summary>
        public bool NotifyDamageTaken(bool parryable)
        {
            _timeSinceCombat = 0f;
            return false;
        }

        /// <summary>Death penalty: dump the ENTIRE resource. Fury (if any) ends immediately —
        /// losing it has to burn, that is the risk side of the signature mechanic.</summary>
        public void ResetFromDeath()
        {
            Current = 0f;
            _timeSinceCombat = 0f;
            EvaluateFury();   // exits Fury (fires OnFuryExit) now that Current is 0
        }

        private void AddResource(float amount)
        {
            Current = Mathf.Clamp(Current + amount, 0f, bloodlustMax);
            _timeSinceCombat = 0f;
            EvaluateFury();
        }

        private void Update()
        {
            _timeSinceCombat += Time.deltaTime;
            if (_timeSinceCombat >= outOfCombatDelay && Current > 0f)
            {
                Current = Mathf.Max(0f, Current - decayPerSecond * Time.deltaTime);
                EvaluateFury();
            }
        }

        private void EvaluateFury()
        {
            if (!IsInFury && Current >= furyThreshold)
            {
                IsInFury = true;
                OnFuryEnter?.Invoke();
            }
            else if (IsInFury && Current < furyExitThreshold)
            {
                IsInFury = false;
                OnFuryExit?.Invoke();
            }
        }

        // Fury is resource-driven: it ends when the resource decays below the exit threshold.
        // Estimate (no further hits): wait out the combat delay, then decay down to the exit line.
        private float EstimateFurySecondsLeft()
        {
            float delayLeft = Mathf.Max(0f, outOfCombatDelay - _timeSinceCombat);
            float toDrop = Mathf.Max(0f, Current - furyExitThreshold);
            return delayLeft + (decayPerSecond > 0f ? toDrop / decayPerSecond : 0f);
        }

        private int HitsToThreshold()
            => gainPerHit > 0f ? Mathf.CeilToInt(Mathf.Max(0f, furyThreshold - Current) / gainPerHit) : 0;
    }
}
