using Mirror;
using UnityEngine;

// 수첩 [끼임 탈출] 버튼. 로컬 플레이어를 스폰 지점으로 옮긴다.
// 이동은 클라 권한(ClientToServer)이라 로컬에서 옮기고, CmdTeleport로 서버·다른 클라에 보간 없이 알린다.
public static class NetPlayerUnstuck
{
    public static bool TryUnstuckLocalPlayer()
    {
        NetworkIdentity local = NetworkClient.localPlayer;
        if (local == null || !PlayerActionGuard.CanAct(local.gameObject)) return false;   // 관전 중이면 불가

        PlayerSpawnArea spawnArea = Object.FindFirstObjectByType<PlayerSpawnArea>();
        if (spawnArea == null) return false;

        // 처음 스폰할 때(RoomManager)와 같은 원형 배치. 다른 플레이어와 겹치지 않게 netId로 칸을 나눔
        int slots = Mathf.Max(1, NetworkManager.singleton.maxConnections);
        spawnArea.GetSpawnPose((int)(local.netId % (uint)slots), slots, out Vector3 position, out _);

        // CharacterController가 켜져 있으면 다음 Move에서 원래 자리로 되돌아감
        CharacterController controller = local.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        local.transform.position = position;
        if (controller != null) controller.enabled = true;

        // 서버에 순간이동 알림 → 서버가 다른 클라에 RpcTeleport (미끄러지는 보간 방지)
        if (local.TryGetComponent(out NetworkTransformUnreliable netTransform)) netTransform.CmdTeleport(position);
        return true;
    }
}