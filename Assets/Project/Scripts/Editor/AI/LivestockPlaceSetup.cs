using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.World;

// Work places for the livestock routines (소 여물·외양간·돼지 먹이·닭 모이·달걀), one per pen: centred on the animals placed
// in the open scene and wide enough to cover where they wander. Re-running moves the existing places instead of adding more.
// Move a place to the trough or gate side afterwards if villagers cannot walk into the pen
public static class LivestockPlaceSetup
{
    private const string PARENT_NAME = "WorkPlaces";
    private const float RADIUS_MARGIN = 1f;
    private const float MIN_RADIUS = 2f;

    private static readonly (string animal, string placeId, string displayName, int maxPeople)[] PENS =
    {
        ("Cattle", "CattleShed", "외양간", 2),
        ("Pig", "PigPen", "돼지우리", 2),
        ("Chicken", "ChickenCoop", "닭장", 3),
    };

    [MenuItem("ProjectShaman/AI/Setup Livestock Places")]
    private static void Setup()
    {
        NetLivestock[] animals = Object.FindObjectsByType<NetLivestock>(FindObjectsSortMode.None);
        if (animals.Length == 0)
        {
            Debug.LogError("[LivestockPlaces] No NetLivestock in the open scene");
            return;
        }

        GameObject parent = GameObject.Find(PARENT_NAME);
        PlaceArea[] existing = Object.FindObjectsByType<PlaceArea>(FindObjectsSortMode.None);
        foreach ((string animal, string placeId, string displayName, int maxPeople) in PENS)
        {
            List<Vector3> positions = animals.Where(a => a.name.Contains(animal)).Select(a => a.transform.position).ToList();
            if (positions.Count == 0)
            {
                Debug.LogWarning($"[LivestockPlaces] No {animal} in the scene, {placeId} skipped");
                continue;
            }

            Vector3 center = positions.Aggregate(Vector3.zero, (sum, p) => sum + p) / positions.Count;
            float radius = Mathf.Max(MIN_RADIUS, positions.Max(p => Vector3.Distance(new Vector3(p.x, center.y, p.z), center)) + RADIUS_MARGIN);

            PlaceArea place = existing.FirstOrDefault(p => p.PlaceId == placeId);
            if (place == null)
            {
                GameObject go = new GameObject($"Place_{placeId}");
                Undo.RegisterCreatedObjectUndo(go, "Setup Livestock Places");
                if (parent != null)
                {
                    go.transform.SetParent(parent.transform, true);
                }

                place = go.AddComponent<PlaceArea>();
            }

            Undo.RecordObject(place.transform, "Setup Livestock Places");
            place.transform.position = center;

            SerializedObject so = new SerializedObject(place);
            so.FindProperty("_placeId").stringValue = placeId;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_placeType").enumValueIndex = (int)PlaceType.Work;
            so.FindProperty("_shape").enumValueIndex = (int)PlaceShape.Sphere;
            so.FindProperty("_radius").floatValue = radius;
            so.FindProperty("_maxPeople").intValue = maxPeople;
            so.ApplyModifiedProperties();
            Debug.Log($"[LivestockPlaces] {placeId} ({displayName}) at {center:F1}, radius {radius:F1}m, {positions.Count} {animal}");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
