using UnityEditor;
using UnityEngine;

// A weak cold point light on the minion and ghost prefabs, so they can be made out in the dark: minions get a wider glow,
// ghosts a narrow one that only lights the face. Re-running updates the light that is already there
public static class MinionLightSetup
{
    private const string LIGHT_NAME = "Minion Light";
    private static readonly (string Path, float Height, float Intensity, float Range)[] PREFABS =
    {
        ("Assets/Project/Prefabs/AI/Ghost/Temp_Minion.prefab", 1.2f, 0.6f, 3f),
        ("Assets/Project/Prefabs/AI/Ghost/Temp_MinionWalker.prefab", 1.2f, 0.6f, 3f),
        // In front of the face, just short enough to light the head
        ("Assets/Project/Prefabs/AI/Ghost/Temp_GhostEntity.prefab", 1.6f, 0.4f, 0.8f),
    };

    [MenuItem("ProjectShaman/AI/Setup Ghost And Minion Light")]
    private static void Setup()
    {
        foreach ((string path, float height, float intensity, float range) in PREFABS)
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

                child.localPosition = new Vector3(0f, height, range < 1f ? 0.3f : 0f);
                Light light = child.GetComponent<Light>();
                if (light == null)
                {
                    light = child.gameObject.AddComponent<Light>();
                }

                light.type = LightType.Point;
                light.color = new Color(0.55f, 0.75f, 1f);
                light.intensity = intensity;
                light.range = range;
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
