#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class QuickSlotBootstrapTests
{
    private readonly List<GameObject> _hidden = new();
    private readonly List<GameObject> _owned = new();
    private PlayerLocalUIBootstrap _bootstrap;
    private PlayerInventory _inventory;
    private PlayerInputReader _input;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private void Update(bool local) => typeof(PlayerLocalUIBootstrap).GetMethod("UpdateQuickSlots", Private).Invoke(_bootstrap, new object[] { local });
    private QuickSlotUI Current => (QuickSlotUI)typeof(PlayerLocalUIBootstrap).GetField("_quickSlots", Private).GetValue(_bootstrap);
    private GameObject Keep(GameObject go) { _owned.Add(go); return go; }

    [SetUp] public void SetUp()
    {
        foreach (var quick in Object.FindObjectsByType<QuickSlotUI>(FindObjectsSortMode.None))
        {
            var root = quick.transform.root.gameObject;
            if (!_hidden.Contains(root)) { _hidden.Add(root); root.SetActive(false); }
        }
        var player = Keep(new GameObject("Quick slot bootstrap QA"));
        _inventory = player.AddComponent<PlayerInventory>();
        _input = player.AddComponent<PlayerInputReader>();
        _bootstrap = player.AddComponent<PlayerLocalUIBootstrap>();
        _bootstrap.enabled = false;
    }

    [TearDown] public void TearDown()
    {
        if (_bootstrap != null) Update(false);
        foreach (var go in _owned) if (go != null) Object.DestroyImmediate(go);
        foreach (var go in _hidden) if (go != null) go.SetActive(true);
        _owned.Clear(); _hidden.Clear();
    }

    [Test] public void MissingSceneHudIsCreatedOnceAndShowsInventoryChanges()
    {
        Assert.IsNotNull(Resources.Load<GameObject>("PlayerUI/QuickSlotHUD"));
        Update(false); Assert.IsNull(Current, "Remote players must not create a HUD");
        Update(true);
        var quick = Current; Assert.IsNotNull(quick);
        Keep(quick.transform.root.gameObject);
        Assert.AreSame(_inventory, quick.BoundInventory);
        Assert.AreEqual(RenderMode.ScreenSpaceOverlay, quick.GetComponentInParent<Canvas>().renderMode);
        Canvas.ForceUpdateCanvases();
        typeof(QuickSlotUI).GetMethod("LateUpdate", Private).Invoke(quick, null);
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4]; ((RectTransform)quick.transform).GetWorldCorners(corners);
        Assert.Greater(corners[2].x - corners[0].x, 100f, "HUD must have a visible on-screen size");
        Assert.GreaterOrEqual(corners[0].x, 0f); Assert.GreaterOrEqual(corners[0].y, 0f);
        Assert.LessOrEqual(corners[2].x, Screen.width); Assert.LessOrEqual(corners[2].y, Screen.height);
        Update(true); Assert.AreSame(quick, Current);
        var salt = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Project/Data/Items/SaltItemData.asset");
        Assert.IsTrue(_inventory.TryAddItem(salt, 2));
        Assert.IsTrue(_inventory.AssignQuickSlot(0, salt));
        var icons = (Image[])typeof(QuickSlotUI).GetField("_slotIcons", Private).GetValue(quick);
        Assert.IsTrue(icons[0].enabled); Assert.IsNotNull(icons[0].sprite);
        Update(false); Assert.IsNull(Current); Assert.IsFalse(quick.transform.root.gameObject.activeSelf);
        Update(true); Assert.IsNotNull(Current); Assert.AreNotSame(quick, Current);
        Keep(Current.transform.root.gameObject);
    }

    [Test] public void ExistingSceneHudIsReusedAndNotDestroyedOnUnbind()
    {
        var root = Keep(Object.Instantiate(Resources.Load<GameObject>("PlayerUI/QuickSlotHUD")));
        var quick = root.GetComponentInChildren<QuickSlotUI>();
        Update(true); Assert.AreSame(quick, Current);
        Update(false); Assert.IsNull(quick.BoundInventory);
        Assert.IsTrue(root.activeSelf, "Scene-owned UI must survive player cleanup");
    }

    [Test] public void HudBoundToAnotherPlayerIsNotTaken()
    {
        var other = Keep(new GameObject("Other owner"));
        var inventory = other.AddComponent<PlayerInventory>();
        var input = other.AddComponent<PlayerInputReader>();
        var root = Keep(Object.Instantiate(Resources.Load<GameObject>("PlayerUI/QuickSlotHUD")));
        var quick = root.GetComponentInChildren<QuickSlotUI>(); quick.Bind(inventory, input);
        Update(true); Assert.AreNotSame(quick, Current); Assert.AreSame(inventory, quick.BoundInventory);
        Keep(Current.transform.root.gameObject);
    }
}
#endif
