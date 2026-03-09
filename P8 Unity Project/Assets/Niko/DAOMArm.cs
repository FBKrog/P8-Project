using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DAOMArm : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] GameObject playerRoot;
    [SerializeField] GameObject playerIKTarget; // Called player hand in comments for readability

    [Header("DAOM")]
    [SerializeField] GameObject daomRoot;
    [SerializeField] GameObject daomIKTarget; // Called daom hand in comments for readability
    [SerializeField] Animator RigAnimator;
    [SerializeField] GameObject extendedArmPart;

    [Header("Travel")]
    [SerializeField][Tooltip("The time it takes the arm to travel from the body to the surface and vice-versa.")] float travelSpeed = 10f;

    [Header("Rotation")]
    [SerializeField] GameObject upperArm;
    [SerializeField] GameObject lowerArm;
    [SerializeField] GameObject tip;
    [SerializeField] [Tooltip("The point in travel time the arm will rotate, normalized from percentage of traveled distance")] float rotationStartTime = 0.8f;
    [SerializeField] [Tooltip("The time it takes the arm to rotate into place relative to the surface's normal.")] float rotationDuration = 0.5f;
    [SerializeField] Vector3 handRotationOffset = new(0, 0,90);

    [Header("Other")]
    [SerializeField] [Tooltip("Make the arm act as if mirrored.")] bool mirror = true;

    [Header("Interactor")]
    [SerializeField] XRDirectInteractor interactor;

    Quaternion startRot;
    Quaternion targetRot;

    Vector3 surfaceNormal;

    Quaternion upperArmStartRot;
    Quaternion lowerArmStartRot;
    Quaternion tipStartRot;

    int dumbfix = 0;

    IXRSelectInteractable selectedInteractable;
    IXRSelectInteractable hitInteractable;

    [SerializeField]bool isTraveling = false;
    [SerializeField]bool isAttachedToSurface = false;
    public bool IsAttachedToSurface => isAttachedToSurface;
    bool recalling = false;
    public bool Recalling => recalling;

    void OnEnable()
    {
        LaunchArm.SetInteractorHandedness += GetHandedness;
        interactor.selectEntered.AddListener(OnGrab);
        interactor.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        LaunchArm.SetInteractorHandedness -= GetHandedness;
        interactor.selectEntered.RemoveListener(OnGrab);
        interactor.selectExited.RemoveListener(OnRelease);
    }

    /// <summary>
    /// The handedness of the interactor and input is set relative to the hand the player used to launch the arm.
    /// </summary>
    void GetHandedness(XRDirectInteractor interactor)
    {
        this.interactor.handedness = interactor.handedness;
        this.interactor.selectInput = interactor.selectInput;
        this.interactor.activateInput = interactor.activateInput;
    }

    /// <summary>
    /// Handles the grab event by updating the currently selected interactable object.
    /// </summary>
    void OnGrab(SelectEnterEventArgs args)
    {
        selectedInteractable = args.interactableObject;
    }

    /// <summary>
    /// Handles the release event for the interactable object.
    /// </summary>
    void OnRelease(SelectExitEventArgs args)
    {
        selectedInteractable = null;
    }

    /// <summary>
    /// Initializes a bunch of variables sent by the player, setting the root, IK and more. It then begins movement toward the specified point.
    /// </summary>
    public void Initialize(GameObject root, GameObject IKTarget, Vector3 point, Vector3 normal, IXRSelectInteractable hitInteractable = null, IXRSelectInteractable interactable = null)
    {
        playerRoot = root;
        playerIKTarget = IKTarget;

        RigAnimator.enabled = false;
        extendedArmPart.SetActive(false);
        
        upperArmStartRot = upperArm.transform.localRotation;
        lowerArmStartRot = lowerArm.transform.localRotation;
        tipStartRot = tip.transform.localRotation;

        if (interactor != null && interactable != null)
        {
            selectedInteractable = interactable;
            interactor.interactionManager.SelectEnter(interactor, selectedInteractable);
            interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Sticky;
        }
        if(hitInteractable != null)
        {
            this.hitInteractable = hitInteractable;
        }
        StartCoroutine(TravelToPoint(point, normal));
    }

    /// <summary>
    /// Initiates the process of recalling the arm to the specified point and orientation.
    /// </summary>
    public void RecallArm(GameObject goPoint, Vector3 normal)
    {
        if (isTraveling) return;
        if (!isAttachedToSurface) return;
        if (recalling) return;
        
        recalling = true;
        
        dumbfix = 0;
        
        RigAnimator.enabled = false;

        interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Sticky;
        
        if(hitInteractable != null)
        {
            selectedInteractable = hitInteractable;
        }
        
        LaunchArm.OnGrabbedGameObject(selectedInteractable);
        
        StartCoroutine(TravelToGameObject(goPoint, normal));

        surfaceNormal = normal;

        startRot = transform.rotation;
        var direction = (goPoint.transform.position - transform.position).normalized;
        var rotation = Quaternion.LookRotation(direction, goPoint.transform.position);
        targetRot = rotation;
        StartCoroutine(RotateToTargetRotation(transform, startRot, targetRot, rotationDuration));

        StartCoroutine(RotateToTargetRotation(upperArm.transform, upperArm.transform.localRotation, upperArmStartRot, rotationDuration, true));

        StartCoroutine(RotateToTargetRotation(lowerArm.transform, lowerArm.transform.localRotation, lowerArmStartRot, rotationDuration, true));

        StartCoroutine(RotateToTargetRotation(tip.transform, tip.transform.localRotation, tipStartRot, rotationDuration, true));
    }

    /// <summary>
    /// Moves the GameObject smoothly to the specified POSITION while storing the given surface normal in a variable for later use.
    /// </summary>
    IEnumerator TravelToPoint(Vector3 point, Vector3 normal)
    {
        if (isTraveling) yield break;
        isTraveling = true;

        extendedArmPart.SetActive(false);

        Vector3 startPos = transform.position;

        startRot = transform.rotation;
        surfaceNormal = normal;
        targetRot = Quaternion.LookRotation(surfaceNormal);


        float totalDistance = Vector3.Distance(startPos, point);
        float traveledDistance = 0f;

        while (traveledDistance < totalDistance)
        {
            float distanceDelta = travelSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, point, distanceDelta);
            traveledDistance += distanceDelta;
            float d = traveledDistance / totalDistance;

            if (d >= rotationStartTime && hitInteractable == null)
            {
                PrepareSurfaceLanding(point);
            }
            yield return null;
        }
        ArmAttaching();
    }

    /// <summary>
    /// Moves the GameObject smoothly to the specified GAMEOBJECT'S POSITION while storing the given surface normal in a variable for later use.
    /// </summary>
    IEnumerator TravelToGameObject(GameObject goPoint, Vector3 normal)
    {
        if (isTraveling) yield break;
        isTraveling = true;

        extendedArmPart.SetActive(false);

        Vector3 startPos = transform.position;

        startRot = transform.rotation;
        surfaceNormal = normal;
        targetRot = Quaternion.LookRotation(surfaceNormal);

        float totalDistance = Vector3.Distance(startPos, goPoint.transform.position);
        float traveledDistance = 0f;

        while (traveledDistance < totalDistance)
        {
            float distanceDelta = travelSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, goPoint.transform.position, distanceDelta);
            traveledDistance += distanceDelta;
            float d = traveledDistance / totalDistance;

            if (d >= rotationStartTime)
            {
                PrepareSurfaceLanding(goPoint.transform.position);
            }
            yield return null;
        }
        ArmAttaching();
    }

    /// <summary>
    /// Prepares the lower arm for landing on a surface by initiating its rotation to the appropriate orientation.
    /// </summary>
    void PrepareSurfaceLanding(Vector3 point)
    {
        if(dumbfix < 1)
        {
            startRot = transform.rotation;
            if (recalling)
            {
                var rotation = Quaternion.LookRotation(-transform.right, surfaceNormal);
                StartCoroutine(RotateToTargetRotation(transform, startRot, rotation, rotationDuration));
                dumbfix++;
            }
            else
            {
                var rotation = Quaternion.LookRotation(-transform.forward, surfaceNormal);
                StartCoroutine(RotateToTargetRotation(transform, startRot, rotation, rotationDuration));
                dumbfix++;
            }
        }
    }

    /// <summary>
    /// Attaches the arm to a surface and initiates alignment to the surface normal.
    /// </summary>
    void ArmAttaching()
    {
        isTraveling = false;
        isAttachedToSurface = true;
        startRot = transform.rotation;

        // If the arm hit an interactable, recall the arm WITH the interactable so the player holds it after recall.
        if (hitInteractable != null && !recalling)
        {
            LaunchArm.OnEarlyRecall();
            interactor.interactionManager.SelectEnter(interactor, hitInteractable);
            interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Sticky;
            return;
        }

        interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.StateChange;
        RigAnimator.enabled = true;
        extendedArmPart.SetActive(true);

        if (recalling)
        {
            LaunchArm.OnArmRecalled();
            return;
        }
        StartCoroutine(RotateToTargetRotation(transform, startRot, targetRot, rotationDuration));
    }

    /// <summary>
    /// Rotates the object from its current orientation to the target rotation over a specified duration.
    /// </summary>
    IEnumerator RotateToTargetRotation(Transform transform, Quaternion startRot, Quaternion targetRot, float duration, bool local = false)
    {
        float elapsedTime = 0f;
        while (true)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            if (local)
            {
                transform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            }
            else
            {
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            }
            if (elapsedTime >= duration)
            {
                break;
            }
            yield return null;
        }
        yield break;
    }

    void LateUpdate()
    {
        if (!isAttachedToSurface || recalling) return;       
        TransformToPlayerHand();
        RotateToPlayerHand();
    }

    /// <summary>
    /// Translates the position of the DAOM hand target to match the relative position of the player hand, optionally
    /// mirroring it across the body.
    /// </summary>
    void TransformToPlayerHand()
    {
        // Get the position of the player hand relative to the player root, and apply that same relative position to the daom root to find the position for the daom hand.
        Vector3 playerHandOffset = playerRoot.transform.InverseTransformPoint(playerIKTarget.transform.position);

        if(mirror)
        {
            playerHandOffset.x *= -1;
            playerHandOffset.y *= -1;
            playerHandOffset.z *= -1;
        }

        // Original solution that doesn't compensate for scaled daom parent :( 
        //daomIKTarget.transform.position = daomRoot.transform.TransformPoint(playerHandOffset);

        // Store the offset from the daom root to world space to find the target position for the daom hand.
        Vector3 worldOffset = daomRoot.transform.rotation * playerHandOffset;

        // Compensate for scaled parent hierarchy (VERY BAD BTW)
        Vector3 parentScale = daomIKTarget.transform.parent.lossyScale;

        worldOffset = new Vector3(
            worldOffset.x / parentScale.x,
            worldOffset.y / parentScale.y,
            worldOffset.z / parentScale.z
            );

        daomIKTarget.transform.position = daomRoot.transform.position + worldOffset;
    }

    /// <summary>
    /// Rotates the DAOM hand to match the orientation of the player hand relative to their respective root transforms.
    /// </summary>
    void RotateToPlayerHand()
    {
        // Get the rotation of the player hand relative to the player root, and apply that same relative rotation to the daom root to find the rotation for the daom hand.
        Quaternion relativeRot = Quaternion.Inverse(playerRoot.transform.rotation) * playerIKTarget.transform.rotation * Quaternion.Euler(handRotationOffset);

        if(mirror)
        {   
            Vector3 euler = relativeRot.eulerAngles;
            //euler.x *= -1;
            //euler.y *= -1;
            //euler.z *= -1;
            relativeRot = Quaternion.Euler(euler);
        }

        // Apply the relative rotation to the daom root to find the target rotation for the daom hand.
        daomIKTarget.transform.rotation = daomRoot.transform.rotation * relativeRot;
    }
}
