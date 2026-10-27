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
        public event Action<IVillagerView> OnDeathKnown;
        public event Action<IVillagerView> OnExorcised;
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
            OnDeathKnown = null;
            OnExorcised = null;
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

        // Today's block hours as the village sees them. Kept for a dead villager whose death is not yet known,
        // so the schedule tab does not give the death away before a body is found
        internal void SetDayHours(string villagerId, int day, int[] blockHours, int homeHour, int bedHour)
        {
            if (!string.IsNullOrEmpty(villagerId) && _byId.TryGetValue(villagerId, out VillagerEntry entry))
            {
                if (entry.Day != day)
                {
                    entry.RestedSlotMask = 0;
                }

                entry.Day = day;
                entry.BlockHours = blockHours;
                entry.HomeHour = homeHour;
                entry.BedHour = bedHour;
            }
        }

        // A work block of today given up for a missing tool (the villager rests instead), or taken up again
        internal void SetSlotRested(string villagerId, int day, int slot, bool rested)
        {
            if (string.IsNullOrEmpty(villagerId) || !_byId.TryGetValue(villagerId, out VillagerEntry entry) || entry.Day != day || slot < 0 || slot >= 32)
            {
                return;
            }

            entry.RestedSlotMask = rested ? entry.RestedSlotMask | (1 << slot) : entry.RestedSlotMask & ~(1 << slot);
        }

        public bool IsSlotRested(string villagerId, int day, int slot)
        {
            return !string.IsNullOrEmpty(villagerId) && _byId.TryGetValue(villagerId, out VillagerEntry entry)
                && entry.Day == day && slot >= 0 && slot < 32 && (entry.RestedSlotMask & (1 << slot)) != 0;
        }

        public bool TryGetDayHours(string villagerId, out int day, out int[] blockHours, out int homeHour, out int bedHour)
        {
            day = 0;
            blockHours = null;
            homeHour = 0;
            bedHour = 0;

            if (string.IsNullOrEmpty(villagerId) || !_byId.TryGetValue(villagerId, out VillagerEntry entry) || entry.Day <= 0)
            {
                return false;
            }

            day = entry.Day;
            blockHours = entry.BlockHours;
            homeHour = entry.HomeHour;
            bedHour = entry.BedHour;
            return true;
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

        internal void MarkDeathKnown(string villagerId)
        {
            if (string.IsNullOrEmpty(villagerId) || !_byId.TryGetValue(villagerId, out VillagerEntry entry) || entry.IsDeathKnown)
            {
                return;
            }

            entry.IsDeathKnown = true;
            AILog.Log(AILog.FACTORY, villagerId, "Registry death now known");
            OnDeathKnown?.Invoke(entry);
        }

        internal void MarkExorcised(string villagerId)
        {
            if (string.IsNullOrEmpty(villagerId) || !_byId.TryGetValue(villagerId, out VillagerEntry entry) || entry.IsExorcised)
            {
                return;
            }

            entry.IsExorcised = true;
            AILog.Log(AILog.FACTORY, villagerId, "Registry marked exorcised");
            OnExorcised?.Invoke(entry);
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
            public string JobName { get; }
            public VillagerGender Gender { get; }
            public VillagerSocialClass SocialClass { get; }
            public VillagerAgeGroup AgeGroup { get; }
            public bool IsAlive { get; set; } = true;
            public bool IsDeathKnown { get; set; }
            public bool IsExorcised { get; set; }
            public VillagerScheduleView Schedule { get; }
            public int Day { get; set; }
            public int[] BlockHours { get; set; }
            public int HomeHour { get; set; }
            public int BedHour { get; set; }
            public int RestedSlotMask { get; set; }

            public VillagerEntry(VillagerPublicInfo info, VillagerScheduleView schedule)
            {
                VillagerId = info.VillagerId;
                DisplayName = info.DisplayName;
                JobId = info.JobId;
                JobName = info.JobName;
                Gender = info.Gender;
                SocialClass = info.SocialClass;
                AgeGroup = info.AgeGroup;
                Schedule = schedule;
            }
        }
    }
}
