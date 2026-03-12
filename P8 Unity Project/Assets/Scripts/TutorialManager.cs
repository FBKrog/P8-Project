using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    public class TutorialStep
    {
        public string subtitleText;
        [Tooltip("Optional short ID for use with TutorialObjective.AdvanceIfStepId().")]
        public string stepId;
        [Tooltip("Seconds to display before auto-advancing. 0 = manual advance only.")]
        public float displayDuration = 3f;
        [Tooltip("Optional: name of an objective to complete when this step is shown.")]
        public string completesObjective;
    }

    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------
    [Header("Tutorial Active")]
    [Tooltip("If true, the tutorial plays automatically on Start. If false, it is skipped entirely.")]
    [SerializeField] private bool playTutorialOnStart = true;

    [Header("VR Player Camera")]
    [Tooltip("Leave empty to use Camera.main.")]
    [SerializeField] private Camera vrCamera;

    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    [Header("Layout")]
    [SerializeField] private float maxPanelWidth = 0.8f;   // meters → *100 for canvas units
    [SerializeField] private float panelPadding  = 8f;     // canvas units around text

    [Header("Follow Behavior")]
    [SerializeField] private float followDistance  = 1.5f;   // meters in front of camera
    [Tooltip("Horizontal offset in camera-local space (meters). Positive = right.")]
    [SerializeField] private float viewOffsetX     = 0f;
    [Tooltip("Vertical offset in camera-local space (meters). Positive = up.")]
    [SerializeField] private float viewOffsetY     = -0.2f;
    [SerializeField] private float followSmoothing = 0.3f;   // SmoothDamp time (lower=snappier)
    [SerializeField] private float rotationSpeed   = 5f;     // Slerp speed multiplier

    [Header("Visuals")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float fontSize = 5f;
    [Tooltip("Optional TMP font. Uses TMP default if null.")]
    [SerializeField] private TMP_FontAsset subtitleFont;

    [Header("Dependencies")]
    [Tooltip("Auto-found if null.")]
    [SerializeField] private ObjectivesManager objectivesManager;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private GameObject      _panelRoot;
    private Image           _backgroundImage;
    private TextMeshProUGUI _subtitleText;
    private int             _currentStepIndex = -1;
    private bool            _tutorialActive;
    private Coroutine       _autoAdvanceCoroutine;
    private Vector3         _followVelocity;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (vrCamera == null)
            vrCamera = Camera.main;

        if (vrCamera == null)
            Debug.LogWarning("[TutorialManager] No camera found. Assign vrCamera or tag a camera MainCamera.");

        BuildSubtitlePanel();
        HideSubtitle();
    }

    private void Start()
    {
        if (objectivesManager == null)
            objectivesManager = FindFirstObjectByType<ObjectivesManager>();

        if (playTutorialOnStart)
            StartTutorial();
    }

    private void LateUpdate()
    {
        if (_panelRoot == null || !_panelRoot.activeSelf || vrCamera == null) return;
        UpdateFollowTransform();
    }

    private void OnDestroy()
    {
        if (_panelRoot != null)
            Destroy(_panelRoot);
    }

    // -------------------------------------------------------------------------
    // Panel construction
    // -------------------------------------------------------------------------

    private void BuildSubtitlePanel()
    {
        if (vrCamera == null) return;

        // --- Canvas root (world space, not parented to camera) ---
        var canvasGO = new GameObject("[TutorialManager] SubtitleCanvas");
        canvasGO.transform.position   = GetTargetPosition();
        canvasGO.transform.rotation   = GetTargetRotation();
        canvasGO.transform.localScale = Vector3.one * 0.01f;   // 1 canvas unit = 1 cm

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        _panelRoot = canvasGO;

        // --- Background panel ---
        var panelGO = new GameObject("Background");
        panelGO.transform.SetParent(canvasGO.transform, false);

        var panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        _backgroundImage             = panelGO.AddComponent<Image>();
        _backgroundImage.color       = backgroundColor;
        _backgroundImage.type        = Image.Type.Simple;
        _backgroundImage.preserveAspect = false;

        // --- Text ---
        var textGO = new GameObject("SubtitleText");
        textGO.transform.SetParent(panelGO.transform, false);

        textGO.AddComponent<RectTransform>();

        _subtitleText = textGO.AddComponent<TextMeshProUGUI>();
        _subtitleText.color              = textColor;
        _subtitleText.fontSize           = fontSize;
        _subtitleText.alignment          = TextAlignmentOptions.Center;
        _subtitleText.textWrappingMode   = TextWrappingModes.Normal;

        if (subtitleFont != null)
            _subtitleText.font = subtitleFont;
    }

    // -------------------------------------------------------------------------
    // Follow helpers
    // -------------------------------------------------------------------------

    private Vector3 GetTargetPosition()
    {
        if (vrCamera == null) return Vector3.zero;
        var t = vrCamera.transform;
        return t.position
            + t.forward * followDistance
            + t.right   * viewOffsetX
            + t.up      * viewOffsetY;
    }

    private Quaternion GetTargetRotation()
    {
        if (vrCamera == null) return Quaternion.identity;
        return Quaternion.LookRotation(vrCamera.transform.forward, vrCamera.transform.up);
    }

    private void UpdateFollowTransform()
    {
        _panelRoot.transform.position = Vector3.SmoothDamp(
            _panelRoot.transform.position, GetTargetPosition(),
            ref _followVelocity, followSmoothing);
        _panelRoot.transform.rotation = Quaternion.Slerp(
            _panelRoot.transform.rotation, GetTargetRotation(),
            Time.deltaTime * rotationSpeed);
    }

    // -------------------------------------------------------------------------
    // Adaptive sizing
    // -------------------------------------------------------------------------

    private void ResizeCanvasToFitText()
    {
        float hPad       = panelPadding;
        float vPad       = panelPadding * 0.6f;
        float maxCanvasW = maxPanelWidth * 100f;
        float maxTextW   = maxCanvasW - hPad * 2f;

        var canvasRT = (RectTransform)_panelRoot.transform;

        // Give text rect full measurement space
        canvasRT.sizeDelta = new Vector2(maxCanvasW, 200f);
        _subtitleText.rectTransform.anchorMin = Vector2.zero;
        _subtitleText.rectTransform.anchorMax = Vector2.one;
        _subtitleText.rectTransform.offsetMin = new Vector2( hPad,  vPad);
        _subtitleText.rectTransform.offsetMax = new Vector2(-hPad, -vPad);

        // Phase 1: unconstrained width
        _subtitleText.textWrappingMode = TextWrappingModes.Normal;
        _subtitleText.ForceMeshUpdate();
        float unconstrainedW = _subtitleText.preferredWidth;

        float canvasW, canvasH;
        if (unconstrainedW <= maxTextW)
        {
            canvasW = unconstrainedW + hPad * 2f;
            canvasH = _subtitleText.preferredHeight + vPad * 2f;
        }
        else
        {
            // Phase 2: wrap at max width
            _subtitleText.textWrappingMode = TextWrappingModes.Normal;
            _subtitleText.ForceMeshUpdate();
            canvasW = maxCanvasW;
            canvasH = _subtitleText.preferredHeight + vPad * 2f;
        }

        canvasRT.sizeDelta = new Vector2(canvasW, canvasH);
        _subtitleText.rectTransform.offsetMin = new Vector2( hPad,  vPad);
        _subtitleText.rectTransform.offsetMax = new Vector2(-hPad, -vPad);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Whether the tutorial is currently active.
    /// </summary>
    public bool IsTutorialActive => _tutorialActive;

    /// <summary>
    /// The stepId of the currently displayed step, or null if no step is active.
    /// </summary>
    public string CurrentStepId =>
        _currentStepIndex >= 0 && _currentStepIndex < steps.Count
            ? steps[_currentStepIndex].stepId
            : null;

    public void StartTutorial()
    {
        _tutorialActive   = true;
        _currentStepIndex = -1;
        AdvanceToNextStep();
    }

    public void AdvanceToNextStep()
    {
        if (_autoAdvanceCoroutine != null)
        {
            StopCoroutine(_autoAdvanceCoroutine);
            _autoAdvanceCoroutine = null;
        }

        _currentStepIndex++;

        if (_currentStepIndex >= steps.Count)
        {
            _tutorialActive = false;
            HideSubtitle();
            return;
        }

        var step = steps[_currentStepIndex];

        if (!string.IsNullOrEmpty(step.completesObjective) && objectivesManager != null)
            objectivesManager.CompleteObjective(step.completesObjective);

        ShowSubtitle(step.subtitleText);

        if (step.displayDuration > 0f)
            _autoAdvanceCoroutine = StartCoroutine(AutoAdvance(step.displayDuration));
    }

    public void ShowSubtitle(string text)
    {
        if (_panelRoot == null) return;
        if (!_panelRoot.activeSelf)   // snap to target when re-showing
        {
            _panelRoot.transform.position = GetTargetPosition();
            _panelRoot.transform.rotation = GetTargetRotation();
            _followVelocity = Vector3.zero;
        }
        _panelRoot.SetActive(true);
        _subtitleText.text = text;
        ResizeCanvasToFitText();
    }

    public void HideSubtitle()
    {
        if (_panelRoot == null) return;
        _panelRoot.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Coroutines
    // -------------------------------------------------------------------------

    private IEnumerator AutoAdvance(float delay)
    {
        yield return new WaitForSeconds(delay);
        AdvanceToNextStep();
    }
}
