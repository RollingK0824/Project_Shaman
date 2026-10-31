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

// Villager schedule spread: villagers on the left page as a small register (이름·직업·신분·성별·나이),
// the chosen villager's day as a timetable on the right.
// Drawn in ink on the notebook paper: one row per half sijin (초/정) of all twelve sijin from 묘시 초, each routine a brush
// wash spanning its rows, then when the villager is home by and when it goes to bed. The table scrolls; the day fits the page.
// Only the planned schedule is shown.
public sealed class NotebookScheduleView : MonoBehaviour
{
    public const int TABLE_ROWS = 24;

    private static readonly Color INK = PlayerItemUIStyle.NotebookInk;

    [SerializeField] private TMP_Text _dayLabel;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _note;
    [SerializeField] private RectTransform _listContent;
    [SerializeField] private NotebookScheduleRow _rowTemplate;
    [SerializeField] private ScrollRect _tableScroll;
    [SerializeField] private RectTransform _blockArea;
    [SerializeField] private Image _blockTemplate;
    [SerializeField] private float _blockInset = 3f;
    // Washes hint at the activity: work red (busy), rest green (at ease), home and sleep ink (nothing going on)
    [SerializeField] private Color _workWash = new(.66f, .24f, .16f, .38f);
    [SerializeField] private Color _restWash = new(.32f, .50f, .28f, .36f);
    [SerializeField] private Color _homeWash = new(.24f, .21f, .19f, .30f);
    [SerializeField] private Color _sleepWash = new(.14f, .12f, .11f, .40f);
    // Pin icon on a row: ink when pinned to the HUD, faded when not
    [SerializeField] private Color _pinOffColor = new(.20f, .14f, .10f, .25f);
    // The current time drawn across the timetable in red ink
    [SerializeField] private Color _nowColor = new(.69f, .16f, .14f, .9f);
    [SerializeField] private float _nowThickness = 3f;

    private readonly List<NotebookScheduleRow> _rows = new();
    private readonly List<(Image wash, TMP_Text label)> _blocks = new();
    // Last block hours seen per villager, kept when their object is gone so a hidden death changes nothing here
    private readonly Dictionary<string, (int day, int[] hours, int leave, int home, int bed)> _hours = new();
    private string _selectedId;
    private float _nextRefresh;
    private Image _nowLine;

    private static int DayHours => DayClock.DAY_HOURS;

    private void OnEnable()
    {
        _nextRefresh = 0f;
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
            NotebookScheduleRow row = _rows[i];
            bool selected = villager.VillagerId == _selectedId;
            row.gameObject.SetActive(true);
            row.Mark.enabled = selected && row.Mark.sprite != null;
            TMP_Text[] cells = row.Cells;
            // A death the village knows of stays on the roster, crossed out with an X
            cells[0].text = villager.IsDeathKnown ? $"<color=#B03030>X</color> <s>{villager.DisplayName}</s>" : villager.DisplayName;
            cells[1].text = string.IsNullOrEmpty(villager.JobName) ? "-" : villager.JobName;
            cells[2].text = SocialClassName(villager.SocialClass);
            cells[3].text = villager.Gender == VillagerGender.Female ? "여" : "남";
            cells[4].text = AgeGroupName(villager.AgeGroup);
            foreach (TMP_Text cell in cells) cell.color = villager.IsDeathKnown ? PlayerItemUIStyle.NotebookMuted : INK;
            if (row.PinIcon != null) row.PinIcon.color = PinManager.Instance.IsVillagerPinned(villager.VillagerId) ? INK : _pinOffColor;
        }
        for (int i = count; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);

        IVillagerView chosen = null;
        if (registry != null && !string.IsNullOrEmpty(_selectedId)) registry.TryGet(_selectedId, out chosen);
        ShowDay(chosen);
        ShowNow();
    }

    // A line at the current time over the rows (and over the washes), with a small 지금 at its end
    private void ShowNow()
    {
        if (_blockArea == null) return;
        if (!TryGetHour(out float hour))
        {
            if (_nowLine != null) _nowLine.gameObject.SetActive(false);
            return;
        }

        if (_nowLine == null) CreateNowLine();
        float hourHeight = _blockArea.rect.height / TABLE_ROWS;
        float row = Mathf.Clamp(hour - DayClock.DAY_START_HOUR, 0f, TABLE_ROWS);
        RectTransform rect = _nowLine.rectTransform;
        rect.anchoredPosition = new Vector2(0, -row * hourHeight);
        rect.sizeDelta = new Vector2(0, _nowThickness);
        _nowLine.transform.SetAsLastSibling();
        _nowLine.gameObject.SetActive(true);
    }

    private void CreateNowLine()
    {
        GameObject line = new GameObject("NowLine", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(_blockArea, false);
        _nowLine = line.GetComponent<Image>();
        _nowLine.color = _nowColor;
        _nowLine.raycastTarget = false;
        RectTransform rect = _nowLine.rectTransform;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(.5f, .5f);

        TMP_Text template = _blockTemplate != null ? _blockTemplate.GetComponentInChildren<TMP_Text>(true) : null;
        GameObject labelObject = new GameObject("Now", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(line.transform, false);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        if (template != null) label.font = template.font;
        label.text = "지금";
        label.fontSize = template != null ? template.fontSize * .7f : 18f;
        label.color = _nowColor;
        label.alignment = TextAlignmentOptions.BottomRight;
        label.raycastTarget = false;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(1, .5f);
        labelRect.anchorMax = new Vector2(1, .5f);
        labelRect.pivot = new Vector2(1, 0);
        labelRect.anchoredPosition = new Vector2(-4f, 2f);
        labelRect.sizeDelta = new Vector2(80f, 30f);
    }

    // The hour into the current day and night (Elapsed counts from the start of the game)
    private static bool TryGetHour(out float hour)
    {
        hour = 0f;
        TimeManager time = TimeManager.Existing;
        if (time == null) return false;
        float elapsed = (float)(time.Elapsed % TimeManager.CYCLE_DURATION);
        hour = elapsed < TimeManager.DAY_DURATION ? DayClock.ToHour(elapsed) : DayClock.NightToHour(elapsed - TimeManager.DAY_DURATION);
        return true;
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
            _note.text = "빈 칸은 자유 시간·오가는 때 · 일과는 도착하면 시작";
            VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
            for (int slot = 0; slot * 2 + 1 < today.hours.Length; slot++)
            {
                RoutineSlotInfo routine = villager.Schedule.Get(today.day, slot);
                if (routine.IsEmpty) continue;
                // A work given up today for a missing tool: the villager rests there instead (C120)
                if (registry != null && registry.IsSlotRested(villager.VillagerId, today.day, slot, out string restPlaceId))
                {
                    // No rest place: the search took the whole block (or there was nowhere to rest), so it never rested
                    bool rested = !string.IsNullOrEmpty(restPlaceId);
                    string restNote = $"{routine.DisplayName} 못 함 · 도구 없음";
                    if (rested) restNote += $" → {MapAreaRegistry.NameOf(restPlaceId)}";
                    if (ShowBlock(shown, rested ? "휴식" : "도구 찾기", restNote, _restWash, today.hours[slot * 2], today.hours[slot * 2 + 1])) shown++;
                    continue;
                }
                Color wash = routine.Category == RoutineCategory.Rest ? _restWash : _workWash;
                // A routine naming a place group (tree shade) shows the group's name: which one it goes to is picked on the day
                string placeName = MapAreaRegistry.NameOf(routine.PlaceId) ?? ProjectShaman.AI.World.PlaceRegistry.NameOf(routine.PlaceId);
                if (ShowBlock(shown, routine.DisplayName, placeName, wash, today.hours[slot * 2], today.hours[slot * 2 + 1])) shown++;
            }

            // Free time between the routines and the walk home is not shown: where it is spent changes as the day goes.
            // Then three rows (C131), estimates until they happen: 귀가 from setting off (one row at least, a short walk
            // would round away), 휴식 at home until bed, 취침 from when it falls asleep (nightfall, or as it gets in later).
            // Setting off at or after its bed hour it goes to bed as it gets in: one row
            int night = DayClock.DAY_START_HOUR + TABLE_ROWS;
            if (today.leave > 0 && today.bed > today.leave)
            {
                int home = Mathf.Min(Mathf.Max(today.home, today.leave + 1), today.bed);
                if (ShowBlock(shown, "귀가", "집", _homeWash, today.leave, home)) shown++;
                if (ShowBlock(shown, "휴식", "집", _homeWash, home, today.bed)) shown++;
                if (ShowBlock(shown, "취침", "집", _sleepWash, today.bed, night)) shown++;
            }
            else if (today.leave > 0)
            {
                if (ShowBlock(shown, "귀가 · 취침", "집", _sleepWash, today.leave, night)) shown++;
            }
        }

        for (int i = shown; i < _blocks.Count; i++) _blocks[i].wash.gameObject.SetActive(false);
    }

    // One entry as a brush wash over the hours it covers
    private bool ShowBlock(int index, string title, string subtitle, Color color, int startHour, int endHour)
    {
        int from = Mathf.Clamp(startHour - DayClock.DAY_START_HOUR, 0, TABLE_ROWS);
        int to = Mathf.Clamp(endHour - DayClock.DAY_START_HOUR, from, TABLE_ROWS);
        if (to <= from) return false;

        while (_blocks.Count <= index)
        {
            Image wash = Instantiate(_blockTemplate, _blockArea);
            wash.name = "Block" + _blocks.Count;
            _blocks.Add((wash, wash.GetComponentInChildren<TMP_Text>(true)));
        }

        var (image, text) = _blocks[index];
        float hourHeight = _blockArea.rect.height / TABLE_ROWS;
        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = new Vector2(0, -(from * hourHeight + _blockInset));
        rect.sizeDelta = new Vector2(-_blockInset * 2, (to - from) * hourHeight - _blockInset * 2);

        image.color = image.sprite == null ? Color.clear : color;
        text.text = string.IsNullOrEmpty(subtitle) ? title : $"{title}\n<size=72%>{subtitle}</size>";
        image.gameObject.SetActive(true);
        return true;
    }

    private void CacheBlockHours()
    {
        foreach (AI_Core core in FindObjectsByType<AI_Core>(FindObjectsSortMode.None))
        {
            if (core.PublicInfo == null || string.IsNullOrEmpty(core.PublicInfo.VillagerId)) continue;
            if (core.TodayBlocksDay > 0) _hours[core.PublicInfo.VillagerId] = (core.TodayBlocksDay, new List<int>(core.TodayBlockHours).ToArray(), core.TodayLeaveHour, core.TodayHomeHour, core.TodayBedHour);
        }

        // A villager killed in the night has no object left; until its death is known its day still shows as usual,
        // so the tab never gives a death away (the hint is the villagers' "not seen all day")
        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        if (registry == null) return;
        foreach (IVillagerView villager in registry.Villagers)
        {
            if (!registry.TryGetDayHours(villager.VillagerId, out int day, out int[] hours, out int leave, out int home, out int bed) || hours == null) continue;
            if (_hours.TryGetValue(villager.VillagerId, out var cached) && cached.day >= day) continue;
            _hours[villager.VillagerId] = (day, hours, leave, home, bed);
        }
    }

    private void AddRow()
    {
        int index = _rows.Count;
        NotebookScheduleRow row = Instantiate(_rowTemplate, _listContent);
        row.name = "Villager" + index;
        if (row.Divider != null) row.Divider.SetActive(index > 0);
        row.Button.onClick.AddListener(() => Select(index));
        if (row.PinButton != null) row.PinButton.onClick.AddListener(() => TogglePin(index));
        _rows.Add(row);
    }

    private static string SocialClassName(VillagerSocialClass socialClass) => socialClass switch
    {
        VillagerSocialClass.Yangban => "양반",
        VillagerSocialClass.Cheonmin => "천민",
        _ => "상민"
    };

    private static string AgeGroupName(VillagerAgeGroup ageGroup) => ageGroup switch
    {
        VillagerAgeGroup.Elder => "노인",
        VillagerAgeGroup.Child => "아이",
        _ => "어른"
    };

    private void Select(int index)
    {
        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        if (registry == null || index >= registry.Villagers.Count) return;
        _selectedId = registry.Villagers[index].VillagerId;
        Refresh();
        ScrollToNow();
    }

    // Pins the villager to the HUD schedule (SchedulePinsHUD), or unpins it
    private void TogglePin(int index)
    {
        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        if (registry == null || index >= registry.Villagers.Count) return;
        PinManager.Instance.ToggleVillager(registry.Villagers[index].VillagerId);
        Refresh();
    }

    // Brings the rows around the current hour into view (at night the table would otherwise open on the day)
    private void ScrollToNow()
    {
        if (_tableScroll == null || !TryGetHour(out float hour)) return;
        float contentHeight = _tableScroll.content.rect.height;
        float hidden = contentHeight - _tableScroll.viewport.rect.height;
        if (hidden <= 0f) return;

        float hourHeight = contentHeight / TABLE_ROWS;
        float row = Mathf.Clamp(hour - DayClock.DAY_START_HOUR, 0f, TABLE_ROWS);
        float top = Mathf.Clamp((row - DayHours * .5f) * hourHeight, 0f, hidden);
        _tableScroll.verticalNormalizedPosition = 1f - top / hidden;
    }
}
