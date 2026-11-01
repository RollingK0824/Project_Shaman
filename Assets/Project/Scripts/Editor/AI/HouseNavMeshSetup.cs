using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

// Villagers took short cuts through houses (in at one door, out at the other). Each house in the open scene gets a
// NavMeshModifier marking its floor as the Indoor area (cost set in Navigation > Areas): paths go round a house unless
// they end inside it (going home). Roofs (SM_Roof*) are made Not Walkable: baked walkable they were islands cut off from
// the ground, and a point picked near a shed landed on its roof with no path to it. Bake the NavMeshSurface again afterwards
public static class HouseNavMeshSetup
{
    private const string AREA_NAME = "Indoor";
    private const string HOUSE_PREFAB_FOLDER = "Naganeupseong/Prefabs/Build/";
    private const string ROOF_NAME_PREFIX = "SM_Roof";

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

        int roofs = MarkRoofsNotWalkable();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[HouseNavMesh] {count} house(s) marked, {roofs} roof(s) not walkable. Bake the NavMeshSurface again to apply");
    }

    // A modifier on the roof itself wins over the house's Indoor one for the roof's own meshes
    private static int MarkRoofsNotWalkable()
    {
        int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
        int count = 0;
        foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!transform.name.StartsWith(ROOF_NAME_PREFIX))
            {
                continue;
            }

            NavMeshModifier modifier = transform.GetComponent<NavMeshModifier>();
            if (modifier == null)
            {
                modifier = Undo.AddComponent<NavMeshModifier>(transform.gameObject);
            }

            Undo.RecordObject(modifier, "Setup House NavMesh Cost");
            modifier.overrideArea = true;
            modifier.area = notWalkable;
            modifier.applyToChildren = true;
            count++;
        }

        return count;
    }
}
