using System.Collections.Generic;
using ProjectShaman.AI.Routine;

namespace ProjectShaman.AI.Memory
{
    // One routine of today as the villager lived it, with the villagers it saw while there: the alibi it can give
    public sealed class RoutineAccount
    {
        public readonly ResolvedRoutine Routine;
        public readonly List<string> Companions = new List<string>();

        public RoutineAccount(in ResolvedRoutine routine)
        {
            Routine = routine;
        }
    }
}
