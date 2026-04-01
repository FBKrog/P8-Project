using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Detects the four orb-pickup steps and advances TutorialManager accordingly.
///
/// HOMER mode (HOMERRaycast enabled):
///   0 → ExtendStarted event  → step 1
///   1 → Orb grabbed          → step 2
///   2 → RetractStarted event → step 3
///   3 → Orb snapped          → /// DAOM mode (LaunchArm enabled, HOMERRaycast disabled/absent):
///   0 → ArmLaunched event                    → step 1
///   1 → GrabbedGameObject == orb (auto-grab) → step 2
///   2 → ArmRecalled event                    → step 3
///   3 → Orb snapped                          → step 4
///
/// GoGo mode (GoGoExtend enabled, HOMERRaycast disabled, LaunchArm disabled/absent):
///   0 → CurrentRv >= 5 m (polled) → step 1
///   1 → Orb grabbed               → step 2
///   2 → Camera within 1 m of orb (polled) → step 3
///   3 → Orb snapped               → step 4
/// </summary>
public class OrbTutorialObjective : MonoBehaviour
{
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private HOMERRaycast homerRaycast;
    [SerializeField] private LaunchArm launchArm;
    [SerializeField] private GoGoExtend goGoExtend;
    [SerializeField] private HandTPOrbConnect orbConnect;
    [SerializeField] private XRGrabInteractable orbGrabInteractable;
    [SerializeField] private Camera vrCamera;   // falls back to Camera.main if null
    [SerializeField] private bool autoStart = true;

    private int _step = -1;
    private bool _gogoMode = false;
    private bool _daomMode = false;

    private void Start()
    {
        if (autoStart) StartObjective();
    }

    public void StartObjective()
    {
        if (_step >= 0) return;
        _step = 0;
        tutorialManager.StartTutorial();

        if (homerRaycast != null && homerRaycast.enabled)
        {
            SubscribeHomer();
        }
        else if (launchArm != null && launchArm.enabled)
        {
            _daomMode = true;
            SubscribeDaom();
        }
        else
        {
            _gogoMode = true;
            orbConnect.OrbSnapped += OnOrbSnapped;
            orbGrabInteractable.selectEntered.AddListener(OnOrbGrabbed);
        }
    }

    // ── HOMER path ────────────────────────────────────────────────────────────

    private void SubscribeHomer()
    {
        homerRaycast.ExtendStarted += OnArmExtended;
        homerRaycast.RetractStarted += OnArmRetracted;
        orbConnect.OrbSnapped += OnOrbSnapped;
        orbGrabInteractable.selectEntered.AddListener(OnOrbGrabbed);
    }

    private void OnArmExtended()
    {
        if (_step != 0) return;
        _step = 1;
        tutorialManager.AdvanceToNextStep();
    }

    private void OnArmRetracted()
    {
        if (_step != 2) return;
        _step = 3;
        tutorialManager.AdvanceToNextStep();
    }

    // ── DAOM path ─────────────────────────────────────────────────────────────

    private void SubscribeDaom()
    {
        LaunchArm.ArmLaunched += OnDAOMArmLaunched;
        LaunchArm.GrabbedGameObject += OnDAOMGrabbed;
        LaunchArm.ArmRecalled += OnDAOMArmRecalled;
        orbConnect.OrbSnapped += OnOrbSnapped;
    }

    private void OnDAOMArmLaunched()
    {
        if (_step != 0) return;
        _step = 1;
        tutorialManager.AdvanceToNextStep();
    }

    private void OnDAOMGrabbed(IXRSelectInteractable grabbed)
    {
        if (_step != 1) return;
        if (grabbed != orbGrabInteractable) return;
        _step = 2;
        tutorialManager.AdvanceToNextStep();
    }

    private void OnDAOMArmRecalled()
    {
        if (_step != 2) return;
        _step = 3;
        tutorialManager.AdvanceToNextStep();
    }

    // ── GoGo path (polled) ────────────────────────────────────────────────────

    private void Update()
    {
        if (!_gogoMode || _step < 0) return;

        if (_step == 0)
        {
            if (goGoExtend != null && goGoExtend.CurrentRv >= 5f)
                AdvanceGoGo();
        }
        else if (_step == 2)
        {
            Camera cam = vrCamera != null ? vrCamera : Camera.main;
            if (cam != null && Vector3.Distance(cam.transform.position,
                    orbGrabInteractable.transform.position) < 1f)
                AdvanceGoGo();
        }
    }

    private void AdvanceGoGo()
    {
        _step++;
        tutorialManager.AdvanceToNextStep();
    }

    // ── Shared listeners ──────────────────────────────────────────────────────

    private void OnOrbGrabbed(SelectEnterEventArgs args)
    {
        if (_step != 1) return;
        _step = 2;
        tutorialManager.AdvanceToNextStep();
    }

    private void OnOrbSnapped()
    {
        if (_step != 3) return;
        _step = 4;
        tutorialManager.AdvanceToNextStep();
        Cleanup();
    }

    private void Cleanup()
    {
        if (_daomMode)
        {
            LaunchArm.ArmLaunched -= OnDAOMArmLaunched;
            LaunchArm.GrabbedGameObject -= OnDAOMGrabbed;
            LaunchArm.ArmRecalled -= OnDAOMArmRecalled;
        }
        else if (!_gogoMode && homerRaycast != null)
        {
            homerRaycast.ExtendStarted -= OnArmExtended;
            homerRaycast.RetractStarted -= OnArmRetracted;
            orbGrabInteractable.selectEntered.RemoveListener(OnOrbGrabbed);
        }
        else if (_gogoMode)
        {
            orbGrabInteractable.selectEntered.RemoveListener(OnOrbGrabbed);
        }
        orbConnect.OrbSnapped -= OnOrbSnapped;

        orbConnect.OrbSnapped -= OnOrbSnapped;
        orbGrabInteractable.selectEntered.RemoveListener(OnOrbGrabbed);
    }
}
