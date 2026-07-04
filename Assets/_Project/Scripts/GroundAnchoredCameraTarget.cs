#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Camera follow target that keeps the CURRENT floor line glued near the bottom
    /// of the frame (PO framing decision, 2026-07-04): X tracks the player; Y anchors
    /// to the last grounded line, so jumps never scroll the camera — it re-anchors
    /// only on landing at a new height, and dips just enough to keep the player in
    /// view when he sinks below the floor line (pits). The vcam damping smooths the
    /// anchor changes; this component itself never lerps.
    /// </summary>
    public sealed class GroundAnchoredCameraTarget : MonoBehaviour
    {
        [Header("Framing — world units")]
        [Tooltip("Gap between the floor line and the bottom edge of the viewport.")]
        [SerializeField] private float groundMargin = 0.5f;
        [Tooltip("Min space kept under the player's feet when below the floor line (pits).")]
        [SerializeField] private float pitVisibilityMargin = 0.5f;

        private Transform? _player;
        private GroundSensor? _sensor;
        private float _halfHeight;
        private float _orthoSize;
        private float _groundLine;

        /// <summary>Wire the avatar and camera size (called by the level builder).</summary>
        public void Initialize(PlayerMovement player, float orthoSize)
        {
            _player = player.transform;
            _sensor = player.GetComponent<GroundSensor>();
            _halfHeight = player.transform.localScale.y * 0.5f;
            _orthoSize = orthoSize;
            _groundLine = _player.position.y - _halfHeight;
            Track();
        }

        private void LateUpdate() => Track();

        private void Track()
        {
            if (_player == null)
                return;

            if (_sensor != null && _sensor.IsGrounded)
                _groundLine = _player.position.y - _halfHeight;

            // Anchor: floor line sits groundMargin above the viewport bottom.
            float anchorY = _groundLine + _orthoSize - groundMargin;
            // Pit clamp: never frame the player out below the bottom edge.
            float keepVisibleY = _player.position.y + _orthoSize - _halfHeight - pitVisibilityMargin;
            transform.position =
                new Vector3(_player.position.x, Mathf.Min(anchorY, keepVisibleY), 0f);
        }
    }
}
