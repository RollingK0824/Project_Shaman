using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public class StressSystemTests
{
    private readonly List<GameObject> _objects = new();

    private GameObject Create(string name)
    {
        var obj = new GameObject(name);
        obj.transform.position = new Vector3(1000f, 1000f, 1000f);
        _objects.Add(obj);
        return obj;
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var obj in _objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        _objects.Clear();
    }

    [Test]
    public void RangeAndInvalidInputsCannotCorruptStress()
    {
        var stress = Create("Player").AddComponent<PlayerStressController>();
        stress.SetStress(200f);
        Assert.AreEqual(100f, stress.CurrentStress);
        stress.SetStress(-10f);
        Assert.AreEqual(0f, stress.CurrentStress);
        stress.SetStress(42f);
        stress.SetStress(float.NaN);
        stress.AddStress(float.PositiveInfinity);
        stress.ReduceStress(-100f);
        Assert.AreEqual(42f, stress.CurrentStress);
    }

    [Test]
    public void ThresholdsAndEventsExposeConsistentState()
    {
        var stress = Create("Player").AddComponent<PlayerStressController>();
        int changes = 0;
        stress.StressLevelChanged += level => { Assert.AreEqual(level, stress.CurrentLevel); changes++; };
        stress.SetStress(34.9f);
        Assert.AreEqual(PlayerStressController.StressLevel.Low, stress.CurrentLevel);
        stress.SetStress(35f);
        Assert.AreEqual(PlayerStressController.StressLevel.Mid, stress.CurrentLevel);
        stress.SetStress(70f);
        Assert.AreEqual(PlayerStressController.StressLevel.High, stress.CurrentLevel);
        stress.SetStress(70f);
        stress.SetStress(0f);
        Assert.AreEqual(3, changes);
        Set(stress, "_midThreshold", 25f);
        Set(stress, "_highThreshold", 60f);
        stress.SendMessage("OnValidate");
        stress.SetStress(60f);
        Assert.AreEqual(PlayerStressController.StressLevel.High, stress.CurrentLevel);
    }

    [Test]
    public void GhostAndAnomalyStressNeverDamageOrKillPlayer()
    {
        var player = Create("Player");
        var health = player.AddComponent<PlayerHealth>();
        var stress = player.AddComponent<PlayerStressController>();
        float initialHealth = health.CurrentHealth;
        int received = 0;
        stress.StressReceived += (_, _, _) => received++;
        stress.ReceiveStress(70f, StressCause.GhostSight);
        stress.ReceiveStress(70f, StressCause.AnomalySight);
        Assert.AreEqual(2, received);
        Assert.AreEqual(100f, stress.CurrentStress);
        Assert.AreEqual(initialHealth, health.CurrentHealth);
        Assert.IsFalse(health.IsDead);
    }

    [UnityTest]
    public IEnumerator SafeSourceOverridesDarknessAndRemovingSourcesStopsChange()
    {
        var stress = Create("Player").AddComponent<PlayerStressController>();
        var dark = Create("Dark");
        var safe = Create("Safe");
        stress.SetStress(50f);
        stress.SetSourceRate(dark, 10f);
        yield return new WaitForSeconds(0.2f);
        Assert.Greater(stress.CurrentStress, 50f);
        float before = stress.CurrentStress;
        stress.SetSourceRate(safe, -15f);
        yield return new WaitForSeconds(0.2f);
        Assert.Less(stress.CurrentStress, before);
        stress.RemoveSource(safe);
        stress.RemoveSource(dark);
        before = stress.CurrentStress;
        yield return new WaitForSeconds(0.15f);
        Assert.AreEqual(before, stress.CurrentStress, 0.001f);
    }

    [UnityTest]
    public IEnumerator CompoundCollidersReceiveOneZoneRateAndDisabledZoneReleasesThem()
    {
        var player = Create("Player");
        var stress = player.AddComponent<PlayerStressController>();
        player.AddComponent<Rigidbody>().isKinematic = true;
        player.AddComponent<BoxCollider>();
        var child = Create("SecondCollider");
        child.transform.SetParent(player.transform, false);
        child.transform.localPosition = Vector3.zero;
        child.AddComponent<SphereCollider>();
        var zoneObj = Create("Dark");
        zoneObj.AddComponent<BoxCollider>().isTrigger = true;
        var zone = zoneObj.AddComponent<StressZone>();
        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.15f);
        float before = stress.CurrentStress;
        float start = Time.time;
        yield return new WaitForSeconds(0.3f);
        float expected = (Time.time - start) * 10f;
        Assert.AreEqual(expected, stress.CurrentStress - before, 0.7f);
        zone.enabled = false;
        before = stress.CurrentStress;
        yield return new WaitForSeconds(0.15f);
        Assert.AreEqual(before, stress.CurrentStress, 0.001f);
    }

    [UnityTest]
    public IEnumerator TeleportWithoutExitReleasesRecoveryAndAllowsAnomalyReentry()
    {
        var player = Create("TeleportPlayer");
        var stress = player.AddComponent<PlayerStressController>();
        var collider = player.AddComponent<CharacterController>();
        var safeObject = Create("Safe");
        safeObject.AddComponent<BoxCollider>().isTrigger = true;
        var safe = safeObject.AddComponent<StressZone>();
        Set(safe, "_zoneType", StressZone.StressZoneType.Reduce);
        var anomalyObject = Create("Anomaly");
        anomalyObject.AddComponent<BoxCollider>().isTrigger = true;
        var anomaly = anomalyObject.AddComponent<StressEventTrigger>();
        stress.SetStress(50f);
        safe.SendMessage("OnTriggerEnter", collider);
        anomaly.SendMessage("OnTriggerEnter", collider);
        yield return new WaitForSeconds(0.1f);
        Assert.Less(stress.CurrentStress, 70f);
        collider.enabled = false;
        player.transform.position += Vector3.right * 20f;
        collider.enabled = true;
        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.1f);
        float outside = stress.CurrentStress;
        yield return new WaitForSeconds(0.1f);
        Assert.AreEqual(outside, stress.CurrentStress, 0.001f);
        player.transform.position = safeObject.transform.position;
        Physics.SyncTransforms();
        anomaly.SendMessage("OnTriggerEnter", collider);
        Assert.AreEqual(outside + 20f, stress.CurrentStress, 0.001f);
    }

    [Test]
    public void EventTriggerTracksEachPlayerAndTheirCompleteExit()
    {
        var triggerObj = Create("Anomaly");
        triggerObj.AddComponent<BoxCollider>().isTrigger = true;
        var trigger = triggerObj.AddComponent<StressEventTrigger>();
        var a = Create("A");
        var sa = a.AddComponent<PlayerStressController>();
        var a1 = a.AddComponent<BoxCollider>();
        var a2 = a.AddComponent<SphereCollider>();
        var b = Create("B");
        var sb = b.AddComponent<PlayerStressController>();
        var b1 = b.AddComponent<BoxCollider>();
        trigger.SendMessage("OnTriggerEnter", a1);
        trigger.SendMessage("OnTriggerEnter", a2);
        trigger.SendMessage("OnTriggerEnter", b1);
        Assert.AreEqual(20f, sa.CurrentStress);
        Assert.AreEqual(20f, sb.CurrentStress);
        trigger.SendMessage("OnTriggerExit", a1);
        trigger.SendMessage("OnTriggerEnter", a1);
        Assert.AreEqual(20f, sa.CurrentStress);
        trigger.SendMessage("OnTriggerExit", a1);
        trigger.SendMessage("OnTriggerExit", a2);
        trigger.SendMessage("OnTriggerEnter", a1);
        Assert.AreEqual(40f, sa.CurrentStress);
    }

    [UnityTest]
    public IEnumerator FeedbackScalesAndRecoversWithoutLosingHitFeedback()
    {
        var player = Create("FeedbackPlayer");
        player.SetActive(false);
        var stress = player.AddComponent<PlayerStressController>();
        var health = player.AddComponent<PlayerHealth>();
        var feedback = player.AddComponent<PlayerFeedbackController>();
        var pivot = Create("Pivot").transform;
        pivot.SetParent(player.transform, false);
        pivot.localPosition = Vector3.zero;
        var cameraObj = Create("Camera");
        cameraObj.transform.SetParent(pivot, false);
        cameraObj.transform.localPosition = Vector3.zero;
        var camera = cameraObj.AddComponent<Camera>();
        var listener = cameraObj.AddComponent<AudioListener>();
        var hand = Create("Hand").transform;
        hand.SetParent(cameraObj.transform, false);
        hand.localPosition = Vector3.zero;
        var volume = Create("StressVolume").AddComponent<Volume>();
        var hitVolume = Create("HitVolume").AddComponent<Volume>();
        var heart = cameraObj.AddComponent<AudioSource>();
        var breath = cameraObj.AddComponent<AudioSource>();
        var clip = AudioClip.Create("TestLoop", 2400, 1, 24000, false);
        heart.clip = clip;
        breath.clip = clip;
        Set(feedback, "_feedbackPivot", pivot);
        Set(feedback, "_feedbackCamera", camera);
        Set(feedback, "_handFeedbackPivot", hand);
        Set(feedback, "_stressVolume", volume);
        Set(feedback, "_hitVolume", hitVolume);
        Set(feedback, "_heartbeatSource", heart);
        Set(feedback, "_breathingSource", breath);
        player.SetActive(true);
        yield return null;
        Assert.AreEqual(0f, feedback.StressFeedbackIntensity);
        Assert.IsFalse(heart.isPlaying);
        stress.SetStress(50f);
        yield return new WaitForSeconds(0.6f);
        float mid = feedback.StressFeedbackIntensity;
        Assert.Greater(mid, 0f);
        Assert.IsTrue(heart.isPlaying && breath.isPlaying);
        Assert.AreEqual(0f, volume.weight);
        stress.SetStress(100f);
        yield return new WaitForSeconds(0.6f);
        Assert.Greater(feedback.StressFeedbackIntensity, mid);
        Assert.AreEqual(1f, volume.weight, 0.01f);
        Assert.Greater(hand.localPosition.sqrMagnitude, 0f);
        health.ReceiveDamage(5f);
        yield return null;
        Assert.Greater(hitVolume.weight, 0f);
        Assert.AreEqual(95f, health.CurrentHealth);
        listener.enabled = false;
        yield return null;
        Assert.AreEqual(0f, volume.weight);
        Assert.IsFalse(heart.isPlaying);
        Assert.AreEqual(Vector3.zero, hand.localPosition);
        listener.enabled = true;
        stress.SetStress(0f);
        yield return new WaitForSeconds(0.7f);
        Assert.AreEqual(0f, feedback.StressFeedbackIntensity);
        Assert.AreEqual(Vector3.zero, pivot.localPosition);
        Assert.IsFalse(breath.isPlaying);
        UnityEngine.Object.DestroyImmediate(clip);
    }
}
