using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using ProjectShaman.AI;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Trace;
using ProjectShaman.AI.World;

// The ghost's anomalies (NA_HauntAnomaly): a bloody handprint on a door, blood or filth on the ground, drawn as URP decals.
// Builds everything and can be run again after the textures change:
// - turns the Decal renderer feature on in the project's URP renderers
// - one decal material and one variant prefab per kind, reading Visuals/Textures/Traces/Anomaly_<Kind>.png
//   (replace those PNGs with the final art and the decals change with them, nothing else to do)
// - Trace_Anomaly (copied from the livestock corpse trace): cleansed with salt only, shows the kind it was left as
// - hands it to NA_HauntAnomaly, registers it with the NetworkManagers
// - AnomalySpots in the open scene: one at each door (handprint) and one at each home's yard (blood, filth)
public static class AnomalyTraceSetup
{
    private const string TEXTURE_FOLDER = "Assets/Project/Visuals/Textures/Traces";
    private const string MATERIAL_FOLDER = "Assets/Project/Visuals/Materials/Traces";
    private const string VARIANT_FOLDER = "Assets/Project/Prefabs/Trace/Anomaly";
    private const string SOURCE_TRACE = "Assets/Project/Prefabs/Trace/Temp_Livestock_Corpse.prefab";
    private const string TRACE_PATH = "Assets/Project/Prefabs/Trace/Trace_Anomaly.prefab";
    private const string SOURCE_PROFILE = "Assets/Project/Data/AI/Interaction/IP_Test_LivestockCorpse.asset";
    private const string PROFILE_PATH = "Assets/Project/Data/AI/Interaction/IP_Anomaly.asset";
    private const string ACTION_PATH = "Assets/Project/Data/AI/Ghost/NightActions/NA_HauntAnomaly.asset";
    private const string SPOT_ROOT = "AnomalySpots";
    private const string DECAL_SHADER = "Shader Graphs/Decal";
    private static readonly string[] RENDERERS =
    {
        "Assets/Project/Settings/URPAsset_Renderer.asset",
        "Assets/Project/Settings/URP-Balanced-Renderer.asset",
        "Assets/Project/Settings/URP-HighFidelity-Renderer.asset",
        "Assets/Project/Settings/URP-Performant-Renderer.asset",
    };
    private static readonly string[] SCENES =
    {
        "Assets/Project/Scenes/NetTestScene/TestScene_MainMenu.unity",
        "Assets/Project/Scenes/NetTestScene/Gameplay.unity",
    };

    // Kind, decal size (width, height, depth) and whether it goes on a door (projected into it) or on the ground (down)
    private static readonly (string Kind, Vector3 Size, bool OnDoor)[] KINDS =
    {
        (AnomalySpot.HANDPRINT, new Vector3(0.35f, 0.35f, 0.6f), true),
        (AnomalySpot.BLOOD, new Vector3(1.3f, 1.3f, 1f), false),
        (AnomalySpot.FILTH, new Vector3(1.1f, 1.1f, 1f), false),
    };

    [MenuItem("ProjectShaman/AI/Setup Anomaly Traces")]
    private static void Setup()
    {
        foreach (string path in RENDERERS)
        {
            EnableDecals(path);
        }

        Dictionary<string, GameObject> variants = new Dictionary<string, GameObject>();
        foreach (var kind in KINDS)
        {
            variants[kind.Kind] = CreateVariant(kind.Kind, kind.Size, kind.OnDoor, CreateMaterial(kind.Kind));
        }

        GameObject trace = CreateTrace(CreateProfile(), variants);
        SetActionTrace(trace);
        foreach (string path in SCENES)
        {
            Register(path, trace);
        }

        PlaceSpots();
        AssetDatabase.SaveAssets();
        Debug.Log("[Anomaly] Setup done. Replace the PNGs in " + TEXTURE_FOLDER + " to change the decals");
    }

    private static void EnableDecals(string path)
    {
        ScriptableRendererData data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
        if (data == null || data.rendererFeatures.Any(feature => feature is DecalRendererFeature))
        {
            return;
        }

        DecalRendererFeature decal = ScriptableObject.CreateInstance<DecalRendererFeature>();
        decal.name = "Decal";
        AssetDatabase.AddObjectToAsset(decal, data);
        data.rendererFeatures.Add(decal);

        SerializedObject so = new SerializedObject(data);
        SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
        if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(decal, out string _, out long localId))
        {
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedProperties();
        }

        data.SetDirty();
        EditorUtility.SetDirty(data);
        Debug.Log($"[Anomaly] Decal renderer feature added to {path}");
    }

    private static Material CreateMaterial(string kind)
    {
        string texturePath = $"{TEXTURE_FOLDER}/Anomaly_{kind}.png";
        if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer && (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp))
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        System.IO.Directory.CreateDirectory(MATERIAL_FOLDER);
        string path = $"{MATERIAL_FOLDER}/M_Anomaly_{kind}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(DECAL_SHADER));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("Base_Map", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateVariant(string kind, Vector3 size, bool onDoor, Material material)
    {
        System.IO.Directory.CreateDirectory(VARIANT_FOLDER);
        GameObject root = new GameObject($"Anomaly_{kind}");
        try
        {
            DecalProjector projector = root.AddComponent<DecalProjector>();
            projector.material = material;
            projector.size = size;
            projector.pivot = Vector3.zero;
            projector.drawDistance = 60f;
            // A decal projects along its +Z: into the door (the spot faces out of it), or straight down onto the ground
            root.transform.localRotation = onDoor ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            return PrefabUtility.SaveAsPrefabAsset(root, $"{VARIANT_FOLDER}/Anomaly_{kind}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static InteractionProfileRef CreateProfile()
    {
        if (AssetDatabase.LoadMainAssetAtPath(PROFILE_PATH) == null)
        {
            AssetDatabase.CopyAsset(SOURCE_PROFILE, PROFILE_PATH);
        }

        Object profile = AssetDatabase.LoadMainAssetAtPath(PROFILE_PATH);
        SerializedObject so = new SerializedObject(profile);
        so.FindProperty("_label").stringValue = "괴이한 흔적";
        so.FindProperty("_witnessStress").floatValue = 8f;
        so.FindProperty("_detailedWitnessStress").floatValue = 15f;
        so.ApplyModifiedProperties();
        return new InteractionProfileRef(profile);
    }

    private static GameObject CreateTrace(InteractionProfileRef profile, Dictionary<string, GameObject> variants)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TRACE_PATH) == null)
        {
            AssetDatabase.CopyAsset(SOURCE_TRACE, TRACE_PATH);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(TRACE_PATH);
        try
        {
            root.name = "Trace_Anomaly";

            SerializedObject trace = new SerializedObject(root.GetComponent<GhostTrace>());
            trace.FindProperty("_kind").enumValueIndex = (int)GhostTraceKind.Anomaly;
            trace.FindProperty("_profile").objectReferenceValue = profile.Asset;
            // Every anomaly is cleansed with salt, never wiped by hand (a TraceCleanse request)
            trace.FindProperty("_clearableByHand").boolValue = false;
            SerializedProperty items = trace.FindProperty("_cleansingItemIds");
            items.arraySize = 1;
            items.GetArrayElementAtIndex(0).stringValue = "item_salt";
            trace.ApplyModifiedProperties();

            SerializedObject visual = new SerializedObject(root.GetComponent<AI_CorpseVisual>());
            SerializedProperty list = visual.FindProperty("_variants");
            list.arraySize = KINDS.Length;
            for (int i = 0; i < KINDS.Length; i++)
            {
                SerializedProperty variant = list.GetArrayElementAtIndex(i);
                variant.FindPropertyRelative("Key").stringValue = KINDS[i].Kind;
                variant.FindPropertyRelative("Model").objectReferenceValue = variants[KINDS[i].Kind];
                variant.FindPropertyRelative("PoseController").objectReferenceValue = null;
                variant.FindPropertyRelative("LocalPosition").vector3Value = Vector3.zero;
                variant.FindPropertyRelative("LocalEuler").vector3Value = variants[KINDS[i].Kind].transform.localEulerAngles;
                variant.FindPropertyRelative("LocalScale").vector3Value = Vector3.one;
            }

            visual.ApplyModifiedProperties();

            // The placeholder stays only as the salt / look-at target: small, around the decal
            SerializedProperty placeholder = visual.FindProperty("_placeholder");
            if (placeholder.objectReferenceValue is GameObject holder)
            {
                holder.transform.localScale = new Vector3(0.6f, 0.3f, 0.6f);
                foreach (Renderer part in holder.GetComponentsInChildren<Renderer>(true))
                {
                    part.enabled = false;
                }
            }

            return PrefabUtility.SaveAsPrefabAsset(root, TRACE_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetActionTrace(GameObject trace)
    {
        Object action = AssetDatabase.LoadMainAssetAtPath(ACTION_PATH);
        SerializedObject so = new SerializedObject(action);
        so.FindProperty("_tracePrefab").objectReferenceValue = trace;
        so.ApplyModifiedProperties();
    }

    private static void Register(string path, GameObject trace)
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
                if (!manager.spawnPrefabs.Contains(trace))
                {
                    Undo.RecordObject(manager, "Setup Anomaly Traces");
                    manager.spawnPrefabs.Add(trace);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Anomaly] {scene.name}: Trace_Anomaly registered as a spawnable prefab");
        }

        if (opened)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    // In the active scene (Gameplay): rebuilt under one root each run
    private static void PlaceSpots()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject old = scene.GetRootGameObjects().FirstOrDefault(go => go.name == SPOT_ROOT);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
        }

        GameObject root = new GameObject(SPOT_ROOT);
        Undo.RegisterCreatedObjectUndo(root, "Setup Anomaly Traces");
        List<PlaceArea> homes = Object.FindObjectsByType<PlaceArea>(FindObjectsSortMode.None).Where(place => place.Type == PlaceType.Home).ToList();
        int doors = 0;
        int grounds = 0;

        foreach (NetDoor door in Object.FindObjectsByType<NetDoor>(FindObjectsSortMode.None))
        {
            if (TryDoorSpot(door, homes, out Vector3 position, out Quaternion rotation))
            {
                AddSpot(root, $"Door_{door.name}_{doors}", position, rotation, AnomalySpot.HANDPRINT);
                doors++;
            }
        }

        foreach (PlaceArea home in homes)
        {
            Vector3 yard = home.GetHomePoint();
            if (NavMesh.SamplePosition(yard, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                AddSpot(root, $"Yard_{home.PlaceId}", hit.position, Quaternion.identity, AnomalySpot.BLOOD, AnomalySpot.FILTH);
                grounds++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[Anomaly] {doors} door spot(s), {grounds} yard spot(s) placed under {SPOT_ROOT} in {scene.name}. Save the scene");
    }

    // Hand height on the door's outer face: the side away from the nearest house centre
    private static bool TryDoorSpot(NetDoor door, List<PlaceArea> homes, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = default;
        Renderer[] renderers = door.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer part in renderers)
        {
            bounds.Encapsulate(part.bounds);
        }

        Vector3 center = bounds.center;
        PlaceArea house = homes.OrderBy(home => (home.transform.position - center).sqrMagnitude).FirstOrDefault();
        Vector3 away = house != null ? center - house.transform.position : door.transform.forward;
        away.y = 0f;

        // The door's own flat axis closest to "away from the house"
        Vector3[] axes = { door.transform.forward, -door.transform.forward, door.transform.right, -door.transform.right };
        Vector3 outward = axes.OrderByDescending(axis => Vector3.Dot(new Vector3(axis.x, 0f, axis.z).normalized, away.normalized)).First();
        outward.y = 0f;
        outward.Normalize();

        float halfDepth = Mathf.Abs(Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(outward.x), 0f, Mathf.Abs(outward.z))));
        position = new Vector3(center.x, Mathf.Min(center.y, bounds.min.y + 1.3f), center.z) + outward * (halfDepth + 0.05f);
        rotation = Quaternion.LookRotation(outward);
        return true;
    }

    private static void AddSpot(GameObject root, string name, Vector3 position, Quaternion rotation, params string[] kinds)
    {
        GameObject spot = new GameObject(name);
        spot.transform.SetParent(root.transform, false);
        spot.transform.SetPositionAndRotation(position, rotation);
        AnomalySpot anomaly = spot.AddComponent<AnomalySpot>();
        SerializedObject so = new SerializedObject(anomaly);
        SerializedProperty list = so.FindProperty("_kinds");
        list.arraySize = kinds.Length;
        for (int i = 0; i < kinds.Length; i++)
        {
            list.GetArrayElementAtIndex(i).stringValue = kinds[i];
        }

        so.ApplyModifiedProperties();
    }

    private readonly struct InteractionProfileRef
    {
        public readonly Object Asset;

        public InteractionProfileRef(Object asset)
        {
            Asset = asset;
        }
    }
}
