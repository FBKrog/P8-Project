using System.Collections;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DAOMArm : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] GameObject playerRoot;
    [SerializeField] GameObject playerIKTarget; // Called player hand in comments for readability

    [Header("DAOM")]
    [SerializeField] Animator RigAnimator;
    [SerializeField] GameObject daomRoot;
    //[SerializeField] GameObject extendedArmPart;
    [SerializeField] GameObject upperArm;
    [SerializeField] GameObject lowerArm;
    [SerializeField] GameObject tip;
    [SerializeField] GameObject daomIKTarget; // Called daom hand in comments for readability

    [Header("Travel")]
    [SerializeField] GameObject thruster;
    [SerializeField][Tooltip("The time it takes the arm to travel from the body to the surface and vice-versa.")] float travelSpeed = 7f;
    [SerializeField] Vector3 lowerArmExtention;
    [SerializeField] Vector3 lowerArmRetraction;
    [SerializeField] float retractionTime = 0.4f;

    [Header("Rotation")]
    [SerializeField] [Tooltip("The point in travel time the arm will rotate, normalized from percentage of traveled distance")] [Range(0,1)] float rotationStartTime = 0.8f;
    [SerializeField] [Tooltip("The time it takes the arm to rotate into place relative to the surface's normal.")] float rotationDuration = 0.5f;
    [SerializeField] Vector3 handRotationOffset = new(0, 0,90);

    [Header("Other")]
    [SerializeField] [Tooltip("Make the arm act as if mirrored.")] bool mirror = true;
    [SerializeField] GameObject littleExtraBit;
    [SerializeField] [Tooltip("Only used when NOT mirrored.")] float wallDistanceOffset = 0.3f;

    [Header("Interactor")]
    [SerializeField] XRDirectInteractor interactor;

    Quaternion targetRot;
    GameObject lookReference;

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
    public void Initialize(GameObject root, GameObject IKTarget, Vector3 point, GameObject lookReference = null, IXRSelectInteractable hitInteractable = null, IXRSelectInteractable interactable = null)
    {
        lowerArm.transform.localPosition = lowerArmRetraction;
        thruster.SetActive(true);
        littleExtraBit.SetActive(false);

        playerRoot = root;
        playerIKTarget = IKTarget;
        this.lookReference = lookReference;

        RigAnimator.enabled = false;
        //extendedArmPart.SetActive(false);
        
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
        StartCoroutine(TravelToPoint(transform, point));
    }

    /// <summary>
    /// Initiates the process of recalling the arm to the specified point and orientation.
    /// </summary>
    public void RecallArm(GameObject goPoint)
    {
        if (isTraveling) return;
        if (!isAttachedToSurface) return;
        if (recalling) return;
        
        recalling = true;

        thruster.SetActive(true);
        littleExtraBit.SetActive(false);

        dumbfix = 0;
        
        RigAnimator.enabled = false;

        interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Sticky;
        
        if(hitInteractable != null)
        {
            selectedInteractable = hitInteractable;
        }
        
        LaunchArm.OnGrabbedGameObject(selectedInteractable);

        targetRot = LookDirection(goPoint.transform.position);
        
        StartCoroutine(RotateToTargetRotation(transform, targetRot, rotationDuration));

        StartCoroutine(RotateToTargetRotation(upperArm.transform, upperArmStartRot, rotationDuration, true));

        StartCoroutine(RotateToTargetRotation(lowerArm.transform, lowerArmStartRot, rotationDuration, true));

        StartCoroutine(RotateToTargetRotation(tip.transform, tipStartRot, rotationDuration, true));
        
        StartCoroutine(TravelToGameObject(goPoint));
    }

    /// <summary>
    /// Moves the GameObject smoothly to the specified POSITION while storing the given surface normal in a variable for later use. This function has become extremely cursed and I am sorry, but it works for now so here we are. The local parameter is used ONLY for the lower arm extension.
    /// </summary>
    IEnumerator TravelToPoint(Transform transform, Vector3 point, bool local = false)
    {
        var startRot = local ? transform.localPosition : transform.position;
        var t = 0f;
        if (local)
        {
            var elapsedTime = 0f;
            while (true)
            {
                elapsedTime += Time.deltaTime;
                if(recalling)
                    t = Mathf.Clamp01(elapsedTime / retractionTime); // Cool little retraction for the arm when recalling
                else
                    t = 1; // Instantly attach to surface

                transform.localPosition = Vector3.Lerp(startRot, point, t);
                yield return null;
            }
        }
        else 
        { 
            if(isTraveling) yield break;
            isTraveling = true;

            var totalDistance = Vector3.Distance(startRot, point);
            var traveledDistance = 0f;

            while (traveledDistance < totalDistance)
            {
                var distanceDelta = travelSpeed * Time.deltaTime;
                
                if(local)
                    transform.localPosition = Vector3.Lerp(transform.localPosition, point, distanceDelta/10);
                else
                    transform.position = Vector3.MoveTowards(transform.position, point, distanceDelta);

                traveledDistance += distanceDelta;
                var d = traveledDistance / totalDistance;

                if (d >= rotationStartTime)
                {
                    PrepareSurfaceLanding(point);
                }
                if (!mirror && traveledDistance >= (totalDistance - wallDistanceOffset))
                {
                    print("Some cool extra thing that shoots out to the wall");
                    littleExtraBit.SetActive(true);
                    ArmAttaching();
                    break;
                }
                if (traveledDistance >= totalDistance)
                {
                    ArmAttaching();
                }
                yield return null;
            }
        }
    }

    /// <summary>
    /// Moves the GameObject smoothly to the specified GAMEOBJECT'S POSITION while storing the given surface normal in a variable for later use.
    /// </summary>
    IEnumerator TravelToGameObject(GameObject goPoint)
    {
        if (isTraveling) yield break;
        isTraveling = true;
        
        float totalDistance = Vector3.Distance(transform.position, goPoint.transform.position);
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
            if (traveledDistance >= totalDistance)
            {
                ArmAttaching();
            }
            yield return null;
        }
    }

    /// <summary>
    /// Prepares the lower arm for landing on a surface by initiating its rotation to the appropriate orientation.
    /// </summary>
    void PrepareSurfaceLanding(Vector3 point)
    {
        if(dumbfix < 1)
        {
            if (recalling)
            {
                thruster.SetActive(false);
                var rotation = Quaternion.LookRotation(-transform.forward, point);
                StartCoroutine(RotateToTargetRotation(transform, rotation, rotationDuration));
                StartCoroutine(TravelToPoint(lowerArm.transform, lowerArmRetraction, true));
                dumbfix++;
                return;
            }
            else
            {
                thruster.SetActive(false);
                var rotation = Quaternion.LookRotation(-transform.forward, point);
                StartCoroutine(RotateToTargetRotation(transform, rotation, rotationDuration));
                StartCoroutine(TravelToPoint(lowerArm.transform, lowerArmExtention, true));
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
        
        if(mirror)
            targetRot = LookDirection(lookReference.transform.position) * Quaternion.AngleAxis(-90, Vector3.up) * lookReference.transform.rotation;
        else
            targetRot = LookDirection(lookReference.transform.position) * Quaternion.AngleAxis(90, Vector3.up) * lookReference.transform.rotation;

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

        if (recalling)
        {
            LaunchArm.OnArmRecalled();
            return;
        }
        StartCoroutine(RotateToTargetRotation(transform, targetRot, rotationDuration));
    }

    Quaternion LookDirection(Vector3 target)
    {
        var direction = (target - transform.position).normalized;
        var rotation = Quaternion.LookRotation(direction, target);
        return rotation;
    }


    /// <summary>
    /// Rotates the object from its current orientation to the target rotation over a specified duration.
    /// </summary>
    IEnumerator RotateToTargetRotation(Transform transform, Quaternion targetRot, float duration, bool local = false)
    {
        var elapsedTime = 0f;
        var startRot = local ? transform.localRotation : transform.rotation;
        while (true)
        {
            elapsedTime += Time.deltaTime;
            var t = Mathf.Clamp01(elapsedTime / duration);

            if(local)
                transform.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            else
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

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

        playerHandOffset.x *= -1;
        playerHandOffset.y *= -1;
        if(mirror)
        {
            playerHandOffset.z *= -1;
        }

        // Original solution that doesn't compensate for scaled daom parent :( 
        //daomIKTarget.transform.position = daomRoot.transform.TransformPoint(playerHandOffset);

        // Store the offset from the daom root to world space to find the target position for the daom hand.
        var worldOffset = daomRoot.transform.rotation * playerHandOffset;

        // Compensate for scaled parent hierarchy (VERY BAD BTW)
        var parentScale = daomIKTarget.transform.parent.lossyScale;

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
        Quaternion relativeRot = Quaternion.Inverse(playerRoot.transform.rotation) * playerIKTarget.transform.rotation; 

        if(mirror)
        {
            relativeRot *= Quaternion.Euler(handRotationOffset);
        }
        else
        {
            relativeRot *= Quaternion.Euler(handRotationOffset) * Quaternion.Euler(0,180,-180);
        }
        // Apply the relative rotation to the daom root to find the target rotation for the daom hand.
        daomIKTarget.transform.rotation = daomRoot.transform.rotation * relativeRot;
    }
}
