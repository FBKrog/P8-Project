using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Detects the four orb-pickup steps and advances TutorialManager accordingly.
/// Steps:
///   0 → Arm extended    → step 1
///   1 → Orb grabbed     → step 2
///   2 → Arm retracted   → step 3
///   3 → Orb snapped     → step 4 (completesObjective = "OrbObjective")
/// </summary>
public class OrbTutorialObjective : MonoBehaviour
{
    [SerializeField] private TutorialManager    tutorialManager;
    [SerializeField] private HOMERRaycast       homerRaycast;
    [SerializeField] private HandTPOrbConnect   orbConnect;
    [SerializeField] private XRGrabInteractable orbGrabInteractable;
    [SerializeField] private bool               autoStart = true;

    private int _step = -1;

    private void Start()
    {
        if (autoStart) StartObjective();
    }

    public void StartObjective()
    {
        if (_step >= 0) return;
        homerRaycast.ExtendStarted              += OnArmExtended;
        homerRaycast.RetractStarted             += OnArmRetracted;
        orbConnect.OrbSnapped                   += OnOrbSnapped;
        orbGrabInteractable.selectEntered.AddListener(OnOrbGrabbed);
        _step = 0;
        tutorialManager.StartTutorial();
    }

    private void OnArmExtended()
    {
        if (_step != 0) return;
        _step = 1;
        tutorialManager.AdvanceToNextStep();
    }

    private void OnOrbGrabbed(SelectEnterEventArgs args)
    {
        if (_step != 1) return;
        _step = 2;
        tutorialManager.AdvanceToNextStep();
    }

    private void OnArmRetracted()
    {
        if (_step != 2) return;
        _step = 3;
        tutorialManager.AdvanceToNextStep();
    }

    private void OnOrbSnapped()
    {
        if (_step != 3) return;
        _step = 4;
        tutorialManager.AdvanceToNextStep();   // step 4 → completesObjective = "OrbObjective"
        homerRaycast.ExtendStarted              -= OnArmExtended;
        homerRaycast.RetractStarted             -= OnArmRetracted;
        orbConnect.OrbSnapped                   -= OnOrbSnapped;
        orbGrabInteractable.selectEntered.RemoveListener(OnOrbGrabbed);
    }
}
