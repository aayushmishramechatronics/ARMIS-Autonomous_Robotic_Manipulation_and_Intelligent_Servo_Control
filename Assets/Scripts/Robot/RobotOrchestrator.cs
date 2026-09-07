using UnityEngine;
using ARMIS.IK;
using ARMIS.Motion;
using ARMIS.Missions;
using ARMIS.Hardware;
using ARMIS.Safety;

namespace ARMIS.Robot
{
    /// <summary>
    /// Drives the end-to-end pipeline:
    ///   Target/Mission action -> IK -> Constraint validation -> Trajectory
    ///   -> ArmController (calibration+direction) -> Transport -> Servos
    ///
    /// This class is not part of the file tree given in the original spec,
    /// which lists IK, Motion, Missions and Hardware as independent
    /// components but does not name the piece that sequences them. Without
    /// it, IKManager, TrajectoryPlanner, RobotStateMachine, MissionScheduler
    /// and ArmController are five correct but disconnected classes. This is
    /// that connective layer.
    ///
    /// DESIGN NOTE: the extended mission states (APPROACHING, PICKING,
    /// LIFTING, TRANSIT, PLACING, RETURNING) exist in RobotStateMachine's
    /// enum/transition table for future fine-grained use, but this
    /// orchestrator deliberately drives missions through the simpler core
    /// cycle (MOVING / AT_TARGET / GRIPPING / RELEASING / HOMING / IDLE).
    /// Routing every mission step through the extended states as well would
    /// require a second, denser transition table and materially raises
    /// deadlock risk for no behavioral gain — the action's human-readable
    /// label (e.g. "MOVE_ABOVE_PICK") already carries that granularity for
    /// telemetry via Mission.CurrentAction.label.
    /// </summary>
    public class RobotOrchestrator : MonoBehaviour
    {
        [Header("References")]
        public RobotConfiguration config;
        public IKManager ikManager;
        public TrajectoryPlanner trajectoryPlanner;
        public RobotStateMachine stateMachine;
        public MissionScheduler missionScheduler;
        public ArmController armController;
        public SafetyManager safetyManager;

        RobotCommand _currentCommand;
        float _waitTimer;
        bool _waiting;

        void Start()
        {
            _currentCommand = new RobotCommand(config.homeBase, config.homeShoulder, config.homeElbow, config.homeGripper);
            armController.SendJointCommand(_currentCommand);
            trajectoryPlanner.OnComplete += HandleTrajectoryComplete;
        }

        void Update()
        {
            trajectoryPlanner.Tick(Time.deltaTime);

            if (trajectoryPlanner.IsRunning)
            {
                armController.SendJointCommand(trajectoryPlanner.CurrentCommand);
                return;
            }

            if (_waiting)
            {
                _waitTimer -= Time.deltaTime;
                if (_waitTimer <= 0f) FinishCurrentMissionAction();
                return;
            }

            TryAdvanceMission();
        }

        // ---------------- Manual control (teleop / single-target moves) ----------------

        /// <summary>Attempts to move to a Cartesian target via IK. Returns false immediately if unreachable, joint-limited, or E-stopped — never silently ignored.</summary>
        public bool MoveToPosition(Vector3 targetWorld, float duration = -1f)
        {
            if (safetyManager != null && safetyManager.IsEStopped) return false;
            if (!stateMachine.Is(RobotState.IDLE, RobotState.AT_TARGET)) return false;

            var result = IKManager.Solve(targetWorld, config, true);
            if (!result.IsValid) return false;

            var target = result.ClampedAngles;
            target.Gripper = _currentCommand.Gripper; // MOVE_TO never changes gripper state
            return StartTrajectory(target, duration);
        }

        public bool MoveHome(float duration = -1f)
        {
            if (safetyManager != null && safetyManager.IsEStopped) return false;
            if (!GoTo(RobotState.HOMING)) return false;
            var home = new RobotCommand(config.homeBase, config.homeShoulder, config.homeElbow, config.homeGripper);
            return StartTrajectoryRaw(home, duration);
        }

        public bool Grip()
        {
            if (safetyManager != null && safetyManager.IsEStopped) return false;
            if (!GoTo(RobotState.GRIPPING)) return false;
            var target = _currentCommand;
            target.Gripper = config.gripperClosedAngle;
            return StartTrajectoryRaw(target, 0.4f);
        }

        public bool Release()
        {
            if (safetyManager != null && safetyManager.IsEStopped) return false;
            if (!GoTo(RobotState.RELEASING)) return false;
            var target = _currentCommand;
            target.Gripper = config.gripperOpenAngle;
            return StartTrajectoryRaw(target, 0.4f);
        }

        bool StartTrajectory(RobotCommand target, float duration)
        {
            if (!GoTo(RobotState.MOVING)) return false;
            return StartTrajectoryRaw(target, duration);
        }

        bool StartTrajectoryRaw(RobotCommand target, float duration)
        {
            float d = duration > 0f ? duration : config.defaultTrajectoryDuration;
            trajectoryPlanner.Play(_currentCommand, target, d);
            return true;
        }

        void HandleTrajectoryComplete()
        {
            _currentCommand = trajectoryPlanner.CurrentCommand;
            armController.SendJointCommand(_currentCommand);

            switch (stateMachine.Current)
            {
                case RobotState.MOVING: GoTo(RobotState.AT_TARGET); break;
                case RobotState.HOMING: GoTo(RobotState.IDLE); break;
                case RobotState.GRIPPING: GoTo(RobotState.AT_TARGET); break;
                case RobotState.RELEASING: GoTo(RobotState.AT_TARGET); break;
            }

            if (missionScheduler.ActiveMission != null)
                FinishCurrentMissionAction();
        }

        // ---------------- Mission execution ----------------

        void TryAdvanceMission()
        {
            var mission = missionScheduler.ActiveMission;
            if (mission == null || missionScheduler.IsPaused) return;
            if (safetyManager != null && safetyManager.IsEStopped) return;
            if (!stateMachine.Is(RobotState.IDLE, RobotState.AT_TARGET)) return;

            var action = mission.CurrentAction;
            if (action == null)
            {
                missionScheduler.CompleteActive();
                return;
            }
            if (action.status != MissionActionStatus.Pending) return;

            action.status = MissionActionStatus.Running;

            switch (action.type)
            {
                case MissionActionType.HOME:
                    if (!MoveHome()) FailMission("HOME failed");
                    break;
                case MissionActionType.MOVE_TO:
                    if (!MoveToPosition(action.targetPosition)) FailMission($"Unreachable target in action '{action.label}'");
                    break;
                case MissionActionType.GRIP:
                    if (!Grip()) FailMission("GRIP failed");
                    break;
                case MissionActionType.RELEASE:
                    if (!Release()) FailMission("RELEASE failed");
                    break;
                case MissionActionType.WAIT:
                    _waiting = true;
                    _waitTimer = action.waitSeconds;
                    break;
            }
        }

        void FinishCurrentMissionAction()
        {
            _waiting = false;
            var mission = missionScheduler.ActiveMission;
            if (mission == null) return;

            var action = mission.CurrentAction;
            if (action != null) action.status = MissionActionStatus.Completed;
            mission.currentActionIndex++;

            if (mission.IsComplete) missionScheduler.CompleteActive();
        }

        void FailMission(string reason)
        {
            _waiting = false;
            missionScheduler.FailActive(reason);
            GoTo(RobotState.IDLE);
        }

        // ---------------- State helper ----------------

        /// <summary>Tries the direct transition; if rejected from AT_TARGET, routes through IDLE first (AT_TARGET -> IDLE -> next is always legal). This is the one deliberate two-hop exception to the strict whitelist, used specifically so mission actions can chain freely after AT_TARGET.</summary>
        bool GoTo(RobotState next)
        {
            if (stateMachine.TryTransition(next)) return true;
            if (stateMachine.Current == RobotState.AT_TARGET && stateMachine.TryTransition(RobotState.IDLE))
                return stateMachine.TryTransition(next);
            return false;
        }
    }
}
