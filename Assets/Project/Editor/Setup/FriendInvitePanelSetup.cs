using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// TestScene_Lobby의 친구 초대 패널(Panel FriendInvite)을 정리하는 1회용 메뉴.
// 여러 번 실행해도 같은 결과가 되도록 작성했다. 정리가 끝나면 이 파일은 지워도 된다.
public static class FriendInvitePanelSetup
{
    private const string SCENE_PATH = "Assets/Project/Scenes/NetTestScene/TestScene_Lobby.unity";
    private const string ROW_PREFAB_PATH = "Assets/Project/Prefabs/Network/UI/MainMenu/SteamLobby/Row FriendInvite.prefab";
    private const string UNDO_NAME = "Friend Invite Panel Setup";

    private const string EMPTY_MESSAGE = "접속 중인 친구가 없습니다.";


    [MenuItem("Tools/Project Shaman/Setup/친구 초대 패널 정리")]
    private static void Run()
    {
        if (!OpenLobbyScene())
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        Transform panel = FindInScene(scene, "Panel FriendInvite");

        if (panel == null)
        {
            EditorUtility.DisplayDialog("친구 초대 패널 정리", "씬에서 'Panel FriendInvite'를 찾지 못했습니다.", "확인");
            return;
        }

        Transform scrollView = FindChild(panel, "Scroll View");
        Transform content = scrollView != null ? FindChild(scrollView, "Content") : null;
        LobbyUI lobbyUI = FindComponentInScene<LobbyUI>(scene);
        FriendInviteRow rowPrefab = AssetDatabase.LoadAssetAtPath<FriendInviteRow>(ROW_PREFAB_PATH);

        if (scrollView == null || content == null || lobbyUI == null || rowPrefab == null)
        {
            EditorUtility.DisplayDialog(
                "친구 초대 패널 정리",
                $"필요한 오브젝트가 없습니다.\nScroll View: {scrollView != null}\nContent: {content != null}\n" +
                $"LobbyUI: {lobbyUI != null}\nRow 프리팹: {rowPrefab != null}",
                "확인");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UNDO_NAME);

        FixScrollView(scrollView);
        FixContent(content);
        RemoveLeftoverRows(panel);

        TMP_Text emptyText = EnsureEmptyText(panel, scrollView);
        FriendInvitePanel invitePanel = EnsurePanelComponent(panel, content, rowPrefab, emptyText);

        BindButton(panel, "Button Refresh", invitePanel.Refresh);
        BindButton(panel, "Button Close", invitePanel.Close);
        BindLobbyUI(lobbyUI, invitePanel);

        Undo.RecordObject(panel.gameObject, UNDO_NAME);
        panel.gameObject.SetActive(false);

        Undo.CollapseUndoOperations(undoGroup);

        FixRowPrefabWrapping();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[FriendInvitePanelSetup] 친구 초대 패널 정리 완료 (씬 저장됨)");
        EditorUtility.DisplayDialog("친구 초대 패널 정리", "정리 완료. 씬을 저장했습니다.", "확인");
    }


    // =========================================================
    // Steps
    // =========================================================

    // 레이아웃 컴포넌트는 Content로 옮기고, 스크롤은 세로만.
    // Scroll View에 붙어 있던 Content Size Fitter가 높이를 0으로, Layout Group이 Viewport·스크롤바 앵커를
    // 망가뜨렸으므로 Rect도 다시 잡는다.
    private static void FixScrollView(Transform scrollView)
    {
        RemoveComponent<ContentSizeFitter>(scrollView);
        RemoveComponent<VerticalLayoutGroup>(scrollView);

        // 창(800×500) 안에서 제목(위 125) 아래 ~ 버튼(아래 145) 위를 채운다
        SetRect(scrollView, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(40f, 145f), new Vector2(-40f, -125f));

        ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
        Undo.RecordObject(scrollRect, UNDO_NAME);
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.horizontalScrollbar = null;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        Transform horizontalBar = FindChild(scrollView, "Scrollbar Horizontal");

        if (horizontalBar != null)
        {
            Undo.DestroyObjectImmediate(horizontalBar.gameObject);
        }

        Transform viewport = FindChild(scrollView, "Viewport");

        if (viewport != null)
        {
            SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(0f, 1f), Vector2.zero, new Vector2(-20f, 0f));
        }

        Transform verticalBar = FindChild(scrollView, "Scrollbar Vertical");

        if (verticalBar != null)
        {
            RectTransform bar = (RectTransform)verticalBar;
            Undo.RecordObject(bar, UNDO_NAME);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 1f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(20f, 0f);
        }
    }


    private static void SetRect(
        Transform target,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 offsetMin,
        Vector2 offsetMax
    )
    {
        RectTransform rect = (RectTransform)target;
        Undo.RecordObject(rect, UNDO_NAME);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }


    // 위에서부터 행이 쌓이고, 행 개수만큼 높이가 늘어나게
    private static void FixContent(Transform content)
    {
        RectTransform rect = (RectTransform)content;
        Undo.RecordObject(rect, UNDO_NAME);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = GetOrAdd<VerticalLayoutGroup>(content);
        Undo.RecordObject(layout, UNDO_NAME);
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(content);
        Undo.RecordObject(fitter, UNDO_NAME);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }


    // 프리팹으로 빼고 남은 편집용 행 삭제 (실행 중엔 Refresh가 새로 만든다)
    private static void RemoveLeftoverRows(Transform panel)
    {
        foreach (FriendInviteRow row in panel.GetComponentsInChildren<FriendInviteRow>(true))
        {
            Undo.DestroyObjectImmediate(row.gameObject);
        }
    }


    private static TMP_Text EnsureEmptyText(Transform panel, Transform scrollView)
    {
        Transform existing = FindChild(panel, "Text Empty");
        TMP_Text text;

        if (existing != null)
        {
            text = existing.GetComponent<TMP_Text>();
        }
        else
        {
            GameObject go = new GameObject("Text Empty", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, UNDO_NAME);
            go.transform.SetParent(panel, false);
            go.layer = panel.gameObject.layer;

            text = go.GetComponent<TextMeshProUGUI>();
        }

        // 목록 영역과 같은 자리에 겹쳐 놓는다
        RectTransform source = (RectTransform)scrollView;
        SetRect(text.transform, source.anchorMin, source.anchorMax, source.pivot, source.offsetMin, source.offsetMax);

        Undo.RecordObject(text, UNDO_NAME);

        TMP_Text title = FindChild(panel, "Text Title")?.GetComponent<TMP_Text>();

        if (title != null)
        {
            text.font = title.font;
        }

        text.text = EMPTY_MESSAGE;
        text.fontSize = 32f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        text.raycastTarget = false;

        return text;
    }


    private static FriendInvitePanel EnsurePanelComponent(
        Transform panel,
        Transform content,
        FriendInviteRow rowPrefab,
        TMP_Text emptyText
    )
    {
        FriendInvitePanel invitePanel = GetOrAdd<FriendInvitePanel>(panel);

        SerializedObject serialized = new SerializedObject(invitePanel);
        serialized.FindProperty("_rowParent").objectReferenceValue = content;
        serialized.FindProperty("_rowPrefab").objectReferenceValue = rowPrefab;
        serialized.FindProperty("_emptyText").objectReferenceValue = emptyText;
        serialized.ApplyModifiedProperties();

        return invitePanel;
    }


    // 기존 OnClick을 비우고 하나만 연결한다
    private static void BindButton(Transform panel, string buttonName, UnityAction action)
    {
        Button button = FindChild(panel, buttonName)?.GetComponent<Button>();

        if (button == null)
        {
            Debug.LogWarning($"[FriendInvitePanelSetup] '{buttonName}'을 찾지 못해 연결을 건너뜁니다.");
            return;
        }

        Undo.RecordObject(button, UNDO_NAME);

        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        }

        UnityEventTools.AddVoidPersistentListener(button.onClick, action);
    }


    private static void BindLobbyUI(LobbyUI lobbyUI, FriendInvitePanel invitePanel)
    {
        SerializedObject serialized = new SerializedObject(lobbyUI);
        serialized.FindProperty("_friendInvitePanel").objectReferenceValue = invitePanel;
        serialized.ApplyModifiedProperties();

        // LobbyUI는 Canvas Lobby 프리팹 인스턴스 안에 있으므로 씬 오버라이드로 기록한다
        PrefabUtility.RecordPrefabInstancePropertyModifications(lobbyUI);
    }


    // 긴 닉네임이 두 줄로 넘어가지 않고 한 줄에서 ...으로 잘리게
    private static void FixRowPrefabWrapping()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ROW_PREFAB_PATH);

        try
        {
            TMP_Text nameText = FindChild(root.transform, "Text Name")?.GetComponent<TMP_Text>();

            if (nameText != null)
            {
                nameText.textWrappingMode = TextWrappingModes.NoWrap;
                nameText.overflowMode = TextOverflowModes.Ellipsis;
                PrefabUtility.SaveAsPrefabAsset(root, ROW_PREFAB_PATH);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }


    // =========================================================
    // Helpers
    // =========================================================

    private static bool OpenLobbyScene()
    {
        if (SceneManager.GetActiveScene().path == SCENE_PATH)
        {
            return true;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return false;
        }

        EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        return true;
    }


    private static Transform FindInScene(Scene scene, string name)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == name);
    }


    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .Select(root => root.GetComponentInChildren<T>(true))
            .FirstOrDefault(component => component != null);
    }


    private static Transform FindChild(Transform parent, string name)
    {
        return parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }


    private static T GetOrAdd<T>(Transform target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target.gameObject);
    }


    private static void RemoveComponent<T>(Transform target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component != null)
        {
            Undo.DestroyObjectImmediate(component);
        }
    }
}
