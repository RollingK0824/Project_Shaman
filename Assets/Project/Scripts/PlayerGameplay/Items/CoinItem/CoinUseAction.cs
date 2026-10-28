using UnityEngine;

// The same confirmed-use path as salt/bell. Receivers return true only after accepting one coin.
[CreateAssetMenu(fileName = "CoinUseAction", menuName = "Project Shaman/Item Use/Coin")]
public sealed class CoinUseAction : ItemUseAction
{
    public override void PlayRemotePresentation(in ItemUseContext context)
    {
        if (context.Owner != null) context.Owner.GetComponent<PlayerItemFeedback>()?.PlayRemoteAudio(context.Data);
    }
    [SerializeField, Min(.1f)] private float _reach = 3f;
    [SerializeField] private LayerMask _targetMask = ~0;

    public override bool Execute(in ItemUseContext context)
    {
        if (!ItemAttackAuthority.CanResolve(context.Owner) || context.Data == null ||
            context.Data.IsPermanent || context.Consume == null) return false;
        var inventory = context.Owner.GetComponent<PlayerInventory>();
        if (inventory == null || !inventory.Contains(context.Data)) return false;

        // Ignore the user's own body, but never hand a coin through another solid object.
        RaycastHit? nearest = null;
        foreach (var hit in Physics.RaycastAll(context.AimOrigin, context.AimDirection, _reach,
                     _targetMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(context.Owner.transform)) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        var target = nearest.HasValue ? nearest.Value.collider.GetComponentInParent<ICoinReactable>() : null;
        if (target == null || !target.ReactToCoin(context.Owner)) return false;

        context.Consume(1);
        Debug.Log($"[Coin] 전달 성공: {context.Data.DisplayName} 1개 소모", context.Owner);
        return true;
    }
}
