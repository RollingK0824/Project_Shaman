using UnityEngine;

// Shared capability check for world actions. Notebook/UI may read data when false.
public static class PlayerActionGuard
{
    public static bool CanAct(GameObject player)
    {
        if (player == null || !player.activeInHierarchy) return false;
        var health = player.GetComponent<PlayerHealth>();
        return health == null || health.CanAffectWorld;
    }
}
