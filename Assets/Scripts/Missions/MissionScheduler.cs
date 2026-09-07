using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARMIS.Missions
{
    /// <summary>
    /// FIFO queue of missions. Owns queue bookkeeping only — actual joint
    /// motion is driven by whatever consumes ActiveMission.CurrentAction
    /// (typically a MonoBehaviour that also talks to RobotStateMachine,
    /// IKManager and TrajectoryPlanner). Kept deliberately dumb to avoid a
    /// circular dependency between missions and motion execution.
    /// </summary>
    public class MissionScheduler : MonoBehaviour
    {
        readonly Queue<Mission> _queue = new Queue<Mission>();
        readonly List<Mission> _history = new List<Mission>();

        public Mission ActiveMission { get; private set; }
        public bool IsPaused { get; private set; }

        public int CompletedCount { get; private set; }
        public int FailedCount { get; private set; }

        public event Action<Mission> OnMissionStarted;
        public event Action<Mission> OnMissionCompleted;
        public event Action<Mission> OnMissionFailed;

        public void AddMission(Mission m)
        {
            m.status = MissionStatus.Queued;
            _queue.Enqueue(m);
            TryActivateNext();
        }

        public bool RemoveMission(string missionId)
        {
            if (ActiveMission != null && ActiveMission.id == missionId)
            {
                CancelActive();
                return true;
            }
            var remaining = new List<Mission>();
            bool removed = false;
            while (_queue.Count > 0)
            {
                var m = _queue.Dequeue();
                if (m.id == missionId) { removed = true; continue; }
                remaining.Add(m);
            }
            foreach (var m in remaining) _queue.Enqueue(m);
            return removed;
        }

        public void Pause() => IsPaused = true;
        public void Resume()
        {
            IsPaused = false;
            TryActivateNext();
        }

        public void CancelActive()
        {
            if (ActiveMission == null) return;
            ActiveMission.status = MissionStatus.Cancelled;
            _history.Add(ActiveMission);
            ActiveMission = null;
            TryActivateNext();
        }

        public void CompleteActive()
        {
            if (ActiveMission == null) return;
            ActiveMission.status = MissionStatus.Completed;
            CompletedCount++;
            _history.Add(ActiveMission);
            OnMissionCompleted?.Invoke(ActiveMission);
            ActiveMission = null;
            TryActivateNext();
        }

        public void FailActive(string reason)
        {
            if (ActiveMission == null) return;
            ActiveMission.status = MissionStatus.Failed;
            FailedCount++;
            _history.Add(ActiveMission);
            Debug.LogWarning($"[ARMIS] Mission {ActiveMission.name} failed: {reason}");
            OnMissionFailed?.Invoke(ActiveMission);
            ActiveMission = null;
            TryActivateNext();
        }

        public bool RetryLastFailed()
        {
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (_history[i].status == MissionStatus.Failed)
                {
                    var m = _history[i];
                    m.status = MissionStatus.Queued;
                    m.currentActionIndex = 0;
                    m.elapsedSeconds = 0f;
                    _queue.Enqueue(m);
                    TryActivateNext();
                    return true;
                }
            }
            return false;
        }

        void TryActivateNext()
        {
            if (IsPaused || ActiveMission != null || _queue.Count == 0) return;
            ActiveMission = _queue.Dequeue();
            ActiveMission.status = MissionStatus.Running;
            OnMissionStarted?.Invoke(ActiveMission);
        }

        public int QueueLength => _queue.Count;
    }
}
