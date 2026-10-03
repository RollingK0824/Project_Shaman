using System;
using System.Collections.Generic;
using ProjectShaman.AI.Data;
namespace ProjectShaman.Util
{
    [Serializable]
    public class AIDataContainer
    {
        public int version;
        public List<NameEntry> names = new List<NameEntry>();
        public List<JobEntry> jobs = new List<JobEntry>();
        public List<RoutineEntry> routines = new List<RoutineEntry>();
    }
}