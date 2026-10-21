using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Trace;

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
        private readonly AIBehaviourConfig _config;
        private readonly List<StressEntry> _stress = new List<StressEntry>();
        private readonly List<DayEffectEntry> _dayEffects = new List<DayEffectEntry>();
        private readonly List<GameObject> _traces = new List<GameObject>();

        public NightOutcomeLedger(AIManager manager, ISpawnHandler spawnHandler, AIBehaviourConfig config)
        {
            _manager = manager;
            _spawnHandler = spawnHandler;
            _config = config;
        }

        public AIBehaviourConfig Config => _config;
        public int NightDay { get; private set; } = -1;

        public void BeginNight(int dayCount)
        {
            NightDay = dayCount;
            AILog.Log(AILog.GHOST, $"Night outcomes Day{dayCount} opened");
        }

        public void AddYin(AI_Possession possession, float amount, string reason)
        {
            if (possession == null || amount <= 0f)
            {
                return;
            }

            possession.AddPendingYin(amount, reason);
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

        public void AddStressAround(Vector3 center, float radius, float amount, string reason)
        {
            if (amount <= 0f || _manager == null)
            {
                return;
            }

            float sqrRadius = radius * radius;

            foreach (AIManager.VillagerRuntime villager in _manager.Villagers)
            {
                if (villager.Profile == null || villager.Profile.IsPossessed)
                {
                    continue;
                }

                Vector3 offset = villager.Profile.HomePosition - center;
                offset.y = 0f;

                if (radius <= 0f || offset.sqrMagnitude <= sqrRadius)
                {
                    AddVictimStress(villager.Id, amount, reason);
                }
            }
        }

        public bool IsAlive(AI_Core core)
        {
            if (core == null || _manager == null)
            {
                return false;
            }

            foreach (AIManager.VillagerRuntime villager in _manager.Villagers)
            {
                if (villager.Core == core)
                {
                    return true;
                }
            }

            return false;
        }

        public GameObject SpawnTrace(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation);
            GhostTrace trace = instance.GetComponent<GhostTrace>();

            if (trace != null)
            {
                trace.Initialize(_config, NightDay, null);
                trace.OnResolved += HandleTraceResolved;
            }
            else
            {
                AILog.Warn(AILog.GHOST, $"Trace prefab {prefab.name} has no GhostTrace, kept until game end");
            }

            if (_spawnHandler != null)
            {
                _spawnHandler.Spawn(instance);
            }

            _traces.Add(instance);
            AILog.Log(AILog.GHOST, $"Trace {prefab.name} spawned at {position}");

            if (trace != null)
            {
                EnforceTraceCap(trace);
            }

            return instance;
        }

        private void EnforceTraceCap(GhostTrace added)
        {
            int cap = _config != null ? _config.TraceMaxPerKind : int.MaxValue;

            while (GhostTraceRegistry.CountActive(added.Kind) > cap)
            {
                GhostTrace oldest = GhostTraceRegistry.FindOldestActive(added.Kind, added);
                if (oldest == null)
                {
                    break;
                }

                oldest.Resolve(TraceResolveReason.Capped);
            }
        }

        private void HandleTraceResolved(GhostTrace trace, TraceResolveReason reason)
        {
            trace.OnResolved -= HandleTraceResolved;
            _traces.Remove(trace.gameObject);

            if (_spawnHandler != null)
            {
                _spawnHandler.Despawn(trace.gameObject);
            }
        }

        public void ScheduleDayEffect(string villagerId, GhostDayEffect effect)
        {
            _dayEffects.Add(new DayEffectEntry { VillagerId = villagerId, Effect = effect });
            AILog.Log(AILog.GHOST, villagerId, $"Day effect {effect} queued for morning");
        }

        public void FlushMorning(int dayCount)
        {
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

            AILog.Log(AILog.GHOST, $"Morning outcomes Day{dayCount}: victimStress={_stress.Count}, dayEffects={_dayEffects.Count}, traces={_traces.Count}");

            _stress.Clear();
            _dayEffects.Clear();
        }

        public void Clear()
        {
            _stress.Clear();
            _dayEffects.Clear();

            foreach (GameObject instance in _traces.ToArray())
            {
                if (instance == null)
                {
                    continue;
                }

                GhostTrace trace = instance.GetComponent<GhostTrace>();
                if (trace != null && trace.IsActive)
                {
                    trace.Resolve(TraceResolveReason.GameEnd);
                }
                else if (_spawnHandler != null)
                {
                    _spawnHandler.Despawn(instance);
                }
            }

            _traces.Clear();
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
