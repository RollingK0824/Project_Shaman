using UnityEngine;

// A world object a quest can point at (trace, well, guardian ...). The quest stores QuestContextId;
// the object itself judges what was done to it and reports through QuestTargets, so completion never
// depends on where the player stood.
public interface IQuestTarget
{
    string QuestContextId { get; }
    string DisplayName { get; }
    Vector3 Position { get; }

    // actionType: QuestActions.*, actionId: item id (UseOn), trace kind (Clear), action id (Perform)
    bool Accepts(string actionType, string actionId);
}

public static class QuestActions
{
    public const string USE = "Use";             // item used anywhere (PlayerItemActions)
    public const string USE_ON = "UseOn";        // item used on a quest target
    public const string CLEAR = "Clear";         // target cleared by hand
    public const string PERFORM = "Perform";     // ritual action performed at a target (gut ...)
    public const string DELIVER = "Deliver";
    public const string TALK = "Talk";
}
