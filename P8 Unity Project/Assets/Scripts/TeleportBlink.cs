using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Produces a quick blink-to-black transition on every teleport.
/// Drop this component on any persistent GameObject in the scene (e.g. VR Player).
/// It auto-discovers the TeleportationProvider if the Inspector reference is left empty.
///
/// Uses a camera-parented quad with ZTest Always so the fade is visible inside
/// the VR headset on both eyes, not just the flat game view.
/// </summary>
public class TeleportBlink : MonoBehaviour
{
    [Tooltip("The TeleportationProvider to listen to. Auto-found at runtime if left empty.")]
    [SerializeField] private TeleportationProvider teleportationProvider;

    [Tooltip("How fast the screen closes (fades to black). Keep short — like an eyelid closing.")]
    [SerializeField, Range(0.02f, 0.4f)] private float fadeOutDuration = 0.08f;

    [Tooltip("How fast the screen opens (fades back in). Slightly slower feels natural.")]
    [SerializeField, Range(0.05f, 0.6f)] private float fadeInDuration = 0.18f;

    private Material _fadeMaterial;
    private Coroutine _blinkCoroutine;

    // -------------------------------------------------------------------------

    private void Start()
    {
        var cam = Camera.main;
        if (cam != null)
            _fadeMaterial = BuildOverlay(cam);
        else
            Debug.LogWarning("[TeleportBlink] No camera tagged MainCamera found.");

        if (teleportationProvider == null)
            teleportationProvider = FindFirstObjectByType<TeleportationProvider>();

        if (teleportationProvider != null)
            teleportationProvider.locomotionStarted += OnLocomotionStarted;
        else
            Debug.LogWarning("[TeleportBlink] No TeleportationProvider found. " +
                             "Assign it via the Inspector, or make sure one exists in the scene.");
    }

    private void OnDestroy()
    {
        if (teleportationProvider != null)
            teleportationProvider.locomotionStarted -= OnLocomotionStarted;

        if (_fadeMaterial != null)
            Destroy(_fadeMaterial);
    }

    // -------------------------------------------------------------------------

    private void OnLocomotionStarted(LocomotionProvider _)
    {
        if (_blinkCoroutine != null)
            StopCoroutine(_blinkCoroutine);

        _blinkCoroutine = StartCoroutine(Blink());
    }

    private IEnumerator Blink()
    {
        // Eyelid closes — fast fade to black
        for (float t = 0f; t < fadeOutDuration; t += Time.deltaTime)
        {
            SetAlpha(t / fadeOutDuration);
            yield return null;
        }
        SetAlpha(1f);

        // Eyelid opens — slightly slower fade in
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

    /// <summary>
    /// Creates a large quad parented to the camera, placed just past the near clip plane.
    /// ZTest Always ensures it renders on top of all scene geometry in both VR eyes —
    /// the Screen Space Overlay Canvas approach only shows in the flat game view.
    /// </summary>
    private Material BuildOverlay(Camera cam)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "[TeleportBlink] FadeQuad";
        Destroy(go.GetComponent<Collider>());

        // Parent to the camera so the quad tracks head rotation in the headset
        go.transform.SetParent(cam.transform, false);

        // Place just past the near clip plane so it is never occluded by the depth buffer.
        // The quad is sized large enough to cover the full FOV of both eyes, accounting
        // for any IPD-driven per-eye offset in single-pass instanced stereo rendering.
        float dist = cam.nearClipPlane + 0.01f;
        float size = dist * 10f; // Far exceeds any VR headset's FOV at this distance
        go.transform.localPosition = new Vector3(0f, 0f, dist);
        go.transform.localScale = new Vector3(size, size, 1f);

        // URP Unlit configured for transparent alpha-blended rendering.
        // ZTest Always overrides the depth test so the quad draws on top of
        // all scene geometry regardless of what is in the depth buffer.
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("[TeleportBlink] Could not find 'Universal Render Pipeline/Unlit' shader. " +
                           "Make sure the project uses URP.");
            Destroy(go);
            return null;
        }

        var mat = new Material(shader) { name = "[TeleportBlink] FadeMat" };
        mat.SetFloat("_Surface", 1f);                                       // Transparent
        mat.SetFloat("_Blend", 0f);                                         // Alpha blend
        mat.SetFloat("_ZWrite", 0f);                                        // No depth writes
        mat.SetFloat("_ZTest", (float)CompareFunction.Always);              // Draw over everything
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));             // Start fully transparent
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 4000;                                             // After all opaque/transparent geometry

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return mat;
    }
}
