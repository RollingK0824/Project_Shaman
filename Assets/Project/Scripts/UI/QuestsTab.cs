using System.Collections.Generic;
using UnityEngine;

public class QuestsTab : MonoBehaviour
{
    [SerializeField] private Transform _activeRoot;    // left
    [SerializeField] private Transform _finishedRoot;  // right

    [SerializeField]
    private QuestEntryUI _entryPrefab;

    [SerializeField]
    private GameObject _emptyText;

    [SerializeField]
    private GameObject _emptyHistoryText;

    private void OnEnable()
    {
        QuestManager quests = QuestManager.Instance;
        quests.QuestStarted += HandleQuestChanged;
        quests.QuestUpdated += HandleQuestChanged;
        quests.QuestEnded += HandleQuestChanged;
        Rebuild();
    }

    private void OnDisable()
    {
        QuestManager quests = QuestManager.Existing;
        if (quests == null) return;
        quests.QuestStarted -= HandleQuestChanged;
        quests.QuestUpdated -= HandleQuestChanged;
        quests.QuestEnded -= HandleQuestChanged;
    }

    private void HandleQuestChanged(IQuestView quest)
    {
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (Transform child in _activeRoot) Destroy(child.gameObject);
        foreach (Transform child in _finishedRoot) Destroy(child.gameObject);

        foreach (IQuestView q in QuestManager.Instance.Quests)
            if (q.State == QuestState.Active) Instantiate(_entryPrefab, _activeRoot).Set(q);

        IReadOnlyList<IQuestView> history = QuestManager.Instance.History;
        for (int i = history.Count - 1; i >= 0; i--)
            Instantiate(_entryPrefab, _finishedRoot).Set(history[i]);

        _emptyText.SetActive(QuestManager.Instance.ActiveCount == 0);
        _emptyHistoryText.SetActive(history.Count == 0);
    }
}
