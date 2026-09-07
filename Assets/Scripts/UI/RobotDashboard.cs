using UnityEngine;
using UnityEngine.UI;
using ARMIS.IK;
using ARMIS.Robot;
using ARMIS.Missions;
using ARMIS.Hardware;
using ARMIS.Safety;

namespace ARMIS.UI
{
    /// <summary>
    /// Read-only telemetry surface. Uses standard UnityEngine.UI.Text so it
    /// works in a stock 2D UGUI canvas with no extra packages. All fields
    /// remain populated (with SIMULATION-appropriate values) even if the
    /// Arduino is disconnected, per spec.
    /// </summary>
    public class RobotDashboard : MonoBehaviour
    {
        [Header("References")]
        public ArmController armController;
        public RobotStateMachine stateMachine;
        public IKManager ikManager;
        public MissionScheduler missionScheduler;
        public SafetyManager safetyManager;

        [Header("Connection UI")]
        public Text connectionStatusText;
        public Text commandFrequencyText;
        public Text timeSinceLastTxText;

        [Header("Robot UI")]
        public Text robotStateText;
        public Text jointAnglesText;

        [Header("IK UI")]
        public Text ikTargetText;
        public Text ikFKText;
        public Text ikErrorText;
        public Text workspaceStatusText;
        public Text jointLimitStatusText;

        [Header("Mission UI")]
        public Text missionNameText;
        public Text missionActionText;
        public Text missionCountsText;

        float _missionElapsedAccumulator;

        void Update()
        {
            DrawConnection();
            DrawRobotState();
            DrawIK();
            DrawMission();
        }

        void DrawConnection()
        {
            if (armController == null) return;

            if (connectionStatusText != null)
                connectionStatusText.text = $"Serial: {(armController.IsConnected ? "CONNECTED" : "DISCONNECTED")}\n{armController.StatusText}";

            if (commandFrequencyText != null && armController.config != null)
                commandFrequencyText.text = $"Rate: {armController.config.commandRateHz:F0} Hz (target)";

            if (timeSinceLastTxText != null)
            {
                float t = armController.TimeSinceLastSuccessfulSend;
                timeSinceLastTxText.text = $"Last TX: {(float.IsInfinity(t) ? "n/a" : t.ToString("F2") + "s ago")}";
            }
        }

        void DrawRobotState()
        {
            if (stateMachine != null && robotStateText != null)
            {
                string faultTag = (safetyManager != null && safetyManager.IsEStopped) ? "  [E-STOP ACTIVE]" : "";
                robotStateText.text = $"STATE: {stateMachine.Current}{faultTag}";
            }

            if (armController != null && jointAnglesText != null)
            {
                var c = armController.LastLogicalCommand;
                jointAnglesText.text = $"Base: {c.Base:F1}\u00B0\nShoulder: {c.Shoulder:F1}\u00B0\nElbow: {c.Elbow:F1}\u00B0\nGripper: {c.Gripper:F1}\u00B0";
            }
        }

        void DrawIK()
        {
            if (ikManager == null || ikManager.LastResult == null) return;
            var r = ikManager.LastResult;

            if (ikTargetText != null)
                ikTargetText.text = $"Target: ({r.TargetPosition.x:F3}, {r.TargetPosition.y:F3}, {r.TargetPosition.z:F3})";

            if (ikFKText != null)
                ikFKText.text = r.HasFKVerification
                    ? $"FK: ({r.FKPosition.x:F3}, {r.FKPosition.y:F3}, {r.FKPosition.z:F3})"
                    : "FK: n/a";

            if (ikErrorText != null)
                ikErrorText.text = r.HasFKVerification ? $"Position Error: {r.PositionErrorEuclidean:F4}" : "Position Error: n/a";

            if (workspaceStatusText != null)
                workspaceStatusText.text = $"TARGET: {(r.IsValid ? "REACHABLE" : "UNREACHABLE")}  |  WORKSPACE: {(r.Workspace == WorkspaceStatus.Unreachable ? "INVALID" : "VALID")}{(r.Workspace == WorkspaceStatus.NearSingular ? " (near singular)" : "")}";

            if (jointLimitStatusText != null)
                jointLimitStatusText.text = $"JOINT LIMITS: {(r.JointLimits == JointLimitStatus.Ok ? "OK" : "VIOLATION")}";
        }

        void DrawMission()
        {
            if (missionScheduler == null) return;

            var active = missionScheduler.ActiveMission;
            if (missionNameText != null)
                missionNameText.text = active != null ? $"Mission: {active.name} ({active.status})" : "Mission: (none active)";

            if (missionActionText != null)
                missionActionText.text = active?.CurrentAction != null ? $"Action: {active.CurrentAction.label}" : "Action: -";

            if (missionCountsText != null)
                missionCountsText.text = $"Completed: {missionScheduler.CompletedCount}  Failed: {missionScheduler.FailedCount}  Queued: {missionScheduler.QueueLength}";
        }
    }
}
