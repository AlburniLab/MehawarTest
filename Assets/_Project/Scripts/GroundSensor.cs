#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Shared ground check for the player. Uses a downward OverlapBox at the feet,
    /// restricted to a dedicated "Ground" LayerMask (never "Everything").
    /// Runs early (negative execution order) so movement reads a fresh result.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class GroundSensor : MonoBehaviour
    {
        [Header("Ground Detection")]
        [Tooltip("Dedicated Ground layer(s). Must NOT be set to Everything.")]
        [SerializeField] private LayerMask groundMask;

        [Tooltip("Vertical thickness of the feet probe, in Unity units (PPU 16).")]
        [SerializeField] private float castDistance = 0.06f;

        [Tooltip("Horizontal shrink of the feet probe to avoid catching side walls.")]
        [SerializeField] private float skinWidth = 0.04f;

        [Tooltip("Body collider used to derive the feet position. Auto-found if null.")]
        [SerializeField] private BoxCollider2D? bodyCollider;

        /// <summary>True while the feet probe overlaps a Ground collider.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>Exposed so a bootstrap can wire the dedicated Ground mask at runtime.</summary>
        public LayerMask GroundMask
        {
            get => groundMask;
            set => groundMask = value;
        }

        private void Awake()
        {
            if (bodyCollider == null)
                bodyCollider = GetComponent<BoxCollider2D>();
        }

        private void FixedUpdate()
        {
            IsGrounded = Probe();
        }

        private bool Probe()
        {
            if (bodyCollider == null)
                return false;

            Bounds b = bodyCollider.bounds;
            Vector2 feetCenter = new Vector2(b.center.x, b.min.y);
            Vector2 feetSize = new Vector2(Mathf.Max(0.01f, b.size.x - skinWidth), castDistance);
            return Physics2D.OverlapBox(feetCenter, feetSize, 0f, groundMask) != null;
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider2D? col = bodyCollider != null ? bodyCollider : GetComponent<BoxCollider2D>();
            if (col == null)
                return;

            Bounds b = col.bounds;
            Vector2 feetCenter = new Vector2(b.center.x, b.min.y);
            Vector2 feetSize = new Vector2(Mathf.Max(0.01f, b.size.x - skinWidth), castDistance);
            Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(feetCenter, feetSize);
        }
    }
}
