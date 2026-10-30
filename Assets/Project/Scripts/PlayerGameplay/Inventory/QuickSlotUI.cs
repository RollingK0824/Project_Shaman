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

    [Header("Presentation")]
    [SerializeField, Range(.4f, .85f)] private float _iconAreaRatio = .68f;
    [SerializeField] private Color _selectionAccent = new Color(.67f, .34f, .09f, 1f);

    public PlayerInventory BoundInventory => _inventory;

    private int _selectedIndex = -1;
    private float _nextBindingCheck;
    private readonly TalismanDrawingSurface[] _drawings = new TalismanDrawingSurface[6];
    private readonly Image[] _selectionFrames = new Image[6];

    private void LateUpdate()
    {
        ResolveLocalPlayer();
        var rect = transform as RectTransform;
        if (rect == null || rect.rect.width <= 0f) return;
        float unit = PlayerItemUIStyle.ViewportUnit(rect);
        rect.localScale = Vector3.one * (560f / rect.rect.width) * unit;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 62f * unit);
    }

    private void ResolveLocalPlayer()
    {
        if (_inventory != null && _inventory.gameObject.activeInHierarchy &&
            PlayerLocalPresentation.IsLocal(_inventory.gameObject) && _inputReader != null) return;
        if (Time.unscaledTime < _nextBindingCheck) return;
        _nextBindingCheck = Time.unscaledTime + .5f;

        // Scene HUDs can outlive a player or start without serialized player references.
        // Never guess between multiple offline players or bind another client's inventory.
        PlayerInventory candidate = null;
        foreach (var inventory in FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None))
        {
            if (!PlayerLocalPresentation.IsLocal(inventory.gameObject) ||
                inventory.GetComponent<PlayerInputReader>() == null) continue;
            if (candidate != null) { candidate = null; break; }
            candidate = inventory;
        }
        if (candidate != null) Bind(candidate, candidate.GetComponent<PlayerInputReader>());
        else if (_inventory != null) Bind(null, null);
    }

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
        ApplyPresentationLayout();
        if (_inventory != null && _inventory.TryGetComponent<PlayerItemController>(out var items)) _selectedIndex = items.SelectedSlotIndex;
        if (_inputReader != null)
        {
            _inputReader.SlotSelected +=
                OnSlotSelected;
        }

        if (_inventory != null)
        {
            var sheets = _inventory.GetComponent<PlayerTalismanCollection>();
            if (sheets != null) sheets.SelectionChanged += RefreshAllSlots;
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
            var sheets = _inventory.GetComponent<PlayerTalismanCollection>();
            if (sheets != null) sheets.SelectionChanged -= RefreshAllSlots;
            _inventory.QuickSlotChanged -=
                OnQuickSlotChanged;

            _inventory.ItemCountChanged -=
                OnItemCountChanged;
        }
    }


    private void ApplyPresentationLayout()
    {
        AlignSlotsToArtwork();
        // The ornamental art is a child Image. A sprite-less panel behind it leaves gray square corners.
        var backdrop = GetComponent<Image>();
        if (backdrop != null && backdrop.sprite == null && GetComponent<Mask>() == null)
        {
            backdrop.color = Color.clear;
            backdrop.raycastTarget = false;
        }
        for (int i = 0; i < _slotIcons.Length && i < _slotBackgrounds.Length; i++)
        {
            var icon = _slotIcons[i];
            var slot = _slotBackgrounds[i];
            if (icon == null || slot == null) continue;
            slot.raycastTarget = false;
            var rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.one * Mathf.Min(slot.rectTransform.rect.width, slot.rectTransform.rect.height) * _iconAreaRatio;
            icon.raycastTarget = false;
            if (i >= _countTexts.Length || _countTexts[i] == null) continue;
            var count = _countTexts[i];
            var countRect = count.rectTransform;
            countRect.anchorMin = countRect.anchorMax = countRect.pivot = new Vector2(1f, 0f);
            countRect.anchoredPosition = new Vector2(-7f, 5f);
            countRect.sizeDelta = new Vector2(46f, 20f);
            count.fontSize = 19f;
            count.fontStyle = FontStyles.Bold;
            count.alignment = TextAlignmentOptions.BottomRight;
            count.color = new Color(.23f, .14f, .07f, 1f);
            count.raycastTarget = false;
        }
    }

    private void AlignSlotsToArtwork()
    {
        var art = transform.Find("BarBackground") as RectTransform;
        if (art == null) return;
        var image = art.GetComponent<Image>();
        if (image == null || image.sprite == null) return;
        // The six painted cells occupy 7.3%..92.7% of the source image, not the
        // old 90px cells + 10px layout spacing. Include the image's transparent margins.
        var layout = GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = false;
        Rect drawing = art.rect;
        float aspect = image.sprite.rect.width / image.sprite.rect.height;
        if (image.preserveAspect)
        {
            Vector2 size = drawing.size;
            if (size.x / size.y > aspect) size.x = size.y * aspect;
            else size.y = size.x / aspect;
            drawing = new Rect(drawing.center - size * .5f, size);
        }
        for (int i = 0; i < _slotBackgrounds.Length; i++)
        {
            if (_slotBackgrounds[i] == null) continue;
            var slot = _slotBackgrounds[i].rectTransform;
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(.5f, .5f);
            slot.sizeDelta = new Vector2(drawing.width * .123f, drawing.height * .355f);
            slot.position = art.TransformPoint(new Vector3(
                drawing.xMin + drawing.width * (.135f + i * .146f),
                drawing.yMin + drawing.height * .51f, 0f));
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

        if (_inventory != null && _inventory.TryGetComponent<PlayerItemController>(out var items) &&
            (!items.CanUseItems || !PlayerActionGuard.CanAct(_inventory.gameObject))) return;
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

            if (_selectionFrames[i] == null)
            {
                var go = new GameObject("SelectedSlotFrame", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(background.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, -7f);
                rect.sizeDelta = new Vector2(28f, 3f);
                var frame = go.GetComponent<Image>(); frame.raycastTarget = false;
                frame.color = _selectionAccent;
                // Small seal-like marker follows the parchment art without redrawing its border.
                var marker = new GameObject("SelectionMarker", typeof(RectTransform), typeof(Image));
                marker.transform.SetParent(rect, false);
                var markerRect = (RectTransform)marker.transform;
                markerRect.sizeDelta = new Vector2(8f, 8f);
                markerRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var markerImage = marker.GetComponent<Image>(); markerImage.raycastTarget = false;
                markerImage.color = _selectionAccent;
                _selectionFrames[i] = frame;
            }
            _selectionFrames[i].gameObject.SetActive(i == _selectedIndex);
            background.color =
                i == _selectedIndex
                    ? new Color(_selectionAccent.r, _selectionAccent.g, _selectionAccent.b, Mathf.Min(_selectedColor.a, .12f))
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

        // Older scene UI starts Icon GameObjects inactive, not just their Image components.
        iconImage.gameObject.SetActive(itemData != null);
        var drawing = _inventory != null ? _inventory.GetQuickSlotDrawing(slotIndex) : null;
        if (drawing != null && itemData is TalismanItemData talisman)
        {
            if (_drawings[slotIndex] == null)
            {
                var go = new GameObject("DrawingThumbnail", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter), typeof(TalismanDrawingSurface));
                go.transform.SetParent(iconImage.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var fit = go.GetComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = .5f;
                go.GetComponent<RawImage>().raycastTarget = false;
                _drawings[slotIndex] = go.GetComponent<TalismanDrawingSurface>();
            }
            var surface = _drawings[slotIndex];
            surface.BindThumbnail(surface.GetComponent<RawImage>(), talisman, drawing);
            iconImage.enabled = false;
            return;
        }
        if (_drawings[slotIndex] != null)
        {
            _drawings[slotIndex].gameObject.SetActive(false);
            Destroy(_drawings[slotIndex].gameObject); _drawings[slotIndex] = null;
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

        if (!string.IsNullOrEmpty(_inventory.GetQuickSlotInstanceId(slotIndex)))
        { HideCount(countText); return; }
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
