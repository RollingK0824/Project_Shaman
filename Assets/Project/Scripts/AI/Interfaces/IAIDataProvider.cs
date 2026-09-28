using System.Collections.Generic;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Interfaces
{
    public interface IAIDataProvider
    {
        string SourceName { get; }
        IReadOnlyList<NameEntry> Names { get; }
        IReadOnlyList<JobEntry> Jobs { get; }
        IReadOnlyList<RoutineEntry> Routines { get; }
    }
}
