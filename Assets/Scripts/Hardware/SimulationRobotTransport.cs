namespace ARMIS.Hardware
{
    /// <summary>
    /// No-op transport used in SIMULATION mode. Unity's IK, trajectories,
    /// missions and telemetry all run identically to hardware mode — only
    /// the final packet send is skipped. Always reports connected so
    /// dashboard/UI code doesn't need separate simulation-mode branches.
    /// </summary>
    public class SimulationRobotTransport : IRobotTransport
    {
        public bool IsConnected { get; private set; }
        public string StatusText => "SIMULATION (no hardware)";
        public float TimeSinceLastSuccessfulSend { get; private set; }

        public void Connect() => IsConnected = true;
        public void Disconnect() => IsConnected = false;

        public bool SendPacket(int baseDeg, int shoulderDeg, int elbowDeg, int gripperDeg)
        {
            TimeSinceLastSuccessfulSend = 0f;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (IsConnected) TimeSinceLastSuccessfulSend += deltaTime;
        }
    }
}
