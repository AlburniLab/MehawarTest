#nullable enable
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mehawar.Greybox.EditorTools
{
    /// <summary>
    /// Scripted import for the R1 Babilonia environment layers (Docs/31 §5/§8: no manual
    /// import settings). Configures the textures (Sprite 2D, Point, Uncompressed, PPU 16,
    /// no mipmaps, Repeat on the tiling layers) and (re)builds the BackgroundDefinition
    /// asset consumed by GreyboxBootstrap via Resources.
    /// </summary>
    public static class EnvironmentImportTool
    {
        private const string ArtFolder = "Assets/_Project/Art/Environments/R1_Babilonia";
        private const string DefAssetPath = "Assets/_Project/Resources/R1BackgroundDefinition.asset";
        private const float PixelsPerUnit = 16f;   // project convention (Docs/31 §1)

        private sealed class LayerConfig
        {
            public string File = "";
            public string Name = "";
            public float ScrollX;
            public int SortingOrder;
            public bool TileH;
            public bool InDefinition = true;   // imported but excluded from the definition
            public Color Tint = Color.white;
            public float YOffset;              // sprite center relative to the rig anchor (ground line)
        }

        // Vertical placement (rig anchor sits ON the ground line, GroundTopY):
        // far (11.25u tall) bottoms on the ground so the mountains rise from it;
        // near (45.25u tall) bottoms 1u BELOW it so the rubble overlaps the ground line;
        // sky (58.8u tall) bottoms one viewport below ground: the abyss glow (light from
        // below, Docs/31 §3) fills pits and the sub-ground band left free for future HUD.
        private const float FarYOffset = 11.25f / 2f;              // bottom at ground
        private const float MidYOffset = 9.8125f / 2f;             // 157px band, bottom at ground
        private const float NearYOffset = 45.25f / 2f - 1f;        // bottom at ground - 1
        private const float SkyYOffset = 58.8f / 2f - 19.5f;       // bottom at ground - 19.5: the
        // glow band is ~15u tall, so this keeps the bright mint below the regular viewport
        // (dark-teal mid at play height) and lets it bleed in only at the bottom of pits

        // Docs/31 §3 layer table (sky/far/mid/near; the 16px tileset atlas is a later axis).
        private static readonly LayerConfig[] Layers =
        {
            new LayerConfig { File = "r1_sky.png",  Name = "sky",  ScrollX = 0.05f, SortingOrder = -100, TileH = false, YOffset = SkyYOffset },
            new LayerConfig { File = "r1_far.png",  Name = "far",  ScrollX = 0.15f, SortingOrder = -80,  TileH = true, YOffset = FarYOffset },
            // mid: ruins skyline band, sky removed via scripted cutout (column skyline scan
            // + morphological closing) from the opaque full-scene delivery.
            new LayerConfig { File = "r1_mid.png",  Name = "mid",  ScrollX = 0.35f, SortingOrder = -60,  TileH = true, YOffset = MidYOffset },
            // near is tinted down: its pale statue masses compete in value with the greybox
            // playfield (readability gate, Docs/31 §5) — darken via tint, never regenerate.
            new LayerConfig { File = "r1_near.png", Name = "near", ScrollX = 0.60f, SortingOrder = -40,  TileH = true, Tint = new Color(0.62f, 0.62f, 0.70f), YOffset = NearYOffset },
        };

        [MenuItem("Mehawar/Import R1 Environment (Docs 31)")]
        public static void ImportAll()
        {
            foreach (LayerConfig cfg in Layers)
                ImportTexture($"{ArtFolder}/{cfg.File}", cfg.TileH);
            AssetDatabase.Refresh();

            BuildDefinition();
            Debug.Log("[EnvironmentImportTool] Import + R1 BackgroundDefinition build complete.");
        }

        private static void ImportTexture(string path, bool tileH)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[EnvironmentImportTool] Missing texture at {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            // Painterly backdrops magnified ~6x on a 1080p view: Point renders them as
            // blocky grain — Bilinear keeps them soft. Characters/tiles stay Point.
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = tileH ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }

        private static void BuildDefinition()
        {
            var layerDefs = new List<BackgroundDefinition.LayerDef>();
            foreach (LayerConfig cfg in Layers)
            {
                if (!cfg.InDefinition)
                    continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{cfg.File}");
                if (sprite == null)
                {
                    Debug.LogError($"[EnvironmentImportTool] No sprite at {ArtFolder}/{cfg.File} — import failed?");
                    continue;
                }
                layerDefs.Add(new BackgroundDefinition.LayerDef
                {
                    name = cfg.Name,
                    sprite = sprite,
                    scrollFactorX = cfg.ScrollX,
                    scrollFactorY = 1f,          // no vertical parallax at this gate
                    sortingOrder = cfg.SortingOrder,
                    tileHorizontally = cfg.TileH,
                    tileVertically = false,
                    tint = cfg.Tint,
                    yOffset = cfg.YOffset,
                });
            }

            var def = AssetDatabase.LoadAssetAtPath<BackgroundDefinition>(DefAssetPath);
            bool created = def == null;
            if (created)
                def = ScriptableObject.CreateInstance<BackgroundDefinition>();
            def!.layers = layerDefs.ToArray();
            if (created)
                AssetDatabase.CreateAsset(def, DefAssetPath);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
        }
    }
}
