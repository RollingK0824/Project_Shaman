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
// Drawn in ink on the notebook paper: one row per half sijin (초/정) of the day, each routine a brush wash
// spanning its rows. Night is the same for everyone and is left out. Only the planned schedule is shown.
public sealed class NotebookScheduleView : MonoBehaviour
{
    private const string SPRITES = "NotebookSchedule/";
    // Day hours 05~20 start at 묘시 초; every sijin is two hours (초, 정)
    private static readonly string[] SIJIN = { "묘시", "진시", "사시", "오시", "미시", "신시", "유시", "술시" };

    private const float LIST_X = 30f, LIST_Y = 104f, LIST_WIDTH = 420f, LIST_HEIGHT = 500f, LIST_ROW_HEIGHT = 42f;
    private const float TABLE_X = 548f, TABLE_Y = 104f, TABLE_WIDTH = 424f;
    private const float TIME_WIDTH = 92f, HOUR_HEIGHT = 31f, BLOCK_INSET = 3f;

    private static readonly Color INK = PlayerItemUIStyle.NotebookInk;
    private static readonly Color RULE = new(.20f, .14f, .10f, .85f);
    private static readonly Color RULE_FAINT = new(.20f, .14f, .10f, .30f);
    private static readonly Color WORK_WASH = new(.38f, .32f, .25f, .42f);
    private static readonly Color REST_WASH = new(.66f, .26f, .17f, .34f);
    private static readonly Color SELECT_WASH = new(.59f, .20f, .14f, .40f);

    private readonly List<Button> _rows = new();
    private readonly List<TMP_Text> _rowLabels = new();
    private readonly List<Image> _rowMarks = new();
    private readonly List<(Image wash, TMP_Text label)> _blocks = new();
    // Last block hours seen per villager, kept when their object is gone so a hidden death changes nothing here
    private readonly Dictionary<string, (int day, int[] hours)> _hours = new();
    private TMP_FontAsset _font;
    private Sprite _wash, _rule, _ruleThin, _select, _underline;
    private RectTransform _listContent, _table;
    private TMP_Text _dayLabel, _title, _note;
    private string _selectedId;
    private float _nextRefresh;

    private static int DayHours => DayClock.DAY_HOURS;

    public void Bind(TMP_FontAsset font)
    {
        _font = font;
        _wash = Resources.Load<Sprite>(SPRITES + "ScheduleWash");
        _rule = Resources.Load<Sprite>(SPRITES + "ScheduleRule");
        _ruleThin = Resources.Load<Sprite>(SPRITES + "ScheduleRuleThin");
        _select = Resources.Load<Sprite>(SPRITES + "ScheduleSelect");
        _underline = Resources.Load<Sprite>(SPRITES + "ScheduleTitleUnderline");

        // Left page: heading and the villager list
        Heading("ScheduleHeading", "주민 일과표", 20);
        _dayLabel = Text(transform, "Day", "", 300, 66, 150, 26, 18, TextAlignmentOptions.Right);
        _dayLabel.color = PlayerItemUIStyle.NotebookMuted;
        _listContent = PlayerItemUIStyle.NotebookScroll(transform, "VillagerList", LIST_X, LIST_Y, LIST_WIDTH, LIST_HEIGHT, out _);
        _listContent.parent.parent.GetComponent<Image>().color = Color.clear;

        // Right page: the chosen villager's day
        _title = Heading("VillagerDay", "주민을 고르세요", 540);
        BuildTable();
        _note = Text(transform, "Note", "", TABLE_X, TABLE_Y + DayHours * HOUR_HEIGHT + 14, TABLE_WIDTH, 24, 15, TextAlignmentOptions.Center);
        _note.color = PlayerItemUIStyle.NotebookMuted;
        Refresh();
    }

    private TMP_Text Heading(string name, string value, float x)
    {
        var label = Text(transform, name, value, x, 12, 440, 46, 32, TextAlignmentOptions.Center);
        label.enableAutoSizing = true; label.fontSizeMin = 22; label.fontSizeMax = 32;
        if (_underline != null)
        {
            var line = Picture(transform, name + "Underline", _underline, Color.white, x + 90, 50, 260, 60, Image.Type.Simple);
            line.preserveAspect = true;
        }
        return label;
    }

    // Time column with the sijin names, ink rules at the sijin edges, and an empty field for the brush washes
    private void BuildTable()
    {
        float height = DayHours * HOUR_HEIGHT;
        _table = PlayerItemUIStyle.NotebookRect(transform, "Timetable", TABLE_X, TABLE_Y, TABLE_WIDTH, height);

        for (int offset = 0; offset < DayHours; offset += 2)
        {
            int rows = Mathf.Min(2, DayHours - offset);
            float y = offset * HOUR_HEIGHT;
            Text(_table, "Sijin" + offset / 2, SIJIN[offset / 2], 0, y, TIME_WIDTH - 26, rows * HOUR_HEIGHT, 21, TextAlignmentOptions.Center);
            for (int half = 0; half < rows; half++)
            {
                var mark = Text(_table, $"Half{offset + half}", half == 0 ? "초" : "정", TIME_WIDTH - 26, y + half * HOUR_HEIGHT, 22, HOUR_HEIGHT, 13, TextAlignmentOptions.Center);
                mark.color = PlayerItemUIStyle.NotebookMuted;
            }
            if (offset > 0) Rule(_table, "SijinRule" + offset / 2, _ruleThin, RULE_FAINT, 4, y, TABLE_WIDTH - 8, 6);
        }

        Rule(_table, "TopRule", _rule, RULE, -6, -6, TABLE_WIDTH + 12, 9);
        Rule(_table, "BottomRule", _rule, RULE, -6, height - 3, TABLE_WIDTH + 12, 9);
        // The thin rule turned upright between the time column and the routines
        var divider = Rule(_table, "TimeDivider", _ruleThin, RULE_FAINT, 0, 0, height, 6).rectTransform;
        divider.pivot = new Vector2(.5f, .5f);
        divider.anchoredPosition = new Vector2(TIME_WIDTH, -height / 2);
        divider.localEulerAngles = new Vector3(0, 0, 90);
    }

    private Image Rule(Transform parent, string name, Sprite sprite, Color color, float x, float y, float width, float height)
    {
        var image = Picture(parent, name, sprite, color, x, y - height / 2, width, height, Image.Type.Sliced);
        image.pixelsPerUnitMultiplier = 4f;
        return image;
    }

    private Image Picture(Transform parent, string name, Sprite sprite, Color color, float x, float y, float width, float height, Image.Type type)
    {
        var rect = PlayerItemUIStyle.NotebookRect(parent, name, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.type = sprite != null ? type : Image.Type.Simple;
        image.color = sprite != null ? color : Color.clear; image.raycastTarget = false;
        return image;
    }

    private TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height, float size, TextAlignmentOptions alignment)
    {
        var label = PlayerItemUIStyle.NotebookLabel(parent, _font, name, value, x, y, width, height, size);
        label.alignment = alignment;
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
            bool selected = villager.VillagerId == _selectedId;
            _rows[i].gameObject.SetActive(true);
            _rowMarks[i].enabled = selected && _select != null;
            _rowLabels[i].text = villager.IsDeathKnown ? $"{villager.DisplayName}  <size=70%>고인</size>" : villager.DisplayName;
            _rowLabels[i].color = villager.IsDeathKnown ? PlayerItemUIStyle.NotebookMuted : INK;
        }
        for (int i = count; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
        _listContent.sizeDelta = new Vector2(LIST_WIDTH, Mathf.Max(LIST_HEIGHT, count * LIST_ROW_HEIGHT));

        IVillagerView chosen = null;
        if (registry != null && !string.IsNullOrEmpty(_selectedId)) registry.TryGet(_selectedId, out chosen);
        ShowDay(chosen);
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
            _note.text = "세상을 떠난 주민입니다";
        }
        else if (villager.Schedule == null || !_hours.TryGetValue(villager.VillagerId, out var today) || today.day <= 0)
        {
            _title.text = $"{villager.DisplayName}의 하루";
            _note.text = "오늘 일과가 아직 정해지지 않았습니다";
        }
        else
        {
            _title.text = $"{villager.DisplayName}의 하루";
            _note.text = "빈 칸은 오가거나 쉬는 때 · 술시 정부터 집에 머묾";
            for (int slot = 0; slot * 2 + 1 < today.hours.Length; slot++)
            {
                RoutineSlotInfo routine = villager.Schedule.Get(today.day, slot);
                if (!routine.IsEmpty && ShowBlock(shown, routine, today.hours[slot * 2], today.hours[slot * 2 + 1])) shown++;
            }
        }

        for (int i = shown; i < _blocks.Count; i++) _blocks[i].wash.gameObject.SetActive(false);
    }

    // One routine as a brush wash over the hours it covers
    private bool ShowBlock(int index, RoutineSlotInfo routine, int startHour, int endHour)
    {
        int from = Mathf.Clamp(startHour - DayClock.DAY_START_HOUR, 0, DayHours);
        int to = Mathf.Clamp(endHour - DayClock.DAY_START_HOUR, from, DayHours);
        if (to <= from) return false;

        while (_blocks.Count <= index)
        {
            var wash = Picture(_table, "Block" + _blocks.Count, _wash, WORK_WASH, 0, 0, 10, 10, Image.Type.Sliced);
            wash.pixelsPerUnitMultiplier = 2.5f;
            var label = Text(wash.transform, "Label", "", 10, 4, 10, 10, 21, TextAlignmentOptions.Center);
            label.richText = true; label.enableAutoSizing = true; label.fontSizeMin = 13; label.fontSizeMax = 21;
            _blocks.Add((wash, label));
        }

        var (image, text) = _blocks[index];
        float width = TABLE_WIDTH - TIME_WIDTH - BLOCK_INSET * 2;
        float height = (to - from) * HOUR_HEIGHT - BLOCK_INSET * 2;
        PlayerItemUIStyle.Place(image.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(TIME_WIDTH + BLOCK_INSET, -(from * HOUR_HEIGHT + BLOCK_INSET)), new Vector2(width, height));
        text.rectTransform.sizeDelta = new Vector2(width - 20, height - 8);

        string place = MapAreaRegistry.NameOf(routine.PlaceId);
        image.color = _wash == null ? Color.clear : routine.Category == RoutineCategory.Rest ? REST_WASH : WORK_WASH;
        text.text = string.IsNullOrEmpty(place) ? routine.DisplayName : $"{routine.DisplayName}\n<size=72%>{place}</size>";
        image.gameObject.SetActive(true);
        return true;
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
        var hit = rect.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = hit; button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Select(index));
        var mark = Picture(rect, "Selected", _select, SELECT_WASH, 4, 3, 240, LIST_ROW_HEIGHT - 6, Image.Type.Sliced);
        mark.pixelsPerUnitMultiplier = 2f;
        var label = Text(rect, "Label", "", 24, 4, LIST_WIDTH - 40, LIST_ROW_HEIGHT - 8, 22, TextAlignmentOptions.Left);
        label.richText = true;
        _rows.Add(button); _rowLabels.Add(label); _rowMarks.Add(mark);
    }

    private void Select(int index)
    {
        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        if (registry == null || index >= registry.Villagers.Count) return;
        _selectedId = registry.Villagers[index].VillagerId;
        Refresh();
    }
}
