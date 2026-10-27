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
    [Tooltip("받지 않은 채 밤이 되면 실패 (그날의 일: 도구 찾기 등). 끄면 다음 날 아침 다시 받을 수 있다")]
    public bool ExpiresAtNight;
    [Tooltip("진행 중이어도 밤에 실패하지 않는다. 기한은 의뢰를 만든 쪽이 정해 ExpireOpenQuest로 끝낸다 (도구 찾기: 그 도구가 필요한 다음 일과 시작)")]
    public bool OutlastsNight;
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