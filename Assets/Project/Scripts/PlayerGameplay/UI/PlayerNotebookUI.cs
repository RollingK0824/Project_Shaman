using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Connects the existing notebook tabs to the existing inventory and crafting views.
// Data, quickslots, crafting transactions and authority remain with their original owners.
[DefaultExecutionOrder(100)]
public class PlayerNotebookUI : MonoBehaviour
{
    [SerializeField] private NotebookPanel _panel;
    private Image _shortcut;
    private QuickSlotUI _quickSlots;
    private readonly Vector3[] _quickCorners = new Vector3[4];
    private PlayerLocalUIBootstrap _owner;
    private PlayerInputReader _input;
    private PlayerNotebook _notebook;
    private PlayerHealth _health;
    private InventoryPanelUI _inventory;
    private CraftingPanelUI _crafting;
    private SharedQuestRewardView _rewardView;
    private GameObject _rewardButton;
    private GameObject _slotToolbar;
    private TMP_Text _selectionText;
    private Button _drawButton;
    private ItemData _selectedItem;
    private readonly List<Button> _slotButtons = new();
    private readonly List<GameObject> _pageRoots = new();
    private readonly List<GameObject> _hiddenPlaceholders = new();
    private bool _claimedInput;
    private bool _listensForCancel;
    private bool _previousCursorVisible;
    private CursorLockMode _previousCursorLock;
    public bool IsBound => _owner != null;
    public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

    public void SetPanel(NotebookPanel panel) => _panel = panel;

    public void Bind(PlayerLocalUIBootstrap owner)
    {
        if (_owner == owner) return;
        Unbind();
        if (owner == null || _panel == null) return;
        _owner = owner;
        if (owner.NotebookHUDIcon != null)
        {
            _shortcut = new GameObject("NotebookShortcut", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _shortcut.transform.SetParent(transform, false);
            _shortcut.sprite = owner.NotebookHUDIcon;
            _shortcut.preserveAspect = true;
            _shortcut.raycastTarget = false;
        }
        _input = owner.GetComponent<PlayerInputReader>();
        _notebook = owner.GetComponent<PlayerNotebook>();
        _health = owner.GetComponent<PlayerHealth>();
        _panel.gameObject.SetActive(false);
        if (_input != null) _input.NotebookPressed += Toggle;
        RefreshCancelSubscription();
        if (_notebook != null) _notebook.NotebookOpenRequested += Open;
        if (_health != null) _health.Died += HandleDeath;
    }

    private void RefreshCancelSubscription()
    {
        var manager = UIManager.Existing;
        bool listen = _input != null && (manager == null || !manager.isActiveAndEnabled);
        if (_listensForCancel == listen) return;
        if (_input != null)
        {
            if (listen) _input.CancelPressed += HandleFallbackCancel;
            else _input.CancelPressed -= HandleFallbackCancel;
        }
        _listensForCancel = listen;
    }

    private void HandleFallbackCancel()
    {
        // Also guard a manager created earlier in this input frame.
        var manager = UIManager.Existing;
        if (!IsOpen || (manager != null && manager.isActiveAndEnabled)) return;
        _input?.MarkCancelHandled();
        Close();
    }

    private void EnsurePages()
    {
        if (_inventory != null) return;
        var owner = _owner;
        var inventoryRoot = CreatePage(0, "PlayerInventoryPage");
        var craftingRoot = CreatePage(2, "PlayerCraftingPage");
        var scheduleRoot = CreatePage(4, "VillagerSchedulePage");
        if (inventoryRoot != null && owner.InventoryPanelPrefab != null)
        {
            var spread = new GameObject("PlayerInformationSpread", typeof(RectTransform), typeof(NotebookPlayerInfoView));
            spread.transform.SetParent(inventoryRoot, false);
            PlayerItemUIStyle.FitNotebookSpread((RectTransform)spread.transform);
            spread.GetComponent<NotebookPlayerInfoView>().Bind(owner.gameObject, owner.InventoryPanelPrefab.GetComponentInChildren<TMP_Text>(true)?.font);
            _inventory = Instantiate(owner.InventoryPanelPrefab, spread.transform);
            _inventory.Bind(owner.GetComponent<PlayerInventory>());
            // Initialize the existing presentation before applying its notebook styling.
            _inventory.gameObject.SetActive(true);
            CreateSlotToolbar();
            var outcome = PlayerQuestOutcomeReceiver.Resolve(owner.gameObject, owner.QuestItemCatalog);
            var rewards = new GameObject("SharedQuestRewards", typeof(RectTransform), typeof(SharedQuestRewardView));
            rewards.transform.SetParent(inventoryRoot, false); _rewardView = rewards.GetComponent<SharedQuestRewardView>();
            _rewardView.Bind(outcome != null ? outcome.Rewards : null, owner.GetComponent<PlayerInventory>(), owner.InventoryPanelPrefab.ItemSlotPrefab, ShowInventory);
            rewards.SetActive(false);
            CreateRewardButton();
        }
        if (craftingRoot != null && owner.CraftingPanelPrefab != null)
        {
            _crafting = Instantiate(owner.CraftingPanelPrefab, craftingRoot);
            _crafting.Bind(owner.GetComponent<PlayerCraftingController>());
            _crafting.SetRecipes(owner.CraftingRecipes);
            _crafting.ConfigureNotebook();
            _crafting.gameObject.SetActive(true);
        }
        if (scheduleRoot != null && owner.VillagerSchedulePrefab != null)
        {
            var spread = Instantiate(owner.VillagerSchedulePrefab, scheduleRoot);
            PlayerItemUIStyle.FitNotebookSpread((RectTransform)spread.transform);
        }
    }

    private Transform CreatePage(int index, string name)
    {
        var tab = _panel.GetTabRoot(index);
        if (tab == null) return null;
        foreach (Transform child in tab)
            if (child.gameObject.activeSelf) { _hiddenPlaceholders.Add(child.gameObject); child.gameObject.SetActive(false); }
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(tab, false);
        PlayerItemUIStyle.Place((RectTransform)root.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1160f, 740f));
        _pageRoots.Add(root);
        return root.transform;
    }

    private void Toggle()
    {
        if (IsOpen) Close();
        else if (_notebook != null) _notebook.RequestOpen();
    }

    private void Open()
    {
        if (_owner == null || _input == null || !_input.isActiveAndEnabled ||
            _input.GameplayInputBlocked || !PlayerActionGuard.CanAct(_owner.gameObject) ||
            !PlayerLocalPresentation.IsLocal(_owner.gameObject)) return;
        var view = _owner.GetComponent<PlayerViewModeController>();
        if ((view != null && view.IsBusy) || GameMenuUI.IsOpen) return;
        var manager = UIManager.Existing;
        if (manager != null && manager.IsMenuOpen) return;
        _previousCursorVisible = Cursor.visible; _previousCursorLock = Cursor.lockState;
        EnsurePages();
        _claimedInput = true;
        if (manager != null) manager.Open(_panel); else _panel.gameObject.SetActive(true);
        _panel.SelectTab(0);
        ShowInventory();
        _inventory?.ConfigureNotebook(OpenDrawing, SelectItem);
        _input.SetGameplayInputBlocked(true);
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
    }

    public void Close()
    {
        if (_panel != null && _panel.gameObject.activeSelf)
        {
            var manager = UIManager.Existing;
            if (manager != null && manager.IsOpen(_panel)) manager.Close(_panel);
            else _panel.gameObject.SetActive(false);
        }
        ReleaseInput();
    }

    private void ReleaseInput()
    {
        if (!_claimedInput) return;
        _claimedInput = false;
        bool otherMenu = UIManager.Existing != null && UIManager.Existing.IsMenuOpen;
        if (_input != null) _input.SetGameplayInputBlocked(otherMenu);
        if (!otherMenu) { Cursor.visible = _previousCursorVisible; Cursor.lockState = _previousCursorLock; }
    }

    private void LateUpdate()
    {
        if (!IsBound) return;
        RefreshCancelSubscription();
        UpdateShortcut();
        if (IsOpen && (_input == null || !_input.isActiveAndEnabled ||
            !PlayerActionGuard.CanAct(_owner.gameObject) || !PlayerLocalPresentation.IsLocal(_owner.gameObject))) Close();
        if (!IsOpen) ReleaseInput(); // Also handles UIManager's Escape/Close path.
        if (_inventory != null && _inventory.isActiveAndEnabled)
        {
            _inventory.ConfigureNotebook(OpenDrawing, SelectItem);
            _slotToolbar.SetActive(!_inventory.IsEffectViewOpen);
            _rewardButton.SetActive(!_inventory.IsEffectViewOpen);
            bool canAssign = _selectedItem != null && _selectedItem.CanUseQuickSlot && _owner.GetComponent<PlayerInventory>().Contains(_selectedItem) &&
                PlayerActionGuard.CanAct(_owner.gameObject);
            var inventory = _owner.GetComponent<PlayerInventory>();
            for (int i = 0; i < _slotButtons.Count; i++)
                _slotButtons[i].interactable = i < 6 ? canAssign : i == 6 ? canAssign && IsSelectedAssigned(inventory) :
                    inventory.Drops.CanDrop(_selectedItem, out _);
            RefreshSlotSelection(inventory);
            bool draw = _selectedItem is TalismanItemData talisman && talisman.AllowDrawing;
            _drawButton.gameObject.SetActive(draw);
            _drawButton.interactable = draw && inventory.Contains(_selectedItem) && PlayerActionGuard.CanAct(_owner.gameObject);
            _selectionText.rectTransform.sizeDelta = new Vector2(draw ? 280 : 440, 40);
            _selectionText.rectTransform.anchoredPosition = new Vector2(draw ? -80 : 0, 0);
        }
    }

    private void UpdateShortcut()
    {
        if (_shortcut == null) return;
        var inventory = _owner.GetComponent<PlayerInventory>();
        if (_quickSlots == null || _quickSlots.BoundInventory != inventory)
            foreach (var quick in FindObjectsByType<QuickSlotUI>(FindObjectsSortMode.None))
                if (quick.BoundInventory == inventory) { _quickSlots = quick; break; }
        bool show = !IsOpen && _input != null && _input.isActiveAndEnabled && !_input.GameplayInputBlocked &&
            PlayerActionGuard.CanAct(_owner.gameObject) && _quickSlots != null && _quickSlots.isActiveAndEnabled;
        _shortcut.gameObject.SetActive(show);
        if (!show) return;
        var quickRect = (RectTransform)_quickSlots.transform;
        quickRect.GetWorldCorners(_quickCorners);
        var quickCanvas = quickRect.GetComponentInParent<Canvas>();
        var camera = quickCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : quickCanvas.worldCamera;
        Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(camera, _quickCorners[3]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, _quickCorners[2]);
        float unit = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        float size = 99f * unit;
        var screen = new Vector2(Mathf.Min(Screen.width - size * .5f - 12f * unit, bottomRight.x + 12f * unit + size * .5f),
            Mathf.Max(size * .5f + 12f * unit, (bottomRight.y + topRight.y) * .5f));
        var parent = (RectTransform)_shortcut.transform.parent;
        var canvas = parent.GetComponentInParent<Canvas>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
        PlayerItemUIStyle.Place(_shortcut.rectTransform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), local,
            Vector2.one * size / Mathf.Max(.001f, Mathf.Abs(parent.lossyScale.x)));
    }

    private void OpenDrawing(TalismanItemData item, string id)
    {
        var drawing = _owner != null ? _owner.GetComponent<PlayerTalismanDrawingController>() : null;
        if (drawing == null || !PlayerActionGuard.CanAct(_owner.gameObject)) return;
        Close();
        drawing.Open(item, id);
    }

    private void HandleDeath(GameObject source) => Close();

    private void CreateSlotToolbar()
    {
        _slotToolbar = new GameObject("QuickSlotAssignment", typeof(RectTransform));
        _slotToolbar.transform.SetParent(_inventory.transform, false);
        PlayerItemUIStyle.Place((RectTransform)_slotToolbar.transform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), Vector2.zero, new Vector2(440f, 130f));
        _selectionText = new GameObject("SelectedItem", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        _selectionText.transform.SetParent(_slotToolbar.transform, false);
        PlayerItemUIStyle.Place(_selectionText.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0, 0), new Vector2(440f, 40f));
        var font = _owner.InventoryPanelPrefab.GetComponentInChildren<TMP_Text>(true).font;
        PlayerItemUIStyle.Label(_selectionText, font, 18f); _selectionText.color = new Color(.16f, .11f, .07f, 1f);
        _selectionText.alignment = TextAlignmentOptions.Center;
        _selectionText.text = "개인 아이템 선택 → 퀵슬롯 등록";
        _owner.GetComponent<PlayerInventory>().Drops.DropResolved += OnDropResolved;
        for (int i = 0; i < 8; i++)
        {
            int slot = i;
            var buttonObject = new GameObject(i < 6 ? "Slot" + (i + 1) : i == 6 ? "Unassign" : "Drop", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_slotToolbar.transform, false);
            float x = -185 + i * 74;
            PlayerItemUIStyle.Place((RectTransform)buttonObject.transform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(i >= 6 ? (i == 6 ? -125 : 125) : x, i >= 6 ? 0 : 44), new Vector2(i >= 6 ? 150f : 66f, 38f));
            var button = buttonObject.GetComponent<Button>(); button.targetGraphic = buttonObject.GetComponent<Image>();
            button.targetGraphic.color = new Color(.23f, .19f, .15f, 1f);
            if (i < 6)
            {
                var outline = buttonObject.AddComponent<Outline>();
                outline.effectColor = new Color(.86f, .63f, .28f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
                outline.enabled = false;
            }
            button.onClick.AddListener(() => { if (slot == 7) DropSelected(); else AssignSelected(slot); }); button.interactable = false;
            var colors = button.colors; colors.disabledColor = new Color(.5f, .5f, .5f, .45f); button.colors = colors;
            var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(buttonObject.transform, false); PlayerItemUIStyle.Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            PlayerItemUIStyle.Label(text, font, 19f); text.alignment = TextAlignmentOptions.Center;
            text.text = i < 6 ? (i + 1).ToString() : i == 6 ? "등록 해제" : "버리기";
            _slotButtons.Add(button);
        }
        _drawButton = Instantiate(_slotButtons[7], _slotToolbar.transform);
        _drawButton.name = "DrawPattern"; _drawButton.onClick = new Button.ButtonClickedEvent();
        _drawButton.onClick.AddListener(() => { if (_selectedItem is TalismanItemData item) OpenDrawing(item, null); });
        PlayerItemUIStyle.Place((RectTransform)_drawButton.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(150, 88), new Vector2(140, 36));
        _drawButton.GetComponentInChildren<TMP_Text>().text = "문양 그리기";
        _drawButton.gameObject.SetActive(false);
    }

    private void SelectItem(ItemData item)
    {
        _selectedItem = item;
        var drops = _owner.GetComponent<PlayerInventory>().Drops;
        _selectionText.text = item == null ? "개인 아이템을 선택하세요." : drops.CanDrop(item, out var reason)
            ? item.DisplayName + " · 퀵슬롯 등록 / 1개 버리기" : item.DisplayName + " · " + reason;
        RefreshSlotSelection(_owner.GetComponent<PlayerInventory>());
    }

    private void RefreshSlotSelection(PlayerInventory inventory)
    {
        // Show the selected item's actual assignment, not the button's keyboard/mouse focus.
        bool owned = inventory != null && _selectedItem != null && inventory.Contains(_selectedItem);
        for (int i = 0; i < Mathf.Min(6, _slotButtons.Count); i++)
        {
            var button = _slotButtons[i];
            bool selected = owned && inventory.GetQuickSlotItem(i) == _selectedItem &&
                string.IsNullOrEmpty(inventory.GetQuickSlotInstanceId(i));
            button.targetGraphic.color = selected ? new Color(.62f, .39f, .13f, 1f) : new Color(.23f, .19f, .15f, 1f);
            if (button.TryGetComponent<Outline>(out var outline)) outline.enabled = selected;
        }
    }

    private bool IsSelectedAssigned(PlayerInventory inventory)
    {
        for (int i = 0; i < inventory.QuickSlotCount; i++)
            if (inventory.GetQuickSlotItem(i) == _selectedItem && string.IsNullOrEmpty(inventory.GetQuickSlotInstanceId(i))) return true;
        return false;
    }

    private void DropSelected()
    {
        if (_owner == null) return;
        var drops = _owner.GetComponent<PlayerInventory>().Drops;
        drops.RequestDrop(_selectedItem);
        _selectionText.text = drops.LastMessage;
    }

    private void OnDropResolved(long id, bool success, string message)
    { if (_selectionText != null) _selectionText.text = message; }

    private void AssignSelected(int slot)
    {
        var inventory = _owner != null ? _owner.GetComponent<PlayerInventory>() : null;
        if (inventory == null || _selectedItem == null || !_selectedItem.CanUseQuickSlot || !inventory.Contains(_selectedItem) || !PlayerActionGuard.CanAct(_owner.gameObject)) return;
        if (slot < inventory.QuickSlotCount)
        {
            if (inventory.AssignQuickSlot(slot, _selectedItem)) _selectionText.text = _selectedItem.DisplayName + " · 퀵슬롯 " + (slot + 1) + " 등록";
        }
        else
        {
            for (int i = 0; i < inventory.QuickSlotCount; i++)
                if (inventory.GetQuickSlotItem(i) == _selectedItem && string.IsNullOrEmpty(inventory.GetQuickSlotInstanceId(i))) inventory.ClearQuickSlot(i);
            _selectionText.text = _selectedItem.DisplayName + " · 등록 해제 (소지품 보관)";
        }
        RefreshSlotSelection(inventory);
    }

    private void CreateRewardButton()
    {
        _rewardButton = new GameObject("OpenSharedRewards", typeof(RectTransform), typeof(Image), typeof(Button));
        _rewardButton.transform.SetParent(_inventory.transform, false);
        PlayerItemUIStyle.Place((RectTransform)_rewardButton.transform, new Vector2(1, 1), new Vector2(1, .5f), new Vector2(0, -36), new Vector2(140, 36));
        var button = _rewardButton.GetComponent<Button>(); button.targetGraphic = _rewardButton.GetComponent<Image>(); button.targetGraphic.color = new Color(.23f, .19f, .15f, 1f);
        button.onClick.AddListener(() => { _inventory.gameObject.SetActive(false); _rewardView.gameObject.SetActive(true); });
        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); text.transform.SetParent(_rewardButton.transform, false);
        PlayerItemUIStyle.Stretch(text.rectTransform, Vector2.zero, Vector2.zero); PlayerItemUIStyle.Label(text, _owner.InventoryPanelPrefab.GetComponentInChildren<TMP_Text>(true).font, 18);
        text.text = "공용 보상함"; text.alignment = TextAlignmentOptions.Center;
    }

    private void ShowInventory()
    {
        if (_rewardView != null) _rewardView.gameObject.SetActive(false);
        if (_inventory != null) _inventory.gameObject.SetActive(true);
    }

    public void Unbind()
    {
        Close();
        if (_owner != null && _owner.TryGetComponent<PlayerItemDropController>(out var drops)) drops.DropResolved -= OnDropResolved;
        if (_input != null) { _input.NotebookPressed -= Toggle; _input.CancelPressed -= HandleFallbackCancel; }
        _listensForCancel = false;
        if (_notebook != null) _notebook.NotebookOpenRequested -= Open;
        if (_health != null) _health.Died -= HandleDeath;
        _inventory?.Bind(null); _crafting?.Bind(null);
        foreach (var root in _pageRoots) if (root != null) { root.SetActive(false); Destroy(root); }
        foreach (var placeholder in _hiddenPlaceholders) if (placeholder != null) placeholder.SetActive(true);
        _pageRoots.Clear(); _hiddenPlaceholders.Clear();
        _slotButtons.Clear(); _selectedItem = null; _slotToolbar = null; _selectionText = null; _drawButton = null;
        _rewardView = null; _rewardButton = null;
        if (_shortcut != null) { _shortcut.gameObject.SetActive(false); Destroy(_shortcut.gameObject); }
        _shortcut = null; _quickSlots = null;
        _owner = null; _input = null; _notebook = null; _health = null; _inventory = null; _crafting = null;
    }

    private void OnDisable() => Unbind();
}
