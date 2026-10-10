using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryItemSlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private TMP_Text _nameText;

    private ItemData _itemData;

    public void Setup(ItemData itemData, int count)
    {
        _itemData = itemData;

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
        if (_nameText != null) _nameText.text = itemData != null ? itemData.DisplayName : string.Empty;
    }
}
