#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GongMovementVfxTests
{
    readonly List<GameObject> _objects = new();
    GameObject Keep(GameObject value) { _objects.Add(value); return value; }
    [TearDown] public void TearDown()
    {
        foreach (var go in _objects) if (go != null) Object.DestroyImmediate(go);
        _objects.Clear();
    }

    [Test] public void FirstPersonPulseStaysInViewWhileMovingAndTurning()
    {
        var owner = Keep(new GameObject("Gong movement owner"));
        owner.transform.position = new Vector3(3000, 3000, 3000);
        var camera = Keep(new GameObject("Gong view camera")).AddComponent<Camera>();
        camera.transform.SetParent(owner.transform, false);
        Assert.IsTrue(ItemVfxPresentation.Pulse(owner, true));
        GongViewPulse pulse = null;
        foreach (var candidate in Object.FindObjectsByType<GongViewPulse>(FindObjectsSortMode.None))
            if (Vector3.Distance(candidate.transform.position, owner.transform.position) < 5) { pulse = candidate; break; }
        Assert.IsNotNull(pulse); Keep(pulse.gameObject);
        var scale = pulse.transform.localScale;
        foreach (var ps in pulse.GetComponentsInChildren<ParticleSystem>())
            Assert.AreEqual(ParticleSystemSimulationSpace.Local, ps.main.simulationSpace);
        foreach (var offset in new[] { Vector3.forward * 8f, Vector3.right * 8f, Vector3.back * 8f })
        {
            owner.transform.position += offset;
            camera.transform.rotation = Quaternion.Euler(25f, camera.transform.eulerAngles.y + 70f, 0f);
            typeof(GongViewPulse).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pulse, null);
            Assert.Less(Vector3.Distance(camera.transform.InverseTransformPoint(pulse.transform.position), GongViewPulse.CameraOffset), .001f);
            var screen = camera.WorldToViewportPoint(pulse.transform.position);
            Assert.That(screen.x, Is.InRange(.1f, .9f)); Assert.That(screen.y, Is.InRange(.1f, .9f));
            Assert.Greater(screen.z, camera.nearClipPlane);
            Assert.AreEqual(scale, pulse.transform.localScale);
        }
        camera.enabled = false;
        typeof(GongViewPulse).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pulse, null);
        Assert.IsFalse(pulse.gameObject.activeSelf);
    }

    [Test] public void WorldPulseWithoutLocalViewStaysAtUsePosition()
    {
        var owner = Keep(new GameObject("World gong owner"));
        owner.transform.position = new Vector3(4000, 4000, 4000);
        Assert.IsTrue(ItemVfxPresentation.Pulse(owner, true));
        ItemVfxPresentation effect = null;
        foreach (var candidate in Object.FindObjectsByType<ItemVfxPresentation>(FindObjectsSortMode.None))
            if (Vector3.Distance(candidate.transform.position, owner.transform.position) < 5) { effect = candidate; break; }
        Assert.IsNotNull(effect); Keep(effect.gameObject);
        Assert.IsNull(effect.GetComponent<GongViewPulse>());
        var origin = effect.transform.position;
        owner.transform.position += Vector3.right * 10f;
        Assert.AreEqual(origin, effect.transform.position);
    }
}
#endif
