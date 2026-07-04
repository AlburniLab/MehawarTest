#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>Data-driven terrain skin for the greybox solids (Docs/31 layer 5).
    /// A region is a configuration, never a fork: cap = walkable top strip (tiles
    /// horizontally), fill = interior texture below it (tiles both ways). Solids
    /// keep their colliders untouched — the skin is rendering only.</summary>
    [CreateAssetMenu(menuName = "Mehawar/Terrain Skin Definition")]
    public sealed class TerrainSkinDefinition : ScriptableObject
    {
        public Sprite? cap;
        public Sprite? fill;
    }
}
