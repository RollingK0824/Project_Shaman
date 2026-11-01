#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

public class InventoryLayoutTests
{
    readonly List<Object> cleanup = new();
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    const string UI = "Assets/Project/Prefabs/UI/";
    static readonly string Output = Path.GetFullPath("Temp/InventoryReworkQA");
    T Keep<T>(T value) where T : Object { cleanup.Add(value); return value; }
    T LoadUI<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(UI + path);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o,value);
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
    static void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o,args);
    EditorWindow gameView; PropertyInfo sizeProperty; int oldSize = -1;
    [TearDown] public void Clean()
    {
        if (oldSize >= 0 && gameView != null) sizeProperty.SetValue(gameView, oldSize);
        foreach (var value in cleanup) if (value is GameObject go && go != null && go.TryGetComponent<PlayerItemController>(out var items)) items.UnequipCurrentItem();
        for (int i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i]) Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }
    void Size(int w, int h)
    {
        var asm = typeof(Editor).Assembly; var type = asm.GetType("UnityEditor.GameView"); gameView = EditorWindow.GetWindow(type);
        sizeProperty = type.GetProperty("selectedSizeIndex", BindingFlags.Public | Private);
        if (oldSize < 0) oldSize = (int)sizeProperty.GetValue(gameView);
        var sizesType = asm.GetType("UnityEditor.GameViewSizes"); var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes,new[] { Enum.Parse(asm.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone") });
        int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null), index = -1;
        for (int i=0;i<count;i++)
        {
            var value=group.GetType().GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
            if ((int)value.GetType().GetProperty("width").GetValue(value)==w && (int)value.GetType().GetProperty("height").GetValue(value)==h) {index=i;break;}
        }
        if(index<0)
        {
            var value=Activator.CreateInstance(asm.GetType("UnityEditor.GameViewSize"),new[]{Enum.Parse(asm.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),(object)w,h,"Inventory QA "+w+"x"+h});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{value});index=count;
        }
        sizeProperty.SetValue(gameView,index);gameView.Focus();gameView.Repaint();
    }
    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners); var canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
        return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
    }
    IEnumerator Shot(string name)
    {
        Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        Directory.CreateDirectory(Output); File.WriteAllBytes(Path.Combine(Output,name+".png"),texture.EncodeToPNG()); Object.Destroy(texture);
    }

    [UnityTest] public IEnumerator HudReadabilitySelectionAndNotebookBindingAtThreeResolutions()
    {
        var player = Keep(new GameObject("HUD presentation QA"));
        var health = player.AddComponent<PlayerHealth>();
        var stress = player.AddComponent<PlayerStressController>(); stress.enabled = false;
        var inventory = player.AddComponent<PlayerInventory>();
        var input = player.AddComponent<PlayerInputReader>();
        var owner = player.AddComponent<PlayerLocalUIBootstrap>(); owner.enabled = false;
        Set(owner, "_inventoryPanelPrefab", LoadUI<InventoryPanelUI>("Inventory/InventoryPanelUI.prefab"));
        Set(owner, "_notebookHUDIcon", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Visuals/Sprites/Items/NoteBook/NoteBookUI.png"));
        var notebook = Keep(Object.Instantiate(LoadUI<GameObject>("PlayerNotebookUI.prefab"))).GetComponent<PlayerNotebookUI>(); notebook.Bind(owner);
        var quick = Keep(Object.Instantiate(Resources.Load<GameObject>("PlayerUI/QuickSlotHUD"))).GetComponentInChildren<QuickSlotUI>(); quick.Bind(inventory, input);
        var hud = Keep(Object.Instantiate(LoadUI<GameObject>("StressHUD.prefab"))).GetComponent<PlayerStressHUD>(); hud.Bind(stress, health);
        var camera = Keep(new GameObject("HUD QA camera")).AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.23f, .30f, .37f);
        var salt = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/Items/SaltItemData.asset");
        var lamp = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/Items/LanternItemData.asset");
        inventory.TryAddItem(salt, 12); inventory.AssignQuickSlot(0, salt);
        inventory.TryAddItem(lamp); inventory.AssignQuickSlot(1, lamp);
        var shortcut = Get<Image>(notebook, "_shortcut");
        var binding = InputManager.Instance.Actions.UI.Notebook;
        var previousOverride = binding.bindings[0];
        try
        {
            binding.ApplyBindingOverride(0, "<Keyboard>/n"); Call(notebook, "RefreshShortcutKey");
            StringAssert.AreEqualIgnoringCase("N", Get<TMP_Text>(notebook, "_shortcutKey").text);
        }
        finally
        {
            binding.RemoveBindingOverride(0);
            binding.ApplyBindingOverride(0, previousOverride);
            Call(notebook, "RefreshShortcutKey");
        }
        int selected = 0;
        foreach (int width in new[] { 1280, 1600, 1920 })
        {
            Size(width, width * 9 / 16);
            stress.SetStress(selected == 0 ? 0 : selected == 1 ? 50 : 85);
            Call(quick, "OnSlotSelected", selected++);
            yield return null; yield return null; yield return new WaitForEndOfFrame();
            Assert.AreEqual(width, Screen.width);
            Canvas.ForceUpdateCanvases();
            var stressRect = ScreenRect((RectTransform)Get<TMP_Text>(hud, "_valueText").transform.parent);
            var quickRect = ScreenRect((RectTransform)quick.transform);
            var bookRect = ScreenRect(shortcut.rectTransform);
            Assert.Less(stressRect.xMax + 10, quickRect.xMin);
            Assert.Greater(bookRect.xMin, quickRect.xMax + 5);
            foreach (var r in new[] { stressRect, quickRect, bookRect })
            {
                Assert.GreaterOrEqual(r.xMin, 0); Assert.GreaterOrEqual(r.yMin, 0);
                Assert.LessOrEqual(r.xMax, Screen.width); Assert.LessOrEqual(r.yMax, Screen.height);
            }
            var frames = quick.GetComponentsInChildren<PlayerHUDFrame>();
            Assert.AreEqual(6, frames.Length);
            Assert.AreEqual(1, frames.Count(f => Get<bool>(f, "_selected")));
            var keys = quick.GetComponentsInChildren<TMP_Text>().Where(t => t.name.StartsWith("KeyText")).ToArray();
            CollectionAssert.AreEquivalent(new[] { "1", "2", "3", "4", "5", "6" }, keys.Select(t => t.text));
            foreach (var t in hud.GetComponentsInChildren<TMP_Text>().Concat(quick.GetComponentsInChildren<TMP_Text>()).Concat(shortcut.GetComponentsInChildren<TMP_Text>()))
            { t.ForceMeshUpdate(); Assert.IsFalse(t.isTextOverflowing, width + ": " + t.name); }
            var notebookKey = Get<TMP_Text>(notebook, "_shortcutKey");
            Assert.AreEqual(ScreenRect(keys[0].rectTransform).center.y, ScreenRect(notebookKey.rectTransform).center.y, .5f,
                width + ": Notebook key must align with the quickslot number row");
            Assert.AreEqual(keys[0].fontSize, notebookKey.fontSize, .1f,
                width + ": Default notebook key and quickslot numbers must have the same size");
            foreach (var icon in Get<Image[]>(quick, "_slotIcons").Where(i => i.enabled && i.gameObject.activeInHierarchy))
            {
                var r = ScreenRect(icon.rectTransform); var slot = ScreenRect((RectTransform)icon.transform.parent);
                Assert.IsTrue(slot.Contains(r.min) && slot.Contains(r.max), "Item icon outside its slot");
            }
            yield return Shot("ShamanHUD-" + width);
            Call(notebook, "Open"); yield return new WaitForEndOfFrame();
            Assert.IsTrue(notebook.IsOpen); Assert.IsFalse(shortcut.gameObject.activeSelf);
            notebook.Close(); yield return new WaitForEndOfFrame();
            Assert.IsFalse(notebook.IsOpen); Assert.IsTrue(shortcut.gameObject.activeSelf);
            Assert.AreEqual(12, inventory.GetItemCount(salt));
        }
    }
    [UnityTest] public IEnumerator ThreeResolutions_HudNotebookAndSaveDescription()
    {
        var fixture=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Scripts/PlayerGameplay/Tests/InventoryRework/QuickSlotFixture.prefab");
        Assert.IsNotNull(fixture, "Read-only copy of the existing scene QuickSlot is required for layout validation.");
        var player=Keep(new GameObject("UI validation player")); var health=player.AddComponent<PlayerHealth>(); var stress=player.AddComponent<PlayerStressController>();stress.enabled=false;
        var inv=player.AddComponent<PlayerInventory>(); var input=player.AddComponent<PlayerInputReader>(); var items=player.AddComponent<PlayerItemController>();
        var hold=Keep(new GameObject("Hold anchor"));hold.transform.SetParent(player.transform);Set(items,"_itemHoldPoint",hold.transform);
        var collection=player.AddComponent<PlayerTalismanCollection>();
        var owner=player.AddComponent<PlayerLocalUIBootstrap>(); owner.enabled=false;
        Set(owner,"_inventoryPanelPrefab",LoadUI<InventoryPanelUI>("Inventory/InventoryPanelUI.prefab"));
        Set(owner,"_notebookHUDIcon",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Visuals/Sprites/Items/NoteBook/NoteBookUI.png"));
        var notebook=Keep(Object.Instantiate(LoadUI<GameObject>("PlayerNotebookUI.prefab"))).GetComponent<PlayerNotebookUI>();notebook.Bind(owner);
        var quick=Keep(Object.Instantiate(fixture)).GetComponentInChildren<QuickSlotUI>();quick.Bind(inv,input);
        var hud=Keep(Object.Instantiate(LoadUI<GameObject>("StressHUD.prefab"))).GetComponent<PlayerStressHUD>();hud.Bind(stress,health);stress.SetStress(50);
        var drawing=Keep(Object.Instantiate(LoadUI<GameObject>("TalismanDrawingPanel.prefab"))).GetComponent<TalismanDrawingPanel>();drawing.gameObject.SetActive(false);
        var salt=AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/Items/SaltItemData.asset");inv.TryAddItem(salt,2);
        var lamp=AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/Items/LanternItemData.asset");inv.TryAddItem(lamp);
        var blank=AssetDatabase.LoadAssetAtPath<TalismanItemData>("Assets/Project/Data/Items/Talismans/RegistryTalisman.asset");inv.TryAddItem(blank,2);
        var camera=Keep(new GameObject("UI QA camera")).AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.23f,.3f,.37f);
        foreach(int w in new[]{1280,1600,1920})
        {
            int h=w*9/16;Size(w,h);yield return null;yield return null;yield return new WaitForEndOfFrame();
            Assert.AreEqual(w,Screen.width);Assert.AreEqual(h,Screen.height);
            Call(hud,"LateUpdate"); Canvas.ForceUpdateCanvases();
            var hr=ScreenRect((RectTransform)Get<TMP_Text>(hud,"_valueText").transform.parent);var qr=ScreenRect((RectTransform)quick.transform);
            yield return Shot("HUD-"+w);
            Assert.GreaterOrEqual(hr.xMin,0,$"{w}: HUD={hr}, QuickSlot={qr}");Assert.GreaterOrEqual(hr.yMin,0);Assert.LessOrEqual(hr.xMax,qr.xMin-10);Assert.LessOrEqual(hr.yMax,h);
            foreach(var text in hud.GetComponentsInChildren<TMP_Text>()) {text.ForceMeshUpdate(); Assert.IsFalse(text.isTextOverflowing,text.name);}
            var icon=Get<Image>(notebook,"_shortcut");Assert.IsNotNull(icon);Assert.Greater(ScreenRect(icon.rectTransform).xMin,qr.xMax);
            Call(notebook,"Open");yield return null;Call(notebook,"SelectItem",salt);yield return null;
            var buttons=Get<List<Button>>(notebook,"_slotButtons");Assert.IsTrue(buttons[6].interactable);Assert.IsTrue(buttons[7].interactable);
            Assert.Less(ScreenRect((RectTransform)buttons[6].transform).xMax,ScreenRect((RectTransform)buttons[7].transform).xMin);
            yield return Shot("Inventory-"+w);buttons[6].onClick.Invoke();yield return new WaitForEndOfFrame();Assert.IsFalse(buttons[6].interactable);Assert.AreEqual(2,inv.GetItemCount(salt));
            inv.AssignQuickSlot(0,salt);Call(notebook,"SelectItem",blank);yield return new WaitForEndOfFrame();Assert.IsTrue(Get<Button>(notebook,"_drawButton").isActiveAndEnabled);Assert.IsTrue(buttons[7].interactable);
            notebook.Close();
            Assert.IsTrue(drawing.Open(player,collection,blank));drawing.CurrentDrawing.BeginStroke(new Vector2(.2f,.2f));drawing.CurrentDrawing.AppendPoint(new Vector2(.8f,.8f));drawing.CurrentDrawing.EndStroke();drawing.RequestSave();
            var dropdown=Get<TMP_Dropdown>(drawing,"_effectDropdown");dropdown.value=3;yield return null;
            var description=Get<TMP_Text>(drawing,"_effectDescription");description.ForceMeshUpdate();Assert.IsFalse(description.isTextOverflowing);
            var dr=ScreenRect(description.rectTransform);var br=ScreenRect((RectTransform)Get<Button>(drawing,"_saveButton").transform);
            Assert.Greater(dr.yMin,br.yMax);Assert.GreaterOrEqual(dr.xMin,0);Assert.LessOrEqual(dr.xMax,w);
            yield return Shot("PatternSave-"+w);drawing.Close();
        }
        Size(1280,720);yield return null;yield return new WaitForEndOfFrame();
        var draft=collection.CreateDraft(blank);draft.BeginStroke(new Vector2(.2f,.2f));draft.AppendPoint(new Vector2(.8f,.8f));draft.EndStroke();
        Assert.IsTrue(collection.Save(blank,draft,"청심 문양","stress_gain"));Assert.IsTrue(inv.AssignTalismanQuickSlot(3,draft.Id));
        Call(notebook,"Open");yield return null;
        var panel=Get<InventoryPanelUI>(notebook,"_inventory");panel.OpenEffectGroup();yield return null;
        var effectView=Get<EffectTalismanInventoryView>(panel,"_effectView");effectView.Select(draft.Id);yield return new WaitForEndOfFrame();
        var unassign=Get<Button>(effectView,"_unassignButton");var drop=Get<Button>(effectView,"_dropButton");
        Assert.IsTrue(unassign.interactable);Assert.IsFalse(drop.interactable);
        Assert.Less(ScreenRect((RectTransform)unassign.transform).xMax,ScreenRect((RectTransform)drop.transform).xMin);
        yield return Shot("PatternInventory-1280");
        notebook.Close();Assert.IsTrue(collection.TryUse(draft.Id));Assert.AreEqual(35,stress.CurrentStress);
        StringAssert.Contains("35",Get<TMP_Text>(hud,"_valueText").text);Assert.IsTrue(string.IsNullOrEmpty(inv.GetQuickSlotInstanceId(3)));
        Assert.AreEqual(0,Get<List<EffectTalismanInventoryRow>>(effectView,"_visibleRows").Count);
    }
    [UnityTest] public IEnumerator NumberAndWheelSelectSameSlotsAndEmptyUnequips()
    {
        var player=Keep(new GameObject("Slot input QA"));player.SetActive(false);var inv=player.AddComponent<PlayerInventory>();var items=player.AddComponent<PlayerItemController>();var input=player.GetComponent<PlayerInputReader>();
        var anchor=Keep(new GameObject("Hold"));anchor.transform.SetParent(player.transform,false);Set(items,"_itemHoldPoint",anchor.transform);
        player.SetActive(true);
        var salt=AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/Items/SaltItemData.asset");inv.TryAddItem(salt);inv.AssignQuickSlot(1,salt);
        var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        try
        {
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Digit2));
            UnityEngine.InputSystem.InputSystem.Update();yield return null;
            Assert.AreEqual(1,items.SelectedSlotIndex);Assert.AreSame(salt,items.EquippedItemData);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());UnityEngine.InputSystem.InputSystem.Update();
            input.CycleQuickSlot(-1);Assert.AreNotEqual(1,items.SelectedSlotIndex);Assert.IsFalse(items.HasEquippedItem);
            input.SetGameplayInputBlocked(true);int before=items.SelectedSlotIndex;yield return new WaitForSeconds(.15f);input.CycleQuickSlot(1);Assert.AreEqual(before,items.SelectedSlotIndex);
        }
        finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard); }
    }
    [UnityTest] public IEnumerator EquippedLanternDropClearsVisualAndLight()
    {
        var player=Keep(new GameObject("Equipped drop player"));player.transform.position=new Vector3(700,0,700);
        var inv=player.AddComponent<PlayerInventory>();var items=player.AddComponent<PlayerItemController>();
        var anchor=Keep(new GameObject("Hold"));anchor.transform.SetParent(player.transform,false);Set(items,"_itemHoldPoint",anchor.transform);
        var floor=Keep(GameObject.CreatePrimitive(PrimitiveType.Cube));floor.transform.position=player.transform.position-Vector3.up*.5f;floor.transform.localScale=new Vector3(8,1,8);
        var lamp=AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/Items/LanternItemData.asset");inv.TryAddItem(lamp);inv.AssignQuickSlot(0,lamp);Call(items,"SelectSlot",0);yield return null;
        var visual=Get<GameObject>(items,"_equippedObject");Assert.IsNotNull(visual);Assert.IsTrue(visual.GetComponentInChildren<Light>().enabled);
        Physics.SyncTransforms();Assert.IsTrue(inv.Drops.RequestDrop(lamp),inv.Drops.LastMessage);
        foreach(var world in Object.FindObjectsByType<WorldItem>(FindObjectsSortMode.None))if(Vector3.Distance(world.transform.position,player.transform.position)<5)Keep(world.gameObject);
        Assert.IsFalse(items.HasEquippedItem);Assert.IsNull(inv.GetQuickSlotItem(0));Assert.IsFalse(visual.GetComponentInChildren<Light>().enabled);
        yield return null;Assert.IsTrue(visual==null);Assert.AreEqual(0,anchor.transform.childCount);
    }
}
#endif
