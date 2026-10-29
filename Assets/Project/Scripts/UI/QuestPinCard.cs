using TMPro;
using UnityEngine;

// One pinned quest on the HUD: just what's needed while playing. Reward and penalty stay in the notebook
public class QuestPinCard : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _title, _objective, _progress;

    public void Set(IQuestView q)
    {
        _title.SetText(string.IsNullOrEmpty(q.Definition.Title) ? "이름 없는 퀘스트" : q.TitleText);
        _objective.SetText(q.ObjectiveText);
        _progress.SetText($"{q.Progress} / {q.Definition.RequiredAmount}");
    }
}
