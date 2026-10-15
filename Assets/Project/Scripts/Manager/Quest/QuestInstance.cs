using UnityEngine;

public class QuestInstance : IQuestView
{
    public int InstanceId { get; set; }
    public QuestDefinition Definition { get; set; }
    public string GiverId { get; set; }
    public int DayIssued { get; set;  }
    public QuestState State { get; set;}
    public string TargetId { get; set; }
    public string ContextId { get; set; }
    public uint AcceptedBy { get; set; }
    public int Progress { get; set; }
}
