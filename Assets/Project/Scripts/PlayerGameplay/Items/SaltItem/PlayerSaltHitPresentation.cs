using System;
using System.Collections.Generic;
using UnityEngine;

// Server/offline confirms a hit; clients consume only its immutable visual information.
// Networking adapters may relay ConfirmedHit to PresentConfirmedHit; never back into damage.
[DisallowMultipleComponent]
public sealed class PlayerSaltHitPresentation : MonoBehaviour
{
    public event Action<long, int, Vector3, Vector3> ConfirmedHit;
    private long _sequence;
    private readonly HashSet<(long, int)> _shown = new();
    private readonly Queue<(long, int)> _history = new();
    public long BeginAttack() => ++_sequence;
    public void PublishConfirmedHit(long attackId, int targetIndex, Vector3 point, Vector3 normal)
    {
        if (!ItemAttackAuthority.CanResolve(gameObject)) return;
        // Presentation/adapter errors must never interrupt damage or consumption callbacks.
        try { PresentConfirmedHit(attackId, targetIndex, point, normal); }
        catch (Exception e) { Debug.LogException(e, this); }
        if (ConfirmedHit == null) return;
        foreach (Action<long, int, Vector3, Vector3> handler in ConfirmedHit.GetInvocationList())
            try { handler(attackId, targetIndex, point, normal); }
            catch (Exception e) { Debug.LogException(e, this); }
    }
    public void PresentConfirmedHit(long attackId, int targetIndex, Vector3 point, Vector3 normal)
    {
        if (attackId <= 0 || targetIndex < 0 || !Finite(point) || !Finite(normal)) return;
        var key = (attackId, targetIndex);
        if (!_shown.Add(key)) return;
        _history.Enqueue(key);
        while (_history.Count > 128) _shown.Remove(_history.Dequeue());
        SaltVfxBurst.Impact(point, normal);
    }
    private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
}
