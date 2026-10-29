using System.Linq;
using ProjectShaman.AI;
using ProjectShaman.Network.AI;
using UnityEditor;
using UnityEngine;

// Temp_GhostEntity / Temp_Minion에 AI_ModelVisual + Net_ModelVisual을 붙이고 Visual 루트와 모델 목록을 채우는 1회용 메뉴.
// 모델 목록은 Prefabs/AI/Ghost(귀신), Prefabs/AI/Ghost/Minion(미니언) 폴더의 프리팹이다.
// 여러 번 실행해도 같은 결과가 되도록 작성했다. 정리가 끝나면 이 파일은 지워도 된다.
public static class GhostModelVisualSetup
{
    private const string GHOST_FOLDER = "Assets/Project/Prefabs/AI/Ghost";
    private const string MINION_FOLDER = GHOST_FOLDER + "/Minion";
    private const string GHOST_ENTITY_PATH = GHOST_FOLDER + "/Temp_GhostEntity.prefab";
    private const string MINION_PATH = GHOST_FOLDER + "/Temp_Minion.prefab";
    private const string VISUAL_NAME = "Visual";
    private const string TITLE = "귀신 모델 Visual 설정";


    [MenuItem("Tools/Project Shaman/Setup/귀신·미니언 모델 Visual 설정")]
    private static void Run()
    {
        GameObject[] ghostModels = LoadModels(GHOST_FOLDER, GHOST_ENTITY_PATH, MINION_PATH);
        GameObject[] minionModels = LoadModels(MINION_FOLDER);

        string ghostResult = SetupPrefab(GHOST_ENTITY_PATH, ghostModels);
        string minionResult = SetupPrefab(MINION_PATH, minionModels);

        string message = $"{ghostResult}\n{minionResult}";
        Debug.Log($"[{TITLE}] {message}");
        EditorUtility.DisplayDialog(TITLE, message, "확인");
    }

    // 하위 폴더는 보지 않는다(귀신 폴더에서 Minion 폴더를 빼기 위해)
    private static GameObject[] LoadModels(string folder, params string[] excludedPaths)
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => System.IO.Path.GetDirectoryName(path).Replace('\\', '/') == folder)
            .Where(path => !excludedPaths.Contains(path))
            .OrderBy(path => path)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(model => model != null)
            .ToArray();
    }

    private static string SetupPrefab(string path, GameObject[] models)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            Transform visual = root.transform.Find(VISUAL_NAME);
            if (visual == null)
            {
                return $"{path}: '{VISUAL_NAME}' 자식이 없습니다.";
            }

            AI_ModelVisual modelVisual = root.GetComponent<AI_ModelVisual>();
            if (modelVisual == null)
            {
                modelVisual = root.AddComponent<AI_ModelVisual>();
            }

            if (root.GetComponent<Net_ModelVisual>() == null)
            {
                root.AddComponent<Net_ModelVisual>();
            }

            SerializedObject serialized = new SerializedObject(modelVisual);
            serialized.FindProperty("_visualRoot").objectReferenceValue = visual;

            SerializedProperty list = serialized.FindProperty("_modelPrefabs");
            list.arraySize = models.Length;
            for (int i = 0; i < models.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = models[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);

            return $"{System.IO.Path.GetFileNameWithoutExtension(path)}: 모델 {models.Length}개 ({string.Join(", ", models.Select(m => m.name))})";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
