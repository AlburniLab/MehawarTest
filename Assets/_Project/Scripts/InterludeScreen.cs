#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Between-levels narrative screen: black background, centered text, line-by-line fade-in.
    /// Via Oscura voice lines are the INTRUSION: they arrive after a silence, italic, in the
    /// empty-violet palette — the memory is warm and fading, the voice is cold and growing.
    /// Skip (attack/submit input) is armed only after <see cref="interludeSkipDelay"/>; a discreet
    /// hint appears when it arms. Input is polled directly because the player (and its input hub)
    /// does not exist between levels. Timing runs on the unscaled clock.
    /// </summary>
    public sealed class InterludeScreen : MonoBehaviour
    {
        [Header("Timing — seconds")]
        [Tooltip("Fade-in of each memory line.")]
        [SerializeField] private float lineFadeDuration = 0.8f;
        [Tooltip("Delay between the start of one line and the next.")]
        [SerializeField] private float lineStagger = 0.6f;
        [Tooltip("Silence between the end of the memory and the voice (the intrusion).")]
        [SerializeField] private float voiceDelay = 1.2f;
        [Tooltip("Fade-in of the voice line (slower: it grows).")]
        [SerializeField] private float voiceFadeDuration = 1.2f;
        [Tooltip("Skip input is ignored until this many seconds have passed.")]
        [SerializeField] private float interludeSkipDelay = 1.5f;
        [SerializeField] private float fadeOutDuration = 0.3f;

        [Header("Palette")]
        [Tooltip("Memory lines: warm, faded parchment.")]
        [SerializeField] private Color memoryColor = new Color(0.85f, 0.78f, 0.62f);
        [Tooltip("Voice lines: the empty violet of the Oscura palette.")]
        [SerializeField] private Color voiceColor = new Color(0.58f, 0.45f, 0.78f);
        [SerializeField] private Color hintColor = new Color(0.45f, 0.45f, 0.48f);

        private Font _font = null!;
        private GameObject _panel = null!;
        private Transform _column = null!;
        private Text _hint = null!;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Action? _onDone;
        private Coroutine? _run;
        private bool _skipArmed;

        public bool IsShowing => _panel != null && _panel.activeSelf;
        public bool SkipArmed => _skipArmed;

        /// <summary>Build the (initially hidden) UI under the flow canvas.</summary>
        public void Configure(Transform canvas, Font font)
        {
            _font = font;

            _panel = new GameObject("InterludePanel");
            _panel.transform.SetParent(canvas, false);
            var rect = _panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _panel.AddComponent<Image>().color = Color.black;   // full black: only the words exist

            var column = new GameObject("Column");
            column.transform.SetParent(_panel.transform, false);
            var colRect = column.AddComponent<RectTransform>();
            colRect.anchorMin = colRect.anchorMax = new Vector2(0.5f, 0.5f);
            colRect.pivot = new Vector2(0.5f, 0.5f);
            var layout = column.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = column.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _column = column.transform;

            _hint = MakeText(_panel.transform, "Premi per continuare", 18, hintColor, FontStyle.Normal);
            var hintRect = _hint.GetComponent<RectTransform>();
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 36f);
            SetAlpha(_hint, 0f);

            _panel.SetActive(false);
        }

        /// <summary>Show the beat; <paramref name="onDone"/> fires when finished or skipped.</summary>
        public void Show(InterludeDefinition definition, Action onDone)
        {
            _onDone = onDone;
            _skipArmed = false;

            foreach (GameObject go in _spawned)
                Destroy(go);
            _spawned.Clear();

            _panel.SetActive(true);
            _run = StartCoroutine(Run(definition));
        }

        /// <summary>Skip request (input or tests). Ignored until the delay arms it.</summary>
        public bool TrySkip()
        {
            if (!IsShowing || !_skipArmed)
                return false;
            Finish();
            return true;
        }

        private void Update()
        {
            if (!IsShowing || !_skipArmed)
                return;
            bool pressed =
                (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame
                    || Keyboard.current.enterKey.wasPressedThisFrame
                    || Keyboard.current.jKey.wasPressedThisFrame))
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame
                    || Gamepad.current.buttonWest.wasPressedThisFrame));
            if (pressed)
                TrySkip();
        }

        private IEnumerator Run(InterludeDefinition definition)
        {
            // Arm the skip on its own clock, independent of the line cadence.
            StartCoroutine(ArmSkip());

            foreach (InterludeLine line in definition.Lines)
            {
                if (line.IsVoice)
                {
                    // The intrusion: a silence, extra space, then the voice grows in.
                    yield return new WaitForSecondsRealtime(voiceDelay);
                    var spacer = new GameObject("VoiceSpacer");
                    spacer.transform.SetParent(_column, false);
                    spacer.AddComponent<RectTransform>().sizeDelta = new Vector2(1f, 18f);
                    _spawned.Add(spacer);
                }

                Text text = MakeText(_column, line.Text, line.IsVoice ? 24 : 26,
                    line.IsVoice ? voiceColor : memoryColor,
                    line.IsVoice ? FontStyle.Italic : FontStyle.Normal);
                _spawned.Add(text.gameObject);
                SetAlpha(text, 0f);
                StartCoroutine(FadeIn(text, line.IsVoice ? voiceFadeDuration : lineFadeDuration));

                yield return new WaitForSecondsRealtime(lineStagger);
            }
            // Then hold: the screen waits for the (armed) skip input.
        }

        private IEnumerator ArmSkip()
        {
            yield return new WaitForSecondsRealtime(interludeSkipDelay);
            _skipArmed = true;
            StartCoroutine(FadeIn(_hint, 0.5f));
        }

        private IEnumerator FadeIn(Text text, float duration)
        {
            float t = 0f;
            while (t < duration && text != null)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(text, Mathf.Clamp01(t / duration));
                yield return null;
            }
        }

        private void Finish()
        {
            if (_run != null)
                StopAllCoroutines();
            _run = null;
            StartCoroutine(FadeOutAndClose());
        }

        private IEnumerator FadeOutAndClose()
        {
            var group = _panel.GetComponent<CanvasGroup>();
            if (group == null)
                group = _panel.AddComponent<CanvasGroup>();
            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
                yield return null;
            }
            _panel.SetActive(false);
            group.alpha = 1f;
            SetAlpha(_hint, 0f);
            Action? done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        private Text MakeText(Transform parent, string content, int size, Color color, FontStyle style)
        {
            var go = new GameObject("InterludeText");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            rect.sizeDelta = new Vector2(760f, size * 1.5f);
            return text;
        }

        private static void SetAlpha(Text text, float alpha)
        {
            Color c = text.color;
            c.a = alpha;
            text.color = c;
        }
    }
}
