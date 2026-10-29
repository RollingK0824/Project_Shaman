using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ProjectShaman.AI.Core;

public static class NotebookSchedulePrefabSetup
{
    private const string PREFAB_PATH = "Assets/Project/Prefabs/UI/Notebook/VillagerScheduleSpread.prefab";
    private const string ART_PATH = "Assets/Project/Data/UI/Resources/NotebookScheduleArt.asset";
    private const string TITLE = "주민 일과표 프리팹";

    private static readonly string[] SIJIN = { "묘시", "진시", "사시", "오시", "미시", "신시", "유시", "술시", "해시", "자시", "축시", "인시" };
    private const int FIRST_SIJIN_ZODIAC = 3;
    private const float ZODIAC_SIZE = 46f;
    private const int TABLE_ROWS = NotebookScheduleView.TABLE_ROWS;

    private const float LIST_X = 30f, LIST_Y = 136f, LIST_WIDTH = 420f, LIST_HEIGHT = 470f, LIST_ROW_HEIGHT = 42f;
    private const float HEADER_Y = 100f, SCROLLBAR_WIDTH = 6f, ROW_WIDTH = LIST_WIDTH - SCROLLBAR_WIDTH - 8f;
    private static readonly string[] COLUMN_TITLES = { "이름", "직업", "신분", "성별", "나이" };
    private static readonly float[] COLUMN_X = { 16f, 148f, 250f, 308f, 350f };
    private static readonly float[] COLUMN_WIDTH = { 128f, 98f, 56f, 40f, 52f };
    private const float TABLE_X = 548f, TABLE_Y = 104f, TABLE_WIDTH = 424f;
    private const float TIME_WIDTH = 126f, HOUR_HEIGHT = 31f;
    private const float TABLE_INNER_WIDTH = TABLE_WIDTH - SCROLLBAR_WIDTH - 4f;

    private static readonly Color RULE = new(.20f, .14f, .10f, .85f);
    private static readonly Color RULE_FAINT = new(.20f, .14f, .10f, .30f);
    private static readonly Color SELECT_WASH = new(.59f, .20f, .14f, .40f);
    private static readonly Color WORK_WASH = new(.66f, .24f, .16f, .38f);
    private static readonly Color NIGHT_SHADE = new(.20f, .14f, .10f, .10f);
    private static readonly Color SCROLL_TRACK = new(.20f, .14f, .10f, .12f);
    private static readonly Color SCROLL_HANDLE = new(.20f, .14f, .10f, .55f);

    private static TMP_FontAsset _font;
    private static Sprite _wash, _rule, _ruleThin, _select, _underline;
    private static NotebookScheduleArt _art;

    [MenuItem("Tools/Project Shaman/Setup/주민 일과표 프리팹 생성")]
    private static void Run()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) != null &&
            !EditorUtility.DisplayDialog(TITLE, "이미 프리팹이 있습니다. 덮어쓸까요? (프리팹에서 고친 배치는 사라집니다)", "덮어쓰기", "취소"))
            return;

        _font = FindNotebookFont();
        _art = AssetDatabase.LoadAssetAtPath<NotebookScheduleArt>(ART_PATH);
        if (_art != null)
        {
            _wash = _art.Wash; _rule = _art.Rule; _ruleThin = _art.RuleThin;
            _select = _art.Select; _underline = _art.TitleUnderline;
        }

        var root = new GameObject("VillagerScheduleSpread", typeof(RectTransform));
        try
        {
            var rootRect = (RectTransform)root.transform;
            PlayerItemUIStyle.Place(rootRect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1000, 640));
            var view = root.AddComponent<NotebookScheduleView>();
            var fields = new SerializedObject(view);

            Heading(root.transform, "ScheduleHeading", "주민 일과표", 20);
            var dayLabel = Text(root.transform, "Day", "", 300, 66, 150, 26, 18, TextAlignmentOptions.Right);
            dayLabel.color = PlayerItemUIStyle.NotebookMuted;
            BuildListHeader(root.transform);
            var listContent = BuildList(root.transform, out NotebookScheduleRow rowTemplate);

            var title = Heading(root.transform, "VillagerDay", "주민을 고르세요", 540);
            var tableScroll = BuildTable(root.transform, out RectTransform blockArea, out Image blockTemplate);
            var note = Text(root.transform, "Note", "", TABLE_X, TABLE_Y + DayClock.DAY_HOURS * HOUR_HEIGHT + 14, TABLE_WIDTH, 24, 15, TextAlignmentOptions.Center);
            note.color = PlayerItemUIStyle.NotebookMuted;

            fields.FindProperty("_dayLabel").objectReferenceValue = dayLabel;
            fields.FindProperty("_title").objectReferenceValue = title;
            fields.FindProperty("_note").objectReferenceValue = note;
            fields.FindProperty("_listContent").objectReferenceValue = listContent;
            fields.FindProperty("_rowTemplate").objectReferenceValue = rowTemplate;
            fields.FindProperty("_tableScroll").objectReferenceValue = tableScroll;
            fields.FindProperty("_blockArea").objectReferenceValue = blockArea;
            fields.FindProperty("_blockTemplate").objectReferenceValue = blockTemplate;
            fields.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH, out bool saved);
            if (!saved || prefab == null)
            {
                EditorUtility.DisplayDialog(TITLE, "프리팹 저장에 실패했습니다.", "확인");
                return;
            }

            EditorGUIUtility.PingObject(prefab);
            string missing = (_font == null ? "\n· 노트 폰트를 찾지 못해 기본 폰트를 썼습니다." : "") +
                (_art == null ? "\n· NotebookScheduleArt가 없어 스프라이트가 비었습니다." : "");
            EditorUtility.DisplayDialog(TITLE, $"{PREFAB_PATH} 생성 완료\n씬의 UI 하위에 비활성으로 두면 노트가 일과표 탭으로 가져갑니다.{missing}", "확인");
        }
        finally
        {
            Object.DestroyImmediate(root);
            _font = null; _art = null; _wash = _rule = _ruleThin = _select = _underline = null;
        }
    }

    private static TMP_FontAsset FindNotebookFont()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Project/Prefabs" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var panel = prefab != null ? prefab.GetComponentInChildren<NotebookPanel>(true) : null;
            var text = panel != null ? panel.GetComponentInChildren<TMP_Text>(true) : null;
            if (text != null && text.font != null) return text.font;
        }
        return null;
    }

    private static TMP_Text Heading(Transform parent, string name, string value, float x)
    {
        var label = Text(parent, name, value, x, 12, 440, 46, 32, TextAlignmentOptions.Center);
        label.enableAutoSizing = true; label.fontSizeMin = 22; label.fontSizeMax = 32;
        if (_underline != null)
        {
            var line = Picture(parent, name + "Underline", _underline, Color.white, x + 90, 50, 260, 60, Image.Type.Simple);
            line.preserveAspect = true;
        }
        return label;
    }

    private static void BuildListHeader(Transform parent)
    {
        var header = PlayerItemUIStyle.NotebookRect(parent, "VillagerListHeader", LIST_X, HEADER_Y, ROW_WIDTH, LIST_Y - HEADER_Y);
        for (int i = 0; i < COLUMN_TITLES.Length; i++)
        {
            var title = Text(header, "Column" + i, COLUMN_TITLES[i], COLUMN_X[i], 0, COLUMN_WIDTH[i], LIST_Y - HEADER_Y - 6, 16,
                i == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
            title.color = PlayerItemUIStyle.NotebookMuted;
        }
        Rule(header, "HeaderRule", _rule, RULE, 0, LIST_Y - HEADER_Y - 3, ROW_WIDTH, 8);
    }

    private static RectTransform BuildList(Transform parent, out NotebookScheduleRow template)
    {
        var content = PlayerItemUIStyle.NotebookScroll(parent, "VillagerList", LIST_X, LIST_Y, LIST_WIDTH, LIST_HEIGHT, out ScrollRect scroll);
        scroll.GetComponent<Image>().color = Color.clear;
        scroll.scrollSensitivity = LIST_ROW_HEIGHT;
        AddScrollbar(scroll, LIST_WIDTH, LIST_HEIGHT);
        content.sizeDelta = new Vector2(ROW_WIDTH, LIST_HEIGHT);

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true; layout.childForceExpandWidth = true;
        layout.childControlHeight = true; layout.childForceExpandHeight = false;
        layout.spacing = 0;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        template = BuildRow(content);
        template.gameObject.SetActive(false);
        return content;
    }

    private static NotebookScheduleRow BuildRow(Transform parent)
    {
        var rect = PlayerItemUIStyle.NotebookRect(parent, "VillagerRowTemplate", 0, 0, ROW_WIDTH, LIST_ROW_HEIGHT);
        var element = rect.gameObject.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = LIST_ROW_HEIGHT;
        var hit = rect.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = hit; button.transition = Selectable.Transition.None;
        var mark = Picture(rect, "Selected", _select, SELECT_WASH, 4, 3, ROW_WIDTH - 8, LIST_ROW_HEIGHT - 6, Image.Type.Sliced);
        mark.pixelsPerUnitMultiplier = 2f;
        mark.enabled = false;
        var cells = new TMP_Text[COLUMN_TITLES.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            bool name = i == 0;
            cells[i] = Text(rect, "Cell" + i, "", COLUMN_X[i], 4, COLUMN_WIDTH[i], LIST_ROW_HEIGHT - 8, name ? 22 : 18,
                name ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
            cells[i].richText = true; cells[i].enableAutoSizing = true; cells[i].fontSizeMin = 13; cells[i].fontSizeMax = name ? 22 : 18;
        }
        var divider = Rule(rect, "RowRule", _ruleThin, SCROLL_TRACK, 8, 0, ROW_WIDTH - 16, 4);

        var row = rect.gameObject.AddComponent<NotebookScheduleRow>();
        var fields = new SerializedObject(row);
        fields.FindProperty("_button").objectReferenceValue = button;
        fields.FindProperty("_mark").objectReferenceValue = mark;
        fields.FindProperty("_divider").objectReferenceValue = divider.gameObject;
        var cellsProperty = fields.FindProperty("_cells");
        cellsProperty.arraySize = cells.Length;
        for (int i = 0; i < cells.Length; i++) cellsProperty.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
        fields.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    private static void AddScrollbar(ScrollRect scroll, float width, float height)
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

    private static ScrollRect BuildTable(Transform parent, out RectTransform blockArea, out Image blockTemplate)
    {
        float visible = DayClock.DAY_HOURS * HOUR_HEIGHT;
        float height = TABLE_ROWS * HOUR_HEIGHT;
        var content = PlayerItemUIStyle.NotebookScroll(parent, "TimetableScroll", TABLE_X, TABLE_Y, TABLE_WIDTH, visible, out ScrollRect tableScroll);
        tableScroll.GetComponent<Image>().color = Color.clear;
        content.sizeDelta = new Vector2(TABLE_INNER_WIDTH, height);
        tableScroll.scrollSensitivity = HOUR_HEIGHT;
        AddScrollbar(tableScroll, TABLE_WIDTH, visible);
        var table = PlayerItemUIStyle.NotebookRect(content, "Timetable", 0, 0, TABLE_INNER_WIDTH, height);

        var night = PlayerItemUIStyle.NotebookRect(table, "NightShade", 0, DayClock.DAY_HOURS * HOUR_HEIGHT, TABLE_INNER_WIDTH, (TABLE_ROWS - DayClock.DAY_HOURS) * HOUR_HEIGHT);
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
                var zodiac = Picture(table, "Zodiac" + offset / 2, animal, Color.white, 4 + (ZODIAC_SIZE - icon) / 2, y + (rows * HOUR_HEIGHT - icon) / 2, icon, icon, Image.Type.Simple);
                zodiac.preserveAspect = true;
            }
            Text(table, "Sijin" + offset / 2, SIJIN[offset / 2], ZODIAC_SIZE + 6, y, TIME_WIDTH - 26 - ZODIAC_SIZE - 6, rows * HOUR_HEIGHT, 21, TextAlignmentOptions.Center);
            for (int half = 0; half < rows; half++)
            {
                var mark = Text(table, $"Half{offset + half}", half == 0 ? "초" : "정", TIME_WIDTH - 26, y + half * HOUR_HEIGHT, 22, HOUR_HEIGHT, 13, TextAlignmentOptions.Center);
                mark.color = PlayerItemUIStyle.NotebookMuted;
            }
            if (offset > 0) Rule(table, "SijinRule" + offset / 2, _ruleThin, RULE_FAINT, 4, y, TABLE_INNER_WIDTH - 8, 6);
        }

        Rule(parent, "TimetableTopRule", _rule, RULE, TABLE_X - 6, TABLE_Y - 6, TABLE_WIDTH + 12, 9);
        Rule(parent, "TimetableBottomRule", _rule, RULE, TABLE_X - 6, TABLE_Y + visible - 3, TABLE_WIDTH + 12, 9);
        var divider = Rule(table, "TimeDivider", _ruleThin, RULE_FAINT, 0, 0, height, 6).rectTransform;
        divider.pivot = new Vector2(.5f, .5f);
        divider.anchoredPosition = new Vector2(TIME_WIDTH, -height / 2);
        divider.localEulerAngles = new Vector3(0, 0, 90);

        blockArea = PlayerItemUIStyle.NotebookRect(table, "Blocks", TIME_WIDTH, 0, TABLE_INNER_WIDTH - TIME_WIDTH, height);
        blockTemplate = Picture(blockArea, "BlockTemplate", _wash, WORK_WASH, 0, 0, 10, HOUR_HEIGHT * 2, Image.Type.Sliced);
        blockTemplate.pixelsPerUnitMultiplier = 2.5f;
        var blockRect = blockTemplate.rectTransform;
        blockRect.anchorMin = new Vector2(0, 1); blockRect.anchorMax = new Vector2(1, 1); blockRect.pivot = new Vector2(.5f, 1);
        blockRect.anchoredPosition = new Vector2(0, -3); blockRect.sizeDelta = new Vector2(-6, HOUR_HEIGHT * 2 - 6);
        var label = Text(blockRect, "Label", "", 0, 0, 10, 10, 21, TextAlignmentOptions.Center);
        PlayerItemUIStyle.Stretch(label.rectTransform, new Vector2(10, 4), new Vector2(-10, -4));
        label.richText = true; label.enableAutoSizing = true; label.fontSizeMin = 13; label.fontSizeMax = 21;
        blockTemplate.gameObject.SetActive(false);
        return tableScroll;
    }

    private static Image Rule(Transform parent, string name, Sprite sprite, Color color, float x, float y, float width, float height)
    {
        var image = Picture(parent, name, sprite, color, x, y - height / 2, width, height, Image.Type.Sliced);
        image.pixelsPerUnitMultiplier = 4f;
        return image;
    }

    private static Image Picture(Transform parent, string name, Sprite sprite, Color color, float x, float y, float width, float height, Image.Type type)
    {
        var rect = PlayerItemUIStyle.NotebookRect(parent, name, x, y, width, height);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.type = sprite != null ? type : Image.Type.Simple;
        image.color = sprite != null ? color : Color.clear; image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height, float size, TextAlignmentOptions alignment)
    {
        var label = PlayerItemUIStyle.NotebookLabel(parent, _font, name, value, x, y, width, height, size);
        label.alignment = alignment;
        return label;
    }
}
