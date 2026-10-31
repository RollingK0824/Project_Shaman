using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using ProjectShaman.AI;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Minion;

// The walking minion: a variant of Temp_Minion that keeps its models (AI_ModelVisual) but walks the NavMesh (AI_Movement)
// instead of flying, with its own MinionData that only goes for the objective. Registered as a spawnable prefab in the
// NetworkManagers and handed to the MinionSpawner. Re-running updates what already exists
public static class MinionWalkerSetup
{
    private const string FLYER_PATH = "Assets/Project/Prefabs/AI/Ghost/Temp_Minion.prefab";
    private const string WALKER_PATH = "Assets/Project/Prefabs/AI/Ghost/Temp_MinionWalker.prefab";
    private const string FLYER_DATA_PATH = "Assets/Project/Data/AI/Minion/MinionData.asset";
    private const string WALKER_DATA_PATH = "Assets/Project/Data/AI/Minion/MinionData_Walker.asset";
    private const float WALK_SPEED = 1.8f;
    private static readonly string[] SCENES =
    {
        "Assets/Project/Scenes/NetTestScene/TestScene_MainMenu.unity",
        "Assets/Project/Scenes/NetTestScene/Gameplay.unity",
    };

    [MenuItem("ProjectShaman/AI/Setup Walking Minion")]
    private static void Setup()
    {
        MinionData data = CreateData();
        GameObject walker = CreateVariant(data);
        foreach (string path in SCENES)
        {
            Register(path, walker);
        }
    }

    private static MinionData CreateData()
    {
        MinionData data = AssetDatabase.LoadAssetAtPath<MinionData>(WALKER_DATA_PATH);
        if (data == null)
        {
            data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<MinionData>(FLYER_DATA_PATH));
            AssetDatabase.CreateAsset(data, WALKER_DATA_PATH);
        }

        SerializedObject so = new SerializedObject(data);
        so.FindProperty("_targetMode").enumValueIndex = (int)MinionTargetMode.ObjectiveOnly;
        so.FindProperty("_moveSpeed").floatValue = WALK_SPEED;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"[MinionWalker] {WALKER_DATA_PATH}: objective only, {WALK_SPEED} m/s");
        return data;
    }

    private static GameObject CreateVariant(MinionData data)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(WALKER_PATH);
        GameObject instance = existing != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(existing)
            : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FLYER_PATH));
        try
        {
            instance.name = "Temp_MinionWalker";
            AI_FlyingMovement flying = instance.GetComponent<AI_FlyingMovement>();
            if (flying != null)
            {
                Object.DestroyImmediate(flying);
            }

            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = instance.AddComponent<NavMeshAgent>();
            }

            agent.radius = 0.35f;
            agent.height = 1.6f;
            agent.speed = WALK_SPEED;
            agent.angularSpeed = 360f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0f;
            if (instance.GetComponent<AI_Movement>() == null)
            {
                instance.AddComponent<AI_Movement>();
            }

            SerializedObject minion = new SerializedObject(instance.GetComponent<AI_Minion>());
            minion.FindProperty("_data").objectReferenceValue = data;
            minion.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, WALKER_PATH);
            Debug.Log($"[MinionWalker] {WALKER_PATH} saved (variant of Temp_Minion, NavMeshAgent + AI_Movement)");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // Every NetworkManager in the scene learns the prefab; the spawner gets it as its walker
    private static void Register(string path, GameObject walker)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (NetworkManager manager in root.GetComponentsInChildren<NetworkManager>(true))
            {
                if (!manager.spawnPrefabs.Contains(walker))
                {
                    Undo.RecordObject(manager, "Setup Walking Minion");
                    manager.spawnPrefabs.Add(walker);
                    changed = true;
                    Debug.Log($"[MinionWalker] {scene.name}/{manager.name}: spawnable prefab added");
                }
            }

            foreach (MinionSpawner spawner in root.GetComponentsInChildren<MinionSpawner>(true))
            {
                SerializedObject so = new SerializedObject(spawner);
                SerializedProperty prop = so.FindProperty("_walkerPrefab");
                if (prop.objectReferenceValue != walker)
                {
                    prop.objectReferenceValue = walker;
                    so.ApplyModifiedProperties();
                    changed = true;
                    Debug.Log($"[MinionWalker] {scene.name}/{spawner.name}: walker prefab set");
                }
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (opened)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
