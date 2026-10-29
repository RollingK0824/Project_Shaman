using System.Collections.Generic;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI.Data
{
    public class JsonAIDataProvider : IAIDataProvider
    {
        private readonly List<NameEntry> _names;
        private readonly List<JobEntry> _jobs;
        private readonly List<RoutineEntry> _routines;
        private readonly List<ActionEntry> _actions;
        private readonly int _version;

        public JsonAIDataProvider(AIDataContainer container)
        {
            _version = container.version;
            _names = container.names ?? new List<NameEntry>();
            _jobs = container.jobs ?? new List<JobEntry>();
            _routines = container.routines ?? new List<RoutineEntry>();
            _actions = container.actions ?? new List<ActionEntry>();
        }

        public string SourceName => $"Json(v{_version})";
        public IReadOnlyList<NameEntry> Names => _names;
        public IReadOnlyList<JobEntry> Jobs => _jobs;
        public IReadOnlyList<RoutineEntry> Routines => _routines;
        public IReadOnlyList<ActionEntry> Actions => _actions;
    }
}
