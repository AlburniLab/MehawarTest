#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Builds greybox ActorAnimationSets at runtime: every state gets frames with a DISTINCT color
    /// and a sweeping white notch, so both the state and the playback progress read at a glance —
    /// a one-shot's notch crosses the body in exactly the gameplay duration, making duration/visual
    /// sync verifiable by eye. Actor identity comes from the SpriteAnimator tint, so one shared set
    /// serves everyone. Replaced later by authored ActorAnimationSet assets with real sprites.
    /// </summary>
    public static class PlaceholderAnimationFactory
    {
        private const int Size = 32;        // px per side; sprites are 1x1 unit (PPU = Size)
        private const int FrameCount = 4;

        private static readonly (AnimState state, Color color, bool loop, float fps)[] Spec =
        {
            (AnimState.Idle,      new Color(0.72f, 0.72f, 0.72f), true,  4f),
            (AnimState.Run,       new Color(0.30f, 0.75f, 0.35f), true, 10f),
            (AnimState.Jump,      new Color(0.25f, 0.80f, 0.85f), true,  8f),
            (AnimState.Fall,      new Color(0.25f, 0.45f, 0.85f), true,  8f),
            (AnimState.Windup,    new Color(1.00f, 0.60f, 0.15f), false, 0f),
            (AnimState.Active,    new Color(1.00f, 0.20f, 0.15f), false, 0f),
            (AnimState.Recovery,  new Color(0.60f, 0.35f, 0.80f), false, 0f),
            (AnimState.Telegraph, new Color(1.00f, 0.85f, 0.20f), false, 0f),
            (AnimState.Hurt,      new Color(1.00f, 1.00f, 1.00f), false, 0f),
            (AnimState.Death,     new Color(0.15f, 0.15f, 0.18f), false, 0f),
            (AnimState.Parry,     new Color(0.55f, 0.75f, 1.00f), false, 0f),
            (AnimState.Fury,      new Color(1.00f, 0.35f, 0.30f), true,  8f),
        };

        private static ActorAnimationSet? _shared;

        /// <summary>Shared placeholder set; identity is expressed via the animator tint.</summary>
        public static ActorAnimationSet GetShared()
        {
            if (_shared == null)
                _shared = Build();
            return _shared;
        }

        private static ActorAnimationSet Build()
        {
            var set = ScriptableObject.CreateInstance<ActorAnimationSet>();
            var entries = new List<ActorAnimationSet.StateAnimation>();
            foreach ((AnimState state, Color color, bool loop, float fps) in Spec)
            {
                var frames = new Sprite[FrameCount];
                for (int i = 0; i < FrameCount; i++)
                    frames[i] = MakeFrame(state, color, i);
                entries.Add(new ActorAnimationSet.StateAnimation
                {
                    state = state,
                    frames = frames,
                    loop = loop,
                    loopFps = fps
                });
            }
            set.SetStates(entries);
            return set;
        }

        private static Sprite MakeFrame(AnimState state, Color color, int index)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color border = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 1f);
            // Hurt alternates white/red per frame (impact flash); Death fades out to invisible.
            Color body = state == AnimState.Hurt
                ? (index % 2 == 0 ? Color.white : new Color(1f, 0.30f, 0.30f))
                : color;
            if (state == AnimState.Death)
                body.a = 1f - (index + 1) / (float)FrameCount;   // last frame fully transparent

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool isBorder = x < 2 || x >= Size - 2 || y < 2 || y >= Size - 2;
                    tex.SetPixel(x, y, isBorder && body.a > 0f ? border : body);
                }
            }

            // Sweeping notch: a bright bar whose X advances with the frame index (playback progress).
            if (body.a > 0f)
            {
                int notchX = 3 + Mathf.RoundToInt((Size - 10) * (index / (float)(FrameCount - 1)));
                for (int y = 4; y < Size - 4; y++)
                    for (int x = notchX; x < notchX + 4; x++)
                        tex.SetPixel(x, y, Color.white);
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
