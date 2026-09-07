using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ARMIS.Robot;

namespace ARMIS.Motion
{
    [Serializable]
    public class Waypoint
    {
        public float baseAngle;
        public float shoulderAngle;
        public float elbowAngle;
        public float gripperAngle;
        public float dwellSeconds;

        public RobotCommand ToCommand() => new RobotCommand(baseAngle, shoulderAngle, elbowAngle, gripperAngle);

        public static Waypoint FromCommand(RobotCommand cmd, float dwell = 0f) => new Waypoint
        {
            baseAngle = cmd.Base,
            shoulderAngle = cmd.Shoulder,
            elbowAngle = cmd.Elbow,
            gripperAngle = cmd.Gripper,
            dwellSeconds = dwell
        };
    }

    [Serializable]
    public class WaypointSequence
    {
        public string sequenceName = "Untitled Sequence";
        public List<Waypoint> waypoints = new List<Waypoint>();
    }

    /// <summary>
    /// Teach-and-repeat: records the arm's current commanded pose into a
    /// WaypointSequence and can play it back. Playback replays stored
    /// logical angles directly — it does NOT re-solve IK unless
    /// replaySolvesIK is explicitly enabled and a target source is supplied
    /// by the caller (kept out of scope here to avoid a hidden dependency
    /// on IKManager from a motion-recording class).
    /// </summary>
    public class WaypointRecorder : MonoBehaviour
    {
        public WaypointSequence sequence = new WaypointSequence();
        public bool IsRecording { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsPaused { get; private set; }
        public int PlaybackIndex { get; private set; }

        public void StartRecording() => IsRecording = true;
        public void StopRecording() => IsRecording = false;

        public void AddWaypoint(RobotCommand current, float dwellSeconds = 0f)
        {
            sequence.waypoints.Add(Waypoint.FromCommand(current, dwellSeconds));
        }

        public void DeleteWaypoint(int index)
        {
            if (index >= 0 && index < sequence.waypoints.Count)
                sequence.waypoints.RemoveAt(index);
        }

        public void ClearWaypoints() => sequence.waypoints.Clear();

        public void PlaySequence()
        {
            if (sequence.waypoints.Count == 0) return;
            IsPlaying = true;
            IsPaused = false;
            PlaybackIndex = 0;
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        public void StopPlayback()
        {
            IsPlaying = false;
            IsPaused = false;
            PlaybackIndex = 0;
        }

        /// <summary>Advances playback index. Caller (e.g. RobotStateMachine)
        /// drives actual motion via TrajectoryPlanner using the returned waypoint.</summary>
        public Waypoint AdvancePlayback()
        {
            if (!IsPlaying || IsPaused) return null;
            if (PlaybackIndex >= sequence.waypoints.Count)
            {
                StopPlayback();
                return null;
            }
            return sequence.waypoints[PlaybackIndex++];
        }

        public void SaveToFile(string path)
        {
            string json = JsonUtility.ToJson(sequence, true);
            File.WriteAllText(path, json);
        }

        public void LoadFromFile(string path)
        {
            if (!File.Exists(path)) return;
            string json = File.ReadAllText(path);
            sequence = JsonUtility.FromJson<WaypointSequence>(json);
        }
    }
}
