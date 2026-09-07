using UnityEngine;
using ARMIS.Robot;

namespace ARMIS.Hardware
{
    public enum OperatingMode { Simulation, Hardware }

    /// <summary>
    /// Owns the IRobotTransport (chosen by OperatingMode) and is the ONLY
    /// place calibration offsets and servo direction are applied. IK and
    /// trajectory code never see physical/calibrated angles — this keeps
    /// servo-specific quirks out of the math layer per the architecture
    /// requirement.
    ///
    ///   Logical angle (from IK/Trajectory)
    ///     -> + calibration offset
    ///     -> x (-1 if Reversed else +1) around a 90-degree pivot... see
    ///        ApplyDirection() for the exact formula and rationale.
    ///     -> clamp to [servoMin, servoMax]
    ///     -> IRobotTransport.SendPacket(...)
    /// </summary>
    public class ArmController : MonoBehaviour
    {
        [Header("References")]
        public RobotConfiguration config;

        [Header("Mode")]
        public OperatingMode mode = OperatingMode.Simulation;

        public IRobotTransport Transport { get; private set; }
        public RobotCommand LastPhysicalCommand { get; private set; }
        public RobotCommand LastLogicalCommand { get; private set; }

        void Awake()
        {
            SetMode(mode);
        }

        public void SetMode(OperatingMode newMode)
        {
            Transport?.Disconnect();
            mode = newMode;
            Transport = mode == OperatingMode.Hardware
                ? new SerialRobotTransport(config.serialPortName, config.baudRate, config.commandRateHz)
                : new SimulationRobotTransport();
            Transport.Connect();
        }

        void Update()
        {
            Transport?.Tick(Time.deltaTime);
        }

        /// <summary>Applies calibration + direction mapping and forwards to the transport. Returns true if a packet was actually transmitted (false if rate-limited or disconnected — not necessarily an error).</summary>
        public bool SendJointCommand(RobotCommand logical)
        {
            LastLogicalCommand = logical;

            int b = ApplyDirection(logical.Base + config.baseCalibrationOffset, config.baseDirection);
            int s = ApplyDirection(logical.Shoulder + config.shoulderCalibrationOffset, config.shoulderDirection);
            int e = ApplyDirection(logical.Elbow + config.elbowCalibrationOffset, config.elbowDirection);
            int g = ApplyDirection(logical.Gripper + config.gripperCalibrationOffset, config.gripperDirection);

            b = Mathf.RoundToInt(Mathf.Clamp(b, config.servoMin, config.servoMax));
            s = Mathf.RoundToInt(Mathf.Clamp(s, config.servoMin, config.servoMax));
            e = Mathf.RoundToInt(Mathf.Clamp(e, config.servoMin, config.servoMax));
            g = Mathf.RoundToInt(Mathf.Clamp(g, config.servoMin, config.servoMax));

            LastPhysicalCommand = new RobotCommand(b, s, e, g);

            return Transport != null && Transport.SendPacket(b, s, e, g);
        }

        /// <summary>
        /// REVERSED mirrors the angle about the middle of the servo travel
        /// range (servoMin..servoMax), e.g. for 0-180: physical = 180 -
        /// logical. This keeps "0 = one mechanical extreme" meaningful for
        /// both directions without touching IK math.
        /// </summary>
        int ApplyDirection(float angle, ServoDirection direction)
        {
            if (direction == ServoDirection.Normal) return Mathf.RoundToInt(angle);
            float mid = (config.servoMin + config.servoMax) * 0.5f;
            return Mathf.RoundToInt(mid - (angle - mid));
        }

        public bool IsConnected => Transport != null && Transport.IsConnected;
        public string StatusText => Transport?.StatusText ?? "No transport";
        public float TimeSinceLastSuccessfulSend => Transport?.TimeSinceLastSuccessfulSend ?? float.PositiveInfinity;
    }
}
