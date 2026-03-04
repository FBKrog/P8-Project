using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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
    [SerializeField] GameObject daomArmPrefab;
    [SerializeField] GameObject launchPoint;
    [SerializeField] [Tooltip("The rotation of which the DAOM prefab is launched")] Vector3 launchRotation = new Vector3(-180, -90, 0);
    GameObject daomArm;

    [Header("Interactor")]
    [SerializeField] XRDirectInteractor interactor;

    [Header("Input")]
    [SerializeField] InputActionReference launchInput;
    [SerializeField] InputActionReference aimInput;

    [Header("State")]
    [SerializeField] bool aiming = false;
    [SerializeField] bool canLaunch = true;

    LineRenderer lineRenderer;

    IXRSelectInteractable carriedInteractable;
    IXRSelectInteractable daomInteractable;
    IXRSelectInteractable hitInteractable;

    public static Action<XRDirectInteractor> SetInteractorHandedness;
    public static void OnSetInteractorHandedness(XRDirectInteractor interactor) => SetInteractorHandedness?.Invoke(interactor);

    public static Action ArmRecalled;
    public static void OnArmRecalled() => ArmRecalled?.Invoke();

    public static Action<IXRSelectInteractable> GrabbedGameObject;
    public static void OnGrabbedGameObject(IXRSelectInteractable interactable) => GrabbedGameObject?.Invoke(interactable);

    public static Action EarlyRecall;
    public static void OnEarlyRecall() => EarlyRecall?.Invoke();

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    void OnEnable()
    {
        ArmRecalled += RemoveDAOMArm;
        EarlyRecall += canLaunch ? Launch : null;
        GrabbedGameObject += AddGrabbedGameObject;

        // Input
        interactor.selectEntered.AddListener(OnGrab);
        interactor.selectExited.AddListener(args => carriedInteractable = null);
        
        launchInput.action.performed += ctx => Launch();
        
        aimInput.action.performed += AimState;
        aimInput.action.canceled += AimState;
    }


    void OnDisable()
    {
        ArmRecalled -= RemoveDAOMArm;
        GrabbedGameObject -= AddGrabbedGameObject;
        EarlyRecall -= canLaunch ? Launch : null;

        // Input
        interactor.selectEntered.RemoveListener(OnGrab);
        interactor.selectExited.RemoveListener(args => carriedInteractable = null);

        launchInput.action.performed -= ctx => Launch();
        
        aimInput.action.performed -= AimState;
        aimInput.action.canceled -= AimState;
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        carriedInteractable = args.interactableObject;
    }

    /// <summary>
    /// Stores the currently grabbed object, if any, to make sure the player will hold that object after recalling the arm.
    /// </summary>
    /// <param name="gameObject"></param>
    void AddGrabbedGameObject(IXRSelectInteractable gameObject)
    {
        if (gameObject == null)
        {
            daomInteractable = null;
            return;
        }
        daomInteractable = gameObject;
    }

    void AimState(InputAction.CallbackContext ctx)
    {
        if(!canLaunch) return;
        if (ctx.ReadValue<float>() > 0)
        {
            interactor.keepSelectedTargetValid = false;
            aiming = true;
        }
        else
        {
            interactor.keepSelectedTargetValid = true;
            aiming = false;
        }
    }

    /// <summary>
    /// Removes the currently active DAOM arm from the scene, if one exists. And resets the state to allow for launching again as well as interaction.
    /// </summary>
    void RemoveDAOMArm()
    {
        if (daomArm != null)
        {
            Destroy(daomArm);
            daomArm = null;
            canLaunch = true;
            ForceGrabInteractable();
        }
    }

    void ForceGrabInteractable()
    {
        interactor.enabled = true;
        if (daomInteractable != null)
        {
            carriedInteractable = daomInteractable;
            interactor.interactionManager.SelectEnter(interactor, daomInteractable);
        }
        daomInteractable = null;
        hitInteractable = null;
    }

    void Update()
    {
        ValidLayer();
    }

    void LateUpdate()
    {
        DrawAimLine();
    }

    /// <summary>
    /// Shoot a ray forward to check for valid surfaces
    /// </summary>
    bool ValidLayer()
    {
        if (aiming && Physics.Raycast(transform.position, transform.forward, out hit, rayLength, surfaceLayer))
        {
            if (hit.collider.gameObject.transform.parent.TryGetComponent(out XRGrabInteractable interactable) && carriedInteractable != null)
            {
                Debug.Log($"Cannot launch arm at grabable({interactable}) due to the player holding an interactble already.");
                return false;
            }
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
            RecallArm();
        }
        if(canLaunch && ValidLayer())
        {
            if (daomArm != null)
            {
                if(!daomArm.GetComponent<DAOMArm>().Recalling)
                {
                    Debug.Log("Arm is recalling, cannot launch!");
                }
                return;
            }
            // Preconditions met, launch the arm and set canLaunch to false until the arm is recalled.
            canLaunch = false;
            aiming = false;
            interactor.keepSelectedTargetValid = true;
            
            if (hit.collider.gameObject.transform.parent.TryGetComponent(out XRGrabInteractable hitInteractable) && carriedInteractable == null)
            {
                this.hitInteractable = hitInteractable;
            }
            daomArm = Instantiate(daomArmPrefab, launchPoint.transform.position, Quaternion.Euler(launchRotation));
            daomArm.GetComponent<DAOMArm>().Initialize(armRoot, armIKTarget, hit.point, hit.normal, this.hitInteractable, carriedInteractable);
            OnSetInteractorHandedness(interactor);
            interactor.enabled = false;
        }
    }

    /// <summary>
    /// Recalls the arm to the launch point if it is currently attached to a surface.
    /// </summary>
    void RecallArm()
    {
        if (daomArm != null)
        {
            if (!daomArm.GetComponent<DAOMArm>().IsAttachedToSurface)
            {
                Debug.Log("Arm is not attached to surface, cannot reset!");
                return;
            }
            daomArm.GetComponent<DAOMArm>().RecallArm(launchPoint, launchPoint.transform.forward);
        }
    }

    void DrawAimLine()
    {
        if(ValidLayer() && daomArm == null)
        {
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, hit.point);
            return;
        }
        lineRenderer.enabled = false;
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
