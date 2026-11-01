using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

// Villagers took short cuts through houses (in at one door, out at the other). Each house in the open scene gets a
// NavMeshModifier marking its floor as the Indoor area (cost set in Navigation > Areas): paths go round a house unless
// they end inside it (going home). Bake the NavMeshSurface again afterwards
public static class HouseNavMeshSetup
{
    private const string AREA_NAME = "Indoor";
    private const string HOUSE_PREFAB_FOLDER = "Naganeupseong/Prefabs/Build/";

    [MenuItem("ProjectShaman/AI/Setup House NavMesh Cost")]
    private static void Setup()
    {
        int area = NavMesh.GetAreaFromName(AREA_NAME);
        if (area < 0)
        {
            Debug.LogError($"[HouseNavMesh] No NavMesh area '{AREA_NAME}' (Navigation > Areas)");
            return;
        }

        int count = 0;
        foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject house = transform.gameObject;
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(house))
            {
                continue;
            }

            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(house);
            if (string.IsNullOrEmpty(source) || !source.Contains(HOUSE_PREFAB_FOLDER) || !System.IO.Path.GetFileName(source).Contains("House"))
            {
                continue;
            }

            NavMeshModifier modifier = house.GetComponent<NavMeshModifier>();
            if (modifier == null)
            {
                modifier = Undo.AddComponent<NavMeshModifier>(house);
            }

            Undo.RecordObject(modifier, "Setup House NavMesh Cost");
            modifier.overrideArea = true;
            modifier.area = area;
            modifier.applyToChildren = true;
            count++;
            Debug.Log($"[HouseNavMesh] {house.name}: floor → {AREA_NAME} (cost {NavMesh.GetAreaCost(area)})");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[HouseNavMesh] {count} house(s) marked. Bake the NavMeshSurface again to apply");
    }
}
