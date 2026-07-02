#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Code-driven sprite animation. DESIGN CHOICE: no Animator/AnimatorController — gameplay
    /// durations live in [SerializeField] tunables and must drive the visuals (animations DERIVE
    /// from the state machines, never the reverse); with Animator that would need per-state speed
    /// multipliers wired in the Inspector, against the all-from-code greybox pipeline, and runtime
    /// generated placeholder sprites cannot live in an AnimatorController anyway.
    /// One-shots are stretched so N frames cover EXACTLY the given duration (scaled time: hitstop
    /// pauses visuals and gameplay in sync) and hold the last frame. Loops play at the set's fps.
    /// Also owns facing (flipX), a persistent tint (actor identity x Fury aura) and a floating
    /// debug label with the Italian state name.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        [Header("Animation Source")]
        [Tooltip("State -> frames contract. Wired by the bootstrap (placeholders) or a prefab (real art).")]
        [SerializeField] private ActorAnimationSet? animationSet;
        [Tooltip("Show the floating state-name label (greybox debug).")]
        [SerializeField] private bool showStateLabel = true;

        private static readonly Dictionary<AnimState, string> ItalianNames = new Dictionary<AnimState, string>
        {
            { AnimState.Idle, "Fermo" },
            { AnimState.Run, "Corsa" },
            { AnimState.Jump, "Salto" },
            { AnimState.Fall, "Caduta" },
            { AnimState.Windup, "Carica" },
            { AnimState.Active, "Colpo" },
            { AnimState.Recovery, "Recupero" },
            { AnimState.Telegraph, "Telegrafo" },
            { AnimState.Hurt, "Colpito" },
            { AnimState.Death, "Morte" },
            { AnimState.Parry, "Parata" },
            { AnimState.Fury, "Furia" },
        };

        private SpriteRenderer _sr = null!;
        private TextMesh? _label;
        private ActorAnimationSet.StateAnimation? _anim;
        private AnimState _current = (AnimState)(-1);   // invalid: first Play always applies
        private float _time;                            // scaled seconds inside the current clip
        private float _duration = 1f;                   // one-shot duration
        private int _facing = 1;
        private Color _identity = Color.white;
        private Color _aura = Color.white;

        public AnimState Current => _current;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (showStateLabel)
                CreateLabel();
        }

        /// <summary>Wire the frame source and the persistent identity tint (who this actor is).</summary>
        public void Configure(ActorAnimationSet set, Color identityTint)
        {
            animationSet = set;
            _identity = identityTint;
            ApplyTint();
            PlayLoop(AnimState.Idle);
        }

        /// <summary>One-shot: the frame set covers exactly <paramref name="duration"/> seconds,
        /// then holds the last frame. Re-playing the same state is a no-op unless restart=true.</summary>
        public void Play(AnimState state, float duration, bool restart = false)
        {
            if (!restart && state == _current)
                return;
            StartClip(state);
            _duration = Mathf.Max(0.0001f, duration);
        }

        /// <summary>Looping state at the set's loopFps (idle/run/jump/fall).</summary>
        public void PlayLoop(AnimState state)
        {
            if (state == _current)
                return;
            StartClip(state);
        }

        private void StartClip(AnimState state)
        {
            _anim = animationSet != null ? animationSet.Get(state) : null;
            _current = state;
            _time = 0f;
            if (_label != null)
                _label.text = ItalianNames.TryGetValue(state, out string? name) ? name! : state.ToString();
            ApplyFrame();
        }

        public void SetFacing(int facing)
        {
            if (facing != 0)
                _facing = facing > 0 ? 1 : -1;
            _sr.flipX = _facing < 0;
        }

        /// <summary>Replace the floating label until the next state change (e.g. boss "FASE 2").</summary>
        public void OverrideLabel(string text)
        {
            if (_label != null)
                _label.text = text;
        }

        /// <summary>Persistent overlay tint (e.g. Fury aura). Multiplied with the identity tint.</summary>
        public void SetAura(Color aura)
        {
            _aura = aura;
            ApplyTint();
        }

        public void ClearAura()
        {
            _aura = Color.white;
            ApplyTint();
        }

        private void ApplyTint() => _sr.color = _identity * _aura;

        private void Update()
        {
            // The label follows the renderer's visibility (hidden actors hide their label).
            if (_label != null && _label.gameObject.activeSelf != _sr.enabled)
                _label.gameObject.SetActive(_sr.enabled);

            if (_anim == null || _anim.frames.Length == 0)
                return;
            _time += Time.deltaTime;   // scaled on purpose: gameplay timers are scaled too
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (_anim == null || _anim.frames.Length == 0)
                return;
            int count = _anim.frames.Length;
            int index;
            if (_anim.loop)
            {
                float fps = Mathf.Max(1f, _anim.loopFps);
                index = Mathf.FloorToInt(_time * fps) % count;
            }
            else
            {
                // Stretched one-shot: full sweep == gameplay duration, hold on the last frame.
                float t01 = Mathf.Clamp01(_time / _duration);
                index = Mathf.Min(count - 1, Mathf.FloorToInt(t01 * count));
            }
            _sr.sprite = _anim.frames[index];
            _sr.flipX = _facing < 0;
        }

        private void CreateLabel()
        {
            // Counter-scaled so the text is not distorted by the actor's non-uniform body scale.
            GameObject go = HitboxFactory.CreateCounterScaledChild(transform, "StateLabel");
            go.transform.localPosition = new Vector3(0f, 0.75f, 0f); // just above the head

            _label = go.AddComponent<TextMesh>();
            _label.anchor = TextAnchor.LowerCenter;
            _label.alignment = TextAlignment.Center;
            _label.characterSize = 0.12f;
            _label.fontSize = 32;
            _label.color = Color.white;
            go.GetComponent<MeshRenderer>().sortingOrder = 50;
        }
    }
}
