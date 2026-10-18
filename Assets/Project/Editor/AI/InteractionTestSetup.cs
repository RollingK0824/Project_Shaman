using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Mock;
using ProjectShaman.AI.World;

public static class InteractionTestSetup
{
    private const string PROFILE_FOLDER = "Assets/Project/Data/AI/Interaction/";
    private const string ROOT_NAME = "InteractionTest";
    private const float OFFSET_FROM_PLACE = 3f;

    [MenuItem("ProjectShaman/AI/Setup Interaction Test")]
    private static void Setup()
    {
        PlaceArea[] places = Object.FindObjectsByType<PlaceArea>(FindObjectsSortMode.None);
        if (places.Length == 0)
        {
            Debug.LogError("[InteractionTest] No PlaceArea in scene");
            return;
        }

        GameObject old = GameObject.Find(ROOT_NAME);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
        }

        GameObject root = new GameObject(ROOT_NAME);
        Undo.RegisterCreatedObjectUndo(root, "Setup Interaction Test");

        string[] profiles = { "IP_Test_LivestockCorpse", "IP_Test_DefiledStorage", "IP_Test_ShadowSpot" };
        int index = 0;

        foreach (PlaceArea place in places)
        {
            if (place.Type != PlaceType.Work && place.Type != PlaceType.Rest)
            {
                continue;
            }

            int profileIndex = index % profiles.Length;
            InteractionProfile profile = AssetDatabase.LoadAssetAtPath<InteractionProfile>(PROFILE_FOLDER + profiles[profileIndex] + ".asset");
            if (profile == null)
            {
                Debug.LogError($"[InteractionTest] Missing profile {profiles[profileIndex]}");
                return;
            }

            GameObject mock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mock.name = $"Mock_{profiles[profileIndex].Replace("IP_Test_", string.Empty)}_{place.PlaceId}";
            mock.transform.SetParent(root.transform);
            mock.transform.position = place.Center + new Vector3(OFFSET_FROM_PLACE, 0.5f, 0f);
            mock.transform.localScale = Vector3.one * 0.8f;
            Object.DestroyImmediate(mock.GetComponent<Collider>());

            MockInteractable interactable = mock.AddComponent<MockInteractable>();
            SerializedObject serialized = new SerializedObject(interactable);
            serialized.FindProperty("_profile").objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[InteractionTest] {mock.name} at {mock.transform.position} ({profile.Label}, priority {profile.Priority}, {profile.MaxResponse})");
            index++;
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
    }
}
