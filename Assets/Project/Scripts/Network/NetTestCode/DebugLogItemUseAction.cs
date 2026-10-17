using UnityEngine;

[CreateAssetMenu(menuName = "Project Shaman/Test/Debug Lopg Item Use Action")]
public class DebugLogItemUseAction : ItemUseAction
{
    [SerializeField] private int _consumeAmount = 1;

    public override bool Execute(in ItemUseContext context)
    {
        Debug.Log($"[DebugLogItemUseAction] 판정 실행 : {context.Owner.name} / {context.Data.DisplayName} / 조준 {context.AimOrigin} -> {context.AimDirection}");
        context.Consume?.Invoke(_consumeAmount);
        return true;
    }

    public override void PlayRemotePresentation(in ItemUseContext context)
    {
        Debug.Log($"[DebugLogItemUseAction] 원격 연출 : {context.Owner.name} / {context.Data.DisplayName}");
        Debug.DrawRay(context.AimOrigin, context.AimDirection * 5f, Color.cyan, 2f);
    }
}
