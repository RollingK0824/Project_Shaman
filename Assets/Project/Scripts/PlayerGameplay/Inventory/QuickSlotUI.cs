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
    private readonly PlayerHUDFrame[] _selectionFrames = new PlayerHUDFrame[6];

    // Bell walkie-talkie mic badge, top-right of whichever slot holds the bell.
    private NetPlayerVoice _voice;
    private readonly Image[] _bellBadges = new Image[6];

    private void LateUpdate()
    {
        ResolveLocalPlayer();
        RefreshBellBadges();
        var rect = transform as RectTransform;
        if (rect == null || rect.rect.width <= 0f) return;
        float unit = PlayerItemUIStyle.ViewportUnit(rect);
        // Write only on change: transform writes dirty the whole HUD canvas even with equal values.
        Vector3 scale = Vector3.one * (560f / rect.rect.width) * unit;
        Vector2 anchor = new Vector2(.5f, 0f), position = new Vector2(0f, 62f * unit);
        if (rect.localScale != scale) rect.localScale = scale;
        if (rect.anchorMin != anchor) rect.anchorMin = anchor;
        if (rect.anchorMax != anchor) rect.anchorMax = anchor;
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
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
        _voice = inventory != null ? inventory.GetComponent<NetPlayerVoice>() : null;
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
        var art = PlayerHUDArt.Current;
        var hudFont = art != null ? art.Font : null;
        if (hudFont != null)
            foreach (var label in GetComponentsInChildren<TMP_Text>(true)) label.font = hudFont;
        // One soft ink wash joins the slots; no individual hard-edged cell backgrounds.
        var backdrop = GetComponent<Image>();
        if (backdrop != null && GetComponent<Mask>() == null)
        {
            backdrop.sprite = art != null ? art.InkWash : null;
            backdrop.color = art != null ? new Color(1, 1, 1, .68f) : Color.clear;
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
            rect.anchoredPosition = new Vector2(0, 10);
            rect.sizeDelta = Vector2.one * 44f;
            icon.raycastTarget = false;
            if (i >= _countTexts.Length || _countTexts[i] == null) continue;
            var count = _countTexts[i];
            var countRect = count.rectTransform;
            countRect.anchorMin = countRect.anchorMax = countRect.pivot = new Vector2(1f, 0f);
            countRect.anchoredPosition = new Vector2(-9f, 28f);
            countRect.sizeDelta = new Vector2(56f, 22f);
            count.fontSize = 15f;
            count.fontStyle = FontStyles.Bold;
            count.alignment = TextAlignmentOptions.BottomRight;
            count.color = PlayerHUDArt.Text;
            count.raycastTarget = false;
        }
    }

    private void AlignSlotsToArtwork()
    {
        var art = transform.Find("BarBackground");
        if (art != null) art.gameObject.SetActive(false);
        var layout = GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = false;
        ((RectTransform)transform).sizeDelta = new Vector2(560, 84);
        for (int i = 0; i < _slotBackgrounds.Length; i++)
        {
            if (_slotBackgrounds[i] == null) continue;
            var slot = _slotBackgrounds[i].rectTransform;
            PlayerItemUIStyle.Place(slot, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2((i - 2.5f) * 94f, 0), new Vector2(86, 84));
            _selectionFrames[i] = PlayerHUDFrame.Attach(slot);
            foreach (var label in slot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!label.name.StartsWith("KeyText")) continue;
                label.gameObject.SetActive(true);
                label.enabled = true;
                label.text = (i + 1).ToString();
                PlayerItemUIStyle.Place(label.rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0),
                    new Vector2(0, 5), new Vector2(28, 28));
                PlayerItemUIStyle.Label(label, label.font, 19);
                label.color = PlayerHUDArt.Text;
                label.alignment = TextAlignmentOptions.Center;
            }
        }
    }

    // Muted: red slashed mic. Transmitting: green. Otherwise a dim mic so the bell reads as a radio.
    private void RefreshBellBadges()
    {
        ItemData bell = _voice != null && _inventory != null ? _voice.BellData : null;
        bool muted = VoiceSettings.MicMuted;
        Color color = muted ? VoiceStatusHUD.MutedColor
            : _voice != null && _voice.IsTalkingOnRadio ? VoiceStatusHUD.SpeakingColor : VoiceStatusHUD.LiveColor;
        for (int i = 0; i < _slotBackgrounds.Length; i++)
        {
            bool show = bell != null && _slotBackgrounds[i] != null && _inventory.GetQuickSlotItem(i) == bell;
            var badge = _bellBadges[i];
            if (!show)
            {
                if (badge != null && badge.gameObject.activeSelf) badge.gameObject.SetActive(false);
                continue;
            }
            if (badge == null)
            {
                badge = _bellBadges[i] = new GameObject("BellMicBadge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                badge.rectTransform.SetParent(_slotBackgrounds[i].rectTransform, false);
                PlayerItemUIStyle.Place(badge.rectTransform, Vector2.one, Vector2.one, new Vector2(-6, -6), new Vector2(24, 24));
                badge.preserveAspect = true;
                badge.raycastTarget = false;
            }
            if (!badge.gameObject.activeSelf) badge.gameObject.SetActive(true);
            var sprite = MicIconSprite.Get(muted);
            if (badge.sprite != sprite) badge.sprite = sprite;
            if (badge.color != color) badge.color = color;
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

            if (_selectionFrames[i] == null) _selectionFrames[i] = PlayerHUDFrame.Attach(background.rectTransform);
            _selectionFrames[i].SetSelected(i == _selectedIndex);
            background.color = Color.clear;
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
