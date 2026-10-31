using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectShaman.AI.Ghost;

// A CarrySocket under the right hand of every ghost model prefab, so a ghost carries a tool in its hand whichever model it
// wears (AI_GhostEntity picks the socket up when AI_ModelVisual swaps the model). Same convention as a villager's hand:
// the socket sits on the hand bone, the tool's Held Local Offset/Euler places it in the fist. Re-running keeps existing sockets
public static class GhostCarrySocketSetup
{
    private const string GHOST_FOLDER = "Assets/Project/Prefabs/AI/Ghost";

    [MenuItem("ProjectShaman/AI/Setup Ghost Carry Sockets")]
    private static void Setup()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { GHOST_FOLDER }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetDirectoryName(path).Replace('\\', '/') != GHOST_FOLDER || System.IO.Path.GetFileName(path).StartsWith("Temp_"))
            {
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform hand = FindRightHand(root);
                if (hand == null)
                {
                    Debug.LogWarning($"[GhostCarrySocket] {path}: no humanoid right hand, skipped");
                    continue;
                }

                if (hand.Find(AI_GhostEntity.CARRY_SOCKET_NAME) != null)
                {
                    Debug.Log($"[GhostCarrySocket] {path}: already has a socket on {hand.name}");
                    continue;
                }

                Transform socket = new GameObject(AI_GhostEntity.CARRY_SOCKET_NAME).transform;
                socket.SetParent(hand, false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[GhostCarrySocket] {path}: socket added under {hand.name}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    // From the avatar's bone mapping, so it works on a model whose Animator has no controller
    private static Transform FindRightHand(GameObject root)
    {
        Animator animator = root.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            return null;
        }

        HumanBone bone = animator.avatar.humanDescription.human.FirstOrDefault(b => b.humanName == HumanBodyBones.RightHand.ToString());
        if (string.IsNullOrEmpty(bone.boneName))
        {
            return null;
        }

        return animator.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == bone.boneName);
    }
}
