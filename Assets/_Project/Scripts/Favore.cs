#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// "Favore di Marduk" — Cesare's signature resource, the mechanical OPPOSITE of Bloodlust.
    /// It rises by landing hits WITHOUT being hit and is ZEROED by any hit taken (discipline:
    /// no time decay — only mistakes drain it). At the threshold it arms BASTIONE: the next
    /// incoming hit is parried (no damage, no stagger), the ATTACKER is shoved back and staggered,
    /// and part of the Favore is consumed. No offensive multipliers: the empowerment is defense
    /// and control, where Fury is aggression. This component holds no combat logic.
    /// </summary>
    public sealed class Favore : MonoBehaviour, IAvatarResource
    {
        [Header("Resource")]
        [SerializeField] private float favoreMax = 100f;
        [Tooltip("Gained per landed hit (only while unhit: any hit taken zeroes the resource).")]
        [SerializeField] private float gainPerHit = 12f;
        [Tooltip("Gained per kill (on top of the killing hit).")]
        [SerializeField] private float gainPerKill = 20f;

        [Header("Bastione")]
        [Tooltip("Favore needed to arm Bastione (parry + shove of the next hit taken).")]
        [SerializeField] private float bastioneThreshold = 50f;
        [Tooltip("Favore consumed by a Bastione parry.")]
        [SerializeField] private float bastioneCost = 40f;

        [Header("Visual")]
        [Tooltip("Avatar aura while Bastione is armed (gold: Marduk's order).")]
        [SerializeField] private Color bastioneAura = new Color(0.95f, 0.85f, 0.45f);

        public string DisplayName => "Favore di Marduk";
        public string EmpoweredLabel => "BASTIONE";
        public float Current { get; private set; }
        public float Max => favoreMax;
        public float Threshold => bastioneThreshold;
        public bool IsEmpowered => Current >= bastioneThreshold;
        public Color AuraColor => bastioneAura;

        // Discipline empowers defense, never the blade: multipliers stay neutral.
        public float DamageMultiplier => 1f;
        public float AttackSpeedMultiplier => 1f;
        public float HitstopMultiplier => 1f;

        public string EmpoweredLine => IsEmpowered
            ? $"BASTIONE: PRONTO — para e respinge il prossimo colpo (-{bastioneCost:0})"
            : $"Bastione: OFF   colpi alla soglia: ~{HitsToThreshold()}";

        public string StatusLine => "Nessun decadimento: si azzera solo se vieni colpito.";

        private void OnValidate()
        {
            // A parry that leaves the threshold armed would make Bastione permanent.
            bastioneCost = Mathf.Max(bastioneCost, 1f);
            bastioneThreshold = Mathf.Min(bastioneThreshold, favoreMax);
        }

        public void RegisterHitLanded() => Gain(gainPerHit);

        public void RegisterKill() => Gain(gainPerKill);

        private void Gain(float amount) => Current = Mathf.Clamp(Current + amount, 0f, favoreMax);

        public bool NotifyDamageTaken(bool parryable)
        {
            if (parryable && IsEmpowered)
            {
                Current = Mathf.Max(0f, Current - bastioneCost);
                Debug.Log($"[Favore] Bastione! Colpo parato (favore {Current:0}).");
                return true;    // hit negated: the receiver shoves the attacker
            }
            if (Current > 0f)
                Debug.Log($"[Favore] Colpito: favore azzerato (era {Current:0}).");
            Current = 0f;       // discipline broken: everything is lost
            return false;
        }

        public void ResetFromDeath() => Current = 0f;

        private int HitsToThreshold()
            => gainPerHit > 0f ? Mathf.CeilToInt(Mathf.Max(0f, bastioneThreshold - Current) / gainPerHit) : 0;
    }
}
