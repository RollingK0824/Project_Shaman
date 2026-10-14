using System;
using Mirror;
using UnityEngine;

// Optional server integration point on the attacker. No commands, identities or sync settings.
[DisallowMultipleComponent]
public class ItemAttackAuthority : MonoBehaviour
{
    private Func<bool> _canResolve;
    // Validate inventory/cooldown before creating an attack; this delegate maintains its permission
    // at contact time (round/authority/revocation), rather than spending or restarting cooldowns.
    public void SetAuthorityCheck(Func<bool> canResolve) => _canResolve = canResolve;

    public static bool CanResolve(GameObject owner)
    {
        if (!PlayerActionGuard.CanAct(owner)) return false;
        var gate = owner.GetComponent<ItemAttackAuthority>();
        if (!NetworkClient.active && !NetworkServer.active)
            return gate == null || (gate.isActiveAndEnabled && (gate._canResolve == null || gate._canResolve()));
        // Client prediction is presentation only. Even a server is denied until explicitly wired.
        return NetworkServer.active && gate != null && gate.isActiveAndEnabled &&
            gate._canResolve != null && gate._canResolve();
    }
}
