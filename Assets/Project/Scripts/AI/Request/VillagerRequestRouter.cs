using System.Collections.Generic;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Dialogue;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Stress;
using ProjectShaman.AI.Trace;

namespace ProjectShaman.AI.Request
{
    // Server only. Turns villager triggers into QuestManager offers; QuestManager owns the quest lifetime.
    public sealed class VillagerRequestRouter
    {
        public const string FIND_TOOL = "FindTool";
        public const string TRACE = "Trace";
        public const string TRACE_CLEANSE = "TraceCleanse";
        public const string CHECK_ON = "CheckOn";
        public const string GUT = "Gut";

        private sealed class RequestOrigin
        {
            public string GiverId;
            public string Kind;
            public string Detail;
            public string TargetId;
            public string ContextId;
            public string BundleKey;
            public int Day;
        }

        private readonly AIBehaviourConfig _config;
        private readonly List<AI_Stress> _stresses;
        private readonly List<GhostTrace> _watchedTraces = new List<GhostTrace>();
        private readonly Dictionary<int, RequestOrigin> _origins = new Dictionary<int, RequestOrigin>();
        private readonly HashSet<string> _openBundles = new HashSet<string>();
        private readonly List<GhostTrace> _bundleBuffer = new List<GhostTrace>();

        private QuestManager _quests;
        private readonly List<(string GiverId, RequestOrigin Origin)> _deferred = new List<(string, RequestOrigin)>();
        private readonly HashSet<string> _deferredGivers = new HashSet<string>();
        private readonly System.Random _random = new System.Random();
        private RequestOrigin _pending;

        public VillagerRequestRouter(AIBehaviourConfig config, List<AI_Stress> stresses)
        {
            _config = config;
            _stresses = stresses;
        }

        public void Start()
        {
            _quests = QuestManager.Existing;
            if (_quests == null)
            {
                AILog.Warn(AILog.REQUEST, "QuestManager not in scene, villager requests disabled");
                return;
            }

            _quests.QuestOffered += HandleQuestOffered;
            _quests.QuestEnded += HandleQuestEnded;
            AIEvents.OnVillagerRequest += HandleRequest;
            AIEvents.OnVillagerToolRecovered += HandleToolRecovered;
            GhostTraceRegistry.OnTraceAdded += WatchTrace;
            GhostTraceRegistry.OnTraceResolved += HandleTraceResolved;

            foreach (GhostTrace trace in GhostTraceRegistry.Traces)
            {
                WatchTrace(trace);
            }

            foreach (AI_Stress stress in _stresses)
            {
                stress.OnStateChanged += HandleStressState;
            }

            AILog.Log(AILog.REQUEST, $"Router started (villagers={_stresses.Count}, traces={_watchedTraces.Count})");
        }

        public void Stop()
        {
            if (_quests == null)
            {
                return;
            }

            _quests.QuestOffered -= HandleQuestOffered;
            _quests.QuestEnded -= HandleQuestEnded;
            AIEvents.OnVillagerRequest -= HandleRequest;
            AIEvents.OnVillagerToolRecovered -= HandleToolRecovered;
            GhostTraceRegistry.OnTraceAdded -= WatchTrace;
            GhostTraceRegistry.OnTraceResolved -= HandleTraceResolved;

            foreach (GhostTrace trace in _watchedTraces)
            {
                if (trace != null)
                {
                    trace.OnWitnessed -= HandleWitnessed;
                }
            }

            foreach (AI_Stress stress in _stresses)
            {
                if (stress != null)
                {
                    stress.OnStateChanged -= HandleStressState;
                }
            }

            _watchedTraces.Clear();
            _origins.Clear();
            _openBundles.Clear();
            _deferred.Clear();
            _deferredGivers.Clear();
            _quests = null;
        }

        private void WatchTrace(GhostTrace trace)
        {
            if (trace == null || _watchedTraces.Contains(trace))
            {
                return;
            }

            _watchedTraces.Add(trace);
            trace.OnWitnessed += HandleWitnessed;
        }

        // Tool search gave up (expected place → other sightings → failed)
        private void HandleRequest(string villagerId, string requestType, string detail)
        {
            if (requestType == FIND_TOOL)
            {
                ExpireFindTool(villagerId, detail);
                Offer(villagerId, new RequestOrigin { Kind = FIND_TOOL, Detail = detail, TargetId = detail });
            }
            else if (requestType == CHECK_ON)
            {
                OfferCheckOn(villagerId, detail);
            }
        }

        // Asked today for this tool and still waiting: the rest of today's work with it is given up, not searched again
        public bool IsWaitingForTool(string villagerId, string toolTypeId, int day)
        {
            return FindToolRequestDay(villagerId, toolTypeId) == day;
        }

        // Day the open FindTool request for this tool was made, 0 if none
        private int FindToolRequestDay(string villagerId, string toolTypeId)
        {
            return TryFindToolRequest(villagerId, toolTypeId, out _, out int day) ? day : 0;
        }

        private bool TryFindToolRequest(string villagerId, string toolTypeId, out int instanceId, out int day)
        {
            instanceId = 0;
            day = 0;

            if (string.IsNullOrEmpty(toolTypeId))
            {
                return false;
            }

            foreach (KeyValuePair<int, RequestOrigin> pair in _origins)
            {
                RequestOrigin origin = pair.Value;
                if (origin.Kind == FIND_TOOL && origin.GiverId == villagerId && origin.TargetId == toolTypeId)
                {
                    instanceId = pair.Key;
                    day = origin.Day;
                    return true;
                }
            }

            return false;
        }

        // The deadline of a FindTool request (C120): the first routine on a later day that needs the tool searched again
        // and gave up. The rest of the asking day is waited out (IsWaitingForTool), so the request outlasts nights and
        // may run for days. The tool is still missing, so the villager asks again right after
        private void ExpireFindTool(string villagerId, string toolTypeId)
        {
            if (TryFindToolRequest(villagerId, toolTypeId, out int instanceId, out int requestDay)
                && TimeManager.Instance != null && TimeManager.Instance.DayCount > requestDay)
            {
                AILog.Log(AILog.REQUEST, villagerId, $"Needed the {toolTypeId} again and it was not brought back, request expired");
                _quests.TryFailQuest(instanceId);
            }
        }

        // The giver found the tool by itself: the request is withdrawn (FindTool's context is the giver)
        private void HandleToolRecovered(string villagerId, string toolTypeId)
        {
            bool hasRequest = false;
            foreach (RequestOrigin origin in _origins.Values)
            {
                if (origin.Kind == FIND_TOOL && origin.GiverId == villagerId && origin.TargetId == toolTypeId)
                {
                    hasRequest = true;
                    break;
                }
            }

            if (hasRequest)
            {
                AILog.Log(AILog.REQUEST, villagerId, $"Found the {toolTypeId} by itself, request withdrawn");
                _quests.CancelByContext(villagerId);
            }
        }

        // "OO hasn't shown up, would you look in on them?" → the request points at the body the hunt left
        private void OfferCheckOn(string giverId, string missingId)
        {
            GhostTrace body = null;
            foreach (GhostTrace trace in GhostTraceRegistry.Traces)
            {
                if (trace != null && trace.IsActive && trace.VictimId == missingId)
                {
                    body = trace;
                    break;
                }
            }

            if (body == null)
            {
                AILog.Log(AILog.REQUEST, giverId, $"Check-on for {missingId} skipped (body already found or cleared)");
                return;
            }

            Offer(giverId, new RequestOrigin { Kind = CHECK_ON, TargetId = missingId, ContextId = body.QuestContextId });
        }

        // One request per trace bundle (C37); the first witness becomes the giver
        private void HandleWitnessed(GhostTrace trace, string villagerId, bool detailed)
        {
            if (!trace.IsActive || _openBundles.Contains(trace.BundleKey))
            {
                return;
            }

            // Hand-clearable traces ask to be cleared, the rest (wells, guardians ...) to be cleansed with an item
            string kind = trace.ClearableByHand ? TRACE : TRACE_CLEANSE;
            RequestOrigin origin = new RequestOrigin
            {
                Kind = kind,
                Detail = trace.Kind.ToString(),
                TargetId = trace.Kind.ToString(),
                ContextId = trace.QuestContextId,
                BundleKey = trace.BundleKey
            };
            bool isBody = !string.IsNullOrEmpty(trace.VictimId);

            // Found on the way home (a look-in): the villager runs home and asks next morning
            if (IsHoming(villagerId))
            {
                _openBundles.Add(trace.BundleKey);
                Defer(villagerId, origin);
                AILog.Log(AILog.REQUEST, villagerId, $"Saw {trace.DisplayName} on the way home, will ask tomorrow morning");

                if (isBody)
                {
                    Panic(villagerId);
                }

                return;
            }

            if (isBody)
            {
                Panic(villagerId);
            }

            if (Offer(villagerId, origin))
            {
                _openBundles.Add(trace.BundleKey);
            }
        }

        // Next morning: requests held back from the evening, if the trace is still there
        public void OnDayStart()
        {
            if (_quests == null)
            {
                return;
            }

            foreach ((string giverId, RequestOrigin origin) in _deferred)
            {
                bool hasBundle = !string.IsNullOrEmpty(origin.BundleKey);
                if (hasBundle)
                {
                    _openBundles.Remove(origin.BundleKey);
                }

                bool targetStillThere = string.IsNullOrEmpty(origin.ContextId) || QuestTargets.TryGet(origin.ContextId, out _);
                if (targetStillThere && Offer(giverId, origin) && hasBundle)
                {
                    _openBundles.Add(origin.BundleKey);
                }
            }

            _deferred.Clear();
            _deferredGivers.Clear();
        }

        // A body is a shock: stress jumps to at least the anxious threshold and the villager cries out
        private void Panic(string villagerId)
        {
            AI_Stress stress = FindStress(villagerId);
            if (stress == null)
            {
                return;
            }

            AI_Conversation conversation = stress.GetComponent<AI_Conversation>();
            conversation?.Say(DialogueLibrary.Line("panic.body", _random), "found a body");

            float lift = _config.VillagerStressAnxious - stress.Value;
            if (lift > 0f)
            {
                AIEvents.RaiseStress(villagerId, "found a body", lift);
            }
        }

        private bool IsHoming(string villagerId)
        {
            AI_Stress stress = FindStress(villagerId);
            return stress != null && stress.TryGetComponent(out AI_Core core)
                && core.TryGetBlackboardVariable(AI_Core.BB_MUST_GO_HOME, out bool mustGoHome) && mustGoHome;
        }

        private AI_Stress FindStress(string villagerId)
        {
            foreach (AI_Stress stress in _stresses)
            {
                if (stress != null && stress.TryGetComponent(out AI_Core core) && core.LogId == villagerId)
                {
                    return stress;
                }
            }

            return null;
        }

        // Leaving Calm (to Anxious, or straight to Breaking) asks for a gut
        private void HandleStressState(AI_Stress stress, VillagerStressState previous, VillagerStressState next)
        {
            if (previous != VillagerStressState.Calm || next == VillagerStressState.Calm || !stress.TryGetComponent(out AI_Core core))
            {
                return;
            }

            // A villager holding a request for the morning keeps its one slot for it
            if (_deferredGivers.Contains(core.LogId))
            {
                return;
            }

            RequestOrigin gut = new RequestOrigin { Kind = GUT };

            // Asked in the evening it would fail at nightfall: the villager asks next morning instead
            if (IsHoming(core.LogId))
            {
                Defer(core.LogId, gut);
                AILog.Log(AILog.REQUEST, core.LogId, "Anxious on the way home, will ask for a gut tomorrow morning");
                return;
            }

            Offer(core.LogId, gut);
        }

        private void Defer(string giverId, RequestOrigin origin)
        {
            _deferred.Add((giverId, origin));
            _deferredGivers.Add(giverId);
        }

        private bool Offer(string giverId, RequestOrigin origin)
        {
            string key = string.IsNullOrEmpty(origin.Detail) ? origin.Kind : $"{origin.Kind}:{origin.Detail}";

            if (!_config.TryGetRequestQuest(origin.Kind, origin.Detail, out string questId))
            {
                AILog.Log(AILog.STUB, giverId, $"Request '{key}' has no quest mapping (AIBehaviourConfig > Villager Request)");
                return false;
            }

            origin.GiverId = giverId;
            _pending = origin;
            bool offered = _quests.TryOffer(giverId, questId, origin.TargetId, origin.ContextId);
            _pending = null;

            if (offered)
            {
                AILog.Log(AILog.REQUEST, giverId, $"Request '{key}' offered as {questId}");
            }
            else
            {
                AILog.Log(AILog.REQUEST, giverId, $"Request '{key}' refused by QuestManager ({questId}: offer window passed, giver busy or dead, or unknown id)");
            }

            return offered;
        }

        // QuestOffered fires inside TryOffer, so the pending origin belongs to this instance
        private void HandleQuestOffered(IQuestView quest)
        {
            if (_pending != null)
            {
                _pending.Day = quest.DayIssued;
                _origins[quest.InstanceId] = _pending;
            }
        }

        private void HandleQuestEnded(IQuestView quest)
        {
            if (!_origins.TryGetValue(quest.InstanceId, out RequestOrigin origin))
            {
                return;
            }

            _origins.Remove(quest.InstanceId);

            if (origin.BundleKey != null)
            {
                _openBundles.Remove(origin.BundleKey);
            }

            AILog.Log(AILog.REQUEST, quest.GiverId, $"Request {origin.Kind} ended {quest.State} (#{quest.InstanceId} {quest.Definition.Id})");

            if (quest.State == QuestState.Completed)
            {
                AIEvents.RaiseStress(quest.GiverId, $"request {origin.Kind} completed", -_config.RequestCompletedStressRelief);
                ResolveBundle(origin);
            }
            else if (quest.State == QuestState.Failed)
            {
                AIEvents.RaiseStress(quest.GiverId, $"request {origin.Kind} failed", _config.RequestFailedStress);
            }
        }

        // A resolved trace takes any request still pointing at it. Request-resolved traces are skipped:
        // that happens inside QuestEnded, the completed quest has already left, and its bundle holds no other request.
        private void HandleTraceResolved(GhostTrace trace, TraceResolveReason reason)
        {
            // A body is dealt with by a player: by hand, or as the completion of its request
            if ((reason == TraceResolveReason.ClearedByPlayer || reason == TraceResolveReason.Request) && !string.IsNullOrEmpty(trace.VictimId))
            {
                AIManager.Existing?.RevealDeath(trace.VictimId, trace.Position, "body cleared");
            }

            if (reason != TraceResolveReason.Request)
            {
                _quests.CancelByContext(trace.QuestContextId);
            }
        }

        private void ResolveBundle(RequestOrigin origin)
        {
            if (origin.BundleKey == null)
            {
                return;
            }

            GhostTraceRegistry.CollectActive(origin.BundleKey, _bundleBuffer);
            foreach (GhostTrace trace in _bundleBuffer)
            {
                trace.Resolve(TraceResolveReason.Request);
            }

            _bundleBuffer.Clear();
        }
    }
}
