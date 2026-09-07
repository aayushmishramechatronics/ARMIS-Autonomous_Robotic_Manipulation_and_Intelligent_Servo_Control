using System;
using UnityEngine;

namespace ARMIS.Robot
{
    /// <summary>
    /// A set of four LOGICAL joint angles (degrees). This struct never
    /// carries calibration or direction information — that is applied only
    /// in the Hardware layer.
    /// </summary>
    [Serializable]
    public struct RobotCommand
    {
        public float Base;
        public float Shoulder;
        public float Elbow;
        public float Gripper;

        public RobotCommand(float b, float s, float e, float g)
        {
            Base = b; Shoulder = s; Elbow = e; Gripper = g;
        }

        public static RobotCommand Lerp(RobotCommand a, RobotCommand b, float t)
        {
            return new RobotCommand(
                Mathf.LerpUnclamped(a.Base, b.Base, t),
                Mathf.LerpUnclamped(a.Shoulder, b.Shoulder, t),
                Mathf.LerpUnclamped(a.Elbow, b.Elbow, t),
                Mathf.LerpUnclamped(a.Gripper, b.Gripper, t));
        }

        public override string ToString() =>
            $"B={Base:F1} S={Shoulder:F1} E={Elbow:F1} G={Gripper:F1}";
    }

    public enum WorkspaceStatus { Valid, Unreachable, NearSingular }
    public enum JointLimitStatus { Ok, Violation }

    /// <summary>
    /// Structured result of an IK solve. Carries both the raw (unclamped)
    /// solution and the safety-clamped solution so the caller can decide
    /// whether to proceed, and always reports why.
    /// </summary>
    public class IKResult
    {
        public bool IsValid;
        public Vector3 TargetPosition;
        public RobotCommand RequestedAngles;   // raw solver output, pre-clamp
        public RobotCommand ClampedAngles;     // after joint-limit clamping
        public WorkspaceStatus Workspace = WorkspaceStatus.Valid;
        public JointLimitStatus JointLimits = JointLimitStatus.Ok;

        public bool HasFKVerification;
        public Vector3 FKPosition;
        public float PositionErrorX;
        public float PositionErrorY;
        public float PositionErrorZ;
        public float PositionErrorEuclidean;

        public override string ToString() =>
            $"Valid={IsValid} Workspace={Workspace} JointLimits={JointLimits} " +
            $"Requested=({RequestedAngles}) Clamped=({ClampedAngles}) " +
            (HasFKVerification ? $"FKErr={PositionErrorEuclidean:F4}" : "FK=n/a");
    }
}
