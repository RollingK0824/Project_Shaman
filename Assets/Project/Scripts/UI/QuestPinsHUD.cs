using UnityEngine;

// Pinned quests on the HUD, right side. Cards follow PinManager's order (oldest first)
public class QuestPinsHUD : MonoBehaviour
{
    // Child holding the cards, hidden when nothing is pinned. Not this object, or the events would unsubscribe
    [SerializeField]
    private GameObject _panel;

    // One card per pin slot (PinManager's cap), already placed in the layout
    [SerializeField]
    private QuestPinCard[] _cards;

    private void OnEnable()
    {
        PinManager.Instance.PinsChanged += Refresh;
        QuestManager quests = QuestManager.Instance;
        quests.QuestUpdated += HandleQuestChanged;
        quests.QuestListChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PinManager pins = PinManager.Existing;
        if (pins != null) pins.PinsChanged -= Refresh;
        QuestManager quests = QuestManager.Existing;
        if (quests == null) return;
        quests.QuestUpdated -= HandleQuestChanged;
        quests.QuestListChanged -= Refresh;
    }

    private void HandleQuestChanged(IQuestView quest)
    {
        if (PinManager.Instance.IsQuestPinned(quest.InstanceId)) Refresh();
    }

    private void Refresh()
    {
        int shown = 0;
        foreach (int id in PinManager.Instance.PinnedQuests)
        {
            IQuestView quest = FindActive(id);
            if (quest == null || shown >= _cards.Length) continue;
            _cards[shown].Set(quest);
            _cards[shown].gameObject.SetActive(true);
            shown++;
        }

        for (int i = shown; i < _cards.Length; i++) _cards[i].gameObject.SetActive(false);
        _panel.SetActive(shown > 0);
    }

    // A pin whose quest just ended may still be listed until PinManager drops it
    private static IQuestView FindActive(int id)
    {
        foreach (IQuestView q in QuestManager.Instance.Quests)
            if (q.InstanceId == id) return q.State == QuestState.Active ? q : null;
        return null;
    }
}
