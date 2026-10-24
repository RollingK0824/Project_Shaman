using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public class DeathSpectatorTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();
    private Keyboard _keyboard;
    private ItemData _item;

    private GameObject Create(string name)
    {
        var obj = new GameObject(name);
        obj.transform.position = new Vector3(2000, 2000, 2000);
        _objects.Add(obj);
        return obj;
    }
    private static void Set(object obj, string field, object value) =>
        obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
    private GameObject Player(string name, bool spectator = false)
    {
        var player = Create(name);
        player.AddComponent<PlayerHealth>();
        player.AddComponent<PlayerInputReader>();
        player.AddComponent<PlayerInventory>();
        var camera = Create(name + " Camera");
        camera.transform.SetParent(player.transform, false);
        camera.transform.localPosition = Vector3.zero;
        camera.AddComponent<Camera>();
        if (spectator) player.AddComponent<PlayerSpectatorController>();
        return player;
    }
    private void Key(params Key[] keys)
    {
        if (_keyboard == null) _keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
        InputSystem.Update();
    }
    [TearDown]
    public void Cleanup()
    {
        foreach (var obj in _objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        _objects.Clear();
        if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
        _keyboard = null;
        if (_item != null) UnityEngine.Object.DestroyImmediate(_item);
    }

    [UnityTest]
    public IEnumerator FInputCollectsThroughExistingOfflineJudge()
    {
        Create("Judge").AddComponent<LocalGameplayJudge>();
        var player = Player("Player");
        var interactor = player.AddComponent<PlayerInteractor>();
        Set(interactor, "_playerCamera", player.GetComponentInChildren<Camera>());
        var itemObject = Create("World Item");
        itemObject.transform.position += Vector3.forward * 2f;
        itemObject.AddComponent<BoxCollider>();
        var item = itemObject.AddComponent<WorldItem>();
        _item = ScriptableObject.CreateInstance<ItemData>();
        Set(item, "_itemData", _item);
        Physics.SyncTransforms();
        yield return null;
        Assert.AreSame(item, interactor.CurrentTarget);
        Key(UnityEngine.InputSystem.Key.F);
        yield return null;
        Key();
        Assert.IsTrue(player.GetComponent<PlayerInventory>().Contains(_item));
        Assert.IsFalse(itemObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator DeathBlocksWorldInputsButKeepsNotebookAndCancel()
    {
        var player = Player("Player");
        var input = player.GetComponent<PlayerInputReader>();
        int world = 0, notebook = 0, cancel = 0;
        input.InteractPressed += () => world++;
        input.JumpPressed += () => world++;
        input.SlotSelected += _ => world++;
        input.NotebookPressed += () => notebook++;
        input.CancelPressed += () => cancel++;
        player.GetComponent<PlayerHealth>().ApplyDeath();
        Key(UnityEngine.InputSystem.Key.F, UnityEngine.InputSystem.Key.Space, UnityEngine.InputSystem.Key.Digit1, UnityEngine.InputSystem.Key.Tab, UnityEngine.InputSystem.Key.Escape);
        yield return null;
        Assert.AreEqual(0, world);
        Assert.AreEqual(1, notebook);
        Assert.AreEqual(1, cancel);
        Assert.IsTrue(input.enabled);
        Key();
        input.SetGameplayInputBlocked(true);
        Key(UnityEngine.InputSystem.Key.Tab);
        Assert.AreEqual(2, notebook);
    }

    [Test]
    public void DeathPreventsDirectPickupSlotsNpcInspectionAndRitual()
    {
        var player = Player("Player");
        _item = ScriptableObject.CreateInstance<ItemData>();
        var inventory = player.GetComponent<PlayerInventory>();
        Assert.IsTrue(inventory.TryAddItem(_item));
        var health = player.GetComponent<PlayerHealth>();
        health.ApplyDeath();
        Assert.IsFalse(inventory.TryAddItem(_item));
        Assert.IsFalse(inventory.AssignQuickSlot(2, _item));
        Assert.IsFalse(inventory.ClearQuickSlot(0));
        var npc = Create("NPC").AddComponent<NPCObservationInteractable>();
        var inspect = Create("Inspect").AddComponent<InspectInteractable>();
        var flagObject = Create("Flag");
        flagObject.AddComponent<BoxCollider>();
        var flag = flagObject.AddComponent<ExorcismFlagInteractable>();
        flag.SetRitualActive(true);
        flag.Interact(player);
        Assert.IsFalse(flag.IsActivated);
        Assert.IsFalse(npc.CanInteract(player));
        Assert.IsFalse(inspect.CanInteract(player));
        Assert.IsFalse(health.CanRecordObservations);
        Assert.IsTrue(health.IsNotebookReadOnly);
        Assert.IsTrue(inventory.Contains(_item));
    }

    [Test]
    public void NonAuthorityCannotMutateHealthOrConfirmDeath()
    {
        var health = Player("Replica").GetComponent<PlayerHealth>();
        int died = 0;
        health.Died += _ => died++;
        health.SetAuthorityCheck(() => false);
        health.ReceiveDamage(100);
        health.SetHealth(0);
        health.ApplyDeath();
        Assert.AreEqual(100, health.CurrentHealth);
        Assert.IsFalse(health.IsDead);
        health.SetAuthorityCheck(() => true);
        health.SetHealth(0);
        health.ApplyDeath();
        Assert.AreEqual(1, died);
    }

    [UnityTest]
    public IEnumerator FreeCameraMovesWithoutMovingDeadBodyAndHonorsBounds()
    {
        var player = Player("Player", true);
        var spectator = player.GetComponent<PlayerSpectatorController>();
        var camera = player.GetComponentInChildren<Camera>();
        Vector3 body = player.transform.position;
        player.GetComponent<PlayerHealth>().ApplyDeath();
        Assert.IsTrue(spectator.IsSpectating);
        Assert.IsFalse(camera.enabled);
        var sourceCamera = camera;
        camera = spectator.SpectatorCamera;
        Assert.IsNull(camera.transform.parent);
        Key(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.Space);
        yield return new WaitForSeconds(0.15f);
        Key();
        Assert.Greater(Vector3.Distance(body, camera.transform.position), 0.1f);
        Assert.AreEqual(body, player.transform.position);
        var bounds = Create("Bounds").AddComponent<BoxCollider>();
        bounds.size = Vector3.one;
        Set(spectator, "_flightBounds", bounds);
        Key(UnityEngine.InputSystem.Key.W);
        yield return new WaitForSeconds(0.2f);
        Key();
        Assert.IsTrue(bounds.bounds.Contains(camera.transform.position));
        spectator.enabled = false;
        Assert.AreSame(player.transform, sourceCamera.transform.parent);
        Assert.IsTrue(sourceCamera.enabled);
    }

    [Test]
    public void FollowCyclesSurvivorsAndRecoversFromDeathDisconnectAndAllDead()
    {
        var owner = Player("Owner", true);
        var a = Player("A");
        var b = Player("B");
        var spectator = owner.GetComponent<PlayerSpectatorController>();
        int noTargets = 0;
        spectator.NoLivingTargets += () => noTargets++;
        owner.GetComponent<PlayerHealth>().ApplyDeath();
        spectator.NextTarget();
        Assert.AreSame(a, spectator.SpectateTarget);
        spectator.NextTarget();
        Assert.AreSame(b, spectator.SpectateTarget);
        a.GetComponent<PlayerHealth>().ApplyDeath();
        Assert.AreSame(b, spectator.SpectateTarget);
        b.SetActive(false);
        Assert.IsNull(spectator.SpectateTarget);
        Assert.AreEqual(1, noTargets);
        spectator.NextTarget();
        spectator.PreviousTarget();
        Assert.AreEqual(1, noTargets);
        Assert.IsTrue(spectator.IsSpectating);
    }

    [Test]
    public void ServerRosterEmitsAllDeadOnceAndNotForEmptySession()
    {
        var a = Create("A").AddComponent<NetPlayerStatus>();
        var b = Create("B").AddComponent<NetPlayerStatus>();
        int allDead = 0;
        Action onAllDead = () => allDead++;
        PlayerRoster.AllPlayersDead += onAllDead;
        try
        {
            PlayerRoster.Register(a);
            PlayerRoster.Register(b);
            Set(a, "_isDead", true);
            PlayerRoster.NotifyChanged();
            Assert.AreEqual(1, PlayerRoster.AliveCount);
            Assert.AreEqual(0, allDead);
            Set(b, "_isDead", true);
            PlayerRoster.NotifyChanged();
            PlayerRoster.NotifyChanged();
            Assert.AreEqual(1, allDead);
            PlayerRoster.Unregister(a);
            PlayerRoster.Unregister(b);
            Assert.AreEqual(1, allDead);
        }
        finally
        {
            PlayerRoster.AllPlayersDead -= onAllDead;
            PlayerRoster.Unregister(a);
            PlayerRoster.Unregister(b);
        }
    }
}
