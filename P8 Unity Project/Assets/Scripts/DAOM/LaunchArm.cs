using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class LaunchArm : MonoBehaviour
{
    [Header("Player Rotation")]
    [SerializeField] new GameObject camera;
    
    [Header("Raycast")]
    [SerializeField] float rayLength = 100f;
    [SerializeField] LayerMask surfaceLayer;
    RaycastHit hit;

    [Header("Firing Arm")]
    [SerializeField] GameObject boomEffect;
    [SerializeField] GameObject armRoot;
    [SerializeField] GameObject armIKTarget;
    [SerializeField] GameObject armGameObject;

    [Header("Launched Arm")]
    [SerializeField] GameObject daomArmPrefab;
    [SerializeField] GameObject launchPoint;
    GameObject daomArm;

    [Header("Interactor")]
    [SerializeField] XRDirectInteractor interactor;

    [Header("Input")]
    [SerializeField] InputActionReference launchInput;
    [SerializeField] InputActionReference aimInput;

    [Header("State")]
    [SerializeField] bool aiming = false;
    [SerializeField] bool canLaunch = true;

    [Header("Line Renderer")]
    [SerializeField] Material validTarget;
    [SerializeField] Material invalidTarget;
    [SerializeField] GameObject holoArm;
    LineRenderer lineRenderer;

    IXRSelectInteractable selectedInteractable;
    IXRSelectInteractable daomInteractable;
    IXRSelectInteractable hitInteractable;

    public static Action<XRDirectInteractor> SetInteractorHandedness;
    public static void OnSetInteractorHandedness(XRDirectInteractor interactor) => SetInteractorHandedness?.Invoke(interactor);

    public static Action ArmLaunched;
    public static void OnArmLaunched() => ArmLaunched?.Invoke();

    public static Action ArmRecalled;
    public static void OnArmRecalled() => ArmRecalled?.Invoke();

    public static Action<IXRSelectInteractable> GrabbedGameObject;
    public static void OnGrabbedGameObject(IXRSelectInteractable interactable) => GrabbedGameObject?.Invoke(interactable);

    public static Action EarlyRecall;
    public static void OnEarlyRecall() => EarlyRecall?.Invoke();

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        holoArm.SetActive(false);
        if (camera == null)
        {
            camera = Camera.main.gameObject;
        }
        aiming = false;
    }

    void OnEnable()
    {
        ArmRecalled += RemoveDAOMArm;
        EarlyRecall += canLaunch ? Launch : null;
        GrabbedGameObject += AddGrabbedGameObject;

        // <Input>
        interactor.selectEntered.AddListener(OnGrab);
        interactor.selectExited.AddListener(OnRelease);
        
        launchInput.action.performed += LaunchState;
        
        aimInput.action.performed += AimState;
        aimInput.action.canceled += AimState;
        // </Input>
    }


    void OnDisable()
    {
        ArmRecalled -= RemoveDAOMArm;
        GrabbedGameObject -= AddGrabbedGameObject;
        EarlyRecall -= canLaunch ? Launch : null;

        // <Input>
        interactor.selectEntered.RemoveListener(OnGrab);
        interactor.selectExited.RemoveListener(OnRelease);

        launchInput.action.performed -= LaunchState;

        aimInput.action.performed -= AimState;
        aimInput.action.canceled -= AimState;
        // </Input>
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

    void LaunchState(InputAction.CallbackContext ctx)
    {
        if (ctx.ReadValue<float>() >= 0.99f)
        {
            Launch();
        }
    }

    /// <summary>
    /// Handles the aiming state based on the input action context.
    /// </summary>
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
            armGameObject.transform.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// Forces the interactor to grab the interactable object from the recalled DAOM.
    /// </summary>
    void ForceGrabInteractable()
    {
        interactor.enabled = true;
        if (daomInteractable != null)
        {
            selectedInteractable = daomInteractable;
            interactor.interactionManager.SelectEnter(interactor, selectedInteractable);
        }
        daomInteractable = null;
        hitInteractable = null;
    }

    void LateUpdate()
    {
        DrawLineRenderer();
        SetLineMaterial(ValidLayer());
        //SetHolographicArm(ValidLayer());
    }

    /// <summary>
    /// Shoot a ray forward to check for valid surfaces
    /// </summary>
    bool ValidLayer()
    {
        if (!aiming) return false;

        RaycastHit[] hits = Physics.RaycastAll(transform.position, transform.forward, rayLength, surfaceLayer);
        Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
        if(hits.Length == 0) return false;
        foreach (var h in hits)
        {
            if (selectedInteractable != null && !h.collider.transform.IsChildOf(selectedInteractable.transform) &&
                h.collider.transform.parent.TryGetComponent(out XRGrabInteractable hitInteractable))
                return false;

            if (selectedInteractable != null && h.collider.transform.IsChildOf(selectedInteractable.transform))
                continue;

            hit = h;
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
            
            if (hit.collider.gameObject.transform.TryGetComponent(out XRGrabInteractable hitInteractable) && selectedInteractable == null)
            {
                this.hitInteractable = hitInteractable;
            }

            armGameObject.transform.localScale = Vector3.zero;

            // Calculate the rotation for the arm to be launched at based on the hit point and the launch point and multiplying with an offset.
            var direction = (hit.point - launchPoint.transform.position).normalized;
            var rotation = Quaternion.LookRotation(direction);

            var boomRotation = Quaternion.LookRotation(-camera.transform.position, Vector3.up);
            Instantiate(boomEffect, launchPoint.transform.position, rotation);
            
            daomArm = Instantiate(daomArmPrefab, launchPoint.transform.position, rotation);
            daomArm.GetComponent<DAOMArm>().Initialize(armRoot, armIKTarget, hit.point, camera, this.hitInteractable, selectedInteractable);
            OnSetInteractorHandedness(interactor);
            interactor.enabled = false;
            OnArmLaunched();
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
            daomArm.GetComponent<DAOMArm>().RecallArm(launchPoint);
        }
    }

    /// <summary>
    /// Renders a line indicating the aiming direction when the player is aiming.
    /// </summary>
    void DrawLineRenderer()
    {
        if (lineRenderer)
        {
            if(aiming && daomArm == null)
            {
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, transform.position);
                if(ValidLayer())
                {
                    lineRenderer.SetPosition(1, hit.point);
                    return;
                }
                lineRenderer.SetPosition(1, transform.forward * rayLength);
                return;
            }
            lineRenderer.enabled = false;
        }
    }

    bool lasttValid = false;
    /// <summary>
    /// Sets the line renderer material based on whether the raycast is hitting a valid surface or not, to give the player feedback on whether they can launch the arm or not. lastValid is used to prevent unnecessary material changes for better performance.
    /// </summary>
    /// <param name="valid"></param>
    void SetLineMaterial(bool valid)
    {
        if(valid == lasttValid && lineRenderer) return;
        lasttValid = valid;
        lineRenderer.material = valid ? validTarget : invalidTarget;
    }

    void SetHolographicArm(bool valid)
    {
        holoArm.SetActive(valid);
        holoArm.transform.position = hit.point;
        holoArm.transform.rotation = Quaternion.LookRotation(hit.normal);
    }

#if UNITY_EDITOR
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
#endif
}
