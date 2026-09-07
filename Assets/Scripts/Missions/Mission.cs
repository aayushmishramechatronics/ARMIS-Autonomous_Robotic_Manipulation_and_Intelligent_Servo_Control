using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARMIS.Missions
{
    public enum MissionStatus { Queued, Running, Completed, Failed, Cancelled }

    [Serializable]
    public class Mission
    {
        public string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string name = "Untitled Mission";
        public List<MissionAction> actions = new List<MissionAction>();

        [NonSerialized] public MissionStatus status = MissionStatus.Queued;
        [NonSerialized] public int currentActionIndex = 0;
        [NonSerialized] public float elapsedSeconds = 0f;

        public MissionAction CurrentAction =>
            (currentActionIndex >= 0 && currentActionIndex < actions.Count) ? actions[currentActionIndex] : null;

        public bool IsComplete => currentActionIndex >= actions.Count;

        /// <summary>
        /// Builds the standard pick-and-place workflow:
        /// HOME -> MOVE_ABOVE_PICK -> MOVE_TO_PICK -> GRIP -> LIFT ->
        /// MOVE_TO_PLACE -> RELEASE -> RETURN_HOME
        /// pickPos/placePos are manually specified positions (no object
        /// detection in the core implementation, per spec).
        /// </summary>
        public static Mission CreatePickAndPlace(string name, Vector3 pickPos, Vector3 placePos, float approachHeight, float homeHeightHint = 0f)
        {
            var m = new Mission { name = name };
            Vector3 abovePick = pickPos + Vector3.up * approachHeight;
            Vector3 abovePlace = placePos + Vector3.up * approachHeight;

            m.actions.Add(MissionAction.Home("HOME"));
            m.actions.Add(MissionAction.MoveTo(abovePick, "MOVE_ABOVE_PICK"));
            m.actions.Add(MissionAction.MoveTo(pickPos, "MOVE_TO_PICK"));
            m.actions.Add(MissionAction.Grip("GRIP"));
            m.actions.Add(MissionAction.MoveTo(abovePick, "LIFT"));
            m.actions.Add(MissionAction.MoveTo(abovePlace, "MOVE_TO_PLACE_ABOVE"));
            m.actions.Add(MissionAction.MoveTo(placePos, "MOVE_TO_PLACE"));
            m.actions.Add(MissionAction.Release("RELEASE"));
            m.actions.Add(MissionAction.Home("RETURN_HOME"));
            return m;
        }
    }
}
