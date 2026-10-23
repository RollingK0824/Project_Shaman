using System.Collections.Generic;
using System.Text;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation only, hosted by the existing local StressHUD Canvas.
[DisallowMultipleComponent]
public sealed class PlayerStatusHUD : MonoBehaviour
{
    [SerializeField, Min(.2f)] private float _feedDuration = 4f;
    [SerializeField, Min(.1f)] private float _fadeDuration = .6f;
    [SerializeField, Min(0f)] private float _mergeWindow = 1.2f;
    [SerializeField, Range(1, 6)] private int _maxFeedRows = 4;
    private sealed class Entry
    {
        public ItemData Item;
        public long Count;
        public float Updated;
        public RectTransform Root;
        public CanvasGroup Group;
        public TMP_Text Label;
    }
    private readonly List<Entry> _entries = new();
    private readonly Dictionary<ItemData, int> _materialCounts = new();
    private readonly List<TalismanEffectDisplay> _effects = new();
    private readonly StringBuilder _text = new();
    private GameObject _owner;
    private NetworkIdentity _identity;
    private PlayerInventory _inventory;
    private PlayerHealth _health;
    private PlayerTalismanEffectController _effectController;
    private SharedMaterialInventory _shared;
    private TMP_FontAsset _font;
    private RectTransform _feedRoot, _timerRoot;
    private TMP_Text _timerText;
    private bool _subscribed;
    private long _barrierVersion = -1;
    private float _replicatedBarrierExpires;
    private bool LocalOwner => _owner != null &&
        ((!NetworkClient.active && !NetworkServer.active) || (_identity != null && _identity.isLocalPlayer));
    private bool CanShow => isActiveAndEnabled && LocalOwner && _owner.activeInHierarchy && (_health == null || !_health.IsDead);

    public void Bind(GameObject owner, TMP_FontAsset font)
    {
        Unsubscribe(); ClearFeed();
        _owner = owner; _font = font;
        _identity = owner != null ? owner.GetComponent<NetworkIdentity>() : null;
        _inventory = owner != null ? owner.GetComponent<PlayerInventory>() : null;
        _health = owner != null ? owner.GetComponent<PlayerHealth>() : null;
        _effectController = owner != null ? owner.GetComponent<PlayerTalismanEffectController>() : null;
        _barrierVersion = -1; _replicatedBarrierExpires = 0;
        Build();
        if (isActiveAndEnabled) Subscribe();
        RefreshTimers();
    }
    private void Build()
    {
        if (_feedRoot == null) _feedRoot = Rect(transform, "Acquisition Feed", new Vector2(300, 176));
        if (_timerRoot == null)
        {
            _timerRoot = Rect(transform, "Talisman Timers", new Vector2(300, 60));
            var background = _timerRoot.gameObject.AddComponent<Image>();
            background.color = new Color(.06f, .07f, .08f, .76f); background.raycastTarget = false;
            _timerText = Label(_timerRoot, "Remaining Time", 18);
            PlayerItemUIStyle.Stretch(_timerText.rectTransform, new Vector2(12, 6), new Vector2(-12, -6));
        }
        _timerText.font = _font;
        Layout();
    }
    private static RectTransform Rect(Transform parent, string name, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size;
        return rect;
    }
    private TMP_Text Label(Transform parent, string name, float size)
    {
        var label = Rect(parent, name, Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>();
        PlayerItemUIStyle.Label(label, _font, size);
        label.richText = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }
    private void OnEnable() { if (_owner != null) Subscribe(); }
    private void OnDisable()
    {
        Unsubscribe(); ClearFeed();
        if (_timerRoot != null) _timerRoot.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        Unsubscribe();
        if (_feedRoot != null) Destroy(_feedRoot.gameObject);
        if (_timerRoot != null) Destroy(_timerRoot.gameObject);
    }
    private void Subscribe()
    {
        if (_subscribed || _inventory == null || !LocalOwner) return;
        _subscribed = true;
        _inventory.ItemUnitsChanged += HandlePersonalUnits;
        _inventory.InventoryChanged += CheckSharedBinding;
        CheckSharedBinding();
    }
    private void Unsubscribe()
    {
        if (_subscribed && _inventory != null)
        {
            _inventory.ItemUnitsChanged -= HandlePersonalUnits;
            _inventory.InventoryChanged -= CheckSharedBinding;
        }
        if (_shared != null) _shared.MaterialCountChanged -= HandleMaterialCount;
        _shared = null; _materialCounts.Clear(); _subscribed = false;
    }
    private void CheckSharedBinding()
    {
        var shared = _inventory != null ? _inventory.SharedMaterials : null;
        if (_shared == shared) return;
        if (_shared != null) _shared.MaterialCountChanged -= HandleMaterialCount;
        _shared = shared; _materialCounts.Clear();
        if (_shared == null) return;
        // Binding an existing store is not a new acquisition (including late join).
        foreach (var pair in _shared.Counts) _materialCounts[pair.Key] = pair.Value;
        _shared.MaterialCountChanged += HandleMaterialCount;
    }
    private void HandlePersonalUnits(ItemData item, int delta)
    {
        if (item != null && item.Category != ItemCategory.Material && item.ItemId != "item_notebook" && delta > 0)
            AddFeed(item, delta);
    }
    private void HandleMaterialCount(ItemData item, int count)
    {
        _materialCounts.TryGetValue(item, out int previous);
        _materialCounts[item] = count;
        if (count > previous) AddFeed(item, count - previous);
    }
    private void AddFeed(ItemData item, int amount)
    {
        if (!CanShow || _feedRoot == null) return;
        float now = Time.unscaledTime;
        var entry = _entries.Find(e => e.Item == item && now - e.Updated <= _mergeWindow);
        if (entry == null)
        {
            while (_entries.Count >= Mathf.Max(1, _maxFeedRows)) RemoveEntry(0);
            entry = new Entry { Item = item, Root = Rect(_feedRoot, "Acquired " + item.ItemId, new Vector2(300, 40)) };
            entry.Group = entry.Root.gameObject.AddComponent<CanvasGroup>();
            entry.Group.blocksRaycasts = false; entry.Group.interactable = false;
            var background = entry.Root.gameObject.AddComponent<Image>();
            background.color = new Color(.06f, .07f, .08f, .82f); background.raycastTarget = false;
            var icon = Rect(entry.Root, "Item Icon", new Vector2(30, 30)).gameObject.AddComponent<Image>();
            PlayerItemUIStyle.Place(icon.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(6, 0), new Vector2(30, 30));
            icon.sprite = item.Icon; icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = item.Icon != null;
            entry.Label = Label(entry.Root, "Item Count", 18);
            PlayerItemUIStyle.Stretch(entry.Label.rectTransform, new Vector2(42, 1), new Vector2(-48, -1));
            var badge = Label(entry.Root, "Inventory Type", 13);
            PlayerItemUIStyle.Place(badge.rectTransform, new Vector2(1,.5f), new Vector2(1,.5f), new Vector2(-6,0), new Vector2(38,30));
            badge.text = item.Category == ItemCategory.Material ? "공용" : "개인";
            badge.color = PlayerItemUIStyle.Muted;
            _entries.Add(entry);
        }
        entry.Count += amount; entry.Updated = now; entry.Group.alpha = 1;
        entry.Label.text = $"{item.DisplayName} +{entry.Count}";
    }
    private void RemoveEntry(int index)
    {
        if (_entries[index].Root != null) { _entries[index].Root.gameObject.SetActive(false); Destroy(_entries[index].Root.gameObject); }
        _entries.RemoveAt(index);
    }
    private void ClearFeed() { while (_entries.Count > 0) RemoveEntry(0); }
    private void Layout()
    {
        if (_feedRoot == null || _timerRoot == null) return;
        float unit = PlayerItemUIStyle.ViewportUnit(_feedRoot);
        PlayerItemUIStyle.Place(_feedRoot, new Vector2(0,.5f), new Vector2(0,.5f), new Vector2(24 * unit,0), new Vector2(300,176));
        PlayerItemUIStyle.Place(_timerRoot, Vector2.zero, Vector2.zero, new Vector2(24,116) * unit, _timerRoot.sizeDelta);
        _feedRoot.localScale = _timerRoot.localScale = Vector3.one * unit;
    }
    private void LateUpdate()
    {
        if (_owner == null) return;
        if (LocalOwner && !_subscribed) Subscribe();
        else if (!LocalOwner && _subscribed) Unsubscribe();
        if (!CanShow) { ClearFeed(); _replicatedBarrierExpires = 0; if (_timerRoot != null) _timerRoot.gameObject.SetActive(false); return; }
        CheckSharedBinding(); Layout();
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            float age = Time.unscaledTime - _entries[i].Updated;
            if (age >= _feedDuration) { RemoveEntry(i); continue; }
            _entries[i].Group.alpha = Mathf.Clamp01((_feedDuration - age) / Mathf.Max(.1f,_fadeDuration));
        }
        for (int i=0;i<_entries.Count;i++)
            PlayerItemUIStyle.Place(_entries[i].Root, new Vector2(0,1), new Vector2(0,1), new Vector2(0,-44*i), new Vector2(300,40));
        RefreshTimers();
    }
    private void RefreshTimers()
    {
        if (_timerRoot == null) return;
        _text.Clear(); int rows = 0;
        if (CanShow)
        {
            float barrier = BarrierWard.GetRemainingDuration(_owner);
            if (barrier <= 0f) barrier = Mathf.Max(0, _replicatedBarrierExpires - Time.time);
            if (barrier > 0f) { AppendTime("결계 부적",barrier); rows++; }
            if (_effectController == null) _effectController = _owner.GetComponent<PlayerTalismanEffectController>();
            if (_effectController != null)
            {
                _effectController.CopyActiveDisplay(_effects);
                foreach (var effect in _effects) { AppendTime(effect.Name,effect.RemainingDuration); rows++; }
            }
        }
        string value = _text.ToString();
        if (_timerText.text != value) _timerText.text = value;
        _timerRoot.sizeDelta = new Vector2(300,12+rows*24);
        _timerRoot.gameObject.SetActive(rows > 0);
    }
    private void AppendTime(string name, float remaining)
    {
        // Float deadline subtraction can produce 30.000002 for a 30-second timer.
        int seconds = Mathf.Max(1, Mathf.CeilToInt(remaining - .001f));
        if (_text.Length > 0) _text.Append('\n');
        _text.Append(name).Append("   ").Append(seconds/60).Append(':').Append((seconds%60).ToString("00"));
    }
    // Trusted owner-client adapter only. Presentation never activates a ward or grants protection.
    // Pass 0 on cancellation/death; versions must increase per bound player session.
    public bool ApplyBarrierPresentation(long version, float remainingSeconds)
    {
        if (!CanShow || version <= _barrierVersion || !float.IsFinite(remainingSeconds) || remainingSeconds < 0f) return false;
        _barrierVersion = version; _replicatedBarrierExpires = Time.time + remainingSeconds;
        RefreshTimers(); return true;
    }
}
