#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Code-driven parallax rig built from a BackgroundDefinition (Docs/31 §6).
    /// The camera is orthographic, so Z produces NO parallax: every layer is
    /// repositioned in LateUpdate from the camera offset relative to this rig's
    /// anchor — scrollFactor 0 glues a layer to the camera (sky), 1 leaves it
    /// world-locked. Horizontally tiled layers use three copies wrapped so the
    /// camera always faces the middle one.
    /// </summary>
    public sealed class ParallaxBackground : MonoBehaviour
    {
        private const int TileCopies = 3;   // center + one guard copy per side

        private sealed class Layer
        {
            public Transform Root = null!;
            public BackgroundDefinition.LayerDef Def = null!;
            public float TileWidth;   // world units; 0 when the layer does not tile
        }

        private Camera? _camera;
        private Vector3 _anchor;
        private readonly List<Layer> _layers = new List<Layer>();

        /// <summary>Builds the layer hierarchy under this object (called by the level builder).
        /// The rig's own position is the anchor: with the camera on it, layers are centered.</summary>
        public void Initialize(BackgroundDefinition definition, Camera camera)
        {
            _camera = camera;
            _anchor = transform.position;

            foreach (BackgroundDefinition.LayerDef def in definition.layers)
            {
                if (def.sprite == null)
                    continue;

                var root = new GameObject($"Layer.{def.name}");
                root.transform.SetParent(transform, false);

                float tileWidth = def.tileHorizontally ? def.sprite.bounds.size.x : 0f;
                int copies = def.tileHorizontally ? TileCopies : 1;
                for (int i = 0; i < copies; i++)
                {
                    var copy = new GameObject($"{def.name}.{i}");
                    copy.transform.SetParent(root.transform, false);
                    // Copies at -W, 0, +W so the wrap only ever moves the root.
                    copy.transform.localPosition =
                        new Vector3((i - copies / 2) * tileWidth, def.yOffset, 0f);
                    var sr = copy.AddComponent<SpriteRenderer>();
                    sr.sprite = def.sprite;
                    sr.sortingOrder = def.sortingOrder;
                    sr.color = def.tint;
                }

                _layers.Add(new Layer { Root = root.transform, Def = def, TileWidth = tileWidth });
            }
        }

        private void LateUpdate()
        {
            if (_camera == null)
                return;

            Vector3 cam = _camera.transform.position;
            foreach (Layer layer in _layers)
            {
                float x = (cam.x - _anchor.x) * (1f - layer.Def.scrollFactorX);
                float y = (cam.y - _anchor.y) * (1f - layer.Def.scrollFactorY);

                if (layer.TileWidth > 0f)
                {
                    // Wrap: keep the middle copy in front of the camera.
                    float centerX = _anchor.x + x;
                    x += layer.TileWidth * Mathf.Round((cam.x - centerX) / layer.TileWidth);
                }

                layer.Root.localPosition = new Vector3(x, y, 0f);
            }
        }
    }
}
