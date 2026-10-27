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
    // Rows run 05~05 from 묘시 초; every sijin is two hours (초, 정)
    private static readonly string[] SIJIN = { "묘시", "진시", "사시", "오시", "미시", "신시", "유시", "술시", "해시", "자시", "축시", "인시" };
    private const int FIRST_SIJIN_ZODIAC = 3;   // 묘시 is the fourth animal after 자·축·인
    private const float ZODIAC_SIZE = 46f;
    private const int TABLE_ROWS = 24;

    private const float LIST_X = 30f, LIST_Y = 136f, LIST_WIDTH = 420f, LIST_HEIGHT = 470f, LIST_ROW_HEIGHT = 42f;
    private const float HEADER_Y = 100f, SCROLLBAR_WIDTH = 6f, ROW_WIDTH = LIST_WIDTH - SCROLLBAR_WIDTH - 8f;
    // Register columns: x and width inside a row, name first
    private static readonly string[] COLUMN_TITLES = { "이름", "직업", "신분", "성별", "나이" };
    private static readonly float[] COLUMN_X = { 16f, 148f, 250f, 308f, 350f };
    private static readonly float[] COLUMN_WIDTH = { 128f, 98f, 56f, 40f, 52f };
    private const float TABLE_X = 548f, TABLE_Y = 104f, TABLE_WIDTH = 424f;
    private const float TIME_WIDTH = 126f, HOUR_HEIGHT = 31f, BLOCK_INSET = 3f;
    // Room on the right of the table for its scrollbar
    private const float TABLE_INNER_WIDTH = TABLE_WIDTH - SCROLLBAR_WIDTH - 4f;

    private static readonly Color INK = PlayerItemUIStyle.NotebookInk;
    private static readonly Color RULE = new(.20f, .14f, .10f, .85f);
    private static readonly Color RULE_FAINT = new(.20f, .14f, .10f, .30f);
    // Washes hint at the activity: work red (busy), rest green (at ease), home and sleep ink (nothing going on)
    private static readonly Color WORK_WASH = new(.66f, .24f, .16f, .38f);
    private static readonly Color REST_WASH = new(.32f, .50f, .28f, .36f);
    private static readonly Color SELECT_WASH = new(.59f, .20f, .14f, .40f);
    private static readonly Color HOME_WASH = new(.24f, .21f, .19f, .30f);
    private static readonly Color SLEEP_WASH = new(.14f, .12f, .11f, .40f);
    private static readonly Color NIGHT_SHADE = new(.20f, .14f, .10f, .10f);
    private static readonly Color SCROLL_TRACK = new(.20f, .14f, .10f, .12f);
    private static readonly Color SCROLL_HANDLE = new(.20f, .14f, .10f, .55f);

    private readonly List<Button> _rows = new();
    private readonly List<TMP_Text[]> _rowCells = new();
    private readonly List<Image> _rowMarks = new();
    private readonly List<(Image wash, TMP_Text label)> _blocks = new();
    // Last block hours seen per villager, kept when their object is gone so a hidden death changes nothing here
    private readonly Dictionary<string, (int day, int[] hours, int home, int bed)> _hours = new();
    private TMP_FontAsset _font;
    private NotebookScheduleArt _art;
    private Sprite _wash, _rule, _ruleThin, _select, _underline;
    private RectTransform _listContent, _table;
    private ScrollRect _tableScroll;
    private TMP_Text _dayLabel, _title, _note;
    private string _selectedId;
    private float _nextRefresh;

    private static int DayHours => DayClock.DAY_HOURS;

    public void Bind(TMP_FontAsset font)
    {
        _font = font;
        _art = Resources.Load<NotebookScheduleArt>("NotebookScheduleArt");
        if (_art != null)
        {
            _wash = _art.Wash; _rule = _art.Rule; _ruleThin = _art.RuleThin;
            _select = _art.Select; _underline = _art.TitleUnderline;
        }

        // Left page: heading and the villager list
        Heading("ScheduleHeading", "주민 일과표", 20);
        _dayLabel = Text(transform, "Day", "", 300, 66, 150, 26, 18, TextAlignmentOptions.Right);
        _dayLabel.color = PlayerItemUIStyle.NotebookMuted;
        BuildListHeader();
        _listContent = PlayerItemUIStyle.NotebookScroll(transform, "VillagerList", LIST_X, LIST_Y, LIST_WIDTH, LIST_HEIGHT, out ScrollRect listScroll);
        _listContent.parent.parent.GetComponent<Image>().color = Color.clear;
        listScroll.scrollSensitivity = LIST_ROW_HEIGHT;
        AddScrollbar(listScroll, LIST_WIDTH, LIST_HEIGHT);

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

    // Column titles over the villager register, with an ink rule under them
    private void BuildListHeader()
    {
        var header = PlayerItemUIStyle.NotebookRect(transform, "VillagerListHeader", LIST_X, HEADER_Y, ROW_WIDTH, LIST_Y - HEADER_Y);
        for (int i = 0; i < COLUMN_TITLES.Length; i++)
        {
            var title = Text(header, "Column" + i, COLUMN_TITLES[i], COLUMN_X[i], 0, COLUMN_WIDTH[i], LIST_Y - HEADER_Y - 6, 16,
                i == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
            title.color = PlayerItemUIStyle.NotebookMuted;
        }
        Rule(header, "HeaderRule", _rule, RULE, 0, LIST_Y - HEADER_Y - 3, ROW_WIDTH, 8);
    }

    // Thin ink bar on a scroll's right edge so it is clear how far it has moved; hidden when everything fits
    private void AddScrollbar(ScrollRect scroll, float width, float height)
    {
        var track = PlayerItemUIStyle.NotebookRect(scroll.transform, "Scrollbar", width - SCROLLBAR_WIDTH, 0, SCROLLBAR_WIDTH, height);
        track.gameObject.AddComponent<Image>().color = SCROLL_TRACK;

        var area = PlayerItemUIStyle.NotebookRect(track, "SlidingArea", 0, 0, SCROLLBAR_WIDTH, height);
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = area.offsetMax = Vector2.zero;
        var handle = PlayerItemUIStyle.NotebookRect(area, "Handle", 0, 0, SCROLLBAR_WIDTH, height);
        handle.pivot = new Vector2(.5f, .5f); handle.anchoredPosition = Vector2.zero; handle.sizeDelta = Vector2.zero;
        var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = SCROLL_HANDLE;

        var bar = track.gameObject.AddComponent<Scrollbar>();
        bar.handleRect = handle; bar.targetGraphic = handleImage; bar.transition = Selectable.Transition.None;
        bar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = bar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    // Time column with each sijin's animal and name, ink rules at the sijin edges, and an empty field for the brush washes.
    // The day's height shows at once; the night rows are scrolled to
    private void BuildTable()
    {
        float visible = DayHours * HOUR_HEIGHT;
        float height = TABLE_ROWS * HOUR_HEIGHT;
        var content = PlayerItemUIStyle.NotebookScroll(transform, "TimetableScroll", TABLE_X, TABLE_Y, TABLE_WIDTH, visible, out _tableScroll);
        content.parent.parent.GetComponent<Image>().color = Color.clear;
        content.sizeDelta = new Vector2(TABLE_INNER_WIDTH, height);
        _tableScroll.scrollSensitivity = HOUR_HEIGHT;
        AddScrollbar(_tableScroll, TABLE_WIDTH, visible);
        _table = PlayerItemUIStyle.NotebookRect(content, "Timetable", 0, 0, TABLE_INNER_WIDTH, height);

        // Night (술시 정 ~ 인시) is the paper a shade darker, under everything else, instead of a rule across the entries
        var night = PlayerItemUIStyle.NotebookRect(_table, "NightShade", 0, DayHours * HOUR_HEIGHT, TABLE_INNER_WIDTH, (TABLE_ROWS - DayHours) * HOUR_HEIGHT);
        var nightImage = night.gameObject.AddComponent<Image>();
        nightImage.color = NIGHT_SHADE;
        nightImage.raycastTarget = false;

        for (int offset = 0; offset < TABLE_ROWS; offset += 2)
        {
            int rows = Mathf.Min(2, TABLE_ROWS - offset);
            float y = offset * HOUR_HEIGHT;
            float icon = Mathf.Min(ZODIAC_SIZE, rows * HOUR_HEIGHT - 6);
            Sprite animal = _art != null ? _art.Zodiac((FIRST_SIJIN_ZODIAC + offset / 2) % SIJIN.Length) : null;
            if (animal != null)
            {
                var zodiac = Picture(_table, "Zodiac" + offset / 2, animal, Color.white, 4 + (ZODIAC_SIZE - icon) / 2, y + (rows * HOUR_HEIGHT - icon) / 2, icon, icon, Image.Type.Simple);
                zodiac.preserveAspect = true;
            }
            Text(_table, "Sijin" + offset / 2, SIJIN[offset / 2], ZODIAC_SIZE + 6, y, TIME_WIDTH - 26 - ZODIAC_SIZE - 6, rows * HOUR_HEIGHT, 21, TextAlignmentOptions.Center);
            for (int half = 0; half < rows; half++)
            {
                var mark = Text(_table, $"Half{offset + half}", half == 0 ? "초" : "정", TIME_WIDTH - 26, y + half * HOUR_HEIGHT, 22, HOUR_HEIGHT, 13, TextAlignmentOptions.Center);
                mark.color = PlayerItemUIStyle.NotebookMuted;
            }
            if (offset > 0) Rule(_table, "SijinRule" + offset / 2, _ruleThin, RULE_FAINT, 4, y, TABLE_INNER_WIDTH - 8, 6);
        }

        // The frame stays on the page while the rows scroll under it
        Rule(transform, "TimetableTopRule", _rule, RULE, TABLE_X - 6, TABLE_Y - 6, TABLE_WIDTH + 12, 9);
        Rule(transform, "TimetableBottomRule", _rule, RULE, TABLE_X - 6, TABLE_Y + visible - 3, TABLE_WIDTH + 12, 9);
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
            TMP_Text[] cells = _rowCells[i];
            // A death the village knows of stays on the roster, crossed out with an X
            cells[0].text = villager.IsDeathKnown ? $"<color=#B03030>X</color> <s>{villager.DisplayName}</s>" : villager.DisplayName;
            cells[1].text = string.IsNullOrEmpty(villager.JobName) ? "-" : villager.JobName;
            cells[2].text = SocialClassName(villager.SocialClass);
            cells[3].text = villager.Gender == VillagerGender.Female ? "여" : "남";
            cells[4].text = AgeGroupName(villager.AgeGroup);
            foreach (TMP_Text cell in cells) cell.color = villager.IsDeathKnown ? PlayerItemUIStyle.NotebookMuted : INK;
        }
        for (int i = count; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
        _listContent.sizeDelta = new Vector2(ROW_WIDTH, Mathf.Max(LIST_HEIGHT, count * LIST_ROW_HEIGHT));

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
                    if (ShowBlock(shown, rested ? "휴식" : "도구 찾기", restNote, REST_WASH, today.hours[slot * 2], today.hours[slot * 2 + 1])) shown++;
                    continue;
                }
                Color wash = routine.Category == RoutineCategory.Rest ? REST_WASH : WORK_WASH;
                // A routine naming a place group (tree shade) shows the group's name: which one it goes to is picked on the day
                string placeName = MapAreaRegistry.NameOf(routine.PlaceId) ?? ProjectShaman.AI.World.PlaceRegistry.NameOf(routine.PlaceId);
                if (ShowBlock(shown, routine.DisplayName, placeName, wash, today.hours[slot * 2], today.hours[slot * 2 + 1])) shown++;
            }

            // Free time between the routines and the walk home is not shown: where it is spent changes as the day goes (C131)
            // The wash starts where the villager is already home, so it is the stay at home, not the walk (that is the gap
            // above it): "집" with the hour it gets in by, then bed from its hour until 묘시 초
            // A villager out late goes to bed as it gets in (C128): no stay at home, the bed row says when it got in
            if (today.home > 0 && today.bed >= today.home)
            {
                // Same as the routines: what it does, then where. In until bed it is up and can be talked to; 취침 is when
                // it really falls asleep (a host's ghost comes out then)
                if (ShowBlock(shown, "휴식", "집", HOME_WASH, today.home, today.bed)) shown++;
                string bedTitle = today.bed == today.home ? "귀가 · 취침" : "취침";
                if (ShowBlock(shown, bedTitle, "집", SLEEP_WASH, today.bed, DayClock.DAY_START_HOUR + TABLE_ROWS)) shown++;
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
            var wash = Picture(_table, "Block" + _blocks.Count, _wash, WORK_WASH, 0, 0, 10, 10, Image.Type.Sliced);
            wash.pixelsPerUnitMultiplier = 2.5f;
            var label = Text(wash.transform, "Label", "", 10, 4, 10, 10, 21, TextAlignmentOptions.Center);
            label.richText = true; label.enableAutoSizing = true; label.fontSizeMin = 13; label.fontSizeMax = 21;
            _blocks.Add((wash, label));
        }

        var (image, text) = _blocks[index];
        float width = TABLE_INNER_WIDTH - TIME_WIDTH - BLOCK_INSET * 2;
        float height = (to - from) * HOUR_HEIGHT - BLOCK_INSET * 2;
        PlayerItemUIStyle.Place(image.rectTransform, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(TIME_WIDTH + BLOCK_INSET, -(from * HOUR_HEIGHT + BLOCK_INSET)), new Vector2(width, height));
        text.rectTransform.sizeDelta = new Vector2(width - 20, height - 8);

        image.color = _wash == null ? Color.clear : color;
        text.text = string.IsNullOrEmpty(subtitle) ? title : $"{title}\n<size=72%>{subtitle}</size>";
        image.gameObject.SetActive(true);
        return true;
    }

    private void CacheBlockHours()
    {
        foreach (AI_Core core in FindObjectsByType<AI_Core>(FindObjectsSortMode.None))
        {
            if (core.PublicInfo == null || string.IsNullOrEmpty(core.PublicInfo.VillagerId)) continue;
            if (core.TodayBlocksDay > 0) _hours[core.PublicInfo.VillagerId] = (core.TodayBlocksDay, new List<int>(core.TodayBlockHours).ToArray(), core.TodayHomeHour, core.TodayBedHour);
        }

        // A villager killed in the night has no object left; until its death is known its day still shows as usual,
        // so the tab never gives a death away (the hint is the villagers' "not seen all day")
        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        if (registry == null) return;
        foreach (IVillagerView villager in registry.Villagers)
        {
            if (!registry.TryGetDayHours(villager.VillagerId, out int day, out int[] hours, out int home, out int bed) || hours == null) continue;
            if (_hours.TryGetValue(villager.VillagerId, out var cached) && cached.day >= day) continue;
            _hours[villager.VillagerId] = (day, hours, home, bed);
        }
    }

    private void AddRow()
    {
        int index = _rows.Count;
        var rect = PlayerItemUIStyle.NotebookRect(_listContent, "Villager" + index, 0, index * LIST_ROW_HEIGHT, ROW_WIDTH, LIST_ROW_HEIGHT);
        var hit = rect.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = hit; button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Select(index));
        var mark = Picture(rect, "Selected", _select, SELECT_WASH, 4, 3, ROW_WIDTH - 8, LIST_ROW_HEIGHT - 6, Image.Type.Sliced);
        mark.pixelsPerUnitMultiplier = 2f;
        var cells = new TMP_Text[COLUMN_TITLES.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            bool name = i == 0;
            cells[i] = Text(rect, "Cell" + i, "", COLUMN_X[i], 4, COLUMN_WIDTH[i], LIST_ROW_HEIGHT - 8, name ? 22 : 18,
                name ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
            cells[i].richText = true; cells[i].enableAutoSizing = true; cells[i].fontSizeMin = 13; cells[i].fontSizeMax = name ? 22 : 18;
        }
        if (index > 0) Rule(rect, "RowRule", _ruleThin, SCROLL_TRACK, 8, 0, ROW_WIDTH - 16, 4);
        _rows.Add(button); _rowCells.Add(cells); _rowMarks.Add(mark);
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

    // Brings the rows around the current hour into view (at night the table would otherwise open on the day)
    private void ScrollToNow()
    {
        TimeManager time = TimeManager.Existing;
        float hidden = (TABLE_ROWS - DayHours) * HOUR_HEIGHT;
        if (_tableScroll == null || time == null || hidden <= 0f) return;

        float elapsed = (float)time.Elapsed;
        float hour = elapsed < TimeManager.DAY_DURATION ? DayClock.ToHour(elapsed) : DayClock.NightToHour(elapsed - TimeManager.DAY_DURATION);
        float row = Mathf.Clamp(hour - DayClock.DAY_START_HOUR, 0f, TABLE_ROWS);
        float top = Mathf.Clamp((row - DayHours * .5f) * HOUR_HEIGHT, 0f, hidden);
        _tableScroll.verticalNormalizedPosition = 1f - top / hidden;
    }
}
