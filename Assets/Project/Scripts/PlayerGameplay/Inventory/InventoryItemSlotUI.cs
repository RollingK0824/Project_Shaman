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
    private bool _notebookStyle;
    private bool _selected;
    private GameObject _selectionBorder;
    public bool IsSelected => _selected;

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (selected && _selectionBorder == null)
        {
            _selectionBorder = new GameObject("SelectionBorder", typeof(RectTransform));
            _selectionBorder.transform.SetParent(transform, false);
            PlayerItemUIStyle.Stretch((RectTransform)_selectionBorder.transform, Vector2.zero, Vector2.zero);
            for (int i = 0; i < 4; i++)
            {
                var edge = new GameObject("Edge" + i, typeof(RectTransform), typeof(Image));
                edge.transform.SetParent(_selectionBorder.transform, false);
                var rect = (RectTransform)edge.transform;
                bool horizontal = i < 2;
                rect.anchorMin = horizontal ? new Vector2(0, i) : new Vector2(i - 2, 0);
                rect.anchorMax = horizontal ? new Vector2(1, i) : new Vector2(i - 2, 1);
                rect.pivot = horizontal ? new Vector2(.5f, i) : new Vector2(i - 2, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = horizontal ? new Vector2(0, 3) : new Vector2(3, 0);
                var image = edge.GetComponent<Image>();
                image.color = new Color(.58f, .34f, .10f, .95f);
                image.raycastTarget = false;
            }
        }
        if (_selectionBorder != null) _selectionBorder.SetActive(selected);
        if (_notebookStyle && TryGetComponent<Image>(out var background))
            background.color = selected ? new Color(.65f, .39f, .13f, .32f) : new Color(.37f, .24f, .12f, .14f);
    }
    public void SetInstance(string displayName, System.Action use)
    {
        if (_nameText != null) _nameText.text = displayName;
        if (_countText != null) _countText.text = string.Empty;
        _use = use;
    }
    public void SetUse(System.Action use) => _use = use;
    public void SetLabel(string displayName) { if (_nameText != null) _nameText.text = displayName; }
    public void ConfigureNotebook()
    {
        _notebookStyle = true;
        SetSelected(_selected);
        if (_icon != null) PlayerItemUIStyle.Place(_icon.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -8), new Vector2(82, 82));
        if (_nameText != null)
        {
            PlayerItemUIStyle.Place(_nameText.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 4), new Vector2(184, 48));
            _nameText.color = PlayerItemUIStyle.NotebookInk; _nameText.fontSize = 19;
        }
        if (_countText != null)
        {
            PlayerItemUIStyle.Place(_countText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -8), new Vector2(66, 25));
            _countText.color = PlayerItemUIStyle.NotebookInk; _countText.fontSize = 18;
        }
    }
    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData e)
    { if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left) _use?.Invoke(); }

    public void Setup(ItemData itemData, int count)
    {
        _use = null;
        SetSelected(false);

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
