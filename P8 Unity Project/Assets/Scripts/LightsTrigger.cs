using System.Collections.Generic;
using UnityEngine;

public class LightsTrigger : MonoBehaviour
{
    [SerializeField] private List<Light> lights = new();

    private void Awake() => Disable();

    public void Enable()
    {
        foreach (var light in lights)
            if (light != null) light.enabled = true;
    }

    public void Disable()
    {
        foreach (var light in lights)
            if (light != null) light.enabled = false;
    }
}
