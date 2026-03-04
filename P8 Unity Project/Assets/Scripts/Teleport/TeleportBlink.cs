using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Produces a blink-to-black transition with correct ordering:
///   1. Fade to black
///   2. Teleport fires (position changes while screen is black)
///   3. Fade back in
///
/// Hooks into TeleportationActivator.onBeforeTeleport so it controls
/// exactly when the teleport executes, preventing the player from ever
/// seeing themselves at the new location before the screen goes dark.
///
/// Drop this component on any persistent GameObject in the scene (e.g. VR Player).
/// Drag the TeleportationActivator into the Activator Override field, or leave it
/// empty to let the script find it automatically.
/// </summary>
public class TeleportBlink : MonoBehaviour
{
    [Tooltip("Drag the GameObject that has TeleportationActivator here. Leave empty to search automatically.")]
    [SerializeField] private TeleportationActivator activatorOverride;

    [Tooltip("How fast the screen closes (fades to black).")]
    [SerializeField, Range(0.02f, 0.4f)] private float fadeOutDuration = 0.08f;

    [Tooltip("How many frames the screen stays fully black before the teleport position change fires. " +
             "0 = teleport fires immediately once black. Increase if you still catch a glimpse of the old location.")]
    [SerializeField, Range(0, 20)] private int blackFramesBeforeExecute = 2;

    [Tooltip("How many frames to wait after the teleport fires before fading back in. " +
             "Increase if you catch a glimpse of the new location appearing mid-fade.")]
    [SerializeField, Range(1, 20)] private int blackFramesAfterExecute = 2;

    [Tooltip("How fast the screen opens (fades back in).")]
    [SerializeField, Range(0.05f, 0.6f)] private float fadeInDuration = 0.18f;

    private TeleportationActivator _activator;
    private Material _fadeMaterial;
    private Coroutine _blinkCoroutine;

    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Register the callback in Awake (before any Start) so it is guaranteed
        // to be set by the time TeleportationActivator.Update() first runs.
        _activator = activatorOverride != null
            ? activatorOverride
            : FindFirstObjectByType<TeleportationActivator>();

        if (_activator != null)
        {
            _activator.onBeforeTeleport = OnBeforeTeleport;
            Debug.Log($"[TeleportBlink] Callback registered on '{_activator.gameObject.name}'.");
        }
        else
        {
            Debug.LogWarning("[TeleportBlink] No TeleportationActivator found — blink will not trigger. " +
                             "Assign one to the Activator Override field on TeleportBlink.");
        }
    }

    private void Start()
    {
        var cam = Camera.main;
        if (cam != null)
            _fadeMaterial = BuildOverlay(cam);
        else
            Debug.LogWarning("[TeleportBlink] No camera tagged MainCamera found — overlay will be invisible.");
    }

    private void OnDestroy()
    {
        if (_activator != null)
            _activator.onBeforeTeleport = null;

        if (_fadeMaterial != null)
            Destroy(_fadeMaterial);
    }

    // -------------------------------------------------------------------------

    private void OnBeforeTeleport(System.Action executeTeleport)
    {
        Debug.Log("[TeleportBlink] OnBeforeTeleport fired — starting blink.");
        if (_blinkCoroutine != null)
            StopCoroutine(_blinkCoroutine);

        _blinkCoroutine = StartCoroutine(BlinkThenTeleport(executeTeleport));
    }

    private IEnumerator BlinkThenTeleport(System.Action executeTeleport)
    {
        // 1. Fade to black — runs over multiple frames before anything moves
        for (float t = 0f; t < fadeOutDuration; t += Time.deltaTime)
        {
            SetAlpha(t / fadeOutDuration);
            yield return null;
        }
        SetAlpha(1f);

        // 2. Hold fully black for N frames before firing the position change.
        for (int i = 0; i < blackFramesBeforeExecute; i++)
            yield return null;

        // 3. Fire the teleport while the screen is fully black.
        //    SetActive(false) on the interactor triggers the hover-exit on the
        //    TeleportationArea, which queues the position change with TeleportationProvider.
        executeTeleport();

        // 4. Wait N frames for TeleportationProvider.Update() to apply the
        //    position change before we start fading back in.
        for (int i = 0; i < blackFramesAfterExecute; i++)
            yield return null;

        // 5. Fade back in to reveal the new location
        for (float t = 0f; t < fadeInDuration; t += Time.deltaTime)
        {
            SetAlpha(1f - t / fadeInDuration);
            yield return null;
        }
        SetAlpha(0f);

        _blinkCoroutine = null;
    }

    private void SetAlpha(float alpha)
    {
        if (_fadeMaterial != null)
            _fadeMaterial.SetColor("_BaseColor", new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)));
    }

    // -------------------------------------------------------------------------

    private Material BuildOverlay(Camera cam)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "[TeleportBlink] FadeQuad";
        Destroy(go.GetComponent<Collider>());

        go.transform.SetParent(cam.transform, false);

        // Placed just past the near clip plane; sized to cover both VR eyes
        // including any IPD-driven per-eye offset in single-pass instanced rendering.
        float dist = cam.nearClipPlane + 0.01f;
        float size = dist * 10f;
        go.transform.localPosition = new Vector3(0f, 0f, dist);
        go.transform.localScale = new Vector3(size, size, 1f);

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("[TeleportBlink] 'Universal Render Pipeline/Unlit' shader not found.");
            Destroy(go);
            return null;
        }

        var mat = new Material(shader) { name = "[TeleportBlink] FadeMat" };
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_ZTest", (float)CompareFunction.Always);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 4000;

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return mat;
    }
}
