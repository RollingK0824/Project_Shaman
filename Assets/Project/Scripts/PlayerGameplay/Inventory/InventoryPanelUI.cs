using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory _inventory;
    [SerializeField] private Transform _itemGrid;
    [SerializeField] private InventoryItemSlotUI _itemSlotPrefab;
    public InventoryItemSlotUI ItemSlotPrefab => _itemSlotPrefab;
    private readonly Dictionary<ItemData, InventoryItemSlotUI> _slots = new();
    private readonly List<ItemData> _removed = new();
    private readonly Dictionary<string, InventoryItemSlotUI> _customSlots = new();
    private PlayerTalismanCollection _talismans;
    [SerializeField] private EffectTalismanInventoryView _effectViewPrefab;
    private EffectTalismanInventoryView _effectView;
    private ScrollRect _scroll;
    private bool _presentationReady;
    private TMP_Text _footer;
    private bool _embedded, _notebookStyled;
    private System.Action<ItemData> _selectItem;
    private System.Action<TalismanItemData, string> _openDrawing;
    public void ConfigureNotebook(System.Action<TalismanItemData, string> openDrawing, System.Action<ItemData> selectItem)
    {
        _embedded = true; _openDrawing = openDrawing; _selectItem = selectItem;
        if (!_presentationReady || _notebookStyled) return;
        _notebookStyled = true;
        var background = GetComponent<Image>();
        if (background != null) background.color = Color.clear;
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            if (label.transform.parent == transform)
            {
                label.color = new Color(.16f, .11f, .07f, 1f);
                if (label != _footer) { label.rectTransform.anchoredPosition = new Vector2(-245f, -37f); label.rectTransform.sizeDelta = new Vector2(420f, 44f); }
            }
        if (_footer != null) _footer.gameObject.SetActive(false);
        PlayerItemUIStyle.Stretch((RectTransform)_scroll.transform, new Vector2(30f, 120f), new Vector2(-30f, -76f));
        var grid = _itemGrid.GetComponent<GridLayoutGroup>();
        if (grid != null) { grid.cellSize = new Vector2(198f, 174f); grid.spacing = new Vector2(32f, 16f); }
        Refresh();
    }
    public bool IsEffectViewOpen => _effectView != null && _effectView.gameObject.activeSelf;
    public bool BackToMain()
    {
        if (!IsEffectViewOpen) return false;
        _effectView.gameObject.SetActive(false); _itemGrid.gameObject.SetActive(true);
        if (_footer != null) _footer.gameObject.SetActive(!_embedded);
        Refresh(); return true;
    }
    public void OpenEffectGroup()
    {
        // Scene-authored panels predating the grouped inventory may not have this new field.
        // Resolve the same UI prefab from the bound Player, without changing scene overrides.
        if (_effectViewPrefab == null && _inventory != null)
            _effectViewPrefab = _inventory.GetComponent<PlayerTalismanDrawingController>()?.EffectInventoryPrefab;
        if (_talismans == null || _talismans.DrawnEffectCount == 0 || _effectViewPrefab == null) return;
        if (_effectView == null) _effectView = Instantiate(_effectViewPrefab, transform);
        _effectView.Bind(_talismans, () => BackToMain()); _itemGrid.gameObject.SetActive(false); _effectView.gameObject.SetActive(true);
        if (_footer != null) _footer.gameObject.SetActive(false);
    }

    public void Bind(PlayerInventory inventory)
    {
        if (_inventory != null) _inventory.InventoryChanged -= Refresh;
        if (_talismans != null) _talismans.SelectionChanged -= Refresh;
        _inventory = inventory;
        _talismans = inventory != null ? inventory.GetComponent<PlayerTalismanCollection>() : null;
        if (isActiveAndEnabled && _talismans != null) _talismans.SelectionChanged += Refresh;
        if (isActiveAndEnabled && _inventory != null) _inventory.InventoryChanged += Refresh;
        Refresh();
    }

    private void OnEnable()
    {
        ApplyPresentation();
        if (_inventory == null)
        {
            return;
        }

        _inventory.InventoryChanged += Refresh;
        _talismans = _inventory.GetComponent<PlayerTalismanCollection>();
        if (_talismans != null) _talismans.SelectionChanged += Refresh;

        Refresh();
    }

    private void LateUpdate()
    {
        if (_presentationReady)
        {
            if (_embedded) PlayerItemUIStyle.FitEmbeddedPanel((RectTransform)transform);
            else PlayerItemUIStyle.FitPanel((RectTransform)transform);
        }
    }

    private void ApplyPresentation()
    {
        if (_itemGrid == null || _itemSlotPrefab == null) return;
        PlayerItemUIStyle.FitPanel((RectTransform)transform);
        if (_presentationReady) return;
        var font = _itemSlotPrefab.GetComponentInChildren<TMP_Text>(true)?.font;
        var background = GetComponent<Image>();
        if (background != null) background.color = PlayerItemUIStyle.Background;
        // Older scene-authored panels have a direct grid and no scroll viewport.
        _scroll = _itemGrid.GetComponentInParent<ScrollRect>();
        if (_scroll == null)
        {
            var go = new GameObject("Items", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            go.transform.SetParent(transform, false);
            go.GetComponent<Image>().color = Color.clear;
            _scroll = go.GetComponent<ScrollRect>();
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(go.transform, false);
            _scroll.viewport = (RectTransform)viewport.transform;
            _itemGrid.SetParent(viewport.transform, false);
            _scroll.content = (RectTransform)_itemGrid;
        }
        _scroll.horizontal = false; _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Clamped; _scroll.scrollSensitivity = 36f;
        PlayerItemUIStyle.Stretch((RectTransform)_scroll.transform, new Vector2(30f, 56f), new Vector2(-30f, -76f));
        PlayerItemUIStyle.Stretch(_scroll.viewport, Vector2.zero, Vector2.zero);
        var content = (RectTransform)_itemGrid;
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var grid = _itemGrid.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.cellSize = new Vector2(210f, 174f); grid.spacing = new Vector2(16f, 16f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
            grid.padding = new RectOffset(14, 14, 8, 8);
        }
        var fit = _itemGrid.GetComponent<ContentSizeFitter>();
        if (fit == null) fit = _itemGrid.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.transform.parent != transform) continue;
            bool footer = label.rectTransform.anchorMax.y < .5f;
            if (footer) _footer = label;
            label.text = footer ? "재료는 팀 공용입니다.  ·  효과 부적을 클릭해 확인하세요.  ·  Esc 닫기" : "소지품";
            PlayerItemUIStyle.Label(label, font, footer ? 18f : 32f);
            label.alignment = TextAlignmentOptions.Center;
            PlayerItemUIStyle.Place(label.rectTransform, new Vector2(.5f, footer ? 0f : 1f), new Vector2(.5f, .5f), new Vector2(0f, footer ? 27f : -37f), new Vector2(916f, 44f));
        }
        _presentationReady = true;
    }

    private void OnDisable()
    {
        if (_inventory == null)
        {
            return;
        }

        _inventory.InventoryChanged -= Refresh;
        if (_talismans != null) _talismans.SelectionChanged -= Refresh;
        BackToMain();
    }

    public void Refresh()
    {
        if (_itemGrid == null ||
            _itemSlotPrefab == null)
        {
            return;
        }

        _removed.Clear();
        foreach (var row in _customSlots.Values) if (row != null) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        _customSlots.Clear();
        foreach (var entry in _slots)
        {
            if (_inventory != null && _inventory.Contains(entry.Key) && !(entry.Key is TalismanItemData effect && effect.IsEffectTalisman)) continue;
            if (entry.Value != null) { entry.Value.gameObject.SetActive(false); Destroy(entry.Value.gameObject); }
            _removed.Add(entry.Key);
        }
        foreach (var item in _removed) _slots.Remove(item);
        if (_inventory == null) return;

        // PlayerInventory에는 같은 ItemData가 수량만큼 들어 있으므로
        // 같은 종류는 한 번만 슬롯을 생성한다.
        HashSet<ItemData> displayedItems = new HashSet<ItemData>();
        int blankEffects = 0;
        TalismanItemData blankTemplate = null;

        var visibleItems = new List<ItemData>(_inventory.Items);
        if (_inventory.SharedMaterials != null) visibleItems.AddRange(_inventory.SharedMaterials.Counts.Keys);
        foreach (ItemData itemData in visibleItems)
        {
            if (itemData == null ||
                displayedItems.Contains(itemData))
            {
                continue;
            }

            displayedItems.Add(itemData);

            int count = _inventory.GetItemCount(itemData);
            // Legacy/test data references may coexist with the canonical asset after migration.
            // Their blank quantities still occupy one visible group, while ownership keeps its original references.
            if (itemData is TalismanItemData blank && blank.IsEffectTalisman && _talismans != null)
            {
                blankEffects += count; blankTemplate ??= blank; continue;
            }

            if (!_slots.TryGetValue(itemData, out var slot) || slot == null)
            {
                slot = Instantiate(_itemSlotPrefab, _itemGrid);
                _slots[itemData] = slot;
            }

            slot.Setup(itemData, count);
            slot.gameObject.SetActive(true);
            if (_embedded && itemData.Category != ItemCategory.Material) slot.SetUse(() => _selectItem?.Invoke(itemData));
            if (itemData is TalismanItemData talisman && _talismans != null)
            {
                int plain = 0; string plainId = null;
                foreach (var sheet in _talismans.GetInstances(talisman))
                {
                    if (!sheet.IsCustom) { plain++; plainId ??= sheet.Id; continue; }
                    var row = Instantiate(_itemSlotPrefab, _itemGrid);
                    row.Setup(itemData, 1);
                    string id = sheet.Id;
                    row.SetInstance(string.IsNullOrEmpty(sheet.CustomName) ? talisman.DisplayName : sheet.CustomName, () => UseTalisman(talisman, id));
                    _customSlots[id] = row;
                }
                slot.Setup(itemData, plain); slot.gameObject.SetActive(plain > 0);
                slot.SetUse(() => UseTalisman(talisman, plainId));
            }
        }
        if (blankTemplate != null && blankEffects > 0)
        {
            var row = Instantiate(_itemSlotPrefab, _itemGrid);
            row.Setup(blankTemplate, blankEffects); row.SetLabel("미사용 효과 부적"); row.SetUse(() => UseTalisman(blankTemplate, null));
            _customSlots["effect-blanks"] = row;
        }
        if (_talismans != null && _talismans.DrawnEffectCount > 0)
        {
            TalismanItemData template = null;
            foreach (var type in _talismans.ItemTypes) if (type.IsEffectTalisman && _talismans.GetInstances(type).Count > 0) { template = type; break; }
            var row = Instantiate(_itemSlotPrefab, _itemGrid);
            row.Setup(template, _talismans.DrawnEffectCount); row.SetLabel("드로잉된 효과 부적"); row.SetUse(OpenEffectGroup);
            _customSlots["effect-group"] = row;
        }
        if (IsEffectViewOpen) _effectView.Refresh();
    }
    private void UseTalisman(TalismanItemData item, string id)
    {
        if (_embedded) { _openDrawing?.Invoke(item, id); return; }
        var drawing = _inventory.GetComponent<PlayerTalismanDrawingController>();
        var ui = _inventory.GetComponentInChildren<InventoryUIController>(true);
        if (ui == null) ui = GetComponentInParent<InventoryUIController>();
        if (ui == null)
            foreach (var candidate in FindObjectsByType<InventoryUIController>(FindObjectsSortMode.None))
                if (candidate.OwnsPanel(transform)) { ui = candidate; break; }
        drawing?.OpenFromInventory(item, id, ui);
    }
}
