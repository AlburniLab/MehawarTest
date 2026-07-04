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
        private const string SkinAssetPath = "Assets/_Project/Resources/R1TerrainSkin.asset";
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
            public float Ppu = PixelsPerUnit;  // per-layer density: higher = smaller/sharper on screen
        }

        // Vertical placement (rig anchor sits ON the ground line, GroundTopY):
        // far (11.25u tall) bottoms on the ground so the mountains rise from it;
        // near (45.25u tall) bottoms 1u BELOW it so the rubble overlaps the ground line;
        // sky (58.8u tall) bottoms one viewport below ground: the abyss glow (light from
        // below, Docs/31 §3) fills pits and the sub-ground band left free for future HUD.
        private const float FarYOffset = 11.25f / 2f;              // bottom at ground
        private const float MidYOffset = 9.8125f / 2f;             // 157px band, bottom at ground
        // near v2: 3072x320 @ PPU 48 -> 64 x 6.67u. Mock-sized towers (~2x magnification on a
        // 1080p view instead of 6x = sharp), bottom hugs the ground line.
        private const float NearPpu = 48f;
        private const float NearYOffset = (320f / NearPpu) / 2f - 0.5f;
        private const float SkyYOffset = 58.8f / 2f - 19.5f;       // bottom at ground - 19.5: the
        // glow band is ~15u tall, so this keeps the bright mint below the regular viewport
        // (dark-teal mid at play height) and lets it bleed in only at the bottom of pits

        // Docs/31 §3 layer table (sky/far/mid/near; the 16px tileset atlas is a later axis).
        private static readonly LayerConfig[] Layers =
        {
            // sky: RETIRED (PO 2026-07-04) — the abyss glow read as a bare teal slab in pits.
            // Pits now fall into the dark clear color (skyClear); far/mid carry their own skies.
            new LayerConfig { File = "r1_sky.png",  Name = "sky",  ScrollX = 0.05f, SortingOrder = -100, TileH = false, YOffset = SkyYOffset, InDefinition = false },
            new LayerConfig { File = "r1_far.png",  Name = "far",  ScrollX = 0.15f, SortingOrder = -80,  TileH = true, YOffset = FarYOffset },
            // mid: ruins skyline band, sky removed via scripted cutout (column skyline scan
            // + morphological closing) from the opaque full-scene delivery.
            new LayerConfig { File = "r1_mid.png",  Name = "mid",  ScrollX = 0.35f, SortingOrder = -60,  TileH = true, YOffset = MidYOffset },
            // near v2 (PO hand-cut alpha, 2026-07-04): already dark and atmospheric — no tint.
            // scrollX 0.75: with the 64u width and the level-center anchor the strip never
            // runs out (needed span ~57u), so its tree-capped edges never show a seam.
            new LayerConfig { File = "r1_near.png", Name = "near", ScrollX = 0.75f, SortingOrder = -40,  TileH = false, YOffset = NearYOffset, Ppu = NearPpu },
        };

        [MenuItem("Mehawar/Import R1 Environment (Docs 31)")]
        public static void ImportAll()
        {
            foreach (LayerConfig cfg in Layers)
                ImportTexture($"{ArtFolder}/{cfg.File}", cfg.TileH, cfg.Ppu);
            ImportTerrainTile($"{ArtFolder}/r1_tile_cap.png", TilePpu);
            ImportTerrainTile($"{ArtFolder}/r1_tile_fill.png", TilePpu);
            AssetDatabase.Refresh();

            BuildDefinition();
            BuildTerrainSkin();
            Debug.Log("[EnvironmentImportTool] Import + R1 background/terrain-skin build complete.");
        }

        private static void ImportTexture(string path, bool tileH, float ppu)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[EnvironmentImportTool] Missing texture at {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            // Painterly backdrops magnified ~6x on a 1080p view: Point renders them as
            // blocky grain — Bilinear keeps them soft. Characters/tiles stay Point.
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = tileH ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }

        /// <summary>Playfield tiles: crisp Point (they ARE the pixel grid), Repeat wrap and
        /// FullRect mesh for SpriteRenderer Tiled mode, top-center pivot so a tiled strip
        /// anchors at the walkable edge and crops from the bottom on thin platforms.</summary>
        // Terrain tiles at double density: 3x screen magnification on 1080p instead of the
        // "ridiculous" 6x (PO 2026-07-04). The real fix stays hi-res tile art.
        private const float TilePpu = 32f;

        private static void ImportTerrainTile(string path, float ppu)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[EnvironmentImportTool] Missing texture at {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = 4096;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.TopCenter;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void BuildTerrainSkin()
        {
            var cap = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/r1_tile_cap.png");
            var fill = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/r1_tile_fill.png");
            if (cap == null || fill == null)
            {
                Debug.LogError("[EnvironmentImportTool] Terrain tiles missing — skin asset not built.");
                return;
            }

            var skin = AssetDatabase.LoadAssetAtPath<TerrainSkinDefinition>(SkinAssetPath);
            bool created = skin == null;
            if (created)
                skin = ScriptableObject.CreateInstance<TerrainSkinDefinition>();
            skin!.cap = cap;
            skin.fill = fill;
            if (created)
                AssetDatabase.CreateAsset(skin, SkinAssetPath);
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
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
            // Deep dark behind everything (pits, level edges) — the retired sky layer's job.
            def.skyClear = new Color(0.045f, 0.05f, 0.075f);
            if (created)
                AssetDatabase.CreateAsset(def, DefAssetPath);
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
        }
    }
}
