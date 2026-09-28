using System.Collections.Generic;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Core
{
    public class AIRuntimeContext
    {
        private static readonly Dictionary<string, List<string>> EMPTY_TOOL_USE_PLACES = new Dictionary<string, List<string>>();

        public AIBehaviourConfig Config { get; }
        public System.Func<float> GameClock { get; }
        public IReadOnlyDictionary<string, List<string>> ToolUsePlaces { get; }

        public float GameNow => GameClock != null ? GameClock() : UnityEngine.Time.time;

        public AIRuntimeContext(AIBehaviourConfig config, System.Func<float> gameClock, IReadOnlyDictionary<string, List<string>> toolUsePlaces)
        {
            Config = config;
            GameClock = gameClock;
            ToolUsePlaces = toolUsePlaces ?? EMPTY_TOOL_USE_PLACES;
        }
    }
}
