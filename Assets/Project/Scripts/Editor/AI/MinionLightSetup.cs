using UnityEditor;
using UnityEngine;

// A weak cold point light on the minion prefabs, so a minion coming at night can be made out in the dark.
// Re-running updates the light that is already there
public static class MinionLightSetup
{
    private const string LIGHT_NAME = "Minion Light";
    private static readonly string[] PREFABS =
    {
        "Assets/Project/Prefabs/AI/Ghost/Temp_Minion.prefab",
        "Assets/Project/Prefabs/AI/Ghost/Temp_MinionWalker.prefab",
    };

    [MenuItem("ProjectShaman/AI/Setup Minion Light")]
    private static void Setup()
    {
        foreach (string path in PREFABS)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform child = root.transform.Find(LIGHT_NAME);
                if (child == null)
                {
                    child = new GameObject(LIGHT_NAME).transform;
                    child.SetParent(root.transform, false);
                }

                child.localPosition = new Vector3(0f, 1.2f, 0f);
                Light light = child.GetComponent<Light>();
                if (light == null)
                {
                    light = child.gameObject.AddComponent<Light>();
                }

                light.type = LightType.Point;
                light.color = new Color(0.55f, 0.75f, 1f);
                light.intensity = 0.6f;
                light.range = 3f;
                light.shadows = LightShadows.None;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[MinionLight] {path}: point light {light.intensity} / {light.range}m");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
