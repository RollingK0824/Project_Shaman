using UnityEngine;

// World pickup remains a WorldItem. A network mounted flag calls SetMounted on
// every peer (also for late joiners); it must not simulate a falling rigidbody.
[DisallowMultipleComponent]
public sealed class LanternWorldPresentation : MonoBehaviour
{
    private Light _light;
    public bool IsMounted { get; private set; }
    private void OnEnable()
    {
        if (_light == null)
        {
            _light = GetComponentInChildren<Light>(true);
            if (_light == null)
            {
                var bulb = new GameObject("LanternWorldLight");
                bulb.transform.SetParent(transform, false);
                bulb.transform.localPosition = new Vector3(0, .23f, 0);
                _light = bulb.AddComponent<Light>();
                _light.type = LightType.Point; _light.color = new Color(1f, .72f, .38f);
                _light.range = 5f; _light.intensity = 1.5f;
                _light.shadows = LightShadows.None;
            }
        }
        _light.enabled = true;
    }
    private void OnDisable() { if (_light != null) _light.enabled = false; }
    public void SetMounted(bool mounted)
    {
        IsMounted = mounted;
        if (!mounted) return;
        foreach (var body in GetComponentsInChildren<Rigidbody>(true))
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.useGravity = false; body.isKinematic = true;
        }
    }
}
