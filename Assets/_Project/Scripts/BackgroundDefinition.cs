#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>Data-driven parallax background for one region/state (Docs/31 §6).
    /// Environment = configuration, never a fork: a new biome is a new asset.
    /// Parallax is code-driven (scrollFactor), NOT camera-Z: ortho projection
    /// makes Z irrelevant to scale — it only orders rendering (sortingOrder).</summary>
    [CreateAssetMenu(menuName = "Mehawar/Background Definition")]
    public sealed class BackgroundDefinition : ScriptableObject
    {
        [System.Serializable]
        public sealed class LayerDef
        {
            public string name = "";
            public Sprite? sprite;
            public float scrollFactorX = 1f;   // 0 = fixed to camera, 1 = world-locked
            public float scrollFactorY = 1f;
            public int sortingOrder;
            public bool tileHorizontally;
            public bool tileVertically;
            public Color tint = Color.white;   // readability knob (Docs/31 §5): darken, never regenerate
            public float yOffset;              // sprite center Y relative to the rig anchor (world units)
        }

        public Color skyClear = new Color(0.16f, 0.17f, 0.20f);
        public LayerDef[] layers = System.Array.Empty<LayerDef>();
    }
}
