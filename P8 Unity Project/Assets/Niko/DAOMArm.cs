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
    [SerializeField] [Tooltip("The time it takes the arm to travel from the body to the surface and vice-versa.")] float travelDuration = 1f;


    [Header("Rotation")]
    [SerializeField] GameObject lowerArm;
    [SerializeField] [Tooltip("The point in travel time the arm will rotate, normalized from percentage of traveled distance")] float rotationStartTime = 0.8f;
    [SerializeField] [Tooltip("The time it takes the arm to rotate into place relative to the surface's normal.")] float rotationDuration = 0.5f;
    [SerializeField] Vector3 rotationOffset = new(0, 180,90);

    [Header("Other")]
    [SerializeField] [Tooltip("Make the arm act as if mirrored.")] bool mirror = true;

    [Header("Interactor")]
    [SerializeField] XRDirectInteractor interactor;

    Quaternion startRot;
    Quaternion targetRot;

    Quaternion lowerArmStartRot;
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
        interactor.selectExited.AddListener(args => selectedInteractable = null);
    }

    void OnDisable()
    {
        LaunchArm.SetInteractorHandedness -= GetHandedness;
    }

    void GetHandedness(XRDirectInteractor interactor)
    {
        this.interactor.handedness = interactor.handedness;
        this.interactor.selectInput = interactor.selectInput;
        this.interactor.activateInput = interactor.activateInput;
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        selectedInteractable = args.interactableObject;
    }

    /// <summary>
    /// Initializes the player by setting the root and inverse kinematics (IK) target, and begins movement toward the
    /// specified point.
    /// </summary>
    public void Initialize(GameObject root, GameObject IKTarget, Vector3 point, Vector3 normal, IXRSelectInteractable hitInteractable = null, IXRSelectInteractable interactable = null)
    {
        playerRoot = root;
        playerIKTarget = IKTarget;

        RigAnimator.enabled = false;
        extendedArmPart.SetActive(false);

        if(interactor != null && interactable != null)
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

        interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Sticky;
        if(hitInteractable != null)
        {
            selectedInteractable = hitInteractable;
        }
        LaunchArm.OnGrabbedGameObject(selectedInteractable);
        
        StartCoroutine(TravelToGameObject(goPoint, normal));
        StartCoroutine(RotateToNormal(transform, startRot, targetRot, rotationDuration));
    }

    /// <summary>
    /// Moves the object smoothly to the specified POSITION while storing the given surface normal in a variable for later use.
    /// </summary>
    IEnumerator TravelToPoint(Vector3 point, Vector3 normal)
    {
        if (isTraveling) yield break;
        isTraveling = true;

        
        extendedArmPart.SetActive(false);

        Vector3 startPos = transform.position;

        startRot = transform.rotation;
        targetRot = Quaternion.LookRotation(normal);

        float elapsedTime = 0f;
        while (true) {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / travelDuration);
            if (t >= rotationStartTime)
            {
                PrepareSurfaceLanding();
            }
            transform.position = Vector3.Lerp(startPos, point, t);
            if (elapsedTime >= travelDuration) 
            {
                ArmAttaching();
                break;
            }
            yield return null;
        }
        yield break;
    }

    /// <summary>
    /// Moves the object smoothly to the specified GAMEOBJECT while storing the given surface normal in a variable for later use.
    /// </summary>
    IEnumerator TravelToGameObject(GameObject goPoint, Vector3 normal)
    {
        if (isTraveling) yield break;
        isTraveling = true;

        extendedArmPart.SetActive(false);

        Vector3 startPos = transform.position;

        startRot = transform.rotation;
        targetRot = Quaternion.LookRotation(normal);

        float elapsedTime = 0f;
        while (true)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / travelDuration);
            if (t >= rotationStartTime)
            {
                PrepareSurfaceLanding();
            }
            transform.position = Vector3.Lerp(startPos, goPoint.transform.position, t);
            if (elapsedTime >= travelDuration)
            {
                ArmAttaching();
                break;
            }
            yield return null;
        }
        yield break;
    }

    void PrepareSurfaceLanding()
    {
        if(dumbfix < 1)
        {
            lowerArmStartRot = lowerArm.transform.rotation;
            var targetRotation = Quaternion.Euler(new Vector3(90,0,0)); // The arm model is rotated funky, so this is a hardcoded fix to make it look like the arm is bending at the elbow as it approaches the surface. This is really scuffed and should be replaced with a better solution ¯\_(ツ)_/¯
            StartCoroutine(RotateToNormal(lowerArm.transform, lowerArmStartRot, targetRotation, rotationDuration));
            dumbfix++;
        }
    }

    /// <summary>
    /// Attaches the arm to a surface and initiates alignment to the surface normal.
    /// </summary>
    void ArmAttaching()
    {
        isTraveling = false;
        isAttachedToSurface = true;

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

        StartCoroutine(RotateToNormal(gameObject.transform, startRot, targetRot, rotationDuration));
        if (recalling)
        {
            LaunchArm.OnArmRecalled();
            return;
        }
    }

    /// <summary>
    /// Rotates the object from its current orientation to the target rotation over a specified duration.
    /// </summary>
    IEnumerator RotateToNormal(Transform transform, Quaternion startRot, Quaternion targetRot, float duration)
    {
        float elapsedTime = 0f;
        while (true)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
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

        if(mirror)
        {
            // Mirror on x-axis.
            playerHandOffset.x *= -1;
            //playerHandOffset.z *= -1; // Mirror on z-axis will 
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
        Quaternion relativeRot = Quaternion.Inverse(playerRoot.transform.rotation) * playerIKTarget.transform.rotation * Quaternion.Euler(rotationOffset);

        if(mirror)
        {
            // Mirror on y-axis.
            //relativeRot = Quaternion.AngleAxis(180f, Vector3.up) * relativeRot;
            
            Vector3 euler = relativeRot.eulerAngles;
            euler.y *= -1;
            euler.z *= -1;
            relativeRot = Quaternion.Euler(euler);
        }

        // Apply the relative rotation to the daom root to find the target rotation for the daom hand.
        daomIKTarget.transform.rotation = daomRoot.transform.rotation * relativeRot;
    }
}
