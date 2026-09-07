using UnityEngine;

namespace ARMIS.Robot
{
    public enum ServoDirection { Normal, Reversed }

    /// <summary>
    /// Single source of truth for arm geometry, joint limits, calibration,
    /// servo direction mapping, home pose, and communication settings.
    ///
    /// COORDINATE CONVENTION (do not change without updating IKManager,
    /// ForwardKinematics and the README in lockstep):
    ///   - World space is right-handed Unity space, Y = up.
    ///   - BaseAngle: rotation of the arm plane about the world Y axis,
    ///     measured as atan2(Tz, Tx), in degrees. 0 deg = +X axis.
    ///   - ShoulderAngle: elevation angle of link1 (L1) above the horizontal
    ///     plane at height L0, measured in the arm's vertical plane.
    ///   - ElbowAngle: INTERIOR angle at the elbow joint between link1 and
    ///     link2. 0 deg = fully extended (straight arm), up to 180 deg =
    ///     fully folded back onto link1. This is NOT an absolute angle.
    ///   - GripperAngle: logical open/close angle, independent of position.
    ///
    /// All angles stored on RobotCommand/IKResult are LOGICAL angles in
    /// degrees. Calibration offsets and servo direction are applied only in
    /// the Hardware layer (ArmController), never inside IK math.
    /// </summary>
    [CreateAssetMenu(fileName = "RobotConfiguration", menuName = "ARMIS/Robot Configuration")]
    public class RobotConfiguration : ScriptableObject
    {
        [Header("Arm Geometry (project units, consistent everywhere)")]
        public float L0 = 0.05f; // base-to-shoulder vertical offset
        public float L1 = 0.10f; // shoulder-to-elbow
        public float L2 = 0.10f; // elbow-to-gripper

        [Header("Workspace Safety")]
        [Tooltip("Subtracted from the theoretical max reach (L1+L2) and added to the theoretical min reach (|L1-L2|) before a target is accepted as reachable.")]
        public float workspaceMargin = 0.005f;

        [Tooltip("Distance (as a fraction of max reach) inside which a target is flagged NearSingular even if technically reachable.")]
        [Range(0f, 0.2f)]
        public float singularityBand = 0.03f;

        [Header("Logical Joint Limits (degrees)")]
        public float baseMin = -90f;
        public float baseMax = 90f;
        public float shoulderMin = 0f;
        public float shoulderMax = 150f;
        public float elbowMin = 10f;
        public float elbowMax = 170f;
        public float gripperMin = 0f;
        public float gripperMax = 60f;

        [Header("Gripper Presets (logical degrees)")]
        public float gripperOpenAngle = 0f;
        public float gripperClosedAngle = 60f;

        [Header("Physical Servo Limits (degrees, hard clamp applied post-calibration)")]
        [Tooltip("MUST be mirrored manually in ARMIS_Controller.ino JOINT_MIN/JOINT_MAX arrays. Unity and Arduino do not share this file — keep them in sync by hand.")]
        public float servoMin = 0f;
        public float servoMax = 180f;

        [Header("Calibration Offsets (degrees, added to logical angle to get physical angle)")]
        public float baseCalibrationOffset = 0f;
        public float shoulderCalibrationOffset = 0f;
        public float elbowCalibrationOffset = 0f;
        public float gripperCalibrationOffset = 0f;

        [Header("Servo Direction Mapping")]
        public ServoDirection baseDirection = ServoDirection.Normal;
        public ServoDirection shoulderDirection = ServoDirection.Normal;
        public ServoDirection elbowDirection = ServoDirection.Normal;
        public ServoDirection gripperDirection = ServoDirection.Normal;

        [Header("Home Pose (logical degrees)")]
        public float homeBase = 0f;
        public float homeShoulder = 90f;
        public float homeElbow = 90f;
        public float homeGripper = 0f;

        [Header("Trajectory")]
        public float defaultTrajectoryDuration = 1.5f;

        [Header("Serial Communication")]
        public string serialPortName = "COM3";
        public int baudRate = 115200;
        [Tooltip("Target command transmission rate. The transport layer rate-limits to this value.")]
        public float commandRateHz = 20f;

        [Header("Watchdog")]
        [Tooltip("Reference value only — the authoritative timeout lives in ARMIS_Controller.ino as WATCHDOG_TIMEOUT_MS. Keep both in sync.")]
        public float watchdogTimeoutMs = 500f;

        public float MaxReach => L1 + L2;
        public float MinReach => Mathf.Abs(L1 - L2);
    }
}
