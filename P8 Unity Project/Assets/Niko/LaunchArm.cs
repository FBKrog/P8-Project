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
            Debug.Log("Hit: " + hit.collider.name);
            // Implement launch logic here
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
            canLaunch = false;
            ToggleArmInteraction(false);
            daomArm = Instantiate(LaunchedArmPrefab, launchPoint.transform.position, launchPoint.transform.rotation);
            daomArm.GetComponent<DAOMArm>().Initialize(armRoot, armIKTarget, hit.point, hit.normal);
            Debug.Log("Launching arm!");
        }
    }

    void ResetArm()
    {
        daomArm.GetComponent<DAOMArm>().RecallArm(launchPoint.transform.position, launchPoint.transform.forward);
        Debug.Log("Resetting arm!");
        ToggleArmInteraction(true);
        canLaunch = true;
    }

    void ToggleArmInteraction(bool state)
    {
        Debug.Log("Toggling arm interaction: " + state);
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
