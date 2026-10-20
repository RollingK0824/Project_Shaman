using System.Collections.Generic;
using System.Linq;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Interaction
{
    public class ConversationSession
    {
        private readonly List<AI_Conversation> _participants = new List<AI_Conversation>();

        public int Serial { get; }
        public ConversationKind Kind { get; }
        public float StartTime { get; }
        public float EndTime { get; }
        public float NextLineTime { get; set; }
        public int LineCount { get; set; }
        public IReadOnlyList<AI_Conversation> Participants => _participants;
        public int Count => _participants.Count;

        public ConversationSession(int serial, ConversationKind kind, float startTime, float endTime)
        {
            Serial = serial;
            Kind = kind;
            StartTime = startTime;
            EndTime = endTime;
            NextLineTime = startTime;
        }

        public void Add(AI_Conversation participant)
        {
            if (participant != null && !_participants.Contains(participant))
            {
                _participants.Add(participant);
            }
        }

        public bool Remove(AI_Conversation participant)
        {
            return _participants.Remove(participant);
        }

        public AI_Conversation NextSpeaker()
        {
            return _participants.Count > 0 ? _participants[LineCount % _participants.Count] : null;
        }

        public override string ToString()
        {
            return $"#{Serial} {Kind} [{string.Join(", ", _participants.Select(p => p != null ? p.VillagerId : "-"))}]";
        }
    }
}
