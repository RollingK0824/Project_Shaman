using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectShaman.Network.AI;

// Net_WorkStation needs a NetworkAnimator, and Mirror's NetworkAnimator reads its Animator on Awake and on every spawn message:
// a station without a hit animation (anvil, washing stone, stream) still needs an Animator, an empty one with no controller
public static class WorkStationAnimatorSetup
{
    [MenuItem("ProjectShaman/AI/Fix Work Station Animators")]
    private static void Fix()
    {
        int fixedCount = 0;
        foreach (Net_WorkStation station in Object.FindObjectsByType<Net_WorkStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            NetworkAnimator networkAnimator = station.GetComponent<NetworkAnimator>();
            if (networkAnimator == null || networkAnimator.animator != null)
            {
                continue;
            }

            Animator animator = station.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = Undo.AddComponent<Animator>(station.gameObject);
            }

            Undo.RecordObject(networkAnimator, "Fix Work Station Animators");
            networkAnimator.animator = animator;
            PrefabUtility.RecordPrefabInstancePropertyModifications(networkAnimator);
            fixedCount++;
            Debug.Log($"[WorkStationAnimators] {station.name}: NetworkAnimator → {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "empty Animator")}");
        }

        if (fixedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        Debug.Log($"[WorkStationAnimators] {fixedCount} station(s) fixed");
    }
}
