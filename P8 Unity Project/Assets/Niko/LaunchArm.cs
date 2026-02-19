using System;
using UnityEngine;

public class LaunchArm : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] float rayLength = 100f;
    [SerializeField] LayerMask surfaceLayer;
    RaycastHit hit;

    [Header("Firing Arm")]
    [SerializeField] GameObject armRoot;
    [SerializeField] GameObject armIKTarget;

    [Header("Launched Arm")]
    [SerializeField] GameObject LaunchedArmPrefab;
    [SerializeField] GameObject launchPoint;
    GameObject daomArm;

    bool canLaunch = true;

    public static Action OnArmRecalled;
    public static void ArmRecalled() => OnArmRecalled?.Invoke();

    void OnEnable()
    {
        OnArmRecalled += RemoveDAOMArm;
    }

    void OnDisable()
    {
        OnArmRecalled -= RemoveDAOMArm;
    }

    void RemoveDAOMArm()
    {
        if (daomArm != null)
        {
            Destroy(daomArm);
            daomArm = null;
            canLaunch = true;
        }
    }

    void Update()
    {
        ValidLayer();
    }

    /// <summary>
    /// Shoot a ray forward to check for valid surfaces
    /// </summary>
    /// <returns></returns>
    bool ValidLayer()
    {
        if (Physics.Raycast(transform.position, transform.forward, out hit, rayLength, surfaceLayer))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Initiates the launch sequence for the arm if all preconditions are met.
    /// </summary>
    public void Launch()
    {
        if (!canLaunch)
        {
            ResetArm();
        }
        if(canLaunch && ValidLayer())
        {
            if(daomArm != null)
            {
                if(!daomArm.GetComponent<DAOMArm>().Recalling)
                {
                    Debug.Log("Arm is recalling, cannot launch!");
                    return;
                }
            }
            canLaunch = false;
            if(daomArm == null)
            {
                daomArm = Instantiate(LaunchedArmPrefab, launchPoint.transform.position, launchPoint.transform.rotation);
                daomArm.GetComponent<DAOMArm>().Initialize(armRoot, armIKTarget, hit.point, hit.normal);
            }
        }
    }

    void ResetArm()
    {
        if (daomArm != null)
        {
            if (!daomArm.GetComponent<DAOMArm>().IsAttachedToSurface)
            {
                Debug.Log("Arm is not attached to surface, cannot reset!");
                return;
            }
            daomArm.GetComponent<DAOMArm>().RecallArm(launchPoint.transform.position, launchPoint.transform.forward);
        }
    }

    void OnDrawGizmos()
    {
        if(ValidLayer())
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.forward * rayLength);
            return;
        }
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * rayLength);
    }
}
