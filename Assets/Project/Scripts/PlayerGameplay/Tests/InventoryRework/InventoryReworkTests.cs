#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class InventoryReworkTests
{
    const string Data = "Assets/Project/Data/Items/";
    readonly List<Object> cleanup = new();
    PlayerInventory inventory;
    PlayerHealth health;
    PlayerStressController stress;
    PlayerTalismanCollection collection;
    PlayerTalismanEffectController effects;
    TalismanItemData blank;
    GameObject ground;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    T Keep<T>(T value) where T : Object { cleanup.Add(value); return value; }
    T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Data + path);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    [SetUp] public void Setup()
    {
        var player = Keep(new GameObject("Inventory rework QA player"));
        player.transform.position = new Vector3(500, 0, 500);
        health = player.AddComponent<PlayerHealth>();
        stress = player.AddComponent<PlayerStressController>(); stress.enabled = false;
        inventory = player.AddComponent<PlayerInventory>();
        collection = player.AddComponent<PlayerTalismanCollection>();
        effects = player.GetComponent<PlayerTalismanEffectController>();
        blank = Load<TalismanItemData>("Talismans/RegistryTalisman.asset");
        ground = Keep(GameObject.CreatePrimitive(PrimitiveType.Cube));
        ground.name = "QA ground"; ground.transform.position = player.transform.position + Vector3.down * .5f;
        ground.transform.localScale = new Vector3(12, 1, 12); Physics.SyncTransforms();
    }
    [TearDown] public void Teardown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }
    TalismanDrawingData Save(string effectId)
    {
        Assert.IsTrue(inventory.TryAddItem(blank));
        var draft = collection.CreateDraft(blank);
        draft.BeginStroke(new Vector2(.2f, .2f)); draft.AppendPoint(new Vector2(.7f, .8f)); draft.EndStroke();
        Assert.IsTrue(collection.Save(blank, draft, "검증 부적", effectId)); return draft;
    }
    [TestCase(50, 35, true)] [TestCase(10, 0, true)] [TestCase(0, 0, false)]
    public void ReliefConsumesOnlyActualChange(float before, float after, bool success)
    {
        stress.SetStress(before); int changes = 0; stress.StressChanged += _ => changes++;
        var draft = Save("stress_gain"); Assert.IsTrue(inventory.AssignTalismanQuickSlot(2, draft.Id));
        Assert.AreEqual(success, collection.TryUse(draft.Id)); Assert.AreEqual(after, stress.CurrentStress);
        Assert.AreEqual(success ? 1 : 0, changes); Assert.AreEqual(success ? 0 : 1, collection.DrawnEffectCount);
        Assert.AreEqual(success, string.IsNullOrEmpty(inventory.GetQuickSlotInstanceId(2)));
        Assert.AreEqual(0, effects.ActiveCount); Assert.AreEqual(1, effects.StressGainMultiplier);
    }
    [TestCase(50, 70, true)] [TestCase(90, 100, true)] [TestCase(100, 100, false)]
    public void HealUsesHealthAPIAndConsumesOnlyActualChange(float before, float after, bool success)
    {
        health.SetHealth(before); int changes = 0; health.HealthChanged += (_, __) => changes++;
        var draft = Save("stress_recovery"); Assert.AreEqual(success, collection.TryUse(draft.Id));
        Assert.AreEqual(after, health.CurrentHealth); Assert.AreEqual(success ? 1 : 0, changes);
        Assert.AreEqual(success ? 0 : 1, collection.DrawnEffectCount); Assert.AreEqual(0, effects.ActiveCount);
    }
    [Test] public void DeathRejectsAllThreeAndClearsSpeed()
    {
        var a = Save("move_speed"); var b = Save("stress_gain"); var c = Save("stress_recovery");
        Assert.IsTrue(effects.TryApply(blank.FindEffect("move_speed"))); health.ReceiveDamage(100);
        foreach (var draft in new[] { a, b, c }) Assert.IsFalse(collection.TryUse(draft.Id));
        Assert.AreEqual(3, collection.DrawnEffectCount); Assert.AreEqual(1, effects.MovementMultiplier);
    }
    [Test] public void DeniedAuthorityAndPendingKeepInstanceAndBinding()
    {
        health.SetHealth(50); var draft = Save("stress_recovery"); inventory.AssignTalismanQuickSlot(3, draft.Id);
        effects.SetAuthorityCheck(() => false); Assert.IsFalse(collection.TryUse(draft.Id));
        effects.SetAuthorityCheck(() => true); health.SetAuthorityCheck(() => false); Assert.IsFalse(collection.TryUse(draft.Id));
        collection.SetAuthorityCheck(() => false); int requests = 0; collection.UseRequested += id => { Assert.AreEqual(draft.Id, id); requests++; };
        Assert.IsFalse(collection.RequestUse(draft.Id)); Assert.AreEqual(1, requests);
        Assert.AreEqual(50, health.CurrentHealth); Assert.AreEqual(1, collection.DrawnEffectCount); Assert.AreEqual(draft.Id, inventory.GetQuickSlotInstanceId(3));
    }
    [UnityTest] public IEnumerator SpeedRefreshExpiresAndDisableResetRestoreOriginal()
    {
        var definition = Keep(Object.Instantiate(blank.FindEffect("move_speed")));
        Assert.AreEqual(1.25f, definition.Magnitude); Assert.AreEqual(15, definition.Duration);
        Set(definition, "_duration", .3f);
        Assert.IsTrue(effects.TryApply(definition)); yield return new WaitForSeconds(.2f);
        Assert.IsTrue(effects.TryApply(definition)); yield return new WaitForSeconds(.15f);
        Assert.AreEqual(1.25f, effects.MovementMultiplier); Assert.AreEqual(1, effects.ActiveCount);
        yield return new WaitForSeconds(.2f); Assert.AreEqual(1, effects.MovementMultiplier);
        effects.TryApply(definition); effects.enabled = false; Assert.AreEqual(1, effects.MovementMultiplier);
        effects.enabled = true; effects.TryApply(definition); collection.ResetRound(); Assert.AreEqual(1, effects.MovementMultiplier);
    }
    [Test] public void ReentrantHealthObserverCannotUseTwice()
    {
        health.SetHealth(50); var draft = Save("stress_recovery"); bool reentrant = true;
        health.HealthChanged += (_, __) => reentrant = collection.TryUse(draft.Id);
        Assert.IsTrue(collection.TryUse(draft.Id)); Assert.IsFalse(reentrant); Assert.AreEqual(70, health.CurrentHealth); Assert.AreEqual(0, collection.DrawnEffectCount);
    }
    [Test] public void FailingObserverDoesNotGrantFreeHealing()
    {
        health.SetHealth(50); var draft = Save("stress_recovery");
        health.HealthChanged += (_, __) => throw new InvalidOperationException("QA observer");
        LogAssert.Expect(LogType.Exception, "InvalidOperationException: QA observer");
        Assert.IsTrue(collection.TryUse(draft.Id)); Assert.AreEqual(70, health.CurrentHealth); Assert.AreEqual(0, collection.DrawnEffectCount);
    }
    [Test] public void DrawingIdentityAndDataSurviveSaveAndFailedUse()
    {
        var draft = Save("stress_recovery"); var before = collection.Export(blank)[0];
        Assert.IsFalse(collection.TryUse(draft.Id)); var after = collection.Export(blank)[0];
        Assert.AreEqual(JsonUtility.ToJson(before), JsonUtility.ToJson(after)); Assert.AreEqual(draft.Id, collection.Find(draft.Id, out _).Id);
    }
    [TestCase("SaltItemData.asset")] [TestCase("CoinItemData.asset")] [TestCase("BellItemData.asset")]
    [TestCase("BeanMugwortItemData.asset")] [TestCase("GongItemData.asset")] [TestCase("MirrorItemData.asset")]
    [TestCase("YinIncenseItemData.asset")] [TestCase("LanternItemData.asset")]
    [TestCase("Talismans/RegistryTalisman.asset")] [TestCase("Talismans/BarrierTalisman.asset")]
    public void RealPrefabCanDropAndAnotherPlayerCanPickUp(string path)
    {
        var item = Load<ItemData>(path); Assert.IsNotNull(item, path); Assert.IsTrue(inventory.TryAddItem(item));
        Assert.IsTrue(inventory.Drops.TryDropFinal(1, item, out var world), inventory.Drops.LastMessage);
        Keep(world.gameObject); Assert.AreEqual(0, inventory.GetItemCount(item)); Assert.AreSame(item, world.ItemData);
        Assert.IsFalse(Enumerable.Range(0,6).Any(i => inventory.GetQuickSlotItem(i) == item));
        Assert.IsFalse(inventory.Drops.TryDropFinal(1, item, out _));
        Keep(new GameObject("Offline pickup judge")).AddComponent<LocalGameplayJudge>();
        var other = Keep(new GameObject("Other pickup player")).AddComponent<PlayerInventory>();
        world.Interact(other.gameObject); Assert.AreEqual(1, other.GetItemCount(item)); Assert.IsFalse(world.gameObject.activeSelf);
    }
    [Test] public void StackDropKeepsBindingUntilLastAndNotifiesCommittedState()
    {
        var item = Load<ItemData>("SaltItemData.asset"); Assert.IsTrue(inventory.TryAddItem(item, 2));
        Assert.IsTrue(inventory.AssignQuickSlot(1, item)); int events = 0;
        inventory.ItemUnitsChanged += (data, count) => { if (data == item) { Assert.AreEqual(-1, count); events++; } };
        Assert.IsTrue(inventory.Drops.TryDropFinal(1, item, out var first), inventory.Drops.LastMessage); Keep(first.gameObject);
        Assert.AreEqual(1, inventory.GetItemCount(item)); Assert.AreSame(item, inventory.GetQuickSlotItem(1));
        first.gameObject.SetActive(false);
        Assert.IsTrue(inventory.Drops.TryDropFinal(2, item, out var second), inventory.Drops.LastMessage); Keep(second.gameObject);
        Assert.AreEqual(0, inventory.GetItemCount(item)); Assert.IsNull(inventory.GetQuickSlotItem(1)); Assert.AreEqual(2, events);
    }
    [Test] public void UnsafeGroundWallAndPlayerOverlapPreserveInventory()
    {
        var item = Load<ItemData>("SaltItemData.asset"); inventory.TryAddItem(item);
        ground.SetActive(false); Physics.SyncTransforms(); Assert.IsFalse(inventory.Drops.RequestDrop(item)); Assert.AreEqual(1, inventory.GetItemCount(item));
        ground.SetActive(true);
        var wall = Keep(GameObject.CreatePrimitive(PrimitiveType.Cube)); wall.transform.position = inventory.transform.position + new Vector3(0, 1, .6f); wall.transform.localScale = new Vector3(4, 3, .05f);
        Physics.SyncTransforms(); Assert.IsFalse(inventory.Drops.RequestDrop(item)); Assert.AreEqual(1, inventory.GetItemCount(item));
        wall.SetActive(false); var collider = inventory.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(3, 3, 3);
        Physics.SyncTransforms(); Assert.IsFalse(inventory.Drops.RequestDrop(item)); Assert.AreEqual(1, inventory.GetItemCount(item));
    }
    [Test] public void ProtectedMissingPrefabAndSharedMaterialCannotDrop()
    {
        var material = Load<ItemData>("LampOilMaterial.asset"); var shared = Keep(new GameObject("Shared")).AddComponent<SharedMaterialInventory>(); inventory.BindSharedMaterials(shared); inventory.TryAddItem(material);
        Assert.IsFalse(inventory.Drops.CanDrop(material, out var reason)); StringAssert.Contains("공용", reason); Assert.AreEqual(1, shared.GetCount(material));
        var notebook = Load<ItemData>("NotebookItemData.asset"); inventory.TryAddItem(notebook); Assert.IsFalse(inventory.Drops.RequestDrop(notebook)); Assert.AreEqual(1, inventory.GetItemCount(notebook));
        var noWorld = Keep(Object.Instantiate(Load<ItemData>("SaltItemData.asset"))); Set(noWorld, "_worldPrefab", null); inventory.TryAddItem(noWorld);
        Assert.IsFalse(inventory.Drops.RequestDrop(noWorld)); Assert.AreEqual(1, inventory.GetItemCount(noWorld));
        Set(noWorld, "_preventDrop", true); Assert.IsFalse(inventory.Drops.CanDrop(noWorld, out reason)); StringAssert.Contains("기본", reason);
    }
    [Test] public void PendingDropAndFailedPublisherCannotLoseOrDuplicateItem()
    {
        var item = Load<ItemData>("SaltItemData.asset"); inventory.TryAddItem(item); long requested = 0; int requests = 0;
        inventory.Drops.SetAuthorityCheck(() => false); inventory.Drops.DropRequested += (id, data) => { requested = id; requests++; Assert.AreSame(item, data); };
        Assert.IsFalse(inventory.Drops.RequestDrop(item)); Assert.IsTrue(inventory.Drops.IsPending); Assert.AreEqual(1, inventory.GetItemCount(item));
        Assert.IsFalse(inventory.Drops.RequestDrop(item)); Assert.AreEqual(1, requests);
        inventory.Drops.ApplyDropResult(requested, false, "rejected"); Assert.IsFalse(inventory.Drops.IsPending);
        inventory.Drops.SetAuthorityCheck(() => true); WorldItem published = null; int rollbacks = 0;
        inventory.Drops.SetWorldPublisher(world => { published = world; Assert.AreEqual(0, inventory.GetItemCount(item)); return false; }, world => rollbacks++);
        Assert.IsFalse(inventory.Drops.TryDropFinal(requested + 1, item, out _)); Assert.AreEqual(1, inventory.GetItemCount(item)); Assert.AreEqual(1, rollbacks); Assert.IsFalse(published.gameObject.activeSelf);
        Keep(published.gameObject); Assert.IsFalse(inventory.Drops.TryDropFinal(requested + 1, item, out _)); Assert.AreEqual(1, rollbacks);
    }
    [Test] public void ExactInstanceUnassignKeepsOtherInstanceAndPattern()
    {
        var first = Save("move_speed"); var second = Save("move_speed");
        inventory.AssignTalismanQuickSlot(1, first.Id); inventory.AssignTalismanQuickSlot(2, second.Id);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/UI/Inventory/EffectTalismanInventory.prefab");
        var view = Keep(Object.Instantiate(prefab)).GetComponent<EffectTalismanInventoryView>(); view.Bind(collection, null); view.Select(first.Id); Call(view, "LateUpdate");
        Assert.IsTrue(Get<Button>(view, "_unassignButton").interactable);
        Assert.IsNotNull(Get<Button>(view, "_useButton").transform.parent.Find("Drop"));
        view.UnassignSelected();
        Assert.IsTrue(string.IsNullOrEmpty(inventory.GetQuickSlotInstanceId(1))); Assert.AreEqual(second.Id, inventory.GetQuickSlotInstanceId(2)); Assert.AreEqual(2, collection.DrawnEffectCount);
        Call(view, "LateUpdate"); Assert.IsFalse(Get<Button>(view, "_unassignButton").interactable);
    }
    [Test] public void DrawingTransferPreservesDataAndFailedDropKeepsOwnership()
    {
        var draft = Save("stress_gain");
        Assert.IsTrue(inventory.AssignTalismanQuickSlot(1, draft.Id));
        Assert.IsFalse(collection.TryDropDrawing(draft.Id, _ => false));
        Assert.IsNotNull(collection.Find(draft.Id, out _));
        Assert.AreEqual(draft.Id, inventory.GetQuickSlotInstanceId(1));
        TalismanSaveData payload = null;
        Assert.IsTrue(collection.TryDropDrawing(draft.Id, data => { payload = data; return true; }));
        Assert.IsNull(collection.Find(draft.Id, out _));
        Assert.IsTrue(string.IsNullOrEmpty(inventory.GetQuickSlotInstanceId(1)));
        var other = Keep(new GameObject("recipient"));
        other.AddComponent<PlayerInventory>();
        var recipient = other.AddComponent<PlayerTalismanCollection>();
        Assert.IsTrue(recipient.TryReceiveDrawing(blank, payload));
        var received = recipient.Find(draft.Id, out var item);
        Assert.AreSame(blank, item); Assert.AreEqual("검증 부적", received.CustomName);
        Assert.AreEqual("stress_gain", received.EffectId); Assert.IsTrue(received.IsValidDrawing());
        Assert.IsFalse(recipient.TryReceiveDrawing(blank, payload));
    }
    [Test] public void DrawnTalismanCanBeDroppedAndPickedUpOnce()
    {
        var draft = Save("stress_gain");
        Assert.IsTrue(inventory.Drops.TryDropDrawingFinal(1, draft.Id, out var world), inventory.Drops.LastMessage);
        Keep(world.gameObject);
        Assert.IsTrue(world.HasDrawing);
        Assert.IsNull(collection.Find(draft.Id, out _));
        Keep(new GameObject("Offline pickup judge")).AddComponent<LocalGameplayJudge>();
        var other = Keep(new GameObject("recipient"));
        var otherInventory = other.AddComponent<PlayerInventory>();
        var recipient = other.AddComponent<PlayerTalismanCollection>();
        world.Interact(other);
        Assert.IsFalse(world.gameObject.activeSelf);
        Assert.AreEqual(0, otherInventory.GetItemCount(blank));
        Assert.AreEqual("stress_gain", recipient.Find(draft.Id, out _).EffectId);
        Assert.IsFalse(world.TryCollectDrawing(other));
    }
    [Test] public void SaveDropdownAndSubInventoryUseSameLiveDescription()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/UI/TalismanDrawingPanel.prefab");
        var panel = Keep(Object.Instantiate(prefab)).GetComponent<TalismanDrawingPanel>();
        var choices = Get<List<TalismanEffectDefinition>>(panel, "_choices"); choices.AddRange(blank.Effects);
        var dropdown = Get<TMP_Dropdown>(panel, "_effectDropdown"); dropdown.ClearOptions(); dropdown.AddOptions(new List<string> { "효과 선택", "신속", "청심", "회생" });
        for (int i = 0; i < choices.Count; i++)
        {
            dropdown.value = i + 1;
            Assert.AreEqual(choices[i].DisplayName + "\n" + choices[i].Description, Get<TMP_Text>(panel, "_effectDescription").text);
        }
        dropdown.value = 0; StringAssert.Contains("선택", Get<TMP_Text>(panel, "_effectDescription").text);
    }
    [UnityTest] public IEnumerator WalkSprintCrouchUseTemporaryMultiplierWithoutChangingBaseSpeeds()
    {
        var player = inventory.gameObject;
        var crouch = player.AddComponent<PlayerCrouch>(); crouch.enabled = false;
        var input = player.GetComponent<PlayerInputReader>();
        var controller = (Behaviour)player.AddComponent(typeof(PlayerInventory).Assembly.GetType("PlayerController"));
        controller.enabled = false; Set(controller, "_gravity", 0f);
        var capsule = player.GetComponent<CharacterController>(); capsule.center = Vector3.up; capsule.height = 2;
        yield return null;
        Set(input, "<MoveInput>k__BackingField", Vector2.up);
        float walk = Get<float>(controller, "_walkSpeed"), sprint = Get<float>(controller, "_sprintSpeed");
        for (int mode = 0; mode < 3; mode++)
        {
            Set(input, "<SprintHeld>k__BackingField", mode == 1);
            Assert.IsTrue(crouch.SetCrouching(mode == 2));
            float start = player.transform.position.z; Call(controller, "HandleMovement"); float baseline = player.transform.position.z - start;
            Assert.Greater(baseline, 0);
            Assert.IsTrue(effects.TryApply(blank.FindEffect("move_speed")));
            start = player.transform.position.z; Call(controller, "HandleMovement"); float boosted = player.transform.position.z - start;
            Assert.AreEqual(baseline * 1.25f, boosted, .0002f);
            effects.ResetEffects(); start = player.transform.position.z; Call(controller, "HandleMovement");
            Assert.AreEqual(baseline, player.transform.position.z - start, .0002f);
        }
        Assert.AreEqual(walk, Get<float>(controller, "_walkSpeed")); Assert.AreEqual(sprint, Get<float>(controller, "_sprintSpeed"));
    }
    [Test] public void BarrierStillConsumesOneAndProtectsUntilDeath()
    {
        var data = Load<TalismanItemData>("Talismans/BarrierTalisman.asset"); inventory.TryAddItem(data);
        var items = inventory.gameObject.AddComponent<PlayerItemController>();
        inventory.AssignQuickSlot(0,data); Call(items,"SelectSlot",0);
        Assert.IsTrue(data.UseAction.Execute(new ItemUseContext(inventory.gameObject,data,inventory.transform.position,Vector3.forward,_=>{})));
        var ward = Object.FindObjectsByType<BarrierWard>(FindObjectsSortMode.None).Single(w=>w.Owner==inventory.gameObject); Keep(ward.gameObject);
        Assert.IsTrue(BarrierWard.IsProtecting(inventory.gameObject));Assert.AreEqual(0,inventory.GetItemCount(data));
        health.ReceiveDamage(100);Assert.IsFalse(BarrierWard.IsProtecting(inventory.gameObject));
    }
    [UnityTest] public IEnumerator DeathStillEntersSpectatorAndSelectsLivingTarget()
    {
        var camera = Keep(new GameObject("Player camera")).AddComponent<Camera>(); camera.transform.SetParent(inventory.transform,false);
        var spectator=inventory.gameObject.AddComponent<PlayerSpectatorController>();
        var survivor=Keep(new GameObject("Living player"));survivor.AddComponent<PlayerHealth>();
        health.ReceiveDamage(100);yield return null;
        Assert.IsTrue(spectator.IsSpectating);Assert.AreEqual(survivor,spectator.SpectateTarget);Assert.IsNotNull(spectator.SpectatorCamera);
        Assert.IsFalse(camera.enabled);spectator.ExitSpectatorMode();
    }
}
#endif
