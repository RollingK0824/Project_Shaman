using System.Collections.Generic;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pinned villagers' days on the HUD, top-left: hour labels, then one column per pin (PinManager's order).
// The table creeps down with the time so the current hour stays near the top
public class SchedulePinsHUD : MonoBehaviour
{
    private static readonly string[] SIJIN = { "묘시", "진시", "사시", "오시", "미시", "신시", "유시", "술시", "해시", "자시", "축시", "인시" };

    // Child holding the table, hidden when nothing is pinned. Not this object, or the events would unsubscribe
    [SerializeField]
    private GameObject _panel;

    // Header name cells and the block columns inside _content, one per pin slot
    [SerializeField]
    private TMP_Text[] _names;

    [SerializeField]
    private RectTransform[] _columns;

    // Masked viewport; _content inside it is anchored + pivoted top and sized here to all rows
    [SerializeField]
    private RectTransform _viewport, _content;

    // Inactive templates: a label under _hourColumn, a block Image (TMP_Text child) anywhere
    [SerializeField]
    private RectTransform _hourColumn;

    [SerializeField]
    private TMP_Text _hourLabelTemplate;

    [SerializeField]
    private Image _blockTemplate;

    // Thin full-width line inside _content, last child so it draws over the blocks
    [SerializeField]
    private RectTransform _nowLine;

    [SerializeField]
    private float _rowHeight = 36f;

    [SerializeField]
    private float _leadRows = 1f;

    [SerializeField]
    private float _blockInset = 2f;

    [SerializeField]
    private Color _nameColor = Color.black, _deadNameColor = new Color(.39f, .30f, .22f, 1f);

    // Same as the notebook: work red, rest green, home and sleep ink
    [SerializeField]
    private Color _workColor = new(.66f, .24f, .16f, .38f);

    [SerializeField]
    private Color _restColor = new(.32f, .50f, .28f, .36f);

    [SerializeField]
    private Color _homeColor = new(.24f, .21f, .19f, .30f);

    [SerializeField]
    private Color _sleepColor = new(.14f, .12f, .11f, .40f);

    private readonly List<DayBlock> _dayBlocks = new List<DayBlock>();
    // Each pooled block keeps what it last showed, so the 0.5s refresh only rebuilds text that changed
    private List<(Image wash, TMP_Text label, DayBlock shown)>[] _blocks;
    private string[] _shownNames;
    private bool[] _shownDead;
    private float _nextRefresh;

    private void Awake()
    {
        _content.sizeDelta = new Vector2(_content.sizeDelta.x, VillagerDayBlocks.TABLE_ROWS * _rowHeight);

        // Row labels as the notebook prints them: each sijin split into 초 and 정
        for (int row = 0; row < VillagerDayBlocks.TABLE_ROWS; row++)
        {
            TMP_Text label = Instantiate(_hourLabelTemplate, _hourColumn);
            label.name = "Hour" + row;
            label.SetText($"{SIJIN[row / 2]} {(row % 2 == 0 ? "초" : "정")}");
            PlaceRow(label.rectTransform, row, row + 1, 0f);
            label.gameObject.SetActive(true);
        }

        _blocks = new List<(Image, TMP_Text, DayBlock)>[_columns.Length];
        for (int i = 0; i < _blocks.Length; i++) _blocks[i] = new List<(Image, TMP_Text, DayBlock)>();
        _shownNames = new string[_names.Length];
        _shownDead = new bool[_names.Length];
    }

    private void OnEnable()
    {
        PinManager.Instance.PinsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PinManager pins = PinManager.Existing;
        if (pins != null) pins.PinsChanged -= Refresh;
    }

    private void LateUpdate()
    {
        // Schedules can change mid-day and the registry raises nothing on clients, so re-read on a timer
        if (Time.unscaledTime >= _nextRefresh) Refresh();
        if (!_panel.activeSelf) return;

        float row = HourNow() - DayClock.DAY_START_HOUR;
        float hidden = Mathf.Max(0f, _content.rect.height - _viewport.rect.height);
        float top = Mathf.Clamp((row - _leadRows) * _rowHeight, 0f, hidden);
        _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, top);
        _nowLine.anchoredPosition = new Vector2(_nowLine.anchoredPosition.x, -row * _rowHeight);
    }

    private void Refresh()
    {
        _nextRefresh = Time.unscaledTime + .5f;
        IReadOnlyList<string> pinned = PinManager.Instance.PinnedVillagers;
        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;

        for (int column = 0; column < _columns.Length; column++)
        {
            bool used = column < pinned.Count;
            _names[column].gameObject.SetActive(used);
            _columns[column].gameObject.SetActive(used);

            int shown = 0;
            if (used)
            {
                IVillagerView villager = null;
                registry?.TryGet(pinned[column], out villager);
                ShowName(column, villager);
                // Death known: crossed-out name, no blocks (TryBuild returns false)
                if (VillagerDayBlocks.TryBuild(villager, _dayBlocks))
                    foreach (DayBlock block in _dayBlocks) ShowBlock(column, shown++, block);
            }

            List<(Image wash, TMP_Text label, DayBlock shown)> pool = _blocks[column];
            for (int i = shown; i < pool.Count; i++)
            {
                pool[i].wash.gameObject.SetActive(false);
                pool[i] = (pool[i].wash, pool[i].label, default);
            }
        }

        _panel.SetActive(pinned.Count > 0);
    }

    // A missing villager counts as dead, so the default (null, false) never matches and the first call always draws
    private void ShowName(int column, IVillagerView villager)
    {
        string name = villager?.DisplayName;
        bool dead = villager == null || villager.IsDeathKnown;
        if (name == _shownNames[column] && dead == _shownDead[column]) return;
        _shownNames[column] = name;
        _shownDead[column] = dead;

        TMP_Text cell = _names[column];
        if (villager == null)
        {
            cell.SetText("???");
            cell.color = _deadNameColor;
            return;
        }

        cell.SetText(villager.IsDeathKnown ? $"<color=#B03030>X</color> <s>{villager.DisplayName}</s>" : villager.DisplayName);
        cell.color = villager.IsDeathKnown ? _deadNameColor : _nameColor;
    }

    // Re-places a pooled block only when it differs from what it last showed; hidden blocks are reset to default
    private void ShowBlock(int column, int index, DayBlock block)
    {
        List<(Image wash, TMP_Text label, DayBlock shown)> pool = _blocks[column];
        while (pool.Count <= index)
        {
            Image wash = Instantiate(_blockTemplate, _columns[column]);
            wash.name = "Block" + pool.Count;
            pool.Add((wash, wash.GetComponentInChildren<TMP_Text>(true), default));
        }

        var (image, label, shown) = pool[index];
        if (Same(shown, block)) return;
        pool[index] = (image, label, block);

        int from = block.StartHour - DayClock.DAY_START_HOUR;
        int to = block.EndHour - DayClock.DAY_START_HOUR;
        PlaceRow(image.rectTransform, from, to, _blockInset);
        image.color = ColorOf(block.Kind);
        label.SetText(string.IsNullOrEmpty(block.Place) ? block.Title : $"{block.Title}\n<size=72%>{block.Place}</size>");
        image.gameObject.SetActive(true);
    }

    // default never matches a real block (Add drops blocks with EndHour <= StartHour)
    private static bool Same(DayBlock a, DayBlock b) =>
        a.StartHour == b.StartHour && a.EndHour == b.EndHour && a.Kind == b.Kind && a.Title == b.Title && a.Place == b.Place;

    // Stretches across its parent's width, covering rows [from, to) from the top
    private void PlaceRow(RectTransform rect, float from, float to, float inset)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -(from * _rowHeight + inset));
        rect.sizeDelta = new Vector2(-inset * 2f, (to - from) * _rowHeight - inset * 2f);
    }

    private Color ColorOf(DayBlockKind kind) => kind switch
    {
        DayBlockKind.Work => _workColor,
        DayBlockKind.Rest => _restColor,
        DayBlockKind.Home => _homeColor,
        _ => _sleepColor
    };

    // Hour of the current cycle as a float (Elapsed counts from the first day, so take the part into this cycle)
    private static float HourNow()
    {
        TimeManager time = TimeManager.Existing;
        if (time == null || time.Elapsed < 0) return DayClock.DAY_START_HOUR;
        float intoCycle = (float)(time.Elapsed % TimeManager.CYCLE_DURATION);
        return intoCycle < TimeManager.DAY_DURATION
            ? DayClock.ToHour(intoCycle)
            : DayClock.NightToHour(intoCycle - TimeManager.DAY_DURATION);
    }
}
