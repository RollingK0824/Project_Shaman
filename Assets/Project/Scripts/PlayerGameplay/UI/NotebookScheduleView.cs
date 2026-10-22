using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectShaman.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Routine;
using ProjectShaman.Map;

// Villager schedule spread: villagers on the left page, the chosen villager's day as a timetable on the right.
// One row per half sijin (초/정) of the day; each routine block is one cell spanning its rows. Night is the same
// for everyone and is left out. Only the planned schedule is shown (the baseline for odd behaviour).
public sealed class NotebookScheduleView : MonoBehaviour
{
    // Day hours 05~20 start at 묘시 초; every sijin is two hours (초, 정)
    private static readonly string[] SIJIN = { "묘시", "진시", "사시", "오시", "미시", "신시", "유시", "술시" };
    private const float LIST_ROW_HEIGHT = 44f;
    private const float LIST_WIDTH = 440f;

    private const float TABLE_X = 540f, TABLE_Y = 70f, TABLE_WIDTH = 440f;
    private const float TIME_WIDTH = 104f, HEADER_HEIGHT = 30f, HOUR_HEIGHT = 33f, GAP = 2f;
    private static readonly Color HEADER = new(.33f, .40f, .30f, 1f);
    private static readonly Color TIME_CELL = new(.33f, .40f, .30f, .22f);
    private static readonly Color EMPTY_CELL = new(.37f, .24f, .12f, .06f);
    private static readonly Color WORK_CELL = new(.42f, .55f, .40f, .55f);
    private static readonly Color REST_CELL = new(.62f, .52f, .34f, .45f);

    private readonly List<Button> _rows = new();
    private readonly List<TMP_Text> _rowLabels = new();
    private readonly List<(Image image, TMP_Text label)> _blocks = new();
    // Last block hours seen per villager, kept when their object is gone so a hidden death changes nothing here
    private readonly Dictionary<string, (int day, int[] hours)> _hours = new();
    private TMP_FontAsset _font;
    private RectTransform _listContent, _table;
    private TMP_Text _dayLabel, _title, _note;
    private string _selectedId;
    private float _nextRefresh;

    private static int DayHours => DayClock.DAY_HOURS;

    public void Bind(TMP_FontAsset font)
    {
        _font = font;
        PlayerItemUIStyle.NotebookLabel(transform, font, "ScheduleHeading", "주민 일과표", 20, 16, 260, 42, 30);
        _dayLabel = PlayerItemUIStyle.NotebookLabel(transform, font, "Day", "", 280, 24, 180, 32, 20);
        _dayLabel.alignment = TextAlignmentOptions.Right; _dayLabel.color = PlayerItemUIStyle.NotebookMuted;
        _listContent = PlayerItemUIStyle.NotebookScroll(transform, "VillagerList", 20, 70, LIST_WIDTH, 550, out _);

        _title = PlayerItemUIStyle.NotebookLabel(transform, font, "VillagerDay", "주민을 고르세요", TABLE_X, 16, TABLE_WIDTH, 42, 28);
        BuildTable();
        _note = PlayerItemUIStyle.NotebookLabel(transform, font, "Note", "", TABLE_X, TABLE_Y + HEADER_HEIGHT + DayHours * HOUR_HEIGHT + 8, TABLE_WIDTH, 28, 16);
        _note.color = PlayerItemUIStyle.NotebookMuted;
        Refresh();
    }

    // Header, the time column and an empty cell per hour; routine blocks are laid over the empty cells
    private void BuildTable()
    {
        _table = PlayerItemUIStyle.NotebookRect(transform, "Timetable", TABLE_X, TABLE_Y, TABLE_WIDTH, HEADER_HEIGHT + DayHours * HOUR_HEIGHT);
        Cell(_table, "TimeHeader", 0, 0, TIME_WIDTH, HEADER_HEIGHT, HEADER, "시각", 17, PlayerItemUIStyle.Text);
        Cell(_table, "RoutineHeader", TIME_WIDTH + GAP, 0, TABLE_WIDTH - TIME_WIDTH - GAP, HEADER_HEIGHT, HEADER, "일과", 17, PlayerItemUIStyle.Text);

        for (int i = 0; i < DayHours; i++)
        {
            float y = HEADER_HEIGHT + i * HOUR_HEIGHT;
            Cell(_table, "Time" + i, 0, y + GAP, TIME_WIDTH, HOUR_HEIGHT - GAP, TIME_CELL, HourName(DayClock.DAY_START_HOUR + i), 16, PlayerItemUIStyle.NotebookInk);
            Cell(_table, "Empty" + i, TIME_WIDTH + GAP, y + GAP, TABLE_WIDTH - TIME_WIDTH - GAP, HOUR_HEIGHT - GAP, EMPTY_CELL, "", 16, PlayerItemUIStyle.NotebookInk);
        }
    }

    private TMP_Text Cell(Transform parent, string name, float x, float y, float width, float height, Color color, string text, float size, Color textColor)
    {
        var rect = PlayerItemUIStyle.NotebookRect(parent, name, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        var label = PlayerItemUIStyle.NotebookLabel(rect, _font, "Label", text, 4, 2, width - 8, height - 4, size);
        label.alignment = TextAlignmentOptions.Center; label.color = textColor;
        label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = size;
        return label;
    }

    private void LateUpdate()
    {
        PlayerItemUIStyle.FitNotebookSpread((RectTransform)transform);
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .5f; Refresh();
    }

    private void Refresh()
    {
        if (_title == null) return;
        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        IReadOnlyList<IVillagerView> villagers = registry != null ? registry.Villagers : null;
        int count = villagers != null ? villagers.Count : 0;

        CacheBlockHours();
        _dayLabel.text = TimeManager.Existing != null && TimeManager.Existing.DayCount > 0 ? $"{TimeManager.Existing.DayCount}일째" : "";

        for (int i = 0; i < count; i++)
        {
            IVillagerView villager = villagers[i];
            if (i >= _rows.Count) AddRow();
            _rows[i].gameObject.SetActive(true);
            _rowLabels[i].text = villager.IsDeathKnown ? $"{villager.DisplayName} · 고인" : villager.DisplayName;
            _rowLabels[i].color = villager.VillagerId == _selectedId ? PlayerItemUIStyle.NotebookAccent :
                villager.IsDeathKnown ? PlayerItemUIStyle.NotebookMuted : PlayerItemUIStyle.NotebookInk;
        }
        for (int i = count; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
        _listContent.sizeDelta = new Vector2(LIST_WIDTH, Mathf.Max(550, count * LIST_ROW_HEIGHT));

        IVillagerView selected = null;
        if (registry != null && !string.IsNullOrEmpty(_selectedId)) registry.TryGet(_selectedId, out selected);
        ShowDay(selected);
    }

    private void ShowDay(IVillagerView villager)
    {
        int shown = 0;

        if (villager == null)
        {
            _title.text = _rows.Count > 0 ? "주민을 고르세요" : "주민 정보가 없습니다";
            _note.text = "";
        }
        else if (villager.IsDeathKnown)
        {
            _title.text = $"{villager.DisplayName}의 하루";
            _note.text = "세상을 떠난 주민입니다.";
        }
        else if (villager.Schedule == null || !_hours.TryGetValue(villager.VillagerId, out var today) || today.day <= 0)
        {
            _title.text = $"{villager.DisplayName}의 하루";
            _note.text = "오늘 일과가 아직 정해지지 않았습니다.";
        }
        else
        {
            _title.text = $"{villager.DisplayName}의 하루";
            _note.text = "빈 칸은 오가거나 쉬는 때, 술시 정부터는 집에 머뭅니다.";
            for (int slot = 0; slot * 2 + 1 < today.hours.Length; slot++)
            {
                RoutineSlotInfo routine = villager.Schedule.Get(today.day, slot);
                if (!routine.IsEmpty) ShowBlock(shown++, routine, today.hours[slot * 2], today.hours[slot * 2 + 1]);
            }
        }

        for (int i = shown; i < _blocks.Count; i++) _blocks[i].image.gameObject.SetActive(false);
    }

    // One routine as a single cell over the hours it covers
    private void ShowBlock(int index, RoutineSlotInfo routine, int startHour, int endHour)
    {
        int from = Mathf.Clamp(startHour - DayClock.DAY_START_HOUR, 0, DayHours);
        int to = Mathf.Clamp(endHour - DayClock.DAY_START_HOUR, from, DayHours);
        if (to <= from) return;

        while (_blocks.Count <= index)
        {
            var text = Cell(_table, "Block" + _blocks.Count, 0, 0, 10, 10, WORK_CELL, "", 18, PlayerItemUIStyle.NotebookInk);
            _blocks.Add((text.transform.parent.GetComponent<Image>(), text));
        }

        var (image, label) = _blocks[index];
        var rect = (RectTransform)image.transform;
        float width = TABLE_WIDTH - TIME_WIDTH - GAP;
        float height = (to - from) * HOUR_HEIGHT - GAP;
        PlayerItemUIStyle.Place(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(TIME_WIDTH + GAP, -(HEADER_HEIGHT + from * HOUR_HEIGHT + GAP)), new Vector2(width, height));
        ((RectTransform)label.transform).sizeDelta = new Vector2(width - 8, height - 4);

        bool rest = routine.Category == RoutineCategory.Rest;
        string place = MapAreaRegistry.NameOf(routine.PlaceId);
        image.color = rest ? REST_CELL : WORK_CELL;
        label.text = string.IsNullOrEmpty(place) ? routine.DisplayName : $"{routine.DisplayName}\n<size=75%>{place}</size>";
        label.richText = true;
        image.gameObject.SetActive(true);
        image.transform.SetAsLastSibling();
    }

    private void CacheBlockHours()
    {
        foreach (AI_Core core in FindObjectsByType<AI_Core>(FindObjectsSortMode.None))
        {
            if (core.PublicInfo == null || string.IsNullOrEmpty(core.PublicInfo.VillagerId)) continue;
            if (core.TodayBlocksDay > 0) _hours[core.PublicInfo.VillagerId] = (core.TodayBlocksDay, new List<int>(core.TodayBlockHours).ToArray());
        }
    }

    private void AddRow()
    {
        int index = _rows.Count;
        var rect = PlayerItemUIStyle.NotebookRect(_listContent, "Villager" + index, 0, index * LIST_ROW_HEIGHT, LIST_WIDTH, LIST_ROW_HEIGHT);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0, 0, 0, 0);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.onClick.AddListener(() => Select(index));
        var label = PlayerItemUIStyle.NotebookLabel(rect, _font, "Label", "", 12, 4, LIST_WIDTH - 24, LIST_ROW_HEIGHT - 8, 21);
        _rows.Add(button); _rowLabels.Add(label);
    }

    private void Select(int index)
    {
        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        if (registry == null || index >= registry.Villagers.Count) return;
        _selectedId = registry.Villagers[index].VillagerId;
        Refresh();
    }

    // 05 → 묘시 초, 06 → 묘시 정, ... 19 → 술시 초
    private static string HourName(int hour)
    {
        int offset = hour - DayClock.DAY_START_HOUR;
        return $"{SIJIN[offset / 2]} {(offset % 2 == 0 ? "초" : "정")}";
    }
}
