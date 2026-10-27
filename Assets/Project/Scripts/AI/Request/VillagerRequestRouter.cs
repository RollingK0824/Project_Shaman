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
            public bool Urgent;     // gut of a marked villager
        }

        // Something villagers saw, asked about next morning (C129). Kept until it is asked or its target is gone
        private sealed class RequestCandidate
        {
            public string Key;
            public RequestOrigin Origin;
            public readonly List<string> Givers = new List<string>();
        }

        private readonly AIBehaviourConfig _config;
        private readonly List<AI_Stress> _stresses;
        private readonly List<GhostTrace> _watchedTraces = new List<GhostTrace>();
        private readonly Dictionary<int, RequestOrigin> _origins = new Dictionary<int, RequestOrigin>();
        private readonly List<GhostTrace> _bundleBuffer = new List<GhostTrace>();

        private QuestManager _quests;
        private readonly Dictionary<string, RequestCandidate> _candidates = new Dictionary<string, RequestCandidate>();
        private readonly Dictionary<int, RequestCandidate> _askedCandidates = new Dictionary<int, RequestCandidate>();
        private readonly List<RequestCandidate> _candidateBuffer = new List<RequestCandidate>();
        private readonly List<string> _giverBuffer = new List<string>();
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

            _watchedTraces.Clear();
            _origins.Clear();
            _candidates.Clear();
            _askedCandidates.Clear();
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
                RememberCheckOn(villagerId, detail);
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
                // Failed whether or not anyone took it: the request was meant to ease the giver's stress
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
        private void RememberCheckOn(string giverId, string missingId)
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

            // Raised at day start, just before OnDayStart asks
            Remember($"{CHECK_ON}:{missingId}", giverId, new RequestOrigin { Kind = CHECK_ON, TargetId = missingId, ContextId = body.QuestContextId });
        }

        // One request per trace bundle (C37); everyone who saw it can be the one who asks
        private void HandleWitnessed(GhostTrace trace, string villagerId, bool detailed)
        {
            if (!trace.IsActive)
            {
                return;
            }

            // The shock and the stress come at once, the request waits for the morning (C129)
            if (!string.IsNullOrEmpty(trace.VictimId))
            {
                Panic(villagerId);
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

            if (Remember(trace.BundleKey, villagerId, origin))
            {
                AILog.Log(AILog.REQUEST, villagerId, $"Saw {trace.DisplayName}, will ask tomorrow morning");
            }
        }

        // Adds the villager as a possible giver; false if it already was one. A request already out keeps the giver
        // for the next time it is asked (failed or given way)
        private bool Remember(string key, string giverId, RequestOrigin origin)
        {
            RequestCandidate candidate = FindCandidate(key);
            if (candidate == null)
            {
                candidate = new RequestCandidate { Key = key, Origin = origin };
                _candidates[key] = candidate;
            }

            if (candidate.Givers.Contains(giverId))
            {
                return false;
            }

            candidate.Givers.Add(giverId);
            return true;
        }

        private RequestCandidate FindCandidate(string key)
        {
            if (_candidates.TryGetValue(key, out RequestCandidate waiting))
            {
                return waiting;
            }

            foreach (RequestCandidate asked in _askedCandidates.Values)
            {
                if (asked.Key == key)
                {
                    return asked;
                }
            }

            return null;
        }

        // Every request is asked in the morning, so a player always has the whole day for it (C129)
        public void OnDayStart()
        {
            if (_quests == null)
            {
                return;
            }

            RememberGuts();
            AskCandidates();
        }

        // A villager not calm this morning asks for a gut; one at the limit is marked and asks first of all.
        // Rebuilt each morning from the stress it has now
        private void RememberGuts()
        {
            _candidateBuffer.Clear();
            foreach (RequestCandidate candidate in _candidates.Values)
            {
                if (candidate.Origin.Kind == GUT)
                {
                    _candidateBuffer.Add(candidate);
                }
            }

            foreach (RequestCandidate candidate in _candidateBuffer)
            {
                _candidates.Remove(candidate.Key);
            }

            foreach (AI_Stress stress in _stresses)
            {
                if (stress == null || stress.State == VillagerStressState.Calm || !stress.TryGetComponent(out AI_Core core)
                    || (stress.TryGetComponent(out AI_Status status) && status.IsDead))
                {
                    continue;
                }

                stress.JudgeMorningMark();
                Remember($"{GUT}:{core.LogId}", core.LogId, new RequestOrigin { Kind = GUT, Urgent = stress.IsMarked });
            }
        }

        // Most urgent first, each to a villager who remembers it and has no request yet (one per villager).
        // At the same urgency the one fewer villagers could ask goes first, so a gut only its owner can ask is not
        // crowded out by a body others saw too. What finds no giver stays for the next morning, unless its trace or
        // body is gone or everyone who saw it is dead by then
        private void AskCandidates()
        {
            _candidateBuffer.Clear();
            foreach (RequestCandidate candidate in _candidates.Values)
            {
                candidate.Givers.RemoveAll(giverId => !IsAlive(giverId));
                _candidateBuffer.Add(candidate);
            }

            _candidateBuffer.Sort((a, b) =>
            {
                int byPriority = PriorityOf(b.Origin).CompareTo(PriorityOf(a.Origin));
                return byPriority != 0 ? byPriority : a.Givers.Count.CompareTo(b.Givers.Count);
            });

            foreach (RequestCandidate candidate in _candidateBuffer)
            {
                RequestOrigin origin = candidate.Origin;
                if (candidate.Givers.Count == 0 || (!string.IsNullOrEmpty(origin.ContextId) && !QuestTargets.TryGet(origin.ContextId, out _)))
                {
                    _candidates.Remove(candidate.Key);
                    continue;
                }

                if (TryAsk(candidate))
                {
                    _candidates.Remove(candidate.Key);
                }
                else
                {
                    AILog.Log(AILog.REQUEST, $"Request {origin.Kind} ({candidate.Key}) found no free giver among [{string.Join(", ", candidate.Givers)}], waits for tomorrow");
                }
            }

            _candidateBuffer.Clear();
        }

        // Villagers with no request go first; one holding a less urgent request nobody took may give it up (Offer)
        private bool TryAsk(RequestCandidate candidate)
        {
            _giverBuffer.Clear();
            foreach (string giverId in candidate.Givers)
            {
                if (!HasRequest(giverId))
                {
                    _giverBuffer.Add(giverId);
                }
            }

            foreach (string giverId in candidate.Givers)
            {
                if (!_giverBuffer.Contains(giverId))
                {
                    _giverBuffer.Add(giverId);
                }
            }

            foreach (string giverId in _giverBuffer)
            {
                if (HasRequest(giverId) && !CanGiveWay(giverId, candidate.Origin))
                {
                    continue;
                }

                // Offer fills the origin's GiverId; QuestOffered has stored it by then
                if (Offer(giverId, candidate.Origin) && TryGetInstance(candidate.Origin, out int instanceId))
                {
                    _askedCandidates[instanceId] = candidate;
                    return true;
                }
            }

            return false;
        }

        // A villager killed or voted out leaves its object; one still here may be lying dead
        private bool IsAlive(string villagerId)
        {
            AI_Stress stress = FindStress(villagerId);
            return stress != null && !(stress.TryGetComponent(out AI_Status status) && status.IsDead);
        }

        private bool HasRequest(string giverId)
        {
            foreach (RequestOrigin origin in _origins.Values)
            {
                if (origin.GiverId == giverId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CanGiveWay(string giverId, RequestOrigin incoming)
        {
            foreach (RequestOrigin open in _origins.Values)
            {
                if (open.GiverId == giverId)
                {
                    return PriorityOf(open) < PriorityOf(incoming);
                }
            }

            return true;
        }

        private bool TryGetInstance(RequestOrigin origin, out int instanceId)
        {
            foreach (KeyValuePair<int, RequestOrigin> pair in _origins)
            {
                if (pair.Value == origin)
                {
                    instanceId = pair.Key;
                    return true;
                }
            }

            instanceId = 0;
            return false;
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

        private bool Offer(string giverId, RequestOrigin origin)
        {
            string key = string.IsNullOrEmpty(origin.Detail) ? origin.Kind : $"{origin.Kind}:{origin.Detail}";

            if (!_config.TryGetRequestQuest(origin.Kind, origin.Detail, out string questId))
            {
                AILog.Log(AILog.STUB, giverId, $"Request '{key}' has no quest mapping (AIBehaviourConfig > Villager Request)");
                return false;
            }

            origin.GiverId = giverId;
            YieldLowerRequest(giverId, origin);
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

        // One open request per villager: a more urgent one takes the place of one nobody has taken yet.
        // A request a player took stays (they are on it), and the newer one is refused as before
        private void YieldLowerRequest(string giverId, RequestOrigin incoming)
        {
            foreach (KeyValuePair<int, RequestOrigin> pair in _origins)
            {
                RequestOrigin open = pair.Value;
                if (open.GiverId != giverId)
                {
                    continue;
                }

                // The cancel ends the quest, which removes its origin: no more looping after it
                int instanceId = pair.Key;
                if (PriorityOf(open) < PriorityOf(incoming) && _quests.TryCancelQuest(instanceId))
                {
                    AILog.Log(AILog.REQUEST, giverId, $"Request {open.Kind} (#{instanceId}) gave way to the more urgent {incoming.Kind}");
                }

                return;
            }
        }

        // Body, missing villager and a marked villager's gut > trace > gut > lost tool
        private static int PriorityOf(RequestOrigin origin)
        {
            switch (origin.Kind)
            {
                case CHECK_ON:
                    return 4;
                case TRACE:
                case TRACE_CLEANSE:
                    return origin.TargetId == GhostTraceKind.VillagerCorpse.ToString() ? 4 : 3;
                case GUT:
                    return origin.Urgent ? 4 : 2;
                default:
                    return 1;
            }
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

            AILog.Log(AILog.REQUEST, quest.GiverId, $"Request {origin.Kind} ended {quest.State} (#{quest.InstanceId} {quest.Definition.Id})");

            // Not done (failed, given way, giver gone): the others who saw it may ask again next morning.
            // A gut is judged again from the stress each morning
            if (_askedCandidates.TryGetValue(quest.InstanceId, out RequestCandidate candidate))
            {
                _askedCandidates.Remove(quest.InstanceId);
                if (quest.State != QuestState.Completed && origin.Kind != GUT)
                {
                    _candidates[candidate.Key] = candidate;
                }
            }

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
