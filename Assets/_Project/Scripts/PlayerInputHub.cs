#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Single owner of the avatar's PlayerControls instance (lifecycle + enable state).
    /// Movement/combat SUBSCRIBE through <see cref="Controls"/> and never enable/disable/dispose
    /// the asset themselves — components can then toggle independently (hitstun, death) without
    /// fighting over shared input state, and pause switches gameplay input off in ONE place.
    /// </summary>
    public sealed class PlayerInputHub : MonoBehaviour
    {
        private PlayerControls? _controls;

        /// <summary>The shared controls instance (created on first access).</summary>
        public PlayerControls Controls => _controls ??= new PlayerControls();

        private void OnEnable() => Controls.Enable();

        private void OnDisable() => _controls?.Disable();

        private void OnDestroy()
        {
            _controls?.Dispose();
            _controls = null;
        }

        /// <summary>Toggle the gameplay action map (used by the pause menu; UI input lives elsewhere).</summary>
        public void SetGameplayEnabled(bool enabled)
        {
            if (enabled)
                Controls.Player.Enable();
            else
                Controls.Player.Disable();
        }
    }
}
