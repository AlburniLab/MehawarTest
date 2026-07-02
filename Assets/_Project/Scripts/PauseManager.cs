#nullable enable
using System;
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Central owner of the PAUSE state. Pausing SAVES the current Time.timeScale and restores
    /// exactly that value on resume — never a hardcoded 1 — so a pause landing in the middle of
    /// a hitstop freeze resumes into the same freeze (Hitstop listens to Paused/Resumed to shift
    /// its unscaled-clock deadline by the pause duration). UI/flow (panel, input maps) stays in
    /// GameFlow; only time ownership lives here.
    /// </summary>
    public static class PauseManager
    {
        public static bool IsPaused { get; private set; }

        /// <summary>Raised right after time freezes / resumes (Hitstop shifts its deadline here).</summary>
        public static event Action? Paused;
        public static event Action? Resumed;

        private static float _savedTimeScale = 1f;

        public static void SetPaused(bool paused)
        {
            if (IsPaused == paused)
                return;
            IsPaused = paused;
            if (paused)
            {
                _savedTimeScale = Time.timeScale;   // may be a live hitstop scale: restored as-is
                Time.timeScale = 0f;
                Paused?.Invoke();
            }
            else
            {
                Time.timeScale = _savedTimeScale;
                Resumed?.Invoke();
            }
        }

        /// <summary>Teardown safety (play-mode exit): never leave the editor with a frozen clock.</summary>
        public static void ForceReset()
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }
}
