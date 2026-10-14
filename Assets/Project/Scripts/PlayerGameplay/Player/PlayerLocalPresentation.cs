using Mirror;
using UnityEngine;

// Read-only ownership check shared by local presentation; no network settings are changed.
public static class PlayerLocalPresentation
{
    public static bool IsLocal(GameObject player)
    {
        if (!NetworkClient.active && !NetworkServer.active) return true;
        var identity = player.GetComponent<NetworkIdentity>();
        return identity != null && identity.isLocalPlayer;
    }
}
