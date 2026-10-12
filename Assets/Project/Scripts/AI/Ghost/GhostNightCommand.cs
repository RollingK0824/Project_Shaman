using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    public struct GhostNightCommand
    {
        public GhostActionVerb Verb;
        public GhostTargetQuery Query;
        public float ArriveDistance;
        public float Duration;

        public static GhostNightCommand Idle => new GhostNightCommand { Verb = GhostActionVerb.None };

        public override string ToString()
        {
            return $"{Verb} [{Query}] arrive={ArriveDistance:F1}m duration={Duration:F1}s";
        }
    }
}
