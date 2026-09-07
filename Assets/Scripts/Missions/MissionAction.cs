using System;
using UnityEngine;

namespace ARMIS.Missions
{
    public enum MissionActionType { HOME, MOVE_TO, GRIP, RELEASE, WAIT }

    public enum MissionActionStatus { Pending, Running, Completed, Failed }

    /// <summary>
    /// A single reusable action primitive. Missions are built by composing
    /// these rather than hardcoding a pick-and-place sequence — e.g.
    /// "approach", "move to pick", "lift" are all just MOVE_TO actions with
    /// different target positions, so the same abstraction covers HOME,
    /// arbitrary multi-point pick-and-place, and inspection sweeps alike.
    /// </summary>
    [Serializable]
    public class MissionAction
    {
        public MissionActionType type;
        public Vector3 targetPosition;   // used by MOVE_TO
        public float waitSeconds;        // used by WAIT
        public string label;             // human-readable, e.g. "MOVE_ABOVE_PICK"

        [NonSerialized] public MissionActionStatus status = MissionActionStatus.Pending;

        public static MissionAction Home(string label = "HOME") =>
            new MissionAction { type = MissionActionType.HOME, label = label };

        public static MissionAction MoveTo(Vector3 pos, string label = "MOVE_TO") =>
            new MissionAction { type = MissionActionType.MOVE_TO, targetPosition = pos, label = label };

        public static MissionAction Grip(string label = "GRIP") =>
            new MissionAction { type = MissionActionType.GRIP, label = label };

        public static MissionAction Release(string label = "RELEASE") =>
            new MissionAction { type = MissionActionType.RELEASE, label = label };

        public static MissionAction Wait(float seconds, string label = "WAIT") =>
            new MissionAction { type = MissionActionType.WAIT, waitSeconds = seconds, label = label };
    }
}
