using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Memory;
using ProjectShaman.AI.Stress;
using ProjectShaman.AI.Trace;
using ProjectShaman.Map;

namespace ProjectShaman.AI.Dialogue
{
    public readonly struct DialogueTopic
    {
        public readonly string Id;
        public readonly string Label;

        public DialogueTopic(string id, string label)
        {
            Id = id;
            Label = label;
        }
    }

    // Server only. What a resting villager can be asked and what it answers, from its own memory.
    // Fixed questions plus questions built from the team's active quests. The team gets a few questions
    // per villager per day, so asking around is a choice, not a sweep.
    public static class VillagerDialogueService
    {
        public const string ASK_ANOMALY = "ask.anomaly";
        public const string ASK_RUMOR = "ask.rumor";
        public const string ASK_TODAY = "ask.today";
        public const string ASK_FEELING = "ask.feeling";
        public const string ASK_TOOL = "ask.tool";
        public const string ASK_TRACE = "ask.trace";
        public const string ASK_BYE = "ask.bye";

        private static readonly Dictionary<string, int> _asked = new Dictionary<string, int>();
        private static readonly Dictionary<string, HashSet<uint>> _thanked = new Dictionary<string, HashSet<uint>>();
        private static readonly List<MemoryEntry> _facts = new List<MemoryEntry>();
        private static readonly System.Random _random = new System.Random();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _asked.Clear();
            _thanked.Clear();
        }

        // From the day after the exorcism, the first time each player talks to the freed villager: a word of thanks
        public static string TakeThanks(AI_Conversation villager, uint playerId)
        {
            AI_Core core = villager.GetComponent<AI_Core>();
            int today = TimeManager.Existing != null ? TimeManager.Existing.DayCount : 0;
            if (core == null || core.Profile == null || core.Profile.ExorcisedDay < 0 || today <= core.Profile.ExorcisedDay)
            {
                return null;
            }

            if (!_thanked.TryGetValue(villager.VillagerId, out HashSet<uint> players))
            {
                players = new HashSet<uint>();
                _thanked[villager.VillagerId] = players;
            }

            if (!players.Add(playerId))
            {
                return null;
            }

            AILog.Log(AILog.INTERACTION, villager.VillagerId, $"Thanks player {playerId} for the exorcism (day {core.Profile.ExorcisedDay})");
            return DialogueLibrary.Line("exorcised.thanks", _random);
        }

        public static string Greeting()
        {
            return DialogueLibrary.Line("greet", _random);
        }

        public static bool HasQuestionsLeft(AI_Conversation villager, AIBehaviourConfig config)
        {
            return AskedToday(villager) < config.PlayerQuestionsPerDay;
        }

        public static List<DialogueTopic> BuildTopics(AI_Conversation villager, AIBehaviourConfig config)
        {
            List<DialogueTopic> topics = new List<DialogueTopic>();

            if (HasQuestionsLeft(villager, config))
            {
                topics.Add(new DialogueTopic(ASK_ANOMALY, DialogueLibrary.Line(ASK_ANOMALY, null)));
                topics.Add(new DialogueTopic(ASK_RUMOR, DialogueLibrary.Line(ASK_RUMOR, null)));
                topics.Add(new DialogueTopic(ASK_TODAY, DialogueLibrary.Line(ASK_TODAY, null)));
                topics.Add(new DialogueTopic(ASK_FEELING, DialogueLibrary.Line(ASK_FEELING, null)));
                AddQuestTopics(topics);
            }

            topics.Add(new DialogueTopic(ASK_BYE, DialogueLibrary.Line(ASK_BYE, null)));
            return topics;
        }

        // Returns the answer; ends == true when the conversation should close after it
        public static string Answer(AI_Conversation villager, string topicId, AIBehaviourConfig config, out bool ends)
        {
            ends = false;

            if (topicId == ASK_BYE)
            {
                ends = true;
                return DialogueLibrary.Line("bye", _random);
            }

            if (!HasQuestionsLeft(villager, config))
            {
                ends = true;
                return DialogueLibrary.Line("budget", _random);
            }

            CountQuestion(villager);
            AI_Memory memory = villager.GetComponent<AI_Memory>();
            int day = memory != null ? memory.CurrentDay : 0;
            Split(topicId, out string kind, out string arg);

            switch (kind)
            {
                case ASK_ANOMALY:
                    return AnswerFact(villager, memory, day, f => f.IsAnomalous, true) ?? DialogueLibrary.Line("answer.none", _random);
                case ASK_RUMOR:
                    return AnswerFact(villager, memory, day, f => f.Source == MemorySource.Heard, false) ?? DialogueLibrary.Line("rumor.none", _random);
                case ASK_TODAY:
                    return AnswerToday(memory);
                case ASK_FEELING:
                    return AnswerFeeling(villager, config);
                case ASK_TOOL:
                    return AnswerTool(villager, memory, arg);
                case ASK_TRACE:
                    return AnswerFact(villager, memory, day, f => f.Kind == MemoryKind.TraceWitness && f.SubjectId == arg, true) ?? DialogueLibrary.Line("trace.ask.none", _random);
                default:
                    return DialogueLibrary.Line("answer.none", _random);
            }
        }

        // How the villager feels hints at its stress without a number: calm, uneasy, anxious (from the anxious threshold),
        // close to breaking (halfway from anxious to the limit, where the death mark falls)
        private static string AnswerFeeling(AI_Conversation villager, AIBehaviourConfig config)
        {
            AI_Stress stress = villager.GetComponent<AI_Stress>();
            float value = stress != null ? stress.Value : 0f;
            float anxious = config.VillagerStressAnxious;
            float limit = config.VillagerStressLimit;

            string level = value >= (anxious + limit) * 0.5f ? "breaking"
                : value >= anxious ? "anxious"
                : value >= anxious * 0.5f ? "uneasy"
                : "fine";

            AILog.Log(AILog.INTERACTION, villager.VillagerId, $"Feeling asked: {level} (stress {value:F0})");
            return DialogueLibrary.Line($"feeling.{level}", _random);
        }

        // Quest targets become questions: a lost tool, a trace. A missing villager is checked at their house, not asked about
        private static void AddQuestTopics(List<DialogueTopic> topics)
        {
            QuestManager quests = QuestManager.Existing;
            if (quests == null)
            {
                return;
            }

            HashSet<string> added = new HashSet<string>();

            foreach (IQuestView quest in quests.Quests)
            {
                if (quest.State != QuestState.Active)
                {
                    continue;
                }

                if (quest.Definition.ObjectiveType == QuestActions.DELIVER && AIDisplayNames.Tool(quest.TargetId) != quest.TargetId)
                {
                    AddTopic(topics, added, $"{ASK_TOOL}:{quest.TargetId}", DialogueComposer.Fill(DialogueLibrary.Line(ASK_TOOL, null), "tool", AIDisplayNames.Tool(quest.TargetId)));
                }

                if (QuestTargets.TryGet(quest.ContextId, out IQuestTarget target) && target is GhostTrace trace)
                {
                    string area = MapAreaRegistry.Describe(trace.Position) ?? DialogueLibrary.Noun("place.unknown", "마을 어귀");
                    AddTopic(topics, added, $"{ASK_TRACE}:{trace.QuestContextId}", DialogueComposer.Fill(DialogueLibrary.Line(ASK_TRACE, null), "area", area, "object", trace.DisplayName));
                }
            }
        }

        private static void AddTopic(List<DialogueTopic> topics, HashSet<string> added, string id, string label)
        {
            if (added.Add(id))
            {
                topics.Add(new DialogueTopic(id, label));
            }
        }

        // The host and stressed villagers twist one detail of what they say (AnswerDistortion)
        private static string AnswerFact(AI_Conversation speaker, AI_Memory memory, int day, System.Predicate<MemoryEntry> match, bool preferSeen)
        {
            if (memory == null || memory.Events == null)
            {
                return null;
            }

            memory.Events.CollectFacts(_facts);
            MemoryEntry? best = null;

            foreach (MemoryEntry fact in _facts)
            {
                if (!match(fact))
                {
                    continue;
                }

                if (best == null || (preferSeen && fact.Source == MemorySource.Direct && best.Value.Source != MemorySource.Direct))
                {
                    best = fact;
                }
            }

            _facts.Clear();
            return best.HasValue ? DialogueComposer.DescribeFact(AnswerDistortion.Apply(speaker, best.Value), day, _random) : null;
        }

        private static string AnswerToday(AI_Memory memory)
        {
            if (memory != null && memory.Events != null)
            {
                foreach (MemoryEntry entry in memory.Events.Get(MemoryKind.Routine))
                {
                    if (entry.DayCount == memory.CurrentDay)
                    {
                        return DialogueComposer.Fill(DialogueLibrary.Line("today", _random), "area", DialogueComposer.AreaName(entry.PlaceId, entry.Position));
                    }
                }
            }

            return DialogueLibrary.Line("today.none", _random);
        }

        private static string AnswerTool(AI_Conversation speaker, AI_Memory memory, string toolType)
        {
            if (memory != null && memory.ToolSightings != null)
            {
                ToolSighting? latest = null;
                foreach (ToolSighting sighting in memory.ToolSightings.Get(toolType))
                {
                    if (latest == null || sighting.SeenTime > latest.Value.SeenTime)
                    {
                        latest = sighting;
                    }
                }

                if (latest.HasValue)
                {
                    return DialogueComposer.DescribeToolSighting(AnswerDistortion.Apply(speaker, latest.Value), _random);
                }
            }

            return DialogueComposer.Fill(DialogueLibrary.Line("tool.none", _random), "tool", AIDisplayNames.Tool(toolType));
        }

        private static int AskedToday(AI_Conversation villager)
        {
            return _asked.TryGetValue(BudgetKey(villager), out int count) ? count : 0;
        }

        private static void CountQuestion(AI_Conversation villager)
        {
            string key = BudgetKey(villager);
            _asked[key] = AskedToday(villager) + 1;
        }

        private static string BudgetKey(AI_Conversation villager)
        {
            AI_Memory memory = villager.GetComponent<AI_Memory>();
            return $"{villager.VillagerId}:{(memory != null ? memory.CurrentDay : 0)}";
        }

        private static void Split(string topicId, out string kind, out string arg)
        {
            int colon = topicId != null ? topicId.IndexOf(':') : -1;
            kind = colon >= 0 ? topicId.Substring(0, colon) : topicId;
            arg = colon >= 0 ? topicId.Substring(colon + 1) : null;
        }
    }
}
