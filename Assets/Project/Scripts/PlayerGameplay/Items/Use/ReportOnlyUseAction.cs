using UnityEngine;

// [판정 전용] 효과가 로컬에서만 보이는 아이템(초혼경 등)의 사용 사실만 판정자에게 알림
// Execute가 true면 판정자가 퀘스트 Use를 보고함
[CreateAssetMenu(fileName = "ReportOnlyUseAction", menuName = "Project Shaman/Item Use/Report Only")]
public class ReportOnlyUseAction : ItemUseAction
{
    public override bool Execute(in ItemUseContext context) => context.Owner != null;
}
