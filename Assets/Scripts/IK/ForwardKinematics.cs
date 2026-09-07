using UnityEngine;
using ARMIS.Robot;

namespace ARMIS.IK
{
    /// <summary>
    /// Forward kinematics for the 2-link planar arm with rotating base.
    /// Must stay mathematically inverse-consistent with IKManager — the
    /// same angle conventions are used in both directions (see
    /// RobotConfiguration.cs header comment for the convention definition).
    /// </summary>
    public static class ForwardKinematics
    {
        public static Vector3 Compute(RobotCommand angles, RobotConfiguration config)
        {
            float baseRad = angles.Base * Mathf.Deg2Rad;
            float shoulderRad = angles.Shoulder * Mathf.Deg2Rad;
            float elbowRad = angles.Elbow * Mathf.Deg2Rad;

            // Planar (arm-plane) forward kinematics.
            float r = config.L1 * Mathf.Cos(shoulderRad) +
                      config.L2 * Mathf.Cos(shoulderRad + elbowRad);
            float h = config.L1 * Mathf.Sin(shoulderRad) +
                      config.L2 * Mathf.Sin(shoulderRad + elbowRad);

            float y = h + config.L0;
            float x = r * Mathf.Cos(baseRad);
            float z = r * Mathf.Sin(baseRad);

            return new Vector3(x, y, z);
        }
    }
}
