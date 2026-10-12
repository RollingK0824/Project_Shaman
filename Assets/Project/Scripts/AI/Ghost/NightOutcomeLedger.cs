using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI.Ghost
{
    public class NightOutcomeLedger
    {
        private struct StressEntry
        {
            public string VillagerId;
            public float Amount;
            public string Reason;
        }

        private struct DayEffectEntry
        {
            public string VillagerId;
            public GhostDayEffect Effect;
        }

        private readonly AIManager _manager;
        private readonly ISpawnHandler _spawnHandler;
        private readonly List<StressEntry> _stress = new List<StressEntry>();
        private readonly List<DayEffectEntry> _dayEffects = new List<DayEffectEntry>();
        private readonly List<GameObject> _tonightTraces = new List<GameObject>();
        private readonly List<GameObject> _previousTraces = new List<GameObject>();

        public NightOutcomeLedger(AIManager manager, ISpawnHandler spawnHandler)
        {
            _manager = manager;
            _spawnHandler = spawnHandler;
        }

        public void AddYin(AI_Possession possession, float amount, string reason)
        {
            if (possession == null || amount <= 0f)
            {
                return;
            }

            possession.AddPendingYin(amount, reason);
        }

        public void KillVillager(AI_Core victim, string reason)
        {
            if (victim != null && _manager != null)
            {
                _manager.RemoveVillager(victim, reason);
            }
        }

        public void AddVictimStress(string villagerId, float amount, string reason)
        {
            if (amount <= 0f)
            {
                return;
            }

            _stress.Add(new StressEntry { VillagerId = villagerId, Amount = amount, Reason = reason });
            AILog.Log(AILog.GHOST, villagerId, $"Victim stress +{amount} queued for morning ({reason})");
        }

        public GameObject SpawnTrace(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation);

            if (_spawnHandler != null)
            {
                _spawnHandler.Spawn(instance);
            }

            _tonightTraces.Add(instance);
            AILog.Log(AILog.GHOST, $"Trace {prefab.name} spawned at {position}");
            return instance;
        }

        public void ScheduleDayEffect(string villagerId, GhostDayEffect effect)
        {
            _dayEffects.Add(new DayEffectEntry { VillagerId = villagerId, Effect = effect });
            AILog.Log(AILog.GHOST, villagerId, $"Day effect {effect} queued for morning");
        }

        public void FlushMorning(int dayCount)
        {
            DespawnTraces(_previousTraces);
            _previousTraces.AddRange(_tonightTraces);
            _tonightTraces.Clear();

            foreach (StressEntry entry in _stress)
            {
                if (IsAlive(entry.VillagerId))
                {
                    AIEvents.RaiseStress(entry.VillagerId, entry.Reason, entry.Amount);
                }
            }

            foreach (DayEffectEntry entry in _dayEffects)
            {
                if (IsAlive(entry.VillagerId))
                {
                    AILog.Log(AILog.STUB, entry.VillagerId, $"Day effect {entry.Effect} not applied yet (day behaviour branch)");
                }
            }

            AILog.Log(AILog.GHOST, $"Morning outcomes Day{dayCount}: victimStress={_stress.Count}, dayEffects={_dayEffects.Count}, traces={_previousTraces.Count}");

            _stress.Clear();
            _dayEffects.Clear();
        }

        public void Clear()
        {
            _stress.Clear();
            _dayEffects.Clear();
            DespawnTraces(_tonightTraces);
            DespawnTraces(_previousTraces);
        }

        private void DespawnTraces(List<GameObject> traces)
        {
            foreach (GameObject trace in traces)
            {
                if (trace != null && _spawnHandler != null)
                {
                    _spawnHandler.Despawn(trace);
                }
            }

            traces.Clear();
        }

        private bool IsAlive(string villagerId)
        {
            if (_manager == null)
            {
                return false;
            }

            foreach (AIManager.VillagerRuntime villager in _manager.Villagers)
            {
                if (villager.Id == villagerId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
