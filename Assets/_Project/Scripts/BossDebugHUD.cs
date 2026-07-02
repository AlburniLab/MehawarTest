#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Debug-only IMGUI overlay for the boss: name, phase, HP bar. Top-right, visible only once
    /// the fight is engaged. Zero game logic, isolated and removable. NOT the final HUD.
    /// </summary>
    public sealed class BossDebugHUD : MonoBehaviour
    {
        [Header("Debug Overlay")]
        [SerializeField] private bool showDebug = true;
        [Tooltip("Overall scale of the overlay (matches the avatar HUD).")]
        [SerializeField, Range(1f, 4f)] private float uiScale = 2f;

        /// <summary>Boss to read; wired at runtime by the level builder.</summary>
        public BossController? Boss { get; set; }

        private Texture2D _pixel = null!;
        private GUIStyle? _label;
        private float _labelScale;

        private void Awake()
        {
            _pixel = new Texture2D(1, 1);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnDestroy()
        {
            if (_pixel != null)
                Destroy(_pixel);
        }

        private void OnGUI()
        {
            BossController? boss = Boss;
            if (!showDebug || boss == null || !boss.HudVisible)
                return;

            float s = Mathf.Max(1f, uiScale);
            if (_label == null || !Mathf.Approximately(_labelScale, s))
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13f * s), fontStyle = FontStyle.Bold };
                _labelScale = s;
            }

            float w = 240f * s, lineH = 18f * s, barH = 16f * s;
            float x = Screen.width - w - 18f;
            float y = 12f;

            DrawRect(new Rect(x - 6f, y - 6f, w + 12f, 78f * s), new Color(0f, 0f, 0f, 0.55f));

            GUI.color = new Color(1f, 0.6f, 0.5f);
            GUI.Label(new Rect(x, y, w, lineH), $"{boss.BossName} — fase {boss.PhaseNumber}: {boss.PhaseName}", _label);
            y += 22f * s;

            float frac = boss.HealthMax > 0 ? Mathf.Clamp01((float)boss.Health / boss.HealthMax) : 0f;
            DrawRect(new Rect(x, y, w, barH), new Color(0.15f, 0.15f, 0.15f, 1f));
            DrawRect(new Rect(x, y, w * frac, barH), new Color(0.85f, 0.20f, 0.15f));
            y += barH + 4f * s;

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, lineH), $"HP: {boss.Health} / {boss.HealthMax}", _label);
            GUI.color = Color.white;
        }

        private void DrawRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _pixel);
            GUI.color = prev;
        }
    }
}
