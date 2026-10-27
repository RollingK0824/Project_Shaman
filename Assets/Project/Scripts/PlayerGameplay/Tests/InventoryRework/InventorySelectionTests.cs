#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class InventorySelectionTests
{
    GameObject _player, _canvas;
    InventoryPanelUI _panel;
    PlayerInventory _inventory;
    ItemData _salt, _beans;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    Dictionary<ItemData, InventoryItemSlotUI> Slots =>
        (Dictionary<ItemData, InventoryItemSlotUI>)typeof(InventoryPanelUI).GetField("_slots", Private).GetValue(_panel);

    [UnityTest] public IEnumerator ClickingChangesOneSelectionAndConsumptionClearsIt()
    {
        _player = new GameObject("Selection test inventory"); _inventory = _player.AddComponent<PlayerInventory>();
        _salt = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/SaltItemData.asset");
        _beans = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/BeanMugwortItemData.asset");
        _inventory.TryAddItem(_salt, 2); _inventory.TryAddItem(_beans);
        _canvas = new GameObject("Selection test canvas", typeof(RectTransform), typeof(Canvas));
        _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        _panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InventoryPanelUI>("Assets/Project/Prefabs/UI/Inventory/InventoryPanelUI.prefab"), _canvas.transform);
        _panel.Bind(_inventory); _panel.gameObject.SetActive(true);
        ItemData callback = null;
        _panel.ConfigureNotebook(null, item => callback = item);
        yield return null;
        var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
        Slots[_salt].OnPointerClick(pointer);
        Assert.AreSame(_salt, callback); Assert.AreSame(_salt, _panel.SelectedItem);
        Assert.IsTrue(Slots[_salt].IsSelected); Assert.IsFalse(Slots[_beans].IsSelected);
        Assert.IsTrue(Slots[_salt].transform.Find("SelectionBorder").gameObject.activeSelf);
        foreach (var image in Slots[_salt].transform.Find("SelectionBorder").GetComponentsInChildren<Image>())
            Assert.IsFalse(image.raycastTarget, "Highlight must not block item clicks");
        Slots[_beans].OnPointerClick(pointer);
        Assert.IsFalse(Slots[_salt].IsSelected); Assert.IsTrue(Slots[_beans].IsSelected);
        Slots[_salt].OnPointerClick(pointer);
        _panel.Refresh(); Assert.IsTrue(Slots[_salt].IsSelected);
        _panel.gameObject.SetActive(false); _panel.gameObject.SetActive(true);
        Assert.IsTrue(Slots[_salt].IsSelected, "Closing/reopening should retain an owned item selection");
        Assert.IsTrue(_inventory.ConsumeItem(_salt)); Assert.IsTrue(Slots[_salt].IsSelected);
        Assert.IsTrue(_inventory.ConsumeItem(_salt)); Assert.IsNull(_panel.SelectedItem); Assert.IsNull(callback);
        Assert.IsFalse(Slots[_beans].IsSelected);
        yield return null;
    }
    [TearDown] public void Cleanup()
    {
        if (_canvas) Object.DestroyImmediate(_canvas);
        if (_player) Object.DestroyImmediate(_player);
    }
}
#endif
