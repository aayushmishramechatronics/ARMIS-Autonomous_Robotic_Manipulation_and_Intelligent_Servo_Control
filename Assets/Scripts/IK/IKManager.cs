// IKManager.cs

using UnityEngine;

public class IKManager : MonoBehaviour
{
    [SerializeField] private ArmController armController;
    [SerializeField] private GameObject jointPref;

    [Header("Arm Properties")]
    [SerializeField] private float L0 = 5f;
    [SerializeField] private float L1 = 12f;
    [SerializeField] private float L2 = 12f;

    [Header("Angles")]
    [SerializeField, Range(0f, 180f)] private float bAngle = 90f;
    [SerializeField, Range(0f, 180f)] private float sAngle = 90f;
    [SerializeField, Range(0f, 180f)] private float eAngle = 90f;
    [SerializeField, Range(0f, 180f)] private float gAngle = 90f;

    [Header("Inverse Kinematics")]
    [SerializeField] private float angularSpeed = 90f;
    [SerializeField] private Transform targetTrans;
    [SerializeField] private Vector2 treshMinMax = new Vector2(0.5f, 0.5f);

    private Transform bJoint;
    private Transform sJoint;
    private Transform eJoint;
    private Transform gJoint;

    private LineRenderer line;

    private float bTarget;
    private float sTarget;
    private float eTarget;

    private float minDist;
    private float maxDist;

    private void Start()
    {
        line = GetComponent<LineRenderer>();

        if (line == null)
        {
            Debug.LogError("IKManager requires a LineRenderer component.");
            enabled = false;
            return;
        }

        if (jointPref == null)
        {
            Debug.LogError("IKManager requires a joint prefab.");
            enabled = false;
            return;
        }

        if (targetTrans == null)
        {
            Debug.LogError("IKManager requires a target Transform.");
            enabled = false;
            return;
        }

        if (armController == null)
        {
            Debug.LogError("IKManager requires an ArmController reference.");
            enabled = false;
            return;
        }

        InstantiateArm();
        UpdateArmSim();
        SetArmAngles();
    }

    private void Update()
    {
        SolveIK();

        Vector3 currentAngles = new Vector3(bAngle, sAngle, eAngle);
        Vector3 targetAngles = new Vector3(bTarget, sTarget, eTarget);

        currentAngles = Vector3.MoveTowards(
            currentAngles,
            targetAngles,
            angularSpeed * Time.deltaTime
        );

        bAngle = currentAngles.x;
        sAngle = currentAngles.y;
        eAngle = currentAngles.z;

        UpdateArmSim();
        SetArmAngles();
    }

    private void InstantiateArm()
    {
        minDist = Mathf.Abs(L1 - L2) + treshMinMax.x;
        maxDist = L1 + L2 - treshMinMax.y;

        if (minDist > maxDist)
        {
            Debug.LogError("Invalid IK distance limits: minDist is greater than maxDist.");
            enabled = false;
            return;
        }

        bJoint = Instantiate(jointPref, transform).transform;
        sJoint = Instantiate(jointPref, transform).transform;
        eJoint = Instantiate(jointPref, transform).transform;
        gJoint = Instantiate(jointPref, transform).transform;

        bJoint.name = "Base Joint";
        sJoint.name = "Shoulder Joint";
        eJoint.name = "Elbow Joint";
        gJoint.name = "Gripper Joint";

        bJoint.position = Vector3.zero;
        sJoint.position = bJoint.position + Vector3.up * L0;
        eJoint.position = sJoint.position + Vector3.up * L1;
        gJoint.position = eJoint.position + Vector3.forward * L2;

        sJoint.SetParent(bJoint);
        eJoint.SetParent(sJoint);
        gJoint.SetParent(eJoint);
    }

    private void UpdateArmSim()
    {
        bAngle = Mathf.Clamp(bAngle, 0f, 180f);
        sAngle = Mathf.Clamp(sAngle, 0f, 180f);
        eAngle = Mathf.Clamp(eAngle, 0f, 180f);
        gAngle = Mathf.Clamp(gAngle, 0f, 180f);

        float bFinal = 90f - bAngle;
        float sFinal = 90f - sAngle;
        float eFinal = 90f - eAngle;

        bJoint.localRotation = Quaternion.Euler(0f, bFinal, 0f);
        sJoint.localRotation = Quaternion.Euler(sFinal, 0f, 0f);
        eJoint.localRotation = Quaternion.Euler(eFinal, 0f, 0f);

        line.SetPosition(0, bJoint.position);
        line.SetPosition(1, sJoint.position);
        line.SetPosition(2, eJoint.position);
        line.SetPosition(3, gJoint.position);
    }

    private void SolveIK()
    {
        Vector3 targetDisp = targetTrans.position - sJoint.position;

        float horizontalDist = Mathf.Sqrt(
            targetDisp.x * targetDisp.x +
            targetDisp.z * targetDisp.z
        );

        float verticalDist = targetDisp.y;

        float L3 = Mathf.Sqrt(
            horizontalDist * horizontalDist +
            verticalDist * verticalDist
        );

        L3 = Mathf.Clamp(L3, minDist, maxDist);

        float shoulderCos =
            (L2 * L2 - L3 * L3 - L1 * L1) /
            (-2f * L3 * L1);

        float elbowCos =
            (L3 * L3 - L2 * L2 - L1 * L1) /
            (-2f * L2 * L1);

        shoulderCos = Mathf.Clamp(shoulderCos, -1f, 1f);
        elbowCos = Mathf.Clamp(elbowCos, -1f, 1f);

        float xAngle = Mathf.Acos(shoulderCos);
        float yAngle = Mathf.Atan2(verticalDist, horizontalDist);

        float alphaAngle = xAngle + yAngle;
        float betaAngle = Mathf.Acos(elbowCos);

        float thetaAngle =
            Mathf.PI / 2f -
            Mathf.Atan2(targetDisp.x, targetDisp.z);

        bTarget = Mathf.Clamp(
            thetaAngle * Mathf.Rad2Deg,
            0f,
            180f
        );

        sTarget = Mathf.Clamp(
            alphaAngle * Mathf.Rad2Deg,
            0f,
            180f
        );

        eTarget = Mathf.Clamp(
            betaAngle * Mathf.Rad2Deg,
            0f,
            180f
        );
    }

    private void SetArmAngles()
    {
        armController.bAngle = Mathf.RoundToInt(bAngle);
        armController.sAngle = Mathf.RoundToInt(sAngle);
        armController.eAngle = Mathf.RoundToInt(eAngle);
        armController.gAngle = Mathf.RoundToInt(gAngle);
    }

    private void OnDestroy()
    {
        if (bJoint != null)
            Destroy(bJoint.gameObject);

        if (sJoint != null)
            Destroy(sJoint.gameObject);

        if (eJoint != null)
            Destroy(eJoint.gameObject);

        if (gJoint != null)
            Destroy(gJoint.gameObject);
    }
}
