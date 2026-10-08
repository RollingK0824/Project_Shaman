using System;
using System.Collections.Generic;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Routine;

namespace ProjectShaman.AI.Core
{
    public sealed class VillagerRegistry
    {
        private readonly List<IVillagerView> _villagers = new List<IVillagerView>();
        private readonly Dictionary<string, VillagerEntry> _byId = new Dictionary<string, VillagerEntry>();

        public IReadOnlyList<IVillagerView> Villagers => _villagers;
        public int Count => _villagers.Count;

        public event Action<IVillagerView> OnRegistered;
        public event Action<IVillagerView> OnDied;
        public event Action OnCleared;

        internal VillagerRegistry()
        {
        }

        internal void Close()
        {
            AILog.Log(AILog.FACTORY, $"Registry closed ({_villagers.Count})");
            _villagers.Clear();
            _byId.Clear();
            OnRegistered = null;
            OnDied = null;
            OnCleared = null;
        }

        public bool TryGet(string villagerId, out IVillagerView view)
        {
            view = null;

            if (string.IsNullOrEmpty(villagerId) || !_byId.TryGetValue(villagerId, out VillagerEntry entry))
            {
                return false;
            }

            view = entry;
            return true;
        }

        internal void Register(VillagerPublicInfo info, VillagerScheduleView schedule)
        {
            if (info == null || string.IsNullOrEmpty(info.VillagerId))
            {
                AILog.Warn(AILog.FACTORY, "Registry ignored villager without id");
                return;
            }

            if (_byId.ContainsKey(info.VillagerId))
            {
                AILog.Warn(AILog.FACTORY, $"Registry duplicate id '{info.VillagerId}', ignored");
                return;
            }

            VillagerEntry entry = new VillagerEntry(info, schedule);
            _byId.Add(entry.VillagerId, entry);
            _villagers.Insert(FindInsertIndex(entry.VillagerId), entry);

            AILog.Log(AILog.FACTORY, entry.VillagerId, $"Registry registered (count={_villagers.Count}, schedule={(entry.Schedule != null ? "yes" : "none")})");
            OnRegistered?.Invoke(entry);
        }

        internal void MarkDead(string villagerId)
        {
            if (string.IsNullOrEmpty(villagerId) || !_byId.TryGetValue(villagerId, out VillagerEntry entry) || !entry.IsAlive)
            {
                return;
            }

            entry.IsAlive = false;
            AILog.Log(AILog.FACTORY, villagerId, "Registry marked dead");
            OnDied?.Invoke(entry);
        }

        internal void Clear()
        {
            if (_villagers.Count == 0)
            {
                return;
            }

            AILog.Log(AILog.FACTORY, $"Registry cleared ({_villagers.Count})");
            _villagers.Clear();
            _byId.Clear();
            OnCleared?.Invoke();
        }

        private int FindInsertIndex(string villagerId)
        {
            for (int i = 0; i < _villagers.Count; i++)
            {
                if (string.CompareOrdinal(villagerId, _villagers[i].VillagerId) < 0)
                {
                    return i;
                }
            }

            return _villagers.Count;
        }

        private sealed class VillagerEntry : IVillagerView
        {
            public string VillagerId { get; }
            public string DisplayName { get; }
            public string JobId { get; }
            public VillagerGender Gender { get; }
            public VillagerSocialClass SocialClass { get; }
            public VillagerAgeGroup AgeGroup { get; }
            public bool IsAlive { get; set; } = true;
            public VillagerScheduleView Schedule { get; }

            public VillagerEntry(VillagerPublicInfo info, VillagerScheduleView schedule)
            {
                VillagerId = info.VillagerId;
                DisplayName = info.DisplayName;
                JobId = info.JobId;
                Gender = info.Gender;
                SocialClass = info.SocialClass;
                AgeGroup = info.AgeGroup;
                Schedule = schedule;
            }
        }
    }
}
