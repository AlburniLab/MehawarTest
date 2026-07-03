#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Mehawar.Greybox.EditorTools
{
    /// <summary>
    /// Fase 3 — scripted import for the Lucius sprite sheets (Docs/30 §5: no manual slicing).
    /// Each sheet has its OWN cell size (native-resolution art, sizes differ per animation).
    /// Pivot is computed per cell from the alpha channel: bottom-center at the lowest solid
    /// pixel, so feet sit on the collider bottom regardless of in-cell padding.
    /// Also (re)builds the ActorAnimationSet asset consumed by GreyboxBootstrap via Resources.
    /// </summary>
    public static class LuciusSpriteImportTool
    {
        private const string ArtFolder = "Assets/_Project/Art/Lucius";
        private const string SetAssetPath = "Assets/_Project/Resources/LuciusAnimationSet.asset";

        /// <summary>World scale: source pixels per world unit, uniform across sheets (same
        /// generator density). ~1.6u tall body against the 1.5u collider; tune at the gate.</summary>
        private const float PixelsPerUnit = 110f;

        private sealed class SheetConfig
        {
            public string File = "";
            public int Cell;      // square cells, uniform grid, one row
            public int Frames;
        }

        private static readonly SheetConfig[] Sheets =
        {
            new SheetConfig { File = "lucius_idle.png",   Cell = 220, Frames = 6 },
            new SheetConfig { File = "lucius_run.png",    Cell = 248, Frames = 8 },
            new SheetConfig { File = "lucius_jump.png",   Cell = 232, Frames = 3 },
            new SheetConfig { File = "lucius_attack.png", Cell = 246, Frames = 6 },
            new SheetConfig { File = "lucius_hurt.png",   Cell = 256, Frames = 2 },
        };

        [MenuItem("Mehawar/Import Lucius Sprites (Fase 3)")]
        public static void ImportAll()
        {
            foreach (SheetConfig cfg in Sheets)
                ImportSheet($"{ArtFolder}/{cfg.File}", cfg);
            AssetDatabase.Refresh();

            BuildAnimationSet();
            Debug.Log("[LuciusSpriteImportTool] Import + ActorAnimationSet build complete.");
        }

        private static void ImportSheet(string path, SheetConfig cfg)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[LuciusSpriteImportTool] Missing texture at {path}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.isReadable = true;                      // needed to measure feet from alpha
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError($"[LuciusSpriteImportTool] Could not load {path} after reimport");
                return;
            }

            string baseName = System.IO.Path.GetFileNameWithoutExtension(path);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var rects = new List<SpriteRect>();
            for (int i = 0; i < cfg.Frames; i++)
            {
                var rect = new Rect(i * cfg.Cell, 0, cfg.Cell, cfg.Cell);
                rects.Add(new SpriteRect
                {
                    name = $"{baseName}_{i}",
                    spriteID = GUID.Generate(),
                    rect = rect,
                    alignment = SpriteAlignment.Custom,
                    pivot = ComputeFeetPivot(texture, rect),
                });
            }

            provider.SetSpriteRects(rects.ToArray());
            // Name/ID table keeps sprite references stable across reimports.
            var nameFileIdProvider = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameFileIdProvider.SetNameFileIdPairs(
                rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        /// <summary>Bottom-center pivot snapped to the lowest non-transparent pixel of the cell,
        /// so the feet touch the collider bottom whatever the in-cell padding is.</summary>
        private static Vector2 ComputeFeetPivot(Texture2D tex, Rect cell)
        {
            int x0 = (int)cell.x, y0 = (int)cell.y, wPx = (int)cell.width, hPx = (int)cell.height;
            Color32[] pixels = tex.GetPixels32();
            int texW = tex.width;
            for (int y = 0; y < hPx; y++)                    // texture rows are bottom-up
            {
                int rowStart = (y0 + y) * texW + x0;
                for (int x = 0; x < wPx; x++)
                {
                    if (pixels[rowStart + x].a > 10)
                        return new Vector2(0.5f, (float)y / hPx);
                }
            }
            return new Vector2(0.5f, 0f);                    // empty cell: plain bottom-center
        }

        private static void BuildAnimationSet()
        {
            Sprite[] idle = LoadFrames("lucius_idle");
            Sprite[] run = LoadFrames("lucius_run");
            Sprite[] jump = LoadFrames("lucius_jump");
            Sprite[] attack = LoadFrames("lucius_attack");
            Sprite[] hurt = LoadFrames("lucius_hurt");

            var entries = new List<ActorAnimationSet.StateAnimation>
            {
                // Loops: fps per Docs/30 §2 gate table.
                Entry(AnimState.Idle, idle, loop: true, loopFps: 6f),
                Entry(AnimState.Run, run, loop: true, loopFps: 10f),
                Entry(AnimState.Jump, new[] { jump[0], jump[1] }, loop: true, loopFps: 6f),
                Entry(AnimState.Fall, new[] { jump[2] }, loop: true, loopFps: 1f),
                // One-shots: runtime stretches frames over the gameplay duration.
                Entry(AnimState.Windup, new[] { attack[0] }, loop: false),
                Entry(AnimState.Active, new[] { attack[1], attack[2] }, loop: false),
                Entry(AnimState.Recovery, new[] { attack[3], attack[4], attack[5] }, loop: false),
                Entry(AnimState.Hurt, hurt, loop: false),
                // Death: no dedicated clip at this gate (Docs/30 §7) — hold the heavy stagger.
                Entry(AnimState.Death, new[] { hurt[1] }, loop: false),
            };

            var set = AssetDatabase.LoadAssetAtPath<ActorAnimationSet>(SetAssetPath);
            bool created = set == null;
            if (created)
                set = ScriptableObject.CreateInstance<ActorAnimationSet>();
            set!.SetStates(entries);
            if (created)
                AssetDatabase.CreateAsset(set, SetAssetPath);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
        }

        private static ActorAnimationSet.StateAnimation Entry(
            AnimState state, Sprite[] frames, bool loop, float loopFps = 8f)
        {
            return new ActorAnimationSet.StateAnimation
            {
                state = state,
                frames = frames,
                loop = loop,
                loopFps = loopFps,
            };
        }

        private static Sprite[] LoadFrames(string baseName)
        {
            string path = $"{ArtFolder}/{baseName}.png";
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => IndexOf(s.name))
                .ToArray();
            if (sprites.Length == 0)
                throw new InvalidOperationException($"No sprites sliced at {path} — run the import first.");
            return sprites;
        }

        private static int IndexOf(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out int i)
                ? i
                : int.MaxValue;
        }
    }
}
