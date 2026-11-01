using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One virtual group; quickslots reference collection instances without copying their data.
public class EffectTalismanInventoryView : MonoBehaviour
{
    [SerializeField] private Transform _rows;
    [SerializeField] private EffectTalismanInventoryRow _rowPrefab;
    [SerializeField] private TMP_Text _details, _status;
    [SerializeField] private Button _backButton, _useButton;
    [SerializeField] private GameObject _slotPicker;
    [SerializeField] private Button[] _slotButtons = new Button[6];
    private PlayerTalismanCollection _collection;
    private readonly List<EffectTalismanInventoryRow> _visibleRows = new();
    private string _selectedId;
    private Action _back;
    private Button _unassignButton;
    private Button _dropButton;
    private PlayerItemDropController _drops;
    private void Awake()
    {
        _backButton.onClick.AddListener(() => _back?.Invoke());
        _useButton.onClick.AddListener(UseSelected);
        _unassignButton = CreateAction("Unassign", "등록 해제", new Vector2(-210, -390));
        _unassignButton.onClick.AddListener(UnassignSelected);
        _dropButton = CreateAction("Drop", "버리기", new Vector2(-30, -390));
        _dropButton.onClick.AddListener(DropSelected);
        for (int i = 0; i < _slotButtons.Length; i++)
        {
            int slot = i;
            if (_slotButtons[i] != null) _slotButtons[i].onClick.AddListener(() => AssignSelected(slot));
        }
    }
    private Button CreateAction(string name, string label, Vector2 position)
    {
        var button = Instantiate(_useButton, _useButton.transform.parent);
        button.name = name; button.onClick = new Button.ButtonClickedEvent();
        PlayerItemUIStyle.Place((RectTransform)button.transform, Vector2.one, Vector2.one, position, new Vector2(168, 42));
        var text = button.GetComponentInChildren<TMP_Text>(); text.text = label;
        PlayerItemUIStyle.Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
        var colors = button.colors; colors.disabledColor = new Color(.5f, .5f, .5f, .45f); button.colors = colors;
        return button;
    }
    public void Bind(PlayerTalismanCollection collection, Action back)
    {
        if (_collection != null) _collection.SelectionChanged -= Refresh;
        if (_drops != null) _drops.DropResolved -= HandleDropResolved;
        _collection = collection; _back = back; _selectedId = null;
        _drops = collection != null ? collection.GetComponent<PlayerInventory>().Drops : null;
        if (_drops != null) _drops.DropResolved += HandleDropResolved;
        if (_collection != null) _collection.SelectionChanged += Refresh;
        if (_slotPicker != null) _slotPicker.SetActive(false); _status.text = string.Empty; Refresh();
    }
    private void OnDestroy() { if (_collection != null) _collection.SelectionChanged -= Refresh; if (_drops != null) _drops.DropResolved -= HandleDropResolved; }
    private void LateUpdate()
    {
        bool assigned = false;
        if (_collection != null && _collection.Find(_selectedId, out _) != null && PlayerActionGuard.CanAct(_collection.gameObject))
        {
            var inventory = _collection.GetComponent<PlayerInventory>();
            for (int i = 0; i < inventory.QuickSlotCount; i++) assigned |= inventory.GetQuickSlotInstanceId(i) == _selectedId;
        }
        _unassignButton.interactable = assigned;
        _dropButton.interactable = _drops != null && !_drops.IsPending && _collection != null &&
            _collection.Find(_selectedId, out _) != null && PlayerActionGuard.CanAct(_collection.gameObject);
    }
    public void UnassignSelected()
    {
        if (_collection == null || !PlayerActionGuard.CanAct(_collection.gameObject) || _collection.Find(_selectedId, out _) == null) return;
        var inventory = _collection.GetComponent<PlayerInventory>();
        for (int i = 0; i < inventory.QuickSlotCount; i++)
            if (inventory.GetQuickSlotInstanceId(i) == _selectedId) inventory.ClearQuickSlot(i);
        _status.text = "등록 해제 · 문양 부적은 소지품에 보관됩니다.";
    }
    public void Refresh()
    {
        foreach (var row in _visibleRows) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        _visibleRows.Clear();
        if (_collection == null) return;
        foreach (var item in _collection.ItemTypes)
        {
            if (!item.IsEffectTalisman) continue;
            foreach (var drawing in _collection.GetInstances(item))
            {
                var row = Instantiate(_rowPrefab, _rows); row.gameObject.SetActive(true);
                row.Bind(item, drawing, () => Select(drawing.Id)); _visibleRows.Add(row);
            }
        }
        ShowDetails();
    }
    public void Select(string id)
    {
        var drawing = _collection.Find(id, out var item);
        if (drawing == null) return;
        _selectedId = id; if (_slotPicker != null) _slotPicker.SetActive(false); _status.text = string.Empty; ShowDetails();
    }
    public void DropSelected()
    {
        if (_drops == null || _collection == null || _collection.Find(_selectedId, out _) == null) return;
        _drops.RequestDrawingDrop(_selectedId);
        _status.text = _drops.LastMessage;
    }
    private void HandleDropResolved(long requestId, bool success, string message) { if (_status != null) _status.text = message; }
    private void ShowDetails()
    {
        var drawing = _collection != null ? _collection.Find(_selectedId, out _) : null;
        _useButton.interactable = drawing != null && PlayerActionGuard.CanAct(_collection.gameObject);
        if (drawing == null) { if (_slotPicker != null) _slotPicker.SetActive(false); _details.text = "효과 부적을 선택해 주세요."; return; }
        _collection.Find(_selectedId, out var item); var effect = item.FindEffect(drawing.EffectId);
        _details.text = effect == null ? "사용할 수 없는 효과입니다." :
            $"{drawing.CustomName}\n{effect.DisplayName}\n{effect.Description}\n{(effect.ConsumeOnUse ? "사용 후 소모" : "사용 후 유지")}";
        _useButton.interactable &= effect != null;
    }
    public void UseSelected()
    {
        if (_collection == null || _collection.Find(_selectedId, out _) == null || !PlayerActionGuard.CanAct(_collection.gameObject)) return;
        if (_slotPicker != null) _slotPicker.SetActive(true);
        _status.text = "등록할 퀵슬롯 1~6을 선택해 주세요.";
    }
    public bool AssignSelected(int slotIndex)
    {
        if (_collection == null) return false;
        var inventory = _collection.GetComponent<PlayerInventory>();
        bool success = inventory != null && inventory.AssignTalismanQuickSlot(slotIndex, _selectedId);
        _status.text = success ? $"슬롯 {slotIndex + 1}에 등록했습니다. 인벤토리를 닫고 숫자키로 장착하세요." : "등록하지 못했습니다. 같은 부적의 중복 등록 또는 소유 상태를 확인해 주세요.";
        if (success && _slotPicker != null) _slotPicker.SetActive(false);
        return success;
    }
}
