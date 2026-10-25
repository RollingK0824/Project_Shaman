using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 씬 뷰에서 꼭짓점을 찍어 담을 경로대로 배치하는 에디터 툴.
// - 담: 여러 프리팹을 팔레트로 등록, 선택한 것만 쓰거나 랜덤으로 섞기
// - 모서리: 꺾이는 꼭짓점(옵션: 양 끝)에 기둥/모서리 프리팹 배치
// - 문: 여러 프리팹을 팔레트로 등록, Shift+클릭한 담 조각을 선택한 문으로 교체
public class WallPainterWindow : EditorWindow
{
    // =========================================================
    // Settings
    // =========================================================

    private enum LengthAxis
    {
        Auto,
        X,
        Z
    }

    private enum MeasureSource
    {
        ColliderFirst,
        Mesh
    }

    // 대칭선이 뻗는 방향. AlongZ면 Z축 방향 선을 기준으로 좌우(X)가 뒤집힌다.
    private enum MirrorAxis
    {
        AlongZ,
        AlongX
    }

    // 드래그한 구간을 채우는 방식. 어느 쪽이든 길이는 드래그 길이에 맞춰 늘이거나 줄인다.
    private enum WallFillMode
    {
        Pieces,
        Single
    }

    private enum WallPickMode
    {
        Selected,
        Random
    }

    private enum CornerAnchor
    {
        BoundsCenter,
        Pivot
    }

    private const string UNDO_NAME = "Wall Painter";

    // 첫 꼭짓점에서 이 픽셀 거리 안을 클릭하면 경로를 닫는다.
    private const float CLOSE_SNAP_PIXELS = 15f;

    // 바닥 높이를 찾을 때 이 높이 위에서 아래로 Raycast 한다.
    private const float GROUND_RAY_HEIGHT = 50f;

    private const float MOUSE_RAY_DISTANCE = 5000f;
    private const float MIN_STEP = 0.05f;
    private const int MAX_PIECES_PER_SPAN = 500;

    // 거의 되돌아가는 예각에서 맞대기 길이가 무한히 커지지 않게 막는다.
    private const float MAX_JOINT_TURN = 135f;

    // 정렬 가이드가 붙는 거리 (HandleSize 배율, 화면에서 대략 10px)
    private const float GUIDE_SNAP_SCALE = 0.12f;
    private const float MIRROR_LINE_LENGTH = 200f;

    private static readonly float[] ANGLE_SNAP_PRESETS = { 1f, 5f, 15f, 45f, 90f, 0f };

    // 0 = 자유, -1 = 담 조각 간격 단위
    private static readonly float[] LENGTH_SNAP_PRESETS = { 0f, 0.1f, 0.5f, 1f, -1f };

    // 구간이 X/Z축과 평행한지 판단하는 기준 (cos 0.8°). 1° 스냅에서 1°만 틀어져도 축 정렬로 보지 않는다.
    private const float AXIS_ALIGNED_DOT = 0.9999f;

    private const float PALETTE_CELL_WIDTH = 76f;
    private const float PALETTE_CELL_HEIGHT = 72f;

    [SerializeField] private List<GameObject> wallPrefabs = new List<GameObject>();
    [SerializeField] private int wallIndex;
    [SerializeField] private WallPickMode wallPickMode = WallPickMode.Selected;
    [SerializeField] private int randomSeed = 1;

    [SerializeField] private List<GameObject> cornerPrefabs = new List<GameObject>();
    [SerializeField] private int cornerIndex = -1;

    [SerializeField] private List<GameObject> doorPrefabs = new List<GameObject>();
    [SerializeField] private int doorIndex;

    [SerializeField] private MeasureSource measureSource = MeasureSource.ColliderFirst;
    [SerializeField] private LengthAxis lengthAxis = LengthAxis.Auto;
    [SerializeField] private float lengthOverride = 0f;
    [SerializeField] private float spacingOffset = 0f;
    [SerializeField] private WallFillMode wallFillMode = WallFillMode.Pieces;
    [SerializeField] private bool flipWall;

    [SerializeField] private CornerAnchor cornerAnchor = CornerAnchor.BoundsCenter;
    [SerializeField] private float cornerInset = -1f;
    [SerializeField] private float cornerYaw = 0f;
    [SerializeField] private bool rotateOnLeftTurn = true;
    [SerializeField] private bool endCaps;
    [SerializeField] private bool autoCornerJoint = true;
    [SerializeField] private float jointThickness = 0f;

    [SerializeField] private bool flipDoor;
    [SerializeField] private bool fitDoorToSlot = true;

    [SerializeField] private Transform parent;
    [SerializeField] private bool groupPerPath = true;
    [SerializeField] private float angleSnapStep = 1f;
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private LayerMask groundMask = ~0;

    // 0이면 자유 길이, 음수면 담 조각 간격 단위
    [SerializeField] private float lengthSnapStep = 0f;
    [SerializeField] private bool smartGuides = true;
    [SerializeField] private bool showMeasurements = true;
    [SerializeField] private bool mirrorEnabled;
    [SerializeField] private MirrorAxis mirrorAxis = MirrorAxis.AlongZ;
    [SerializeField] private Vector3 mirrorOrigin;

    [SerializeField] private bool painting;


    // =========================================================
    // State
    // =========================================================

    private class PreviewMesh
    {
        public Mesh mesh;
        public Matrix4x4 matrix;
        public Material[] materials;
    }

    // 프리팹 루트 기준으로 측정한 크기 정보
    private class PrefabMetrics
    {
        // 길이 방향 축 (X 또는 Z)과 그 축을 +Z로 돌리는 회전
        public Vector3 axis;
        public Quaternion align;
        public float min;
        public float max;

        // 두께 방향 축. 피벗이 담 중심선에서 비켜나 있는 만큼(perpCenter)을 보정한다.
        public Vector3 perpAxis;
        public float perpCenter;
        public float thickness;
        public bool measuredByCollider;

        // 미리보기용 렌더러 바운즈 (지붕 등 튀어나온 부분 포함)
        public Bounds bounds;
        public readonly List<PreviewMesh> meshes = new List<PreviewMesh>();

        public float Length => max - min;
        public float Center => (min + max) * 0.5f;

        // 루트 공간에서 피벗 → 메시 중심(바닥 높이) 벡터. stretch는 길이 방향 늘이기 비율.
        public Vector3 CenterOffset(Vector3 stretch)
        {
            return Vector3.Scale(stretch, axis * Center + perpAxis * perpCenter);
        }

        // stretch가 적용된 실제 길이
        public float StretchedLength(Vector3 stretch)
        {
            return Length * Vector3.Dot(stretch, axis);
        }
    }

    // 배치 예정인 조각 하나 (담 또는 모서리)
    private class PlannedPiece
    {
        public GameObject prefab;
        public PrefabMetrics metrics;
        public Vector3 position;
        public Quaternion rotation;

        // 프리팹 루트 스케일 위에 곱해지는 늘이기 비율
        public Vector3 stretch;
    }

    private readonly Dictionary<GameObject, PrefabMetrics> metricsCache =
        new Dictionary<GameObject, PrefabMetrics>();

    private readonly List<Vector3> pathPoints = new List<Vector3>();

    private SerializedObject serializedWindow;
    private GUIStyle paletteStyle;
    private Vector2 scroll;

    // 마우스 점이 어떤 기준에 붙었는지 (씬 뷰에 가이드 선을 그리는 데 쓴다)
    private struct SnapGuides
    {
        public bool snappedToAnchor;
        public Vector3 anchor;

        public bool alignedX;
        public Vector3 anchorX;

        public bool alignedZ;
        public Vector3 anchorZ;
    }

    private bool dragging;
    private GameObject hoverTarget;

    // 씬에 있는 팔레트 담·문의 양 끝점, 모서리 중심. 정렬 가이드 기준점으로 쓴다.
    private readonly List<Vector3> anchors = new List<Vector3>();
    private bool anchorsDirty = true;


    // =========================================================
    // Menu / Lifecycle
    // =========================================================

    [MenuItem("Tools/Project Shaman/Wall Painter")]
    private static void Open()
    {
        GetWindow<WallPainterWindow>("Wall Painter");
    }


    private void OnEnable()
    {
        serializedWindow = new SerializedObject(this);
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.hierarchyChanged += MarkAnchorsDirty;
        Undo.undoRedoPerformed += MarkAnchorsDirty;
    }


    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.hierarchyChanged -= MarkAnchorsDirty;
        Undo.undoRedoPerformed -= MarkAnchorsDirty;
        ResetPath();
    }


    private void MarkAnchorsDirty()
    {
        anchorsDirty = true;
    }


    // =========================================================
    // Window GUI
    // =========================================================

    private void OnGUI()
    {
        if (paletteStyle == null)
        {
            paletteStyle = new GUIStyle(GUI.skin.button)
            {
                imagePosition = ImagePosition.ImageAbove,
                fixedHeight = PALETTE_CELL_HEIGHT,
                fontSize = 9
            };
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        serializedWindow.Update();

        DrawPaintToggle();

        EditorGUILayout.Space();
        wallIndex = DrawPalette("담", "wallPrefabs", wallPrefabs, wallIndex, false);
        DrawProperty("wallPickMode", "담 고르기", "Random은 등록된 담을 섞어서 깐다. 씬 뷰에서 R로 다시 섞기.");

        EditorGUILayout.Space();
        cornerIndex = DrawPalette("모서리", "cornerPrefabs", cornerPrefabs, cornerIndex, true);

        EditorGUILayout.Space();
        doorIndex = DrawPalette("문", "doorPrefabs", doorPrefabs, doorIndex, false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("담 조각", EditorStyles.boldLabel);
        DrawProperty("measureSource", "크기 측정 기준", "ColliderFirst: 박스 콜라이더가 있으면 그 크기로 간격·두께를 잰다. Mesh: 지붕까지 포함한 메시 크기.");
        DrawProperty("lengthAxis", "길이 방향 축", "Auto는 X/Z 중 더 긴 쪽을 담 길이 방향으로 본다.");
        DrawProperty("lengthOverride", "조각 길이 고정 (0=자동)", "측정값이 어긋날 때 직접 입력한다. 모든 담에 적용.");
        DrawProperty("spacingOffset", "간격 보정 (m)", "+면 조각 사이에 틈, -면 겹침.");
        DrawProperty("wallFillMode", "구간 채우기", "Pieces: 원래 길이에 가까운 조각 여러 개를 늘이거나 줄여 채운다. Single: 구간 전체를 조각 하나로 늘인다.");
        DrawProperty("flipWall", "담 앞뒤 뒤집기");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("모서리", EditorStyles.boldLabel);
        DrawProperty("autoCornerJoint", "모서리 자동 맞대기", "모서리 프리팹이 없을 때, 담 두께만큼 늘이고 줄여서 꺾이는 곳을 빈틈없이 맞댄다.");
        DrawProperty("jointThickness", "맞대기 두께 (0=자동)", "모서리에서 담이 튀어나오면 줄이고, 바깥 모서리가 비면 늘린다.");
        DrawProperty("cornerAnchor", "기준점", "BoundsCenter: 메시 중심을 꼭짓점에 (기둥). Pivot: 프리팹 피벗을 꼭짓점에 (L자 조각).");
        DrawProperty("cornerInset", "모서리 여백 (-1=자동)", "꼭짓점에서 이 거리만큼 떨어진 곳부터 담을 깐다.");
        DrawProperty("cornerYaw", "모서리 회전 보정 (°)");
        DrawProperty("rotateOnLeftTurn", "왼쪽으로 꺾일 때 90° 추가", "L자 모서리 조각이 꺾이는 방향을 따라가게 한다.");
        DrawProperty("endCaps", "경로 양 끝에도 배치");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("문", EditorStyles.boldLabel);
        DrawProperty("flipDoor", "문 앞뒤 뒤집기");
        DrawProperty("fitDoorToSlot", "문 폭을 담 조각에 맞추기", "켜면 문을 담 조각 길이만큼 늘이거나 줄여 빈자리 없이 채운다. 끄면 문 원래 크기로 가운데에 놓는다.");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("배치", EditorStyles.boldLabel);
        DrawProperty("parent", "부모 오브젝트", "비워 두면 씬 루트에 생성된다.");
        DrawProperty("groupPerPath", "경로마다 그룹 생성", "한 번 확정한 경로를 빈 오브젝트 하나로 묶는다.");
        DrawProperty("angleSnapStep", "각도 스냅 단위 (°, 0=끔)", "꼭짓점 방향을 이 각도 단위로 맞춘다. Ctrl을 누르면 스냅 없이 자유 각도.");
        DrawAngleSnapPresets();
        DrawProperty("snapToGround", "바닥 높이 맞추기");
        DrawProperty("groundMask", "바닥 레이어", "지형만 고르면 지붕·집 콜라이더에 걸리지 않는다.");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("정렬 · 대칭", EditorStyles.boldLabel);
        DrawProperty("lengthSnapStep", "길이 스냅 (m, 0=자유)", "구간 길이를 이 단위로 반올림한다. 양쪽 담 길이를 똑같이 맞출 때 쓴다.");
        DrawLengthSnapPresets();
        DrawProperty("smartGuides", "정렬 가이드", "씬에 깔린 담·문 끝점과 X/Z가 맞으면 점선이 뜨면서 붙는다. 끝점 근처에선 끝점에 붙는다.");
        DrawProperty("showMeasurements", "치수 표시", "그리는 중 구간마다 길이·조각 수, 가이드 사이 간격을 표시한다.");
        DrawProperty("mirrorEnabled", "대칭 배치", "대칭선 반대편에 같은 담을 뒤집어서 함께 깐다. 씬 뷰에서 M으로 대칭선 위치 지정.");

        using (new EditorGUI.DisabledScope(!mirrorEnabled))
        {
            DrawProperty("mirrorAxis", "대칭선 방향", "AlongZ: Z축 방향 선 기준 좌우 반전. AlongX: X축 방향 선 기준 앞뒤 반전. 씬 뷰에서 Shift+M으로 전환.");
            DrawProperty("mirrorOrigin", "대칭선 위치");
        }

        if (serializedWindow.ApplyModifiedProperties())
        {
            metricsCache.Clear();
            MarkAnchorsDirty();
            SceneView.RepaintAll();
        }

        DrawMeasureInfo();

        EditorGUILayout.HelpBox(
            "클릭(또는 드래그): 꼭짓점 추가\n" +
            "Enter / 더블클릭: 경로 확정 · 첫 점 클릭: 닫힌 경로로 확정\n" +
            "Backspace: 마지막 꼭짓점 삭제 · Esc: 취소, 한 번 더 누르면 종료\n" +
            "Ctrl: 모든 스냅(각도·길이·가이드) 잠시 해제 · R: 랜덤 다시 섞기\n" +
            "M: 마우스 위치에 대칭선 놓기 · Shift+M: 대칭선 방향 전환\n" +
            "Shift+클릭: 담 조각을 선택한 문으로 교체\n" +
            "Ctrl+Z: 경로 / 문 교체 한 번 단위로 되돌리기",
            MessageType.Info
        );

        EditorGUILayout.EndScrollView();

        if (AssetPreview.IsLoadingAssetPreviews())
        {
            Repaint();
        }
    }


    private void DrawPaintToggle()
    {
        using (new EditorGUI.DisabledScope(GetSelected(wallPrefabs, 0) == null))
        {
            bool nextPainting = GUILayout.Toggle(
                painting,
                painting ? "배치 중 (Esc로 종료)" : "배치 시작",
                "Button",
                GUILayout.Height(30f)
            );

            if (nextPainting != painting)
            {
                painting = nextPainting;
                ResetPath();
                SceneView.RepaintAll();
            }
        }
    }


    // 프리팹 목록을 썸네일 그리드로 보여 주고 선택된 인덱스를 돌려준다.
    // allowNone이면 맨 앞에 "없음"이 붙고 -1을 돌려줄 수 있다.
    private int DrawPalette(
        string label,
        string propertyName,
        List<GameObject> prefabs,
        int selected,
        bool allowNone
    )
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        List<GUIContent> contents = new List<GUIContent>();

        if (allowNone)
        {
            contents.Add(new GUIContent("없음"));
        }

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null)
            {
                contents.Add(new GUIContent("(비어 있음)"));
                continue;
            }

            Texture preview = AssetPreview.GetAssetPreview(prefab);

            if (preview == null)
            {
                preview = AssetPreview.GetMiniThumbnail(prefab);
            }

            contents.Add(new GUIContent(prefab.name, preview));
        }

        int offset = allowNone ? 1 : 0;
        int gridIndex = Mathf.Clamp(selected + offset, 0, Mathf.Max(0, contents.Count - 1));

        if (contents.Count > 0)
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 30f) / PALETTE_CELL_WIDTH));
            gridIndex = GUILayout.SelectionGrid(gridIndex, contents.ToArray(), columns, paletteStyle);
        }
        else
        {
            EditorGUILayout.HelpBox("Project 창에서 프리팹을 선택하고 아래 버튼을 누르세요.", MessageType.None);
        }

        if (GUILayout.Button($"Project 창에서 선택한 프리팹을 {label}에 추가"))
        {
            AddSelectedPrefabs(prefabs);
        }

        EditorGUILayout.PropertyField(
            serializedWindow.FindProperty(propertyName),
            new GUIContent($"{label} 목록 편집"),
            true
        );

        return gridIndex - offset;
    }


    private void AddSelectedPrefabs(List<GameObject> prefabs)
    {
        Undo.RecordObject(this, "Add Prefabs");

        foreach (GameObject prefab in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
        {
            if (prefab.transform.parent == null &&
                PrefabUtility.IsPartOfPrefabAsset(prefab) &&
                !prefabs.Contains(prefab))
            {
                prefabs.Add(prefab);
            }
        }

        // 직접 바꾼 목록을 SerializedObject 쪽에도 반영한다.
        serializedWindow.Update();
        MarkAnchorsDirty();
    }


    private void DrawMeasureInfo()
    {
        GameObject wall = GetSelected(wallPrefabs, wallIndex);
        GameObject corner = GetActiveCorner();

        if (wall == null)
        {
            return;
        }

        PrefabMetrics wallMetrics = GetMetrics(wall);
        string axisName = wallMetrics.axis == Vector3.right ? "X" : "Z";
        string source = wallMetrics.measuredByCollider ? "콜라이더" : "메시";
        string info =
            $"{wall.name} ({source} 기준): 조각 길이 {GetWallLength(wallMetrics):0.00}m (축 {axisName}), " +
            $"두께 {wallMetrics.thickness:0.00}m, 배치 간격 {GetStep(wallMetrics):0.00}m, 맞대기 두께 {GetJointThickness():0.00}m";

        if (corner != null)
        {
            PrefabMetrics cornerMetrics = GetMetrics(corner);
            Vector3 size = cornerMetrics.bounds.size;
            info += $"\n{corner.name}: 바닥 크기 {size.x:0.00} × {size.z:0.00}m, 모서리 여백 {GetCornerInset(cornerMetrics):0.00}m";
        }

        EditorGUILayout.HelpBox(info, MessageType.None);
    }


    private void DrawAngleSnapPresets()
    {
        SerializedProperty property = serializedWindow.FindProperty("angleSnapStep");

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(" ");

            foreach (float preset in ANGLE_SNAP_PRESETS)
            {
                bool selected = Mathf.Approximately(property.floatValue, preset);
                string label = preset > 0f ? $"{preset}°" : "끔";

                if (GUILayout.Toggle(selected, label, EditorStyles.miniButton) && !selected)
                {
                    property.floatValue = preset;
                }
            }
        }
    }


    private void DrawLengthSnapPresets()
    {
        SerializedProperty property = serializedWindow.FindProperty("lengthSnapStep");

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(" ");

            foreach (float preset in LENGTH_SNAP_PRESETS)
            {
                bool selected = Mathf.Approximately(property.floatValue, preset);
                string label = preset > 0f ? $"{preset}m" : preset < 0f ? "조각 단위" : "자유";

                if (GUILayout.Toggle(selected, label, EditorStyles.miniButton) && !selected)
                {
                    property.floatValue = preset;
                }
            }
        }
    }


    private void DrawProperty(string propertyName, string label, string tooltip = null)
    {
        EditorGUILayout.PropertyField(
            serializedWindow.FindProperty(propertyName),
            new GUIContent(label, tooltip)
        );
    }


    // =========================================================
    // Scene GUI
    // =========================================================

    private void OnSceneGUI(SceneView view)
    {
        if (!painting)
        {
            return;
        }

        Event e = Event.current;
        EventType type = e.type;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        // 씬 뷰 기본 클릭 선택을 막는다.
        if (type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(controlId);
        }

        bool nearFirst = IsNearFirstPoint(e.mousePosition);
        bool hasMousePoint = TryGetMousePoint(e.mousePosition, out Vector3 mousePoint);
        SnapGuides guides = default;

        if (hasMousePoint)
        {
            mousePoint = nearFirst ? pathPoints[0] : SnapPoint(mousePoint, e.control, out guides);
        }

        switch (type)
        {
            case EventType.MouseMove:
                hoverTarget = e.shift && pathPoints.Count == 0 ? FindDoorTarget(e.mousePosition) : null;
                break;

            case EventType.MouseDown:
                if (e.button != 0 || e.alt)
                {
                    break;
                }

                if (pathPoints.Count == 0 && e.shift)
                {
                    ReplaceWithDoor(FindDoorTarget(e.mousePosition));
                    hoverTarget = null;
                    e.Use();
                    break;
                }

                // 더블클릭의 첫 클릭에서 이미 꼭짓점이 찍혔으므로 확정만 한다.
                if (pathPoints.Count > 0 && e.clickCount >= 2)
                {
                    FinishPath(false);
                    e.Use();
                    break;
                }

                if (nearFirst)
                {
                    FinishPath(true);
                    e.Use();
                    break;
                }

                if (!hasMousePoint)
                {
                    break;
                }

                if (pathPoints.Count == 0)
                {
                    // 첫 점은 드래그로 두 번째 점까지 한 번에 찍을 수 있다.
                    dragging = true;
                    GUIUtility.hotControl = controlId;
                }

                AddPathPoint(mousePoint);
                e.Use();
                break;

            case EventType.MouseDrag:
                if (dragging && GUIUtility.hotControl == controlId)
                {
                    e.Use();
                }

                break;

            case EventType.MouseUp:
                if (!dragging || e.button != 0)
                {
                    break;
                }

                dragging = false;
                GUIUtility.hotControl = 0;

                if (hasMousePoint)
                {
                    AddPathPoint(mousePoint);
                }

                e.Use();
                break;

            case EventType.KeyDown:
                HandleKey(e, hasMousePoint, mousePoint);
                break;

            case EventType.Repaint:
                DrawScenePreview(view, hasMousePoint, mousePoint, nearFirst, guides);
                break;
        }

        if (type == EventType.MouseMove || type == EventType.MouseDrag)
        {
            view.Repaint();
        }
    }


    private void HandleKey(Event e, bool hasMousePoint, Vector3 mousePoint)
    {
        switch (e.keyCode)
        {
            case KeyCode.M:
                if (e.shift)
                {
                    mirrorAxis = mirrorAxis == MirrorAxis.AlongZ ? MirrorAxis.AlongX : MirrorAxis.AlongZ;
                }
                else if (hasMousePoint)
                {
                    mirrorOrigin = mousePoint;
                }

                mirrorEnabled = true;
                Repaint();
                e.Use();
                break;

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                if (pathPoints.Count > 0)
                {
                    FinishPath(false);
                    e.Use();
                }

                break;

            case KeyCode.Backspace:
                if (pathPoints.Count > 0)
                {
                    pathPoints.RemoveAt(pathPoints.Count - 1);
                    e.Use();
                }

                break;

            case KeyCode.R:
                if (wallPickMode == WallPickMode.Random)
                {
                    RerollSeed();
                    e.Use();
                }

                break;

            case KeyCode.Escape:
                if (pathPoints.Count > 0)
                {
                    ResetPath();
                }
                else
                {
                    painting = false;
                    hoverTarget = null;
                    Repaint();
                }

                e.Use();
                break;
        }
    }


    private void DrawScenePreview(
        SceneView view,
        bool hasMousePoint,
        Vector3 mousePoint,
        bool nearFirst,
        SnapGuides guides
    )
    {
        if (hoverTarget != null)
        {
            DrawHoverTarget(hoverTarget, view.camera);
        }

        if (mirrorEnabled)
        {
            DrawMirrorLine();
        }

        if (hasMousePoint)
        {
            DrawGuides(mousePoint, guides);
        }

        if (pathPoints.Count == 0)
        {
            if (hasMousePoint)
            {
                Handles.color = Color.cyan;
                Handles.DrawWireDisc(mousePoint, Vector3.up, HandleUtility.GetHandleSize(mousePoint) * 0.15f);

                if (mirrorEnabled)
                {
                    Handles.color = Color.magenta;
                    Vector3 mirrored = Mirror(mousePoint);
                    Handles.DrawWireDisc(mirrored, Vector3.up, HandleUtility.GetHandleSize(mirrored) * 0.15f);
                }
            }

            return;
        }

        List<Vector3> previewPoints = new List<Vector3>(pathPoints);
        bool closed = nearFirst;

        if (hasMousePoint && !nearFirst)
        {
            previewPoints.Add(mousePoint);
        }

        if (previewPoints.Count >= 2)
        {
            List<PlannedPiece> pieces = BuildPath(previewPoints, closed);
            DrawPlannedPieces(pieces, view.camera, Color.cyan);

            int total = pieces.Count;

            if (mirrorEnabled)
            {
                List<Vector3> mirroredPoints = MirrorPath(previewPoints);
                List<PlannedPiece> mirroredPieces = BuildPath(mirroredPoints, closed);
                DrawPlannedPieces(mirroredPieces, view.camera, Color.magenta);
                DrawPolyline(mirroredPoints, closed, Color.magenta);
                total += mirroredPieces.Count;
            }

            if (showMeasurements)
            {
                DrawSegmentMeasurements(previewPoints, closed);
            }

            Vector3 labelPoint = previewPoints[previewPoints.Count - 1];
            Handles.Label(
                labelPoint + Vector3.up * HandleUtility.GetHandleSize(labelPoint) * 0.5f,
                closed
                    ? $"{total}개 · 클릭하면 닫힌 경로로 확정"
                    : $"{total}개 · Enter 확정 / Backspace 되돌리기 / Esc 취소"
            );
        }

        DrawPolyline(previewPoints, closed, Color.cyan);

        for (int i = 0; i < pathPoints.Count; i++)
        {
            Vector3 point = pathPoints[i];
            float size = HandleUtility.GetHandleSize(point) * (i == 0 && nearFirst ? 0.14f : 0.08f);

            Handles.color = i == 0 ? Color.yellow : Color.cyan;
            Handles.DrawSolidDisc(point, Vector3.up, size);
        }
    }


    private static void DrawPolyline(List<Vector3> points, bool closed, Color color)
    {
        List<Vector3> line = new List<Vector3>(points);

        if (closed)
        {
            line.Add(points[0]);
        }

        Handles.color = color;
        Handles.DrawAAPolyLine(3f, line.ToArray());
    }


    // 구간마다 가운데에 "길이 · 조각 수"를 띄운다.
    private void DrawSegmentMeasurements(List<Vector3> points, bool closed)
    {
        GameObject wall = GetSelected(wallPrefabs, wallIndex);

        if (wall == null)
        {
            return;
        }

        float step = GetStep(GetMetrics(wall));
        int segmentCount = closed ? points.Count : points.Count - 1;

        for (int s = 0; s < segmentCount; s++)
        {
            Vector3 a = points[s];
            Vector3 b = points[(s + 1) % points.Count];
            float length = HorizontalDistance(a, b);

            if (length < MIN_STEP)
            {
                continue;
            }

            // AddWallSpan과 같은 규칙으로 조각 수와 늘임 비율을 계산한다 (모서리 맞대기 보정 전 기준).
            int pieces = wallFillMode == WallFillMode.Single ? 1 : Mathf.Max(1, Mathf.RoundToInt(length / step));
            float stretch = length / (pieces * step);
            string count = $"{pieces}조각 · 늘임 {stretch * 100f:0}%";

            Vector3 middle = (a + b) * 0.5f;
            Handles.Label(
                middle + Vector3.up * HandleUtility.GetHandleSize(middle) * 0.25f,
                $"{length:0.00}m · {count} · 방향 {GetYaw(a, b):0.#}°"
            );

            // 이 구간이 시작되는 꼭짓점에서 직전 구간 대비 몇 도 꺾였는지
            bool hasIncoming = closed || s > 0;

            if (hasIncoming)
            {
                Vector3 before = points[(s - 1 + points.Count) % points.Count];
                float turn = Mathf.DeltaAngle(GetYaw(before, a), GetYaw(a, b));

                if (Mathf.Abs(turn) > 0.05f)
                {
                    Handles.Label(
                        a + Vector3.up * HandleUtility.GetHandleSize(a) * 0.4f,
                        $"꺾임 {Mathf.Abs(turn):0.#}° {(turn > 0f ? "오른쪽" : "왼쪽")}"
                    );
                }
            }
        }
    }


    // 위에서 봤을 때 +Z를 0°로 시계 방향 각도 (0~360)
    private static float GetYaw(Vector3 from, Vector3 to)
    {
        float yaw = Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
        return yaw < 0f ? yaw + 360f : yaw;
    }


    private void DrawGuides(Vector3 point, SnapGuides guides)
    {
        if (guides.snappedToAnchor)
        {
            Handles.color = Color.green;
            Handles.DrawWireDisc(guides.anchor, Vector3.up, HandleUtility.GetHandleSize(guides.anchor) * 0.2f);
        }

        if (guides.alignedX)
        {
            DrawGuideLine(guides.anchorX, point);
        }

        if (guides.alignedZ)
        {
            DrawGuideLine(guides.anchorZ, point);
        }
    }


    // 기준점 → 현재 점 점선과 둘 사이 간격
    private void DrawGuideLine(Vector3 anchor, Vector3 point)
    {
        Handles.color = new Color(1f, 0.5f, 0f);
        Handles.DrawDottedLine(anchor, point, 4f);
        Handles.DrawWireDisc(anchor, Vector3.up, HandleUtility.GetHandleSize(anchor) * 0.08f);

        if (showMeasurements)
        {
            Vector3 middle = (anchor + point) * 0.5f;
            Handles.Label(middle, $"↔ {HorizontalDistance(anchor, point):0.00}m");
        }
    }


    private void DrawMirrorLine()
    {
        Vector3 direction = mirrorAxis == MirrorAxis.AlongZ ? Vector3.forward : Vector3.right;
        Vector3 origin = mirrorOrigin;

        Handles.color = Color.magenta;
        Handles.DrawDottedLine(origin - direction * MIRROR_LINE_LENGTH, origin + direction * MIRROR_LINE_LENGTH, 6f);
        Handles.DrawSolidDisc(origin, Vector3.up, HandleUtility.GetHandleSize(origin) * 0.06f);
    }


    private static void DrawPreviewMesh(PreviewMesh preview, Matrix4x4 matrix, Camera camera)
    {
        if (preview.materials.Length == 0)
        {
            return;
        }

        for (int i = 0; i < preview.mesh.subMeshCount; i++)
        {
            Material material = preview.materials[Mathf.Min(i, preview.materials.Length - 1)];

            if (material != null)
            {
                Graphics.DrawMesh(preview.mesh, matrix, material, 0, camera, i);
            }
        }
    }


    private static void DrawPlannedPieces(List<PlannedPiece> pieces, Camera camera, Color color)
    {
        foreach (PlannedPiece piece in pieces)
        {
            Matrix4x4 world = Matrix4x4.TRS(piece.position, piece.rotation, piece.stretch);

            foreach (PreviewMesh preview in piece.metrics.meshes)
            {
                DrawPreviewMesh(preview, world * preview.matrix, camera);
            }

            using (new Handles.DrawingScope(color, world))
            {
                Handles.DrawWireCube(piece.metrics.bounds.center, piece.metrics.bounds.size);
            }
        }
    }


    private void DrawHoverTarget(GameObject target, Camera camera)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        GameObject door = GetSelected(doorPrefabs, doorIndex);

        Handles.color = Color.yellow;
        Handles.DrawWireCube(bounds.center, bounds.size);
        Handles.Label(
            bounds.center + Vector3.up * bounds.extents.y,
            $"{target.name} → {(door != null ? door.name : "문 팔레트가 비어 있음")}"
        );

        if (door != null)
        {
            DrawPlannedPieces(PlanDoor(target, door), camera, Color.yellow);
        }
    }


    // =========================================================
    // Path
    // =========================================================

    private void AddPathPoint(Vector3 point)
    {
        // 같은 자리를 두 번 찍은 경우(드래그 없이 클릭 등)는 무시한다.
        if (pathPoints.Count > 0 && HorizontalDistance(pathPoints[pathPoints.Count - 1], point) < MIN_STEP)
        {
            return;
        }

        pathPoints.Add(point);
    }


    private void FinishPath(bool closed)
    {
        int required = closed ? 3 : 2;

        if (pathPoints.Count >= required)
        {
            List<List<Vector3>> paths = new List<List<Vector3>> { new List<Vector3>(pathPoints) };

            if (mirrorEnabled)
            {
                paths.Add(MirrorPath(pathPoints));
            }

            PlacePaths(paths, closed);
        }

        ResetPath();
    }


    private void ResetPath()
    {
        pathPoints.Clear();
        hoverTarget = null;

        if (dragging)
        {
            dragging = false;
            GUIUtility.hotControl = 0;
        }
    }


    private bool IsNearFirstPoint(Vector2 mousePosition)
    {
        if (pathPoints.Count < 3)
        {
            return false;
        }

        Vector2 screen = HandleUtility.WorldToGUIPoint(pathPoints[0]);
        return Vector2.Distance(screen, mousePosition) <= CLOSE_SNAP_PIXELS;
    }


    private void RerollSeed()
    {
        randomSeed = Random.Range(1, int.MaxValue);
        SceneView.RepaintAll();
    }


    // =========================================================
    // Layout
    // =========================================================

    private List<PlannedPiece> BuildPath(List<Vector3> points, bool closed)
    {
        List<PlannedPiece> pieces = new List<PlannedPiece>();

        int pointCount = points.Count;
        int segmentCount = closed ? pointCount : pointCount - 1;

        if (segmentCount < 1 || GetSelected(wallPrefabs, wallIndex) == null)
        {
            return pieces;
        }

        GameObject corner = GetActiveCorner();
        PrefabMetrics cornerMetrics = corner != null ? GetMetrics(corner) : null;
        float inset = cornerMetrics != null ? GetCornerInset(cornerMetrics) : 0f;

        Vector3[] directions = new Vector3[segmentCount];
        float[] lengths = new float[segmentCount];

        for (int s = 0; s < segmentCount; s++)
        {
            Vector3 flat = points[(s + 1) % pointCount] - points[s];
            flat.y = 0f;

            lengths[s] = flat.magnitude;
            directions[s] = lengths[s] > 0.0001f ? flat / lengths[s] : Vector3.forward;
        }

        // 닫힌 경로는 모든 꼭짓점, 열린 경로는 중간 꼭짓점(+옵션: 양 끝)에 모서리를 둔다.
        bool[] hasCorner = new bool[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            bool isEnd = !closed && (i == 0 || i == pointCount - 1);
            hasCorner[i] = corner != null && (!isEnd || endCaps);
        }

        // 구간 s는 [spanStarts[s], lengths[s] + spanEndExtras[s]] 를 채운다.
        float[] spanStarts = new float[segmentCount];
        float[] spanEndExtras = new float[segmentCount];
        float jointHalfThickness = GetJointThickness() * 0.5f;

        for (int i = 0; i < pointCount; i++)
        {
            int incoming = closed ? (i - 1 + segmentCount) % segmentCount : i - 1;
            int outgoing = i < segmentCount ? i : -1;

            if (hasCorner[i])
            {
                // 모서리 프리팹 자리만큼 양쪽 담을 물린다.
                if (incoming >= 0) spanEndExtras[incoming] = -inset;
                if (outgoing >= 0) spanStarts[outgoing] = inset;
                continue;
            }

            if (!autoCornerJoint || incoming < 0 || outgoing < 0)
            {
                continue;
            }

            // 맞대기: 들어오는 담은 바깥 모서리까지 늘이고, 나가는 담은 그만큼 물려서 시작한다.
            // 바깥 모서리까지 거리 = 반두께 × tan(꺾인 각 / 2), 직각이면 정확히 반두께.
            float turn = Mathf.Min(Vector3.Angle(directions[incoming], directions[outgoing]), MAX_JOINT_TURN);
            float joint = jointHalfThickness * Mathf.Tan(turn * 0.5f * Mathf.Deg2Rad);

            spanEndExtras[incoming] = joint;
            spanStarts[outgoing] = joint;
        }

        System.Random random = new System.Random(randomSeed);

        for (int s = 0; s < segmentCount; s++)
        {
            AddWallSpan(
                pieces,
                points[s],
                points[(s + 1) % pointCount],
                directions[s],
                lengths[s],
                spanStarts[s],
                lengths[s] + spanEndExtras[s],
                random
            );
        }

        for (int i = 0; i < pointCount; i++)
        {
            if (!hasCorner[i])
            {
                continue;
            }

            Vector3 directionIn;
            Vector3 directionOut;

            if (closed)
            {
                directionIn = directions[(i - 1 + segmentCount) % segmentCount];
                directionOut = directions[i];
            }
            else
            {
                directionIn = directions[Mathf.Max(0, i - 1)];
                directionOut = directions[Mathf.Min(segmentCount - 1, i)];
            }

            pieces.Add(PlanCorner(corner, cornerMetrics, points[i], directionIn, directionOut));
        }

        return pieces;
    }


    // 꼭짓점 a → b 사이의 [spanStart, spanEnd] 구간을 담 조각으로 채운다.
    private void AddWallSpan(
        List<PlannedPiece> pieces,
        Vector3 a,
        Vector3 b,
        Vector3 direction,
        float length,
        float spanStart,
        float spanEnd,
        System.Random random
    )
    {
        float spanLength = spanEnd - spanStart;

        if (spanLength < MIN_STEP)
        {
            return;
        }

        // 조각 수를 정한다. Single은 구간 전체를 한 조각으로,
        // Pieces는 다음 조각을 넣었을 때 절반 이상 넘치면 멈춘다 (= 반올림 개수).
        List<GameObject> chosen = new List<GameObject>();
        float total = 0f;

        while (chosen.Count < MAX_PIECES_PER_SPAN)
        {
            GameObject prefab = PickWall(random);
            float step = GetStep(GetMetrics(prefab));

            if (chosen.Count > 0 && (wallFillMode == WallFillMode.Single || total + step * 0.5f > spanLength))
            {
                break;
            }

            chosen.Add(prefab);
            total += step;
        }

        // 드래그한 길이가 기준이므로 조각을 늘이거나 줄여서 구간을 정확히 채운다.
        float factor = spanLength / total;
        float along = spanStart;

        foreach (GameObject prefab in chosen)
        {
            PrefabMetrics metrics = GetMetrics(prefab);
            float step = GetStep(metrics) * factor;

            Vector3 stretch = metrics.axis == Vector3.right
                ? new Vector3(factor, 1f, 1f)
                : new Vector3(1f, 1f, factor);

            Quaternion rotation =
                Quaternion.LookRotation(direction, Vector3.up) *
                Quaternion.Euler(0f, flipWall ? 180f : 0f, 0f) *
                metrics.align;

            // 피벗 위치와 상관없이 조각의 중심이 자기 구간 중심, 경로 선 위에 오도록 맞춘다.
            float centerAlong = along + step * 0.5f;
            Vector3 center = a + direction * centerAlong;
            center.y = GetGroundHeight(center, Mathf.Lerp(a.y, b.y, centerAlong / length));

            pieces.Add(new PlannedPiece
            {
                prefab = prefab,
                metrics = metrics,
                position = center - rotation * metrics.CenterOffset(stretch),
                rotation = rotation,
                stretch = stretch
            });

            along += step;
        }
    }


    private PlannedPiece PlanCorner(
        GameObject corner,
        PrefabMetrics metrics,
        Vector3 vertex,
        Vector3 directionIn,
        Vector3 directionOut
    )
    {
        // 위에서 봤을 때 반시계로 꺾이면 왼쪽 회전
        bool leftTurn = Vector3.Cross(directionIn, directionOut).y < -0.01f;
        float yaw = cornerYaw + (rotateOnLeftTurn && leftTurn ? 90f : 0f);

        Quaternion rotation =
            Quaternion.LookRotation(directionIn, Vector3.up) *
            Quaternion.Euler(0f, yaw, 0f);

        Vector3 position = vertex;
        position.y = GetGroundHeight(vertex, vertex.y);

        if (cornerAnchor == CornerAnchor.BoundsCenter)
        {
            Vector3 center = metrics.bounds.center;
            center.y = 0f;
            position -= rotation * center;
        }

        return new PlannedPiece
        {
            prefab = corner,
            metrics = metrics,
            position = position,
            rotation = rotation,
            stretch = Vector3.one
        };
    }


    // =========================================================
    // Placement
    // =========================================================

    // 경로(대칭이면 대칭 경로까지)를 한 번의 Undo 단위로 배치한다.
    private void PlacePaths(List<List<Vector3>> paths, bool closed)
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);

        for (int p = 0; p < paths.Count; p++)
        {
            List<Vector3> points = paths[p];
            List<PlannedPiece> pieces = BuildPath(points, closed);

            if (pieces.Count == 0)
            {
                continue;
            }

            Transform container = parent;

            if (groupPerPath)
            {
                string groupName = closed ? "WallPath_Closed" : "WallPath";
                GameObject group = new GameObject(p == 0 ? groupName : $"{groupName}_Mirror");
                group.transform.position = points[0];
                Undo.RegisterCreatedObjectUndo(group, UNDO_NAME);

                if (parent != null)
                {
                    Undo.SetTransformParent(group.transform, parent, UNDO_NAME);
                }

                container = group.transform;
            }

            foreach (PlannedPiece piece in pieces)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(piece.prefab, container);
                instance.transform.SetPositionAndRotation(piece.position, piece.rotation);
                instance.transform.localScale = Vector3.Scale(piece.prefab.transform.localScale, piece.stretch);
                Undo.RegisterCreatedObjectUndo(instance, UNDO_NAME);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        // 다음 경로는 다른 조합으로 섞이도록 한다.
        if (wallPickMode == WallPickMode.Random)
        {
            RerollSeed();
        }
    }


    // Shift+클릭 대상: 담 팔레트에 등록된 프리팹 인스턴스의 최상위 루트
    private GameObject FindDoorTarget(Vector2 mousePosition)
    {
        GameObject picked = HandleUtility.PickGameObject(mousePosition, false);

        if (picked == null)
        {
            return null;
        }

        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(picked);

        if (root == null)
        {
            return null;
        }

        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(root);
        return source != null && wallPrefabs.Contains(source) ? root : null;
    }


    private void ReplaceWithDoor(GameObject target)
    {
        GameObject doorPrefab = GetSelected(doorPrefabs, doorIndex);

        if (doorPrefab == null)
        {
            ShowNotification(new GUIContent("문 팔레트가 비어 있습니다."));
            return;
        }

        if (target == null)
        {
            return;
        }

        List<PlannedPiece> pieces = PlanDoor(target, doorPrefab);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);

        Transform wall = target.transform;
        int siblingIndex = wall.GetSiblingIndex();

        foreach (PlannedPiece piece in pieces)
        {
            GameObject instance = wall.parent != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(piece.prefab, wall.parent)
                : (GameObject)PrefabUtility.InstantiatePrefab(piece.prefab, target.scene);

            instance.transform.SetPositionAndRotation(piece.position, piece.rotation);
            instance.transform.localScale = Vector3.Scale(piece.prefab.transform.localScale, piece.stretch);
            instance.transform.SetSiblingIndex(siblingIndex++);
            Undo.RegisterCreatedObjectUndo(instance, UNDO_NAME);
        }

        Undo.DestroyObjectImmediate(target);
        Undo.CollapseUndoOperations(undoGroup);
    }


    // 담 조각 하나를 통째로 문 하나로 바꿀 배치를 계산한다.
    // 문은 담 조각의 중심, 담 두께의 가운데에 놓인다.
    private List<PlannedPiece> PlanDoor(GameObject target, GameObject doorPrefab)
    {
        List<PlannedPiece> pieces = new List<PlannedPiece>();

        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(target);
        PrefabMetrics wallMetrics = GetMetrics(source);
        PrefabMetrics doorMetrics = GetMetrics(doorPrefab);

        Transform wall = target.transform;
        Vector3 wallStretch = GetStretch(wall.localScale, source.transform.localScale);
        Vector3 center = wall.position + wall.rotation * wallMetrics.CenterOffset(wallStretch);
        float wallLength = wallMetrics.StretchedLength(wallStretch);

        // 담의 정렬 회전을 걷어내고 문의 정렬 회전을 다시 적용한다.
        Quaternion doorRotation =
            wall.rotation *
            Quaternion.Inverse(wallMetrics.align) *
            Quaternion.Euler(0f, flipDoor ? 180f : 0f, 0f) *
            doorMetrics.align;

        Vector3 doorStretch = Vector3.one;

        // 문 폭을 담 조각 길이에 맞춰 빈자리 없이 채운다.
        if (fitDoorToSlot && doorMetrics.Length > 0.001f)
        {
            doorStretch = AxisStretch(doorMetrics.axis, Vector3.one, wallLength / doorMetrics.Length);
        }

        pieces.Add(new PlannedPiece
        {
            prefab = doorPrefab,
            metrics = doorMetrics,
            position = center - doorRotation * doorMetrics.CenterOffset(doorStretch),
            rotation = doorRotation,
            stretch = doorStretch
        });

        return pieces;
    }


    // baseStretch에서 axis 방향 성분만 factor로 바꾼다.
    private static Vector3 AxisStretch(Vector3 axis, Vector3 baseStretch, float factor)
    {
        return axis == Vector3.right
            ? new Vector3(factor, baseStretch.y, baseStretch.z)
            : new Vector3(baseStretch.x, baseStretch.y, factor);
    }


    // =========================================================
    // Palette
    // =========================================================

    // 인덱스가 가리키는 프리팹, 비어 있으면 목록의 첫 유효 프리팹
    private static GameObject GetSelected(List<GameObject> prefabs, int index)
    {
        if (index >= 0 && index < prefabs.Count && prefabs[index] != null)
        {
            return prefabs[index];
        }

        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }


    private GameObject GetActiveCorner()
    {
        return cornerIndex >= 0 && cornerIndex < cornerPrefabs.Count
            ? cornerPrefabs[cornerIndex]
            : null;
    }


    private GameObject PickWall(System.Random random)
    {
        if (wallPickMode == WallPickMode.Selected)
        {
            return GetSelected(wallPrefabs, wallIndex);
        }

        List<GameObject> valid = wallPrefabs.FindAll(prefab => prefab != null);
        return valid[random.Next(valid.Count)];
    }


    private bool IsPaintedObject(GameObject target)
    {
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(target);

        if (root == null)
        {
            return false;
        }

        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(root);

        return source != null &&
               (wallPrefabs.Contains(source) || cornerPrefabs.Contains(source) || doorPrefabs.Contains(source));
    }


    // =========================================================
    // Raycast / Snap
    // =========================================================

    private bool TryGetMousePoint(Vector2 mousePosition, out Vector3 point)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        if (TryRaycastGround(ray, MOUSE_RAY_DISTANCE, out point))
        {
            return true;
        }

        // 콜라이더가 없는 곳은 마지막 꼭짓점 높이(없으면 0)의 평면을 쓴다.
        float planeY = pathPoints.Count > 0 ? pathPoints[pathPoints.Count - 1].y : 0f;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));

        if (plane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        return false;
    }


    private bool TryRaycastGround(Ray ray, float maxDistance, out Vector3 point)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, groundMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // 팔레트에 있는 담·모서리·문 위에 다음 담이 올라타지 않게 건너뛴다.
            if (IsPaintedObject(hit.collider.gameObject))
            {
                continue;
            }

            point = hit.point;
            return true;
        }

        point = default;
        return false;
    }


    private float GetGroundHeight(Vector3 point, float fallback)
    {
        if (!snapToGround)
        {
            return fallback;
        }

        Ray ray = new Ray(point + Vector3.up * GROUND_RAY_HEIGHT, Vector3.down);

        return TryRaycastGround(ray, GROUND_RAY_HEIGHT * 2f, out Vector3 hit)
            ? hit.y
            : fallback;
    }


    // 마우스 점에 스냅을 순서대로 적용한다: 각도 → 조각 단위 길이 → 정렬 가이드 → 끝점.
    // 뒤에 오는 것이 우선한다. freeMode(Ctrl)면 아무 스냅도 하지 않는다.
    private Vector3 SnapPoint(Vector3 point, bool freeMode, out SnapGuides guides)
    {
        guides = default;

        if (freeMode)
        {
            return point;
        }

        bool hasPrevious = pathPoints.Count > 0;
        Vector3 previous = hasPrevious ? pathPoints[pathPoints.Count - 1] : Vector3.zero;
        Vector3 direction = Vector3.zero;

        if (hasPrevious)
        {
            point = ApplyAngleSnap(previous, point);
            point = ApplyLengthSnap(previous, point, out direction);
        }

        if (!smartGuides)
        {
            return point;
        }

        RefreshAnchors();
        float threshold = HandleUtility.GetHandleSize(point) * GUIDE_SNAP_SCALE;

        if (TryFindNearestAnchor(point, threshold, previous, hasPrevious, out Vector3 nearest))
        {
            guides.snappedToAnchor = true;
            guides.anchor = nearest;
            return new Vector3(nearest.x, point.y, nearest.z);
        }

        // 구간 방향을 바꾸지 않는 좌표만 맞춘다 (X로 뻗는 중이면 x만, 첫 점이면 둘 다).
        bool canMoveX = !hasPrevious || Mathf.Abs(direction.x) > AXIS_ALIGNED_DOT;
        bool canMoveZ = !hasPrevious || Mathf.Abs(direction.z) > AXIS_ALIGNED_DOT;

        if (canMoveX && TryFindAlignedAnchor(point, threshold, true, previous, hasPrevious, out Vector3 anchorX))
        {
            point.x = anchorX.x;
            guides.alignedX = true;
            guides.anchorX = anchorX;
        }

        if (canMoveZ && TryFindAlignedAnchor(point, threshold, false, previous, hasPrevious, out Vector3 anchorZ))
        {
            point.z = anchorZ.z;
            guides.alignedZ = true;
            guides.anchorZ = anchorZ;
        }

        return point;
    }


    // 직전 점에서의 거리를 길이 스냅 단위로 반올림한다. 0이면 드래그한 길이 그대로.
    private Vector3 ApplyLengthSnap(Vector3 start, Vector3 end, out Vector3 direction)
    {
        Vector3 flat = end - start;
        flat.y = 0f;

        float distance = flat.magnitude;
        direction = distance > 0.0001f ? flat / distance : Vector3.zero;

        float step = lengthSnapStep;

        if (step < 0f)
        {
            GameObject wall = GetSelected(wallPrefabs, wallIndex);
            step = wall != null ? GetStep(GetMetrics(wall)) : 0f;
        }

        if (step <= 0f || distance < 0.0001f)
        {
            return end;
        }

        float snapped = Mathf.Max(1f, Mathf.Round(distance / step)) * step;

        Vector3 result = start + direction * snapped;
        result.y = end.y;
        return result;
    }


    // 기준점 후보: 씬의 담·문 끝점, 모서리 중심, 지금 그리는 경로의 꼭짓점, 대칭선 위치
    private IEnumerable<Vector3> GetGuideCandidates(Vector3 previous, bool hasPrevious)
    {
        foreach (Vector3 anchor in anchors)
        {
            yield return anchor;
        }

        foreach (Vector3 point in pathPoints)
        {
            // 직전 점에 맞추면 구간 길이가 0이 되므로 뺀다.
            if (!hasPrevious || HorizontalDistance(point, previous) > MIN_STEP)
            {
                yield return point;
            }
        }

        if (mirrorEnabled)
        {
            yield return mirrorOrigin;
        }
    }


    private bool TryFindNearestAnchor(
        Vector3 point,
        float threshold,
        Vector3 previous,
        bool hasPrevious,
        out Vector3 nearest
    )
    {
        nearest = default;
        float best = threshold;
        bool found = false;

        foreach (Vector3 candidate in GetGuideCandidates(previous, hasPrevious))
        {
            float distance = HorizontalDistance(candidate, point);

            if (distance < best)
            {
                best = distance;
                nearest = candidate;
                found = true;
            }
        }

        return found;
    }


    private bool TryFindAlignedAnchor(
        Vector3 point,
        float threshold,
        bool alignX,
        Vector3 previous,
        bool hasPrevious,
        out Vector3 anchor
    )
    {
        anchor = default;
        float best = threshold;
        float bestDistance = float.MaxValue;
        bool found = false;

        foreach (Vector3 candidate in GetGuideCandidates(previous, hasPrevious))
        {
            float offset = Mathf.Abs(alignX ? candidate.x - point.x : candidate.z - point.z);
            float distance = HorizontalDistance(candidate, point);

            // 같은 정도로 맞으면 가까운 기준점을 쓴다.
            if (offset < best || (found && Mathf.Approximately(offset, best) && distance < bestDistance))
            {
                best = offset;
                bestDistance = distance;
                anchor = candidate;
                found = true;
            }
        }

        return found;
    }


    private void RefreshAnchors()
    {
        if (!anchorsDirty)
        {
            return;
        }

        anchorsDirty = false;
        anchors.Clear();

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            GameObject instance = t.gameObject;

            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(instance))
            {
                continue;
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);

            if (source == null)
            {
                continue;
            }

            if (wallPrefabs.Contains(source) || doorPrefabs.Contains(source))
            {
                PrefabMetrics metrics = GetMetrics(source);
                Vector3 stretch = GetStretch(t.localScale, source.transform.localScale);
                Vector3 center = t.position + t.rotation * metrics.CenterOffset(stretch);
                Vector3 along = t.rotation * metrics.axis * (metrics.StretchedLength(stretch) * 0.5f);

                anchors.Add(center - along);
                anchors.Add(center + along);
            }
            else if (cornerPrefabs.Contains(source))
            {
                anchors.Add(t.position);
            }
        }
    }


    private Vector3 Mirror(Vector3 point)
    {
        if (mirrorAxis == MirrorAxis.AlongZ)
        {
            point.x = 2f * mirrorOrigin.x - point.x;
        }
        else
        {
            point.z = 2f * mirrorOrigin.z - point.z;
        }

        return point;
    }


    // 뒤집으면 진행 방향의 좌우가 바뀌므로 순서도 거꾸로 해서 담의 앞뒤를 원래 경로와 맞춘다.
    private List<Vector3> MirrorPath(List<Vector3> points)
    {
        List<Vector3> mirrored = points.ConvertAll(Mirror);
        mirrored.Reverse();
        return mirrored;
    }


    private Vector3 ApplyAngleSnap(Vector3 start, Vector3 end)
    {
        if (angleSnapStep <= 0f)
        {
            return end;
        }

        Vector3 flat = end - start;
        flat.y = 0f;

        if (flat.sqrMagnitude < 0.0001f)
        {
            return end;
        }

        float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        yaw = Mathf.Round(yaw / angleSnapStep) * angleSnapStep;

        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 snapped = start + direction * Vector3.Dot(flat, direction);
        snapped.y = end.y;

        return snapped;
    }


    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }


    // =========================================================
    // Prefab Metrics
    // =========================================================

    private float GetWallLength(PrefabMetrics metrics)
    {
        return lengthOverride > 0f ? lengthOverride : metrics.Length;
    }


    private float GetStep(PrefabMetrics metrics)
    {
        return Mathf.Max(MIN_STEP, GetWallLength(metrics) + spacingOffset);
    }


    private float GetCornerInset(PrefabMetrics metrics)
    {
        if (cornerInset >= 0f)
        {
            return cornerInset;
        }

        // 자동: 기둥처럼 중심 기준이면 바닥 크기의 절반, 피벗 기준이면 0
        Vector3 size = metrics.bounds.size;
        return cornerAnchor == CornerAnchor.BoundsCenter ? Mathf.Max(size.x, size.z) * 0.5f : 0f;
    }


    // 맞대기에 쓸 담 두께. 랜덤이면 섞이는 담 중 가장 두꺼운 것 기준.
    private float GetJointThickness()
    {
        if (jointThickness > 0f)
        {
            return jointThickness;
        }

        if (wallPickMode == WallPickMode.Selected)
        {
            GameObject wall = GetSelected(wallPrefabs, wallIndex);
            return wall != null ? GetMetrics(wall).thickness : 0f;
        }

        float thickness = 0f;

        foreach (GameObject wall in wallPrefabs)
        {
            if (wall != null)
            {
                thickness = Mathf.Max(thickness, GetMetrics(wall).thickness);
            }
        }

        return thickness;
    }


    private static Vector3 GetStretch(Vector3 scale, Vector3 prefabScale)
    {
        return new Vector3(
            Mathf.Abs(prefabScale.x) > 0.00001f ? scale.x / prefabScale.x : 1f,
            Mathf.Abs(prefabScale.y) > 0.00001f ? scale.y / prefabScale.y : 1f,
            Mathf.Abs(prefabScale.z) > 0.00001f ? scale.z / prefabScale.z : 1f
        );
    }


    private PrefabMetrics GetMetrics(GameObject prefab)
    {
        if (metricsCache.TryGetValue(prefab, out PrefabMetrics cached))
        {
            return cached;
        }

        PrefabMetrics metrics = new PrefabMetrics();
        Transform root = prefab.transform;

        // 인스턴스는 루트 스케일을 그대로 가지므로 스케일만 남긴 루트 공간에서 잰다.
        Matrix4x4 rootSpace = Matrix4x4.Scale(root.localScale) * root.worldToLocalMatrix;

        // LOD가 있으면 LOD0만 측정·미리보기에 쓴다.
        HashSet<Renderer> lod0 = null;
        LODGroup lodGroup = prefab.GetComponentInChildren<LODGroup>(true);

        if (lodGroup != null && lodGroup.lodCount > 0)
        {
            lod0 = new HashSet<Renderer>(lodGroup.GetLODs()[0].renderers);
        }

        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

        foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!renderer.enabled || !IsActiveInPrefab(renderer.transform, root))
            {
                continue;
            }

            if (lod0 != null && !lod0.Contains(renderer))
            {
                continue;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 local = rootSpace * renderer.transform.localToWorldMatrix;

            metrics.meshes.Add(new PreviewMesh
            {
                mesh = filter.sharedMesh,
                matrix = local,
                materials = renderer.sharedMaterials
            });

            EncapsulateMeshBounds(ref bounds, ref hasBounds, filter.sharedMesh.bounds, local);
        }

        metrics.bounds = bounds;

        // 길이·두께·중심은 박스 콜라이더로 잰다. 메시는 지붕(기와)이 몸통보다 넓게 튀어나와
        // 간격과 맞대기 길이가 커지기 때문 (Wall01c: 메시 2.24×0.99m, 콜라이더 2.14×0.5m).
        Bounds measure = bounds;

        if (measureSource == MeasureSource.ColliderFirst &&
            TryGetBoxColliderBounds(prefab, rootSpace, out Bounds colliderBounds))
        {
            measure = colliderBounds;
            metrics.measuredByCollider = true;
        }

        bool useX = lengthAxis == LengthAxis.X ||
                    (lengthAxis == LengthAxis.Auto && measure.size.x >= measure.size.z);

        metrics.axis = useX ? Vector3.right : Vector3.forward;
        metrics.align = useX
            ? Quaternion.FromToRotation(Vector3.right, Vector3.forward)
            : Quaternion.identity;

        metrics.min = useX ? measure.min.x : measure.min.z;
        metrics.max = useX ? measure.max.x : measure.max.z;

        metrics.perpAxis = useX ? Vector3.forward : Vector3.right;
        metrics.perpCenter = useX ? measure.center.z : measure.center.x;
        metrics.thickness = useX ? measure.size.z : measure.size.x;

        metricsCache[prefab] = metrics;
        return metrics;
    }


    private static bool TryGetBoxColliderBounds(GameObject prefab, Matrix4x4 rootSpace, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = default;

        foreach (BoxCollider box in prefab.GetComponentsInChildren<BoxCollider>(true))
        {
            if (!box.enabled || box.isTrigger || !IsActiveInPrefab(box.transform, prefab.transform))
            {
                continue;
            }

            Matrix4x4 local = rootSpace * box.transform.localToWorldMatrix;
            EncapsulateMeshBounds(ref bounds, ref hasBounds, new Bounds(box.center, box.size), local);
        }

        return hasBounds;
    }


    private static bool IsActiveInPrefab(Transform target, Transform root)
    {
        for (Transform t = target; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf)
            {
                return false;
            }

            if (t == root)
            {
                break;
            }
        }

        return true;
    }


    private static void EncapsulateMeshBounds(
        ref Bounds bounds,
        ref bool hasBounds,
        Bounds meshBounds,
        Matrix4x4 matrix
    )
    {
        Vector3 min = meshBounds.min;
        Vector3 max = meshBounds.max;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y,
                (i & 4) == 0 ? min.z : max.z
            );

            Vector3 point = matrix.MultiplyPoint3x4(corner);

            if (!hasBounds)
            {
                bounds = new Bounds(point, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(point);
            }
        }
    }
}
