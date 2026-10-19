using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Interaction
{
    public readonly struct ConversationLine
    {
        public readonly string SpeakerId;
        public readonly string Text;

        public ConversationLine(string speakerId, string text)
        {
            SpeakerId = speakerId;
            Text = text;
        }

        public override string ToString()
        {
            return $"{SpeakerId}: {Text}";
        }
    }

    public interface IConversationContent
    {
        ConversationLine NextLine(ConversationSession session, AI_Conversation speaker);
    }

    public class DebugConversationContent : IConversationContent
    {
        public ConversationLine NextLine(ConversationSession session, AI_Conversation speaker)
        {
            string topic = session.Kind == ConversationKind.RestTalk ? "휴식 잡담" : "작업 수다";
            return new ConversationLine(speaker.VillagerId, $"({topic} {session.LineCount + 1})");
        }
    }
}
