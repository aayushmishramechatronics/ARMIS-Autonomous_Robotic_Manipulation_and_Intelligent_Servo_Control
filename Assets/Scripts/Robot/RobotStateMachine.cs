using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARMIS.Robot
{
    public enum RobotState
    {
        IDLE, HOMING, MOVING, AT_TARGET, GRIPPING, RELEASING, PAUSED, STOPPED, FAULT,
        // Mission-level sub-states
        APPROACHING, PICKING, LIFTING, TRANSIT, PLACING, RETURNING
    }

    /// <summary>
    /// Explicit finite-state machine. Every transition is checked against a
    /// whitelist — invalid transitions are rejected and logged rather than
    /// silently applied, which is what prevents "everything lives in
    /// Update()" spaghetti and mission/E-stop race conditions.
    /// </summary>
    public class RobotStateMachine : MonoBehaviour
    {
        public RobotState Current { get; private set; } = RobotState.IDLE;
        public event Action<RobotState, RobotState> OnStateChanged;

        static readonly Dictionary<RobotState, RobotState[]> Allowed = new Dictionary<RobotState, RobotState[]>
        {
            { RobotState.IDLE,        new[] { RobotState.HOMING, RobotState.MOVING, RobotState.APPROACHING, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.HOMING,      new[] { RobotState.IDLE, RobotState.AT_TARGET, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.MOVING,      new[] { RobotState.AT_TARGET, RobotState.PAUSED, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.AT_TARGET,   new[] { RobotState.IDLE, RobotState.GRIPPING, RobotState.RELEASING, RobotState.MOVING, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.GRIPPING,    new[] { RobotState.AT_TARGET, RobotState.LIFTING, RobotState.PICKING, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.RELEASING,   new[] { RobotState.AT_TARGET, RobotState.RETURNING, RobotState.PLACING, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.PAUSED,      new[] { RobotState.MOVING, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.STOPPED,     new[] { RobotState.IDLE, RobotState.FAULT } },
            { RobotState.FAULT,       new[] { RobotState.IDLE } }, // requires explicit reset

            { RobotState.APPROACHING, new[] { RobotState.PICKING, RobotState.PAUSED, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.PICKING,     new[] { RobotState.LIFTING, RobotState.PAUSED, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.LIFTING,     new[] { RobotState.TRANSIT, RobotState.PAUSED, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.TRANSIT,     new[] { RobotState.PLACING, RobotState.PAUSED, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.PLACING,     new[] { RobotState.RETURNING, RobotState.PAUSED, RobotState.STOPPED, RobotState.FAULT } },
            { RobotState.RETURNING,   new[] { RobotState.IDLE, RobotState.PAUSED, RobotState.STOPPED, RobotState.FAULT } },
        };

        /// <summary>FAULT and STOPPED are reachable from ANY state — this is
        /// the safety override path used by SafetyManager and the serial
        /// watchdog, and it bypasses the whitelist above deliberately.</summary>
        public bool TryTransition(RobotState next)
        {
            if (next == RobotState.FAULT || next == RobotState.STOPPED)
            {
                Apply(next);
                return true;
            }

            if (Allowed.TryGetValue(Current, out var options) && Array.IndexOf(options, next) >= 0)
            {
                Apply(next);
                return true;
            }

            Debug.LogWarning($"[ARMIS] Rejected transition {Current} -> {next}");
            return false;
        }

        void Apply(RobotState next)
        {
            var prev = Current;
            Current = next;
            OnStateChanged?.Invoke(prev, next);
        }

        public bool Is(params RobotState[] states) => Array.IndexOf(states, Current) >= 0;
    }
}
