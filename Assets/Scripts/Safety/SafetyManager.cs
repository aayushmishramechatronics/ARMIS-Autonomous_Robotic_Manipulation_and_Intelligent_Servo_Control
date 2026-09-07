using UnityEngine;
using ARMIS.Robot;
using ARMIS.Motion;
using ARMIS.Missions;
using ARMIS.Hardware;

namespace ARMIS.Safety
{
    /// <summary>
    /// Software emergency stop and comms-health monitor.
    ///
    /// IMPORTANT: this is a SOFTWARE E-stop. It stops trajectories, missions
    /// and outgoing commands from the Unity side. It cannot cut servo power
    /// and cannot protect against a hung/crashed microcontroller. A real
    /// deployment must wire a physical E-stop that interrupts servo power
    /// directly, independent of any software state. Do not present this
    /// class as equivalent to a hardware safety circuit.
    /// </summary>
    public class SafetyManager : MonoBehaviour
    {
        [Header("References")]
        public RobotStateMachine stateMachine;
        public TrajectoryPlanner trajectoryPlanner;
        public MissionScheduler missionScheduler;
        public ArmController armController;

        [Header("Comms Watchdog (Unity-side awareness only — authoritative watchdog is on the Arduino)")]
        public bool monitorCommsHealth = true;
        public float commsTimeoutSeconds = 1.0f;

        public bool IsEStopped { get; private set; }

        void Update()
        {
            if (monitorCommsHealth && armController != null && armController.mode == OperatingMode.Hardware)
            {
                if (armController.TimeSinceLastSuccessfulSend > commsTimeoutSeconds && !IsEStopped)
                {
                    TriggerEStop("Communication timeout");
                }
            }
        }

        public void TriggerEStop(string reason)
        {
            IsEStopped = true;
            trajectoryPlanner?.Cancel();
            if (missionScheduler != null && missionScheduler.ActiveMission != null)
                missionScheduler.FailActive($"E-STOP: {reason}");
            stateMachine?.TryTransition(RobotState.FAULT);
            Debug.LogWarning($"[ARMIS] EMERGENCY STOP triggered: {reason}");
        }

        /// <summary>Explicit operator action required — the state machine
        /// will not leave FAULT on its own.</summary>
        public bool Reset()
        {
            if (!IsEStopped) return true;
            IsEStopped = false;
            return stateMachine != null && stateMachine.TryTransition(RobotState.IDLE);
        }
    }
}
