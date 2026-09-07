using System;
using UnityEngine;
using ARMIS.Robot;

namespace ARMIS.Motion
{
    /// <summary>
    /// Generates a smooth, jointly-synchronized trajectory between two
    /// RobotCommands using a quintic smootherstep function
    /// (s(t) = 6t^5 - 15t^4 + 10t^3), which has zero velocity AND zero
    /// acceleration at both endpoints — no abrupt starts/stops, no jerk
    /// discontinuity at the boundary. All four joints share a single time
    /// parameter t, so they always arrive together by construction.
    /// </summary>
    public class TrajectoryPlanner : MonoBehaviour
    {
        public bool IsRunning { get; private set; }
        public RobotCommand CurrentCommand { get; private set; }
        public float Progress01 { get; private set; }

        public event Action OnComplete;
        public event Action OnCancelled;

        RobotCommand _start;
        RobotCommand _end;
        float _duration;
        float _elapsed;

        public void Play(RobotCommand start, RobotCommand end, float durationSeconds)
        {
            _start = start;
            _end = end;
            _duration = Mathf.Max(0.001f, durationSeconds);
            _elapsed = 0f;
            Progress01 = 0f;
            CurrentCommand = start;
            IsRunning = true;
        }

        public void Cancel()
        {
            if (!IsRunning) return;
            IsRunning = false;
            OnCancelled?.Invoke();
        }

        /// <summary>Call once per frame (from RobotStateMachine or a controller) while IsRunning.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsRunning) return;

            _elapsed += deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            float s = QuinticSmootherstep(t);

            CurrentCommand = RobotCommand.Lerp(_start, _end, s);
            Progress01 = t;

            if (t >= 1f)
            {
                IsRunning = false;
                CurrentCommand = _end;
                OnComplete?.Invoke();
            }
        }

        public static float QuinticSmootherstep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }
}
