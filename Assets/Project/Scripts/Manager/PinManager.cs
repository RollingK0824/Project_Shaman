using System;
using System.Collections.Generic;
using UnityEngine;

public class PinManager : SceneSingleton<PinManager>
{
    private const int MAX_PINS = 3;

    private List<int> _quests = new List<int>();
    private List<string> _villagers = new List<string>();

    public IReadOnlyList<int> PinnedQuests => _quests;
    public IReadOnlyList<string> PinnedVillagers => _villagers;

    public event Action PinsChanged;

    private void OnEnable()
    {
        QuestManager quests = QuestManager.Instance;
        quests.QuestEnded += HandleQuestEnded;
        quests.QuestListChanged += DropInactiveQuests;
    }

    private void OnDisable()
    {
        QuestManager quests = QuestManager.Existing;
        if (quests == null) return;
        quests.QuestEnded -= HandleQuestEnded;
        quests.QuestListChanged -= DropInactiveQuests;
    }

    public bool IsQuestPinned(int id)
    {
        return _quests.Contains(id);
    }

    public bool IsVillagerPinned(string id)
    {
        return _villagers.Contains(id);
    }

    public bool ToggleQuest(int id)
    {
        bool pinned = Toggle(_quests, id);
        PinsChanged?.Invoke();
        return pinned;
    }

    public bool ToggleVillager(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        bool pinned = Toggle(_villagers, id);
        PinsChanged?.Invoke();
        return pinned;
    }

    private static bool Toggle<T>(List<T> pins, T id)
    {
        if (pins.Remove(id)) return false;
        if (pins.Count >= MAX_PINS) pins.RemoveAt(0);
        pins.Add(id);
        return true;
    }

    private void HandleQuestEnded(IQuestView quest)
    {
        if (_quests.Remove(quest.InstanceId)) PinsChanged?.Invoke();
    }

    private void DropInactiveQuests()
    {
        int removed = _quests.RemoveAll(id => !IsActive(id));
        if (removed > 0) PinsChanged?.Invoke();
    }

    private static bool IsActive(int id)
    {
        foreach (IQuestView q in QuestManager.Instance.Quests)
            if (q.InstanceId == id) return q.State == QuestState.Active;
        return false;
    }
}
