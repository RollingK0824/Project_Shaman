using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuickSlotUI : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerInputReader _inputReader;
    [SerializeField] private PlayerInventory _inventory;

    [Header("Slot Backgrounds")]
    [SerializeField]
    private Image[] _slotBackgrounds =
        new Image[6];

    [Header("Item Icons")]
    [SerializeField]
    private Image[] _slotIcons =
        new Image[6];

    [Header("Item Counts")]
    [SerializeField]
    private TMP_Text[] _countTexts =
        new TMP_Text[6];

    [Header("Selection Colors")]
    [SerializeField]
    private Color _normalColor =
        new Color(1f, 1f, 1f, 0f);

    [SerializeField]
    private Color _selectedColor =
        new Color(1f, 0.72f, 0.18f, 0.18f);

    private int _selectedIndex = -1;

    public void Bind(PlayerInventory inventory, PlayerInputReader inputReader)
    {
        OnDisable();
        _inventory = inventory;
        _inputReader = inputReader;
        _selectedIndex = -1;
        if (isActiveAndEnabled) OnEnable();
        RefreshAllSlots();
        RefreshSelection();
    }


    private void OnEnable()
    {
        if (_inventory != null && _inventory.TryGetComponent<PlayerItemController>(out var items)) _selectedIndex = items.SelectedSlotIndex;
        if (_inputReader != null)
        {
            _inputReader.SlotSelected +=
                OnSlotSelected;
        }

        if (_inventory != null)
        {
            _inventory.QuickSlotChanged +=
                OnQuickSlotChanged;

            _inventory.ItemCountChanged +=
                OnItemCountChanged;
        }
        RefreshAllSlots();
        RefreshSelection();
    }


    private void Start()
    {
        RefreshAllSlots();
        RefreshSelection();
    }


    private void OnDisable()
    {
        if (_inputReader != null)
        {
            _inputReader.SlotSelected -=
                OnSlotSelected;
        }

        if (_inventory != null)
        {
            _inventory.QuickSlotChanged -=
                OnQuickSlotChanged;

            _inventory.ItemCountChanged -=
                OnItemCountChanged;
        }
    }


    // =========================================================
    // Selection
    // =========================================================

    private void OnSlotSelected(
        int slotIndex)
    {
        if (slotIndex < 0 ||
            slotIndex >= _slotBackgrounds.Length)
        {
            return;
        }

        _selectedIndex = slotIndex;

        RefreshSelection();
    }


    private void RefreshSelection()
    {
        for (int i = 0;
             i < _slotBackgrounds.Length;
             i++)
        {
            Image background =
                _slotBackgrounds[i];

            if (background == null)
            {
                continue;
            }

            background.color =
                i == _selectedIndex
                    ? _selectedColor
                    : _normalColor;
        }
    }


    // =========================================================
    // Inventory Events
    // =========================================================

    private void OnQuickSlotChanged(
        int slotIndex,
        ItemData itemData)
    {
        RefreshSlot(
            slotIndex,
            itemData
        );
    }


    private void OnItemCountChanged(
        ItemData itemData,
        int count)
    {
        if (_inventory == null ||
            itemData == null)
        {
            return;
        }

        for (int i = 0;
             i < _inventory.QuickSlotCount;
             i++)
        {
            ItemData slotItem =
                _inventory.GetQuickSlotItem(i);

            if (slotItem != itemData)
            {
                continue;
            }

            RefreshCount(
                i,
                itemData
            );
        }
    }


    // =========================================================
    // Refresh
    // =========================================================

    private void RefreshAllSlots()
    {
        for (int i = 0;
             i < 6;
             i++)
        {
            ItemData itemData =
                _inventory != null ? _inventory.GetQuickSlotItem(i) : null;

            RefreshSlot(
                i,
                itemData
            );
        }
    }


    private void RefreshSlot(
        int slotIndex,
        ItemData itemData)
    {
        RefreshIcon(
            slotIndex,
            itemData
        );

        RefreshCount(
            slotIndex,
            itemData
        );
    }


    private void RefreshIcon(
        int slotIndex,
        ItemData itemData)
    {
        if (slotIndex < 0 ||
            slotIndex >= _slotIcons.Length)
        {
            return;
        }

        Image iconImage =
            _slotIcons[slotIndex];

        if (iconImage == null)
        {
            return;
        }

        Sprite icon =
            itemData != null
                ? itemData.Icon
                : null;

        iconImage.sprite = icon;
        iconImage.preserveAspect = true;
        iconImage.enabled = icon != null;
    }


    private void RefreshCount(
        int slotIndex,
        ItemData itemData)
    {
        if (slotIndex < 0 ||
            slotIndex >= _countTexts.Length)
        {
            return;
        }

        TMP_Text countText =
            _countTexts[slotIndex];

        if (countText == null)
        {
            return;
        }

        if (itemData == null ||
            _inventory == null)
        {
            HideCount(countText);
            return;
        }

        int count =
            _inventory.GetItemCount(
                itemData
            );

        if (count <= 1)
        {
            HideCount(countText);
            return;
        }

        countText.text = $"x{count}";
        countText.enabled = true;
    }


    private void HideCount(
        TMP_Text countText)
    {
        countText.text = string.Empty;
        countText.enabled = false;
    }
}
