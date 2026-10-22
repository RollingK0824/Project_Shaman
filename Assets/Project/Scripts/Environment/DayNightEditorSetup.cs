#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 메뉴(Tools/Day-Night Cycle)로만 실행한다. 씬을 열거나 에디터를 켤 때 자동 실행하면 씬을 덮어써 저장해 버린다.
public static class DayNightEditorSetup
{
    private const string ShaderName = "Custom/DayNightCubemapSkybox";
    private const string ShaderAssetPath = "Assets/Project/Visuals/Shaders/Environment/Skybox/DayNightCubemapSkybox.shader";
    private const string MaterialPath = "Assets/Project/Visuals/Materials/Environment/Skybox/DayNightSkybox.mat";
    private const string DayCubemapPath = "Assets/ThirdParty/AllSkyFree/Cold Sunset/Cold Sunset Equirect.png";
    private const string NightCubemapPath = "Assets/ThirdParty/AllSkyFree/Cold Night/Cold Night Equirect.png";

    [MenuItem("Tools/Day-Night Cycle/Setup Test in Current Scene")]
    public static void SetupCurrentScene()
    {
        SetupCurrentSceneInternal();
    }

    private static bool SetupCurrentSceneInternal()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(activeScene.path) && activeScene.path.StartsWith("Assets/ThirdParty"))
        {
            return false;
        }

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath);
        if (shader == null)
        {
            shader = Shader.Find(ShaderName);
        }

        if (shader == null)
        {
            Debug.LogWarning($"[DayNightEditorSetup] Shader '{ShaderName}' not found.");
            return false;
        }

        Material skyMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (skyMat == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material DayNightSkybox");
            if (guids != null && guids.Length > 0)
            {
                skyMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }

        if (skyMat == null)
        {
            Debug.LogWarning($"[DayNightEditorSetup] Material not found at '{MaterialPath}'.");
            return false;
        }

        if (skyMat.shader != shader)
        {
            skyMat.shader = shader;
        }

        Cubemap dayCube = AssetDatabase.LoadAssetAtPath<Cubemap>(DayCubemapPath);
        Cubemap nightCube = AssetDatabase.LoadAssetAtPath<Cubemap>(NightCubemapPath);

        if (dayCube != null)
        {
            skyMat.SetTexture("_Tex", dayCube);
        }

        if (nightCube != null)
        {
            skyMat.SetTexture("_NightTex", nightCube);
        }

        EditorUtility.SetDirty(skyMat);
        AssetDatabase.SaveAssets();

        RenderSettings.skybox = skyMat;

        Light dirLight = RenderSettings.sun;
        if (dirLight == null)
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    dirLight = l;
                    break;
                }
            }
        }

        if (dirLight == null)
        {
            return false;
        }

        RenderSettings.sun = dirLight;

        DayNightCycleController controller = dirLight.GetComponent<DayNightCycleController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<DayNightCycleController>(dirLight.gameObject);
        }

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("directionalLight").objectReferenceValue = dirLight;
        so.FindProperty("skyboxMaterial").objectReferenceValue = skyMat;
        so.FindProperty("autoSimulatePreview").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(dirLight.gameObject);

        controller.ApplyFromCycleProgress(0.15f);

        if (!string.IsNullOrEmpty(activeScene.path))
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }

        Selection.activeGameObject = dirLight.gameObject;
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        Debug.Log($"[DayNightEditorSetup] Linked existing '{MaterialPath}' to scene '{activeScene.name}'.");
        return true;
    }
}
#endif
