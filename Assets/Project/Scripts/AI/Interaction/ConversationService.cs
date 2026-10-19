using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Interaction
{
    public class ConversationService
    {
        private const float LINE_INTERVAL_SECONDS = 2.5f;
        private const float LEAVE_DISTANCE_MULTIPLIER = 1.5f;

        private readonly AIRuntimeContext _context;
        private readonly System.Random _random;
        private readonly IConversationContent _content;
        private readonly List<ConversationSession> _sessions = new List<ConversationSession>();
        private readonly Dictionary<string, float> _pairCooldowns = new Dictionary<string, float>();
        private readonly Dictionary<AI_Core, AI_Conversation> _byCore = new Dictionary<AI_Core, AI_Conversation>();
        private readonly List<AI_Conversation> _eligible = new List<AI_Conversation>();
        private readonly List<AI_Conversation> _group = new List<AI_Conversation>();
        private readonly HashSet<AI_Conversation> _usedThisCheck = new HashSet<AI_Conversation>();
        private readonly List<ConversationSession> _endBuffer = new List<ConversationSession>();
        private readonly List<AI_Conversation> _leaveBuffer = new List<AI_Conversation>();

        private float _nextCheckTime;
        private int _serial;

        private AIBehaviourConfig Config => _context.Config;

        public IReadOnlyList<ConversationSession> Sessions => _sessions;

        public ConversationService(AIRuntimeContext context, int seed, IConversationContent content = null)
        {
            _context = context;
            _random = new System.Random(seed);
            _content = content ?? new DebugConversationContent();
        }

        public void Tick(IReadOnlyList<AIManager.VillagerRuntime> villagers)
        {
            float now = Time.time;
            TickSessions(now);

            if (now < _nextCheckTime)
            {
                return;
            }

            _nextCheckTime = now + Config.ConversationCheckInterval;
            CollectEligible(villagers);
            TryStartSessions(now);
        }

        public void EndAll(string reason)
        {
            for (int i = _sessions.Count - 1; i >= 0; i--)
            {
                End(_sessions[i], reason);
            }
        }

        public void Clear()
        {
            EndAll("cleared");
            _pairCooldowns.Clear();
            _byCore.Clear();
        }

        private void TickSessions(float now)
        {
            float leaveDistance = Config.ConversationRadius * LEAVE_DISTANCE_MULTIPLIER;
            _endBuffer.Clear();

            foreach (ConversationSession session in _sessions)
            {
                _leaveBuffer.Clear();

                foreach (AI_Conversation participant in session.Participants)
                {
                    if (participant == null)
                    {
                        _leaveBuffer.Add(participant);
                        continue;
                    }

                    if (!participant.IsStillValid(session, leaveDistance, out string reason))
                    {
                        participant.Leave(reason);
                        _leaveBuffer.Add(participant);
                    }
                }

                foreach (AI_Conversation participant in _leaveBuffer)
                {
                    session.Remove(participant);
                }

                if (session.Count < 2)
                {
                    _endBuffer.Add(session);
                    continue;
                }

                if (now >= session.EndTime)
                {
                    _endBuffer.Add(session);
                    continue;
                }

                if (now >= session.NextLineTime)
                {
                    AI_Conversation speaker = session.NextSpeaker();
                    speaker.Speak(session, _content.NextLine(session, speaker));
                    session.LineCount++;
                    session.NextLineTime = now + LINE_INTERVAL_SECONDS;
                }
            }

            foreach (ConversationSession session in _endBuffer)
            {
                End(session, session.Count < 2 ? "not enough participants" : "time over");
            }
        }

        private void End(ConversationSession session, string reason)
        {
            foreach (AI_Conversation participant in session.Participants)
            {
                if (participant != null)
                {
                    participant.Leave(reason);
                }
            }

            _sessions.Remove(session);
            AILog.Log(AILog.INTERACTION, $"Conversation end {session} ({reason}, lines={session.LineCount})");
        }

        private void CollectEligible(IReadOnlyList<AIManager.VillagerRuntime> villagers)
        {
            _eligible.Clear();
            _usedThisCheck.Clear();

            foreach (AIManager.VillagerRuntime villager in villagers)
            {
                AI_Conversation conversation = GetConversation(villager.Core);
                if (conversation == null || villager.IsHomeOrdered || !conversation.IsAvailable())
                {
                    continue;
                }

                if (conversation.CountThisRoutine() >= Config.ConversationsPerRoutine)
                {
                    continue;
                }

                VillagerActivity activity = conversation.CurrentActivity();
                if (activity == VillagerActivity.Rest || activity == VillagerActivity.Work)
                {
                    _eligible.Add(conversation);
                }
            }

            Shuffle(_eligible);
        }

        private void TryStartSessions(float now)
        {
            foreach (AI_Conversation initiator in _eligible)
            {
                if (_usedThisCheck.Contains(initiator))
                {
                    continue;
                }

                VillagerActivity activity = initiator.CurrentActivity();
                AI_Conversation partner = FindPartner(initiator, activity, now);
                if (partner == null || _random.NextDouble() >= Config.ConversationChance)
                {
                    continue;
                }

                _group.Clear();
                _group.Add(initiator);
                _group.Add(partner);

                if (activity == VillagerActivity.Rest)
                {
                    GatherGroup(initiator, now);
                    StartRestTalk(now);
                }
                else
                {
                    StartWorkChatter(now);
                }

                foreach (AI_Conversation member in _group)
                {
                    _usedThisCheck.Add(member);
                }
            }
        }

        private AI_Conversation FindPartner(AI_Conversation initiator, VillagerActivity activity, float now)
        {
            AI_Conversation best = null;
            float bestDistance = Config.ConversationRadius * Config.ConversationRadius;

            foreach (AI_Conversation candidate in _eligible)
            {
                if (candidate == initiator || _usedThisCheck.Contains(candidate) || IsOnCooldown(initiator, candidate, now))
                {
                    continue;
                }

                if (candidate.CurrentActivity() != activity)
                {
                    continue;
                }

                float distance = (candidate.transform.position - initiator.transform.position).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void GatherGroup(AI_Conversation initiator, float now)
        {
            float sqr = Config.ConversationRadius * Config.ConversationRadius;

            foreach (AI_Conversation candidate in _eligible)
            {
                if (_group.Count >= Config.ConversationGroupMax)
                {
                    return;
                }

                if (_group.Contains(candidate) || _usedThisCheck.Contains(candidate) || IsOnCooldown(initiator, candidate, now))
                {
                    continue;
                }

                if (candidate.CurrentActivity() == VillagerActivity.Rest && (candidate.transform.position - initiator.transform.position).sqrMagnitude <= sqr)
                {
                    _group.Add(candidate);
                }
            }
        }

        private void StartRestTalk(float now)
        {
            float duration = RandomDuration();
            ConversationSession session = new ConversationSession(++_serial, ConversationKind.RestTalk, now, now + duration);

            for (int i = 0; i < _group.Count; i++)
            {
                AI_Conversation member = _group[i];
                AI_Conversation target = i == 0 ? _group[1] : _group[0];

                if (member.Interaction.TryBegin(target, InteractionResponse.Converse, duration))
                {
                    session.Add(member);
                    member.Join(session, member.Interaction.Serial);
                }
            }

            Register(session, now);
        }

        private void StartWorkChatter(float now)
        {
            ConversationSession session = new ConversationSession(++_serial, ConversationKind.WorkChatter, now, now + RandomDuration());

            foreach (AI_Conversation member in _group)
            {
                session.Add(member);
                member.Join(session, 0);
            }

            Register(session, now);
        }

        private void Register(ConversationSession session, float now)
        {
            if (session.Count < 2)
            {
                End(session, "start failed");
                return;
            }

            for (int i = 0; i < session.Count; i++)
            {
                for (int j = i + 1; j < session.Count; j++)
                {
                    _pairCooldowns[PairKey(session.Participants[i], session.Participants[j])] = now + Config.ConversationPartnerCooldown;
                }
            }

            _sessions.Add(session);
            AILog.Log(AILog.INTERACTION, $"Conversation start {session} ({session.EndTime - session.StartTime:F1}s)");
        }

        private bool IsOnCooldown(AI_Conversation a, AI_Conversation b, float now)
        {
            return _pairCooldowns.TryGetValue(PairKey(a, b), out float until) && now < until;
        }

        private float RandomDuration()
        {
            return Config.ConversationDurationMin + (float)_random.NextDouble() * (Config.ConversationDurationMax - Config.ConversationDurationMin);
        }

        private AI_Conversation GetConversation(AI_Core core)
        {
            if (core == null)
            {
                return null;
            }

            if (!_byCore.TryGetValue(core, out AI_Conversation conversation))
            {
                conversation = core.GetComponent<AI_Conversation>();
                _byCore[core] = conversation;
            }

            return conversation;
        }

        private void Shuffle(List<AI_Conversation> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static string PairKey(AI_Conversation a, AI_Conversation b)
        {
            return string.CompareOrdinal(a.VillagerId, b.VillagerId) < 0 ? $"{a.VillagerId}|{b.VillagerId}" : $"{b.VillagerId}|{a.VillagerId}";
        }
    }
}
