#nullable enable
using UnityEngine;

namespace Mehawar.Greybox
{
    /// <summary>
    /// Single owner of combat hitstop (CLAUDE.md: Time.timeScale handled only from code).
    /// Every freeze funnels through <see cref="Request"/>: concurrent requests EXTEND the current
    /// freeze (latest end wins, lowest scale wins) instead of stacking coroutines, and the restore
    /// is driven by unscaled time on a dedicated runner — guaranteed even if the requester is
    /// disabled mid-freeze and on teardown. Restore goes back to the timeScale captured BEFORE the
    /// freeze (never a hardcoded 1). PAUSE coexistence: while PauseManager owns the clock the
    /// runner idles, and the freeze deadline is shifted by the pause duration on resume, so a
    /// paused hitstop continues exactly where it stopped.
    /// </summary>
    public static class Hitstop
    {
        private static HitstopRunner? _runner;

        /// <summary>Freeze time for <paramref name="duration"/> real seconds. Safe to call many times per frame.</summary>
        public static void Request(float duration, float timeScale)
        {
            if (duration <= 0f || PauseManager.IsPaused || !Application.isPlaying)
                return;

            if (_runner == null)
                _runner = new GameObject("Hitstop (runtime)").AddComponent<HitstopRunner>();

            _runner.Extend(duration, Mathf.Clamp01(timeScale));
        }
    }

    /// <summary>Scene-local ticker for <see cref="Hitstop"/>. Restores timeScale in LateUpdate and OnDestroy.</summary>
    internal sealed class HitstopRunner : MonoBehaviour
    {
        private float _endUnscaledTime;
        private float _remainingAtPause;
        private float _preFreezeScale = 1f;
        private bool _frozen;

        private void OnEnable()
        {
            PauseManager.Paused += OnPaused;
            PauseManager.Resumed += OnResumed;
        }

        private void OnDisable()
        {
            PauseManager.Paused -= OnPaused;
            PauseManager.Resumed -= OnResumed;
        }

        public void Extend(float duration, float timeScale)
        {
            float end = Time.unscaledTime + duration;
            if (_frozen)
            {
                _endUnscaledTime = Mathf.Max(_endUnscaledTime, end);
                Time.timeScale = Mathf.Min(Time.timeScale, timeScale);
            }
            else
            {
                _frozen = true;
                _preFreezeScale = Time.timeScale;   // captured, not assumed: restore returns HERE
                _endUnscaledTime = end;
                Time.timeScale = timeScale;
            }
        }

        private void OnPaused()
        {
            // The unscaled clock keeps running under pause: park the remaining freeze time.
            if (_frozen)
                _remainingAtPause = Mathf.Max(0f, _endUnscaledTime - Time.unscaledTime);
        }

        private void OnResumed()
        {
            if (_frozen)
                _endUnscaledTime = Time.unscaledTime + _remainingAtPause;
        }

        private void LateUpdate()
        {
            if (PauseManager.IsPaused)
                return;   // pause owns the clock; the deadline is parked
            if (_frozen && Time.unscaledTime >= _endUnscaledTime)
                Restore();
        }

        private void Restore()
        {
            _frozen = false;
            Time.timeScale = _preFreezeScale;
        }

        private void OnDestroy()
        {
            if (_frozen && !PauseManager.IsPaused)
                Restore();
        }
    }
}
