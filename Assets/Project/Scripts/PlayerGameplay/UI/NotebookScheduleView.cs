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

// Villager schedule spread: villagers on the left page, the chosen villager's day as 12 sijin on the right.
// Only the planned schedule is shown (the baseline for spotting odd behaviour), never what they actually do.
public sealed class NotebookScheduleView : MonoBehaviour
{
    // Day starts at 묘시 (05), so the table reads from there round to 인시
    private static readonly string[] SIJIN = { "묘시", "진시", "사시", "오시", "미시", "신시", "유시", "술시", "해시", "자시", "축시", "인시" };
    private const int FIRST_SIJIN_HOUR = 5;
    private const float ROW_HEIGHT = 44f;
    private const float LIST_WIDTH = 440f;

    private readonly List<Button> _rows = new();
    private readonly List<TMP_Text> _rowLabels = new();
    private readonly TMP_Text[] _sijinText = new TMP_Text[SIJIN.Length];
    // Last block hours seen per villager, kept when their object is gone so a hidden death changes nothing here
    private readonly Dictionary<string, (int day, int[] hours)> _hours = new();
    private TMP_FontAsset _font;
    private RectTransform _listContent;
    private TMP_Text _dayLabel, _title, _note;
    private string _selectedId;
    private float _nextRefresh;

    public void Bind(TMP_FontAsset font)
    {
        _font = font;
        PlayerItemUIStyle.NotebookLabel(transform, font, "ScheduleHeading", "주민 일과표", 20, 16, 260, 42, 30);
        _dayLabel = PlayerItemUIStyle.NotebookLabel(transform, font, "Day", "", 280, 24, 180, 32, 20);
        _dayLabel.alignment = TextAlignmentOptions.Right; _dayLabel.color = PlayerItemUIStyle.NotebookMuted;
        _listContent = PlayerItemUIStyle.NotebookScroll(transform, "VillagerList", 20, 70, LIST_WIDTH, 550, out _);

        _title = PlayerItemUIStyle.NotebookLabel(transform, font, "VillagerDay", "주민을 고르세요", 540, 16, 440, 42, 28);
        for (int i = 0; i < SIJIN.Length; i++)
        {
            float y = 66 + i * ROW_HEIGHT;
            var name = PlayerItemUIStyle.NotebookLabel(transform, font, "Sijin" + i, SijinName(i), 540, y, 150, ROW_HEIGHT, 19);
            name.color = PlayerItemUIStyle.NotebookMuted;
            _sijinText[i] = PlayerItemUIStyle.NotebookLabel(transform, font, "SijinRoutine" + i, "", 690, y, 290, ROW_HEIGHT, 20);
        }
        _note = PlayerItemUIStyle.NotebookLabel(transform, font, "Note", "", 540, 66 + SIJIN.Length * ROW_HEIGHT, 440, 30, 16);
        _note.color = PlayerItemUIStyle.NotebookMuted;
        Refresh();
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
        _listContent.sizeDelta = new Vector2(LIST_WIDTH, Mathf.Max(550, count * ROW_HEIGHT));

        IVillagerView selected = null;
        if (registry != null && !string.IsNullOrEmpty(_selectedId)) registry.TryGet(_selectedId, out selected);
        ShowDay(selected);
    }

    private void ShowDay(IVillagerView villager)
    {
        if (villager == null)
        {
            _title.text = _rows.Count > 0 ? "주민을 고르세요" : "주민 정보가 없습니다";
            SetAll("");
            _note.text = "";
            return;
        }

        _title.text = $"{villager.DisplayName}의 하루";
        if (villager.IsDeathKnown)
        {
            SetAll("");
            _note.text = "세상을 떠난 주민입니다.";
            return;
        }

        if (villager.Schedule == null || !_hours.TryGetValue(villager.VillagerId, out var today) || today.day <= 0)
        {
            SetAll("");
            _note.text = "오늘 일과가 아직 정해지지 않았습니다.";
            return;
        }

        for (int i = 0; i < SIJIN.Length; i++)
        {
            int hour = (FIRST_SIJIN_HOUR + i * 2) % 24;
            _sijinText[i].text = Describe(HourText(villager.Schedule, today.day, today.hours, hour),
                HourText(villager.Schedule, today.day, today.hours, (hour + 1) % 24));
        }
        _note.text = "초 = 시진의 앞 시간, 정 = 뒤 시간";
    }

    // One clock hour: the routine of the block covering it, home time after the day ends, blank for the gaps
    private static string HourText(VillagerScheduleView schedule, int day, int[] hours, int hour)
    {
        for (int slot = 0; slot * 2 + 1 < hours.Length; slot++)
        {
            if (hour >= hours[slot * 2] && hour < hours[slot * 2 + 1])
            {
                RoutineSlotInfo routine = schedule.Get(day, slot);
                if (routine.IsEmpty) return "";
                string place = MapAreaRegistry.NameOf(routine.PlaceId);
                string what = routine.Category == RoutineCategory.Rest ? $"휴식 · {routine.DisplayName}" : routine.DisplayName;
                return string.IsNullOrEmpty(place) ? what : $"{what} ({place})";
            }
        }

        if (hour == DayClock.DAY_END_HOUR) return "귀가";
        return hour > DayClock.DAY_END_HOUR || hour < DayClock.DAY_START_HOUR ? "밤 · 집" : "";
    }

    // A sijin is two hours (초, 정); only say which half when the two differ
    private static string Describe(string early, string late)
    {
        if (early == late) return early;
        if (string.IsNullOrEmpty(early)) return $"정부터 {late}";
        if (string.IsNullOrEmpty(late)) return $"{early} (초까지)";
        return $"{early} → {late}";
    }

    private void CacheBlockHours()
    {
        foreach (AI_Core core in FindObjectsByType<AI_Core>(FindObjectsSortMode.None))
        {
            if (core.PublicInfo == null || string.IsNullOrEmpty(core.PublicInfo.VillagerId)) continue;
            if (core.TodayBlocksDay > 0) _hours[core.PublicInfo.VillagerId] = (core.TodayBlocksDay, new List<int>(core.TodayBlockHours).ToArray());
        }
    }

    private void SetAll(string text)
    {
        foreach (var label in _sijinText) label.text = text;
    }

    private void AddRow()
    {
        int index = _rows.Count;
        var rect = PlayerItemUIStyle.NotebookRect(_listContent, "Villager" + index, 0, index * ROW_HEIGHT, LIST_WIDTH, ROW_HEIGHT);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0, 0, 0, 0);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.onClick.AddListener(() => Select(index));
        var label = PlayerItemUIStyle.NotebookLabel(rect, _font, "Label", "", 12, 4, LIST_WIDTH - 24, ROW_HEIGHT - 8, 21);
        _rows.Add(button); _rowLabels.Add(label);
    }

    private void Select(int index)
    {
        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        if (registry == null || index >= registry.Villagers.Count) return;
        _selectedId = registry.Villagers[index].VillagerId;
        Refresh();
    }

    private static string SijinName(int index)
    {
        int hour = (FIRST_SIJIN_HOUR + index * 2) % 24;
        return $"{SIJIN[index]} ({hour:00}~{(hour + 2) % 24:00}시)";
    }
}
