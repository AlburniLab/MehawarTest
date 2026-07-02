#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Debug-only IMGUI overlay reading the avatar state through IAvatarResource: HP + death
    /// countdown, resource bar with threshold marker, empowered line (FURIA/BASTIONE) and a
    /// context line — each resource describes itself, the HUD stays generic. Zero game logic,
    /// isolated and removable. NOT the final HUD. All text Italian.
    /// </summary>
    public sealed class AvatarDebugHUD : MonoBehaviour
    {
        [Header("Debug Overlay")]
        [Tooltip("Toggle the whole overlay on/off.")]
        [SerializeField] private bool showDebug = true;
        [Tooltip("Overall scale of the overlay (2 = double size, for readability).")]
        [SerializeField, Range(1f, 4f)] private float uiScale = 2f;

        /// <summary>Signature resource to read; wired at runtime by the bootstrap.</summary>
        public IAvatarResource? Resource { get; set; }

        /// <summary>Player health to read; wired at runtime by the bootstrap.</summary>
        public PlayerHealth? Health { get; set; }

        /// <summary>Avatar name shown as the panel title (from the chosen campaign).</summary>
        public string ActorName { get; set; } = "Player";

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
            // Runtime-created texture is not GC'd by Unity: release it explicitly.
            if (_pixel != null)
                Destroy(_pixel);
        }

        private void OnGUI()
        {
            IAvatarResource? res = Resource;
            if (!showDebug || res == null)
                return;

            float s = Mathf.Max(1f, uiScale);
            if (_label == null || !Mathf.Approximately(_labelScale, s))
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13f * s), fontStyle = FontStyle.Bold };
                _labelScale = s;
            }

            float norm = res.Max > 0f ? Mathf.Clamp01(res.Current / res.Max) : 0f;
            float x = 12f, w = 260f * s, lineH = 18f * s;
            float y = 12f;

            DrawRect(new Rect(x - 6f, y - 6f, w + 12f, 148f * s), new Color(0f, 0f, 0f, 0.55f));

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, lineH), ActorName + " (debug)", _label);
            y += 22f * s;

            // HP line (numbers only, same philosophy as the rest of the overlay).
            if (Health != null)
            {
                if (Health.IsDead)
                {
                    GUI.color = new Color(1f, 0.30f, 0.30f);
                    GUI.Label(new Rect(x, y, w, lineH),
                        $"HP: MORTO — respawn tra {Health.RespawnTimeLeft:0.0}s", _label);
                }
                else
                {
                    float frac = Health.Max > 0 ? (float)Health.Current / Health.Max : 0f;
                    GUI.color = frac > 0.5f ? new Color(0.6f, 0.9f, 0.6f)
                              : frac > 0.25f ? new Color(1f, 0.7f, 0.3f)
                              : new Color(1f, 0.35f, 0.35f);
                    GUI.Label(new Rect(x, y, w, lineH), $"HP: {Health.Current} / {Health.Max}", _label);
                }
                y += lineH;
            }

            // Resource bar + threshold marker. Filled color follows the empowered aura.
            float barH = 20f * s;
            DrawRect(new Rect(x, y, w, barH), new Color(0.15f, 0.15f, 0.15f, 1f));
            DrawRect(new Rect(x, y, w * norm, barH),
                res.IsEmpowered ? res.AuraColor : new Color(0.80f, 0.35f, 0.20f));
            float markerX = x + w * (res.Max > 0f ? Mathf.Clamp01(res.Threshold / res.Max) : 0f);
            DrawRect(new Rect(markerX - s, y - 2f * s, 2f * s, barH + 4f * s), Color.yellow);
            y += barH + 6f * s;

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, lineH),
                $"{res.DisplayName}: {res.Current:0} / {res.Max:0}   (soglia {res.Threshold:0})", _label);
            y += lineH;

            GUI.color = res.IsEmpowered ? new Color(1f, 0.75f, 0.45f) : new Color(0.75f, 0.75f, 0.75f);
            GUI.Label(new Rect(x, y, w, lineH), res.EmpoweredLine, _label);
            y += lineH;

            GUI.color = new Color(0.65f, 0.80f, 0.75f);
            GUI.Label(new Rect(x, y, w, lineH), res.StatusLine, _label);

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
