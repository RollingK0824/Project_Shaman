using UnityEngine;

public enum QuestState { Offered, Active, Completed, Failed, Cancelled }

public interface IQuestView
{
    int InstanceId { get; }
    QuestDefinition Definition { get; }
    string TitleText { get; }      // Definition.Title with {giver}/{target}/{context}/{area} filled
    string ObjectiveText { get; }  // Definition.Objective, same placeholders
    string GiverId { get; }       // may be a ghost; never reveal in UI
    string TargetId { get; }      // resolved when offered ("$target" → value passed to TryOffer)
    string ContextId { get; }    // resolved when offered ("$context" → value passed to TryOffer), null if none
    int Progress { get; }
    QuestState State { get; }
    int DayIssued { get; }
    uint AcceptedBy { get; }    // player netId who received it, 0 = not yet
}
