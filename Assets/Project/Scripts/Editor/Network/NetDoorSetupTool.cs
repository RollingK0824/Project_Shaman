using UnityEngine;
using UnityEditor;

public static class NetDoorSetupTool
{
    private const StaticEditorFlags MovingBlockers = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

    [MenuItem("Tools/Newtork/Door/선택 하위 문 세팅")]
    private static void SetupSelected()
    {
        int count = 0;
        foreach (GameObject root in Selection.gameObjects)
        {
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                GameObject go = mf.gameObject;
                // 콜라이더가 없는 건 대문 통메시(Door01k/l) -> 제외
                if (!go.name.StartsWith("SM_Door") || !go.TryGetComponent<Collider>(out _)) continue;
                if (go.GetComponent<NetDoor>() != null) continue;

                Undo.RecordObject(go, "NetDoor Setup");
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) & ~MovingBlockers);
                Undo.AddComponent<NetDoor>(go); // RequireComponent로 NetworkIdentity도 같이 붙음
                count++;
            }
        }

        Debug.Log($"[NetDoorSetup] 문 {count}개 세팅");
    }
}
