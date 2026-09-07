namespace ARMIS.Hardware
{
    /// <summary>
    /// The only interface ArmController talks to. This is what makes
    /// SIMULATION vs HARDWARE mode a swap of one object rather than an
    /// if/else scattered through the codebase:
    ///
    ///   IK / Planner -> RobotCommand -> ArmController (calibration + direction)
    ///                -> IRobotTransport.SendPacket(physical angles)
    ///                       |-- SimulationRobotTransport (no I/O)
    ///                       '-- SerialRobotTransport (Arduino over USB)
    /// </summary>
    public interface IRobotTransport
    {
        bool IsConnected { get; }
        string StatusText { get; }
        float TimeSinceLastSuccessfulSend { get; }

        void Connect();
        void Disconnect();

        /// <summary>Angles here are already calibrated, direction-mapped,
        /// and physically clamped (0-180 servo range) by ArmController.</summary>
        bool SendPacket(int baseDeg, int shoulderDeg, int elbowDeg, int gripperDeg);

        void Tick(float deltaTime);
    }
}
