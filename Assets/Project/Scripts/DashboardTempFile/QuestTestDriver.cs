using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

// 임시 테스트용. 팀원 쪽 호출(TryOffer, TryAcceptQuest, RaiseActionDone)이 생기면 삭제
// F5: 의뢰 생성 / F6: 의뢰 받기 / F7: 진행 중인 퀘스트의 행동 1회 발생
public class QuestTestDriver : MonoBehaviour
{
    private const uint TEST_PLAYER_ID = 1;

    private void OnEnable()
    {
        QuestManager quests = QuestManager.Instance;
        quests.QuestOffered += q => Log("Offered", q);
        quests.QuestStarted += q => Log("Started", q);
        quests.QuestUpdated += q => Log("Updated", q);
        quests.QuestEnded += q => Log("Ended", q);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.f5Key.wasPressedThisFrame)
        {
            OfferToFirstFreeVillager();
        }
        else if (keyboard.f6Key.wasPressedThisFrame)
        {
            AcceptFirstOffer();
        }
        else if (keyboard.f7Key.wasPressedThisFrame)
        {
            ActOnFirstActiveQuest();
        }
    }

    private void OfferToFirstFreeVillager()
    {
        VillagerRegistry registry = AIManager.Instance != null ? AIManager.Instance.Registry : null;
        if (registry == null)
        {
            Debug.LogWarning("[QuestTestDriver] No villager registry in this scene");
            return;
        }

        foreach (IVillagerView villager in registry.Villagers)
        {
            if (villager.IsAlive && QuestManager.Instance.TryOffer(villager.VillagerId))
            {
                return;
            }
        }

        Debug.LogWarning("[QuestTestDriver] TryOffer refused for every villager (window passed, all busy, or empty database)");
    }

    private void AcceptFirstOffer()
    {
        foreach (IQuestView quest in QuestManager.Instance.Quests)
        {
            if (quest.State == QuestState.Offered)
            {
                bool accepted = QuestManager.Instance.TryAcceptQuest(TEST_PLAYER_ID, quest.GiverId);
                Debug.Log($"[QuestTestDriver] Accept #{quest.InstanceId}: {accepted}");
                return;
            }
        }

        Debug.LogWarning("[QuestTestDriver] No offered quest");
    }

    private void ActOnFirstActiveQuest()
    {
        foreach (IQuestView quest in QuestManager.Instance.Quests)
        {
            if (quest.State == QuestState.Active)
            {
                GameplayEvents.RaiseActionDone(TEST_PLAYER_ID, quest.Definition.ObjectiveType, quest.TargetId, quest.ContextId);
                return;
            }
        }

        Debug.LogWarning("[QuestTestDriver] No active quest");
    }

    private void Log(string what, IQuestView quest)
    {
        Debug.Log($"[QuestTestDriver] {what}: #{quest.InstanceId} {quest.Definition.Id} giver={quest.GiverId} " +
                  $"state={quest.State} progress={quest.Progress}/{quest.Definition.RequiredAmount} context={quest.ContextId}");
    }
}
