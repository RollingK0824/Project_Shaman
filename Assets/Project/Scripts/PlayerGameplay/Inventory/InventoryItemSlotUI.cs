using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryItemSlotUI : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private TMP_Text _nameText;

    private System.Action _use;
    public void SetInstance(string displayName, System.Action use)
    {
        if (_nameText != null) _nameText.text = displayName;
        if (_countText != null) _countText.text = string.Empty;
        _use = use;
    }
    public void SetUse(System.Action use) => _use = use;
    public void SetLabel(string displayName) { if (_nameText != null) _nameText.text = displayName; }
    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
    { if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left) _use?.Invoke(); }

    public void Setup(ItemData itemData, int count)
    {
        _use = null;

        if (_icon != null)
        {
            _icon.sprite = itemData != null ? itemData.Icon : null;
            _icon.enabled = _icon.sprite != null;
            _icon.preserveAspect = true;
        }

        if (_countText != null)
        {
            _countText.text = $"x{count}";
        }
        if (_nameText != null) _nameText.text = itemData != null ? (itemData.Category == ItemCategory.Material ? "[공용] " : "") + itemData.DisplayName : string.Empty;
    }
}
