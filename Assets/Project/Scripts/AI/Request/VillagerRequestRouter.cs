using System.Collections.Generic;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
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
            public string Kind;
            public string Detail;
            public string TargetId;
            public string ContextId;
            public string BundleKey;
        }

        private readonly AIBehaviourConfig _config;
        private readonly List<AI_Stress> _stresses;
        private readonly List<GhostTrace> _watchedTraces = new List<GhostTrace>();
        private readonly Dictionary<int, RequestOrigin> _origins = new Dictionary<int, RequestOrigin>();
        private readonly HashSet<string> _openBundles = new HashSet<string>();
        private readonly List<GhostTrace> _bundleBuffer = new List<GhostTrace>();

        private QuestManager _quests;
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
                Offer(villagerId, new RequestOrigin { Kind = FIND_TOOL, Detail = detail, TargetId = detail });
            }
            else if (requestType == CHECK_ON)
            {
                OfferCheckOn(villagerId, detail);
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
                AILog.Log(AILog.REQUEST, giverId, $"Check-on for {missingId} skipped (no body left, already found or cleared)");
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
            if (Offer(villagerId, origin))
            {
                _openBundles.Add(trace.BundleKey);
            }
        }

        // Leaving Calm (to Anxious, or straight to Breaking) asks for a gut
        private void HandleStressState(AI_Stress stress, VillagerStressState previous, VillagerStressState next)
        {
            if (previous != VillagerStressState.Calm || next == VillagerStressState.Calm || !stress.TryGetComponent(out AI_Core core))
            {
                return;
            }

            Offer(core.LogId, new RequestOrigin { Kind = GUT });
        }

        private bool Offer(string giverId, RequestOrigin origin)
        {
            string key = string.IsNullOrEmpty(origin.Detail) ? origin.Kind : $"{origin.Kind}:{origin.Detail}";

            if (!_config.TryGetRequestQuest(origin.Kind, origin.Detail, out string questId))
            {
                AILog.Log(AILog.STUB, giverId, $"Request '{key}' has no quest mapping (AIBehaviourConfig > Villager Request)");
                return false;
            }

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
