using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;
using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestManager : SceneSingleton<QuestManager>
{
    [SerializeField]
    private QuestDatabase _database;

    [SerializeField]
    private float _offerWindowSeconds = 120f;

    [SerializeField]
    private int _maxActive = 3;

    [SerializeField]
    private int _maxHistory = 3;

    private List<QuestInstance> _quests = new List<QuestInstance>();
    private List<QuestInstance> _history = new List<QuestInstance>();
    private int _nextInstanceId;
    private VillagerRegistry _registry => AIManager.Instance != null ? AIManager.Instance.Registry : null;
    private VillagerRegistry _subscribedReg;


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
        if (!TryGetOpenQuest(giverId, out QuestInstance quest) || quest.State != QuestState.Offered || ActiveCount >= _maxActive)
        {
            return false;
        }

        if (!TryResolveContextId(quest, out string contextId))
        {
            return false;
        }

        quest.State = QuestState.Active;
        quest.AcceptedBy = playerId;
        quest.TargetId = quest.Definition.TargetId;
        quest.ContextId = contextId;
        QuestStarted?.Invoke(quest);
        return true;
    }

    // "$giver" → 의뢰인, "$randomVillager" → 의뢰인이 아닌 살아있는 무작위 주민, 빈 값 → null (아무나/어디서나)
    private bool TryResolveContextId(QuestInstance quest, out string contextId)
    {
        string raw = quest.Definition.ContextId;

        if (string.IsNullOrEmpty(raw))
        {
            contextId = null;
            return true;
        }

        if (raw == "$giver")
        {
            contextId = quest.GiverId;
            return true;
        }

        if (raw == "$randomVillager")
        {
            List<string> candidates = new List<string>();

            if (_registry != null)
            {
                foreach (IVillagerView villager in _registry.Villagers)
                {
                    if (villager.VillagerId != quest.GiverId && IsGiverAlive(villager.VillagerId))
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
        if (!IsInOfferWindow() || TryGetOpenQuest(giverId, out _) || !IsGiverAlive(giverId))
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

        QuestInstance quest = new QuestInstance
        {
            InstanceId = _nextInstanceId++,
            Definition = definition,
            GiverId = giverId,
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

    // 받지 않은 의뢰와 진행 중인 퀘스트 모두 실패 (패널티)
    private void HandleNightStart()
    {
        // End 가 리스트에서 지우므로 뒤에서부터 돈다
        for (int i = _quests.Count - 1; i >= 0; i--)
        {
            QuestInstance quest = _quests[i];

            if (quest.State == QuestState.Offered || quest.State == QuestState.Active)
            {
                End(quest, QuestState.Failed);
            }
        }
    }

    private void HandleVillagerDied(IVillagerView villager)
    {
        HandleGiverRemoved(villager.VillagerId);
    }

    // 의뢰인 사망/추방 → 그 퀘스트 삭제. 받을 주민이 죽은 Deliver 퀘스트도 완료할 수 없으므로 삭제
    private void HandleGiverRemoved(string villagerId)
    {
        // 투표에서 아무도 추방되지 않으면 null
        if (villagerId == null)
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
            bool isReceiver = quest.Definition.ObjectiveType == "Deliver" && quest.ContextId == villagerId;

            if (isGiver || isReceiver)
            {
                End(quest, QuestState.Cancelled);
            }
        }
    }

    // GameplayEvents.ActionDone 구독: 플레이어가 아이템을 전달/사용할 때마다 호출됨
    private void HandleActionDone(uint playerId, string actionType, string targetId, string contextId)
    {
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
    }

    private bool IsInOfferWindow()
    {
        double intoCycle = TimeManager.Instance.Elapsed % TimeManager.CYCLE_DURATION;
        float window = Mathf.Min(_offerWindowSeconds, TimeManager.DAY_DURATION);
        return intoCycle < window;
    }
}