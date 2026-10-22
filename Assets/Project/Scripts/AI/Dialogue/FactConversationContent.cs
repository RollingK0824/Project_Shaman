using System.Collections.Generic;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;
using ProjectShaman.AI.Memory;

namespace ProjectShaman.AI.Dialogue
{
    // Villager-to-villager lines (server only). Now and then the speaker passes on a strange fact a listener
    // does not know yet: that is how rumours spread (⑤). Everyone else hears small talk.
    // Villagers only ever tell what they saw or heard; details blur with every retelling.
    public sealed class FactConversationContent : IConversationContent
    {
        private readonly AIBehaviourConfig _config;
        private readonly System.Random _random;
        private readonly List<MemoryEntry> _facts = new List<MemoryEntry>();

        public FactConversationContent(AIBehaviourConfig config, int seed)
        {
            _config = config;
            _random = new System.Random(seed);
        }

        public ConversationLine NextLine(ConversationSession session, AI_Conversation speaker)
        {
            if (_random.NextDouble() < _config.RumorChance && TryRumor(session, speaker, out string rumor))
            {
                return new ConversationLine(speaker.VillagerId, rumor);
            }

            string id = session.Kind == ConversationKind.RestTalk ? "smalltalk.rest" : "smalltalk.work";
            return new ConversationLine(speaker.VillagerId, DialogueLibrary.Line(id, _random));
        }

        private bool TryRumor(ConversationSession session, AI_Conversation speaker, out string line)
        {
            line = null;
            AI_Memory speakerMemory = speaker.GetComponent<AI_Memory>();
            if (speakerMemory == null || speakerMemory.Events == null)
            {
                return false;
            }

            speakerMemory.Events.CollectFacts(_facts);

            foreach (MemoryEntry fact in _facts)
            {
                if (!fact.IsAnomalous || fact.Hops >= _config.RumorMaxHops)
                {
                    continue;
                }

                int told = 0;
                foreach (AI_Conversation listener in session.Participants)
                {
                    if (listener == null || listener == speaker)
                    {
                        continue;
                    }

                    AI_Memory listenerMemory = listener.GetComponent<AI_Memory>();
                    if (listenerMemory == null || listenerMemory.Knows(fact) || fact.ActorId == listener.VillagerId)
                    {
                        continue;
                    }

                    MemoryEntry heard = fact;
                    heard.Source = MemorySource.Heard;
                    heard.SourceId = speaker.VillagerId;
                    heard.Hops = fact.Hops + 1;
                    heard.TraitMask = fact.Kind == MemoryKind.Absent ? fact.TraitMask : DialogueComposer.BlurTraits(fact.TraitMask, heard.Hops, _random);
                    heard.RecordedTime = fact.RecordedTime;

                    if (listenerMemory.RecordFact(heard))
                    {
                        told++;
                    }
                }

                if (told > 0)
                {
                    line = DialogueComposer.DescribeFact(fact, speakerMemory.CurrentDay, _random);
                    _facts.Clear();
                    return true;
                }
            }

            _facts.Clear();
            return false;
        }
    }
}
