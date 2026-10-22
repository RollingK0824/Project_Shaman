using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;
using TMPro;
using UnityEngine;

public class QuestEntryUI : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _giver, _title, _objective, _progress, _reward, _penalty, _state;

    [SerializeField]
    private CanvasGroup _canvasGroup;

    public void Set(IQuestView q)
    {
        if (q == null)
        {
            return;
        }

        VillagerRegistry registry = AIManager.Existing != null ? AIManager.Existing.Registry : null;
        _giver.SetText(registry != null && registry.TryGet(q.GiverId, out IVillagerView view) ? view.DisplayName : "???");

        _title.SetText(string.IsNullOrEmpty(q.Definition.Title) ? "이름 없는 퀘스트" : q.TitleText);
        _objective.SetText(q.ObjectiveText);
        _progress.SetText($"{q.Progress} / {q.Definition.RequiredAmount}");
        _reward.SetText(q.Definition.Reward.Description);
        _penalty.SetText(q.Definition.Penalty.Description);
        string state = q.State == QuestState.Active ? "진행 중"
             : q.State == QuestState.Completed ? "완료"
             : q.State == QuestState.Failed ? "실패" : "";
        _state.SetText(state);

        _canvasGroup.alpha = q.State == QuestState.Active ? 1f : 0.5f;
    }
}
