using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct QuestDefinition
{
    public string Id;
    public string Title;
    [TextArea] public string Objective;
    public string ObjectiveType;
    public string TargetId;
    public string ContextId;

    public int RequiredAmount;
    public QuestEffect Reward;
    public QuestEffect Penalty;
}

[Serializable]
public struct QuestEffect
{
    public string Type; // 받는 보상/패널티 타입
    public string Id;
    public int Amount;
    public string Description;
}

[CreateAssetMenu(menuName = "Project Shaman/Quest Database")]
public class QuestDatabase : ScriptableObject
{
    [SerializeField] List<QuestDefinition> _quests;
    public IReadOnlyList<QuestDefinition> Quests => _quests;
    public bool TryGet(string id, out QuestDefinition def)
    {
        foreach (QuestDefinition quest in _quests)
        {
            if (quest.Id == id) { def = quest; return true; }
        }
        def = default;
        return false;
    }
}