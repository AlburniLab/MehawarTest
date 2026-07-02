#nullable enable
using System.Collections.Generic;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Per-campaign level sequences (names + builder ids) — the code-side mirror of
    /// Docs/10_Narrative_Spine.md §4. The flow reads ONLY this table to know what comes next,
    /// so adding a level is one entry + one geometry builder. Via Romana's first slot is a
    /// placeholder ("La Chiamata" does not exist yet): it reuses the Risveglio build.
    /// </summary>
    public static class LevelCatalog
    {
        public sealed class LevelInfo
        {
            public LevelInfo(string name, int builderId)
            {
                Name = name;
                BuilderId = builderId;
            }

            public string Name { get; }
            public int BuilderId { get; }
        }

        public const int RisveglioBuilder = 1;
        public const int PassoContesoBuilder = 2;
        public const int ChiamataBuilder = 3;

        private static readonly LevelInfo[] Oscura =
        {
            new LevelInfo("Il Risveglio", RisveglioBuilder),
        };

        private static readonly LevelInfo[] Romana =
        {
            new LevelInfo("La Chiamata", ChiamataBuilder),
            new LevelInfo("Il Passo Conteso", PassoContesoBuilder),
        };

        private static IReadOnlyList<LevelInfo> For(Campaign campaign)
            => campaign == Campaign.ViaOscura ? Oscura : Romana;

        /// <summary>1-based level lookup (matches GameState.CurrentLevel).</summary>
        public static LevelInfo Get(Campaign campaign, int levelIndex)
        {
            IReadOnlyList<LevelInfo> list = For(campaign);
            int i = UnityEngine.Mathf.Clamp(levelIndex - 1, 0, list.Count - 1);
            return list[i];
        }

        public static bool HasLevel(Campaign campaign, int levelIndex)
            => levelIndex >= 1 && levelIndex <= For(campaign).Count;
    }
}
