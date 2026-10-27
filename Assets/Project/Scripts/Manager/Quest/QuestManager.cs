using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : SceneSingleton<QuestManager>
{
    [SerializeField]
    private QuestDatabase _database;

    // Optional: item display names for quest text ({target} = item id)
    [SerializeField]
    private QuestItemCatalog _itemCatalog;

    [SerializeField]
    private int _maxActive = 3;

    [SerializeField]
    private int _maxHistory = 3;

    private List<QuestInstance> _quests = new List<QuestInstance>();
    private List<QuestInstance> _history = new List<QuestInstance>();
    private int _nextInstanceId;
    private VillagerRegistry _registry => AIManager.Instance != null ? AIManager.Instance.Registry : null;
    private VillagerRegistry _subscribedReg;

    public const string TARGET_PARAM = "$target";
    public const string CONTEXT_PARAM = "$context";


    public IReadOnlyList<IQuestView> Quests => _quests;
    public IReadOnlyList<IQuestView> History => _history;

    public int ActiveCount
    {
        get
        {
            int count = 0;
            foreach (QuestInstance quest in _quests)
            {
                if (quest.State == QuestState.Active)
                {
                    count++;
                }
            }
            return count;
        }
    }

    public event Action<IQuestView> QuestOffered;
    public event Action<IQuestView> QuestStarted;
    public event Action<IQuestView> QuestUpdated;
    public event Action<IQuestView> QuestEnded;
    // Clients: the replicated list was replaced (no per-quest events there)
    public event Action QuestListChanged;

    public QuestItemCatalog ItemCatalog => _itemCatalog;

    // Quest rules run on the server (or offline); clients only mirror the list
    private static bool IsServerSide => !NetworkClient.active || NetworkServer.active;


    private bool IsGiverAlive(string villagerId)
    {
        return _registry != null
            && _registry.TryGet(villagerId, out IVillagerView villager)
            && villager.IsAlive
            && !VoteManager.Instance.WasVotedOut(villagerId);
    }

    public bool HasOffer(string giverId)
    {
        return TryGetOpenQuest(giverId, out QuestInstance quest) && quest.State == QuestState.Offered;
    }

    // An accepted quest is finished at this villager / quest target (ContextId), e.g. delivery back to the giver
    public bool HasActiveAt(string contextId)
    {
        if (string.IsNullOrEmpty(contextId))
        {
            return false;
        }

        foreach (QuestInstance quest in _quests)
        {
            if (quest.State == QuestState.Active && quest.ContextId == contextId)
            {
                return true;
            }
        }
        return false;
    }

    private bool TryGetOpenQuest(string giverId, out QuestInstance quest)
    {
        foreach (QuestInstance q in _quests)
        {
            if (q.GiverId == giverId && (q.State == QuestState.Offered || q.State == QuestState.Active))
            {
                quest = q;
                return true;
            }
        }
        quest = null;
        return false;
    }

    public bool TryAcceptQuest (uint playerId, string giverId)
    {
        if (!IsServerSide)
        {
            return false;
        }

        if (!TryGetOpenQuest(giverId, out QuestInstance quest) || quest.State != QuestState.Offered || ActiveCount >= _maxActive)
        {
            return false;
        }

        quest.State = QuestState.Active;
        quest.AcceptedBy = playerId;
        QuestStarted?.Invoke(quest);
        return true;
    }

    // "$target" → 호출자가 넘긴 값 (필수), 그 외 → 정의 값 그대로
    private static bool TryResolveTargetId(QuestDefinition definition, string targetId, out string resolved)
    {
        resolved = definition.TargetId == TARGET_PARAM ? targetId : definition.TargetId;
        return !string.IsNullOrEmpty(resolved);
    }

    // "$context" → 호출자가 넘긴 값 (필수, 퀘스트 대상 오브젝트 ID 등), "$giver" → 의뢰인,
    // "$randomVillager" → 의뢰인이 아닌 살아있는 무작위 주민, 빈 값 → null (아무나/어디서나)
    private bool TryResolveContextId(QuestDefinition definition, string giverId, string contextArg, out string contextId)
    {
        string raw = definition.ContextId;

        if (raw == CONTEXT_PARAM)
        {
            contextId = contextArg;
            return !string.IsNullOrEmpty(contextId);
        }

        if (string.IsNullOrEmpty(raw))
        {
            contextId = null;
            return true;
        }

        if (raw == "$giver")
        {
            contextId = giverId;
            return true;
        }

        if (raw == "$randomVillager")
        {
            List<string> candidates = new List<string>();

            if (_registry != null)
            {
                foreach (IVillagerView villager in _registry.Villagers)
                {
                    if (villager.VillagerId != giverId && IsGiverAlive(villager.VillagerId))
                    {
                        candidates.Add(villager.VillagerId);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                contextId = null;
                return false;
            }

            contextId = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return true;
        }

        // 장소 ID 등 그대로 사용
        contextId = raw;
        return true;
    }

    public bool TryOffer(string giverId, string questId = null)
    {
        return TryOffer(giverId, questId, null, null);
    }

    // targetId / contextId fill the definition's "$target" / "$context" (e.g. tool type, quest target object id).
    // Both are resolved here, at offer time, so "!" and dialogue can already describe the request.
    public bool TryOffer(string giverId, string questId, string targetId, string contextId)
    {
        if (!IsServerSide || !IsDay() || TryGetOpenQuest(giverId, out _) || !IsGiverAlive(giverId))
        {
            return false;
        }

        QuestDefinition definition;

        if (string.IsNullOrEmpty(questId))
        {
            if (_database == null || _database.Quests.Count == 0) return false;
            definition = _database.Quests[UnityEngine.Random.Range(0, _database.Quests.Count)];
        }
        else if (!_database.TryGet(questId, out definition))
        {
            Debug.LogWarning($"[QuestManager] Quest '{questId}' not found in QuestDatabase");
            return false;
        }

        if (!TryResolveTargetId(definition, targetId, out string resolvedTarget))
        {
            Debug.LogWarning($"[QuestManager] Quest '{definition.Id}' needs a target ($target) but none was given");
            return false;
        }

        if (!TryResolveContextId(definition, giverId, contextId, out string resolvedContext))
        {
            Debug.LogWarning($"[QuestManager] Quest '{definition.Id}' context '{definition.ContextId}' could not be resolved");
            return false;
        }

        QuestInstance quest = new QuestInstance
        {
            InstanceId = _nextInstanceId++,
            Definition = definition,
            GiverId = giverId,
            TargetId = resolvedTarget,
            ContextId = resolvedContext,
            DayIssued = TimeManager.Instance.DayCount,
            State = QuestState.Offered
        };

        _quests.Add(quest);
        QuestOffered?.Invoke(quest);
        return true;
    }

    private void OnEnable()
    {
        // RaiseActionDone 은 서버에서만 호출되므로 클라이언트에서는 구독해도 아무 일도 없음
        GameplayEvents.ActionDone += HandleActionDone;
        QuestText.AddNameResolver(ResolveItemName);

        if (TimeManager.Instance)
        {
            TimeManager.Instance.OnNightStart += HandleNightStart;
        }

        if (VoteManager.Instance)
        {
            VoteManager.Instance.NpcVotedOut += HandleGiverRemoved;
        }

        SubscribeRegistry();
    }

    private void Start()
    {
        // OnEnable 때 AI 시스템이 아직 레지스트리를 만들지 않았을 수 있으므로 한 번 더 시도
        if (_subscribedReg == null)
        {
            SubscribeRegistry();
        }
    }

    private void SubscribeRegistry()
    {
        _subscribedReg = _registry;
        if (_subscribedReg != null)
        {
            _subscribedReg.OnDied += HandleVillagerDied;
        }
    }

    private void OnDisable()
    {
        GameplayEvents.ActionDone -= HandleActionDone;
        QuestText.RemoveNameResolver(ResolveItemName);

        if (TimeManager.Existing)
        {
            TimeManager.Existing.OnNightStart -= HandleNightStart;
        }

        if (VoteManager.Existing)
        {
            VoteManager.Existing.NpcVotedOut -= HandleGiverRemoved;
        }

        if (_subscribedReg != null)
        {
            _subscribedReg.OnDied -= HandleVillagerDied;
            _subscribedReg = null;
        }
    }

    // 진행 중인 퀘스트는 실패 (패널티). 받지 않은 의뢰는 남겨 두었다가 다음 날 아침 다시 받을 수 있다
    // (ExpiresAtNight 의뢰는 그날로 실패, 의뢰인 사망·대상 소멸 등으로 사라질 일이면 그때 취소된다)
    // OutlastsNight 퀘스트는 밤을 넘긴다: 기한은 ExpireOpenQuest가 정한다
    private void HandleNightStart()
    {
        if (!IsServerSide)
        {
            return;
        }

        // End 가 리스트에서 지우므로 뒤에서부터 돈다
        for (int i = _quests.Count - 1; i >= 0; i--)
        {
            QuestInstance quest = _quests[i];

            if (quest.Definition.OutlastsNight)
            {
                continue;
            }

            if (quest.State == QuestState.Active || (quest.State == QuestState.Offered && quest.Definition.ExpiresAtNight))
            {
                End(quest, QuestState.Failed);
            }
        }
    }

    private void HandleVillagerDied(IVillagerView villager)
    {
        HandleGiverRemoved(villager.VillagerId);
    }

    // 의뢰인 사망/추방 → 그 퀘스트 삭제. 대상·문맥이 그 주민인 퀘스트(전달 받을 주민 등)도 완료할 수 없으므로 삭제
    private void HandleGiverRemoved(string villagerId)
    {
        // 투표에서 아무도 추방되지 않으면 null
        if (villagerId == null || !IsServerSide)
        {
            return;
        }

        // End(Cancelled) 가 리스트에서 지우므로 뒤에서부터 돈다
        for (int i = _quests.Count - 1; i >= 0; i--)
        {
            QuestInstance quest = _quests[i];

            if (quest.State != QuestState.Offered && quest.State != QuestState.Active)
            {
                continue;
            }

            bool isGiver = quest.GiverId == villagerId;
            bool isReceiver = quest.ContextId == villagerId || quest.TargetId == villagerId;

            if (isGiver || isReceiver)
            {
                End(quest, QuestState.Cancelled);
            }
        }
    }

    // GameplayEvents.ActionDone 구독: 플레이어가 아이템을 전달/사용할 때마다 호출됨
    private void HandleActionDone(uint playerId, string actionType, string targetId, string contextId)
    {
        if (!IsServerSide)
        {
            return;
        }

        // 완료되면 End 가 리스트에서 지우므로 뒤에서부터 돈다
        for (int i = _quests.Count - 1; i >= 0; i--)
        {
            QuestInstance quest = _quests[i];

            if (quest.State != QuestState.Active)
            {
                continue;
            }

            if (quest.Definition.ObjectiveType != actionType || quest.TargetId != targetId)
            {
                continue;
            }

            // ContextId 가 정해진 퀘스트만 받는 주민/장소까지 비교 (null = 아무나/어디서나)
            if (!string.IsNullOrEmpty(quest.ContextId) && quest.ContextId != contextId)
            {
                continue;
            }

            quest.Progress++;
            QuestUpdated?.Invoke(quest);

            if (quest.Progress >= quest.Definition.RequiredAmount)
            {
                OnObjectiveMet(quest);
            }
        }
    }

    // 보상 수령 기능이 정해지면 여기만 바꾸면 됨
    private void OnObjectiveMet(QuestInstance quest)
    {
        End(quest, QuestState.Completed);
    }

    // 끝난 퀘스트는 _quests 에서 빠짐. 완료/실패는 기록에 남고 취소는 사라짐
    // 리스트를 먼저 바꾼 뒤 이벤트를 보내야 구독자(노트북)가 바뀐 목록을 읽음
    private void End(QuestInstance quest, QuestState result)
    {
        quest.State = result;
        _quests.Remove(quest);

        if (result == QuestState.Completed || result == QuestState.Failed)
        {
            _history.Add(quest);

            // 가장 오래된 기록부터 지워서 최근 _maxHistory 개만 남김
            while (_history.Count > _maxHistory)
            {
                _history.RemoveAt(0);
            }
        }

        QuestEnded?.Invoke(quest);
        QuestOutcomeApplier.Apply(quest, _itemCatalog);
    }

    // Server → network: open quests then history, ids and numbers only
    public List<QuestSyncEntry> ExportForNetwork()
    {
        List<QuestSyncEntry> entries = new List<QuestSyncEntry>(_quests.Count + _history.Count);

        foreach (QuestInstance quest in _quests)
        {
            entries.Add(ToEntry(quest));
        }

        foreach (QuestInstance quest in _history)
        {
            entries.Add(ToEntry(quest));
        }

        return entries;
    }

    // Network → client: rebuild the read-only copy from the same QuestDatabase
    public void ApplyNetworkQuests(IList<QuestSyncEntry> entries)
    {
        if (IsServerSide || entries == null)
        {
            return;
        }

        _quests.Clear();
        _history.Clear();

        foreach (QuestSyncEntry entry in entries)
        {
            if (_database == null || !_database.TryGet(entry.QuestId, out QuestDefinition definition))
            {
                Debug.LogWarning($"[QuestManager] Replicated quest '{entry.QuestId}' not in QuestDatabase");
                continue;
            }

            QuestInstance quest = new QuestInstance
            {
                InstanceId = entry.InstanceId,
                Definition = definition,
                GiverId = entry.GiverId,
                TargetId = entry.TargetId,
                ContextId = entry.ContextId,
                Progress = entry.Progress,
                State = (QuestState)entry.State,
                DayIssued = entry.DayIssued,
                AcceptedBy = entry.AcceptedBy
            };

            if (quest.State == QuestState.Offered || quest.State == QuestState.Active)
            {
                _quests.Add(quest);
            }
            else
            {
                _history.Add(quest);
            }
        }

        QuestListChanged?.Invoke();
    }

    private static QuestSyncEntry ToEntry(QuestInstance quest)
    {
        return new QuestSyncEntry
        {
            InstanceId = quest.InstanceId,
            QuestId = quest.Definition.Id,
            GiverId = quest.GiverId,
            TargetId = quest.TargetId,
            ContextId = quest.ContextId,
            Progress = quest.Progress,
            State = (int)quest.State,
            DayIssued = quest.DayIssued,
            AcceptedBy = quest.AcceptedBy
        };
    }

    // 퀘스트 대상 오브젝트가 퀘스트와 무관하게 사라짐(다른 방법으로 해결, 파괴 등) → 완료할 수 없으므로 삭제
    public void CancelByContext(string contextId)
    {
        if (string.IsNullOrEmpty(contextId) || !IsServerSide)
        {
            return;
        }

        for (int i = _quests.Count - 1; i >= 0; i--)
        {
            QuestInstance quest = _quests[i];

            if ((quest.State == QuestState.Offered || quest.State == QuestState.Active) && quest.ContextId == contextId)
            {
                End(quest, QuestState.Cancelled);
            }
        }
    }

    // 기한이 된 의뢰: 받은 퀘스트는 실패(패널티), 아무도 받지 않은 의뢰는 취소
    public void ExpireOpenQuest(string giverId, string targetId)
    {
        if (!IsServerSide || !TryGetOpenQuest(giverId, out QuestInstance quest) || quest.TargetId != targetId)
        {
            return;
        }

        End(quest, quest.State == QuestState.Active ? QuestState.Failed : QuestState.Cancelled);
    }

    private string ResolveItemName(string id)
    {
        ItemData item = _itemCatalog != null ? _itemCatalog.Find(id) : null;
        return item != null ? item.DisplayName : null;
    }

    // 의뢰는 낮 동안 언제든 생길 수 있다 (트리거 기반). 받지 않은 의뢰는 밤을 넘겨 남는다
    private bool IsDay()
    {
        return TimeManager.Instance.Elapsed % TimeManager.CYCLE_DURATION < TimeManager.DAY_DURATION;
    }
}