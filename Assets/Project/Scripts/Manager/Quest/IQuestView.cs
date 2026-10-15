using UnityEngine;

public enum QuestState { Offered, Active, Completed, Failed, Cancelled }

public interface IQuestView
{
    int InstanceId { get; }
    QuestDefinition Definition { get; }
    string GiverId { get; }       // may be a ghost; never reveal in UI
    string TargetId { get; }      // resolved (null while Offered)
    string ContextId { get; }    // resolved, null if none
    int Progress { get; }
    QuestState State { get; }
    int DayIssued { get; }
    uint AcceptedBy { get; }    // player netId who received it, 0 = not yet
}
