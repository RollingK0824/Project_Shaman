using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Observation;
using ProjectShaman.AI.Routine;

namespace ProjectShaman.AI.Ghost
{
    // Server only. What a possessed villager shows of its ghost, in proportion to its yin. Separate from the corrupted routines.
    //  - Behaviour symptoms (tremble while walking, staring or neck-twisting at rest ...): rolled only while the villager is walking
    //    or resting, never while working, talking or asleep. One animation (plus a line for nonsense), then a cooldown.
    //  - Appearance symptoms (no shadow, pale skin, black eyes): episodes that start and end by themselves at any time,
    //    kept in AI_Core.AppearanceMask and synced to every peer.
    // Each symptom has a minimum yin (SymptomRule). Both kinds are witnessed: a villager who sees one remembers it and can be asked.
    public sealed class PossessionSymptoms
    {
        private const float MOVING_SPEED = 0.1f;

        private sealed class State
        {
            public float NextRoll;
            public float BehaviourFreeAt;
            public readonly Dictionary<SymptomType, float> EpisodeEnd = new Dictionary<SymptomType, float>();
            public readonly Dictionary<SymptomType, float> RestUntil = new Dictionary<SymptomType, float>();
        }

        private readonly AIBehaviourConfig _config;
        private readonly Action<AI_Core, SymptomType> _showBehaviour;
        private readonly System.Random _random;
        private readonly Dictionary<string, State> _states = new Dictionary<string, State>();
        private readonly List<SymptomRule> _candidates = new List<SymptomRule>();
        private readonly List<SymptomType> _ended = new List<SymptomType>();

        public PossessionSymptoms(AIBehaviourConfig config, Action<AI_Core, SymptomType> showBehaviour, int seed)
        {
            _config = config;
            _showBehaviour = showBehaviour;
            _random = new System.Random(seed);
        }

        public void Forget(string villagerId)
        {
            _states.Remove(villagerId);
        }

        // Night and exorcism: an episode in progress still has to end on time, and a freed host shows nothing
        public void TickEnd(AIManager.VillagerRuntime villager)
        {
            AI_Core core = villager.Core;
            AI_Possession possession = villager.Profile.Possession;
            bool dead = villager.Status != null && villager.Status.IsDead;

            if (core.AppearanceMask == 0)
            {
                return;
            }

            if (possession == null || dead)
            {
                ClearAppearance(core);
                _states.Remove(villager.Id);
                return;
            }

            if (_states.TryGetValue(villager.Id, out State state))
            {
                EndExpired(core, state, Time.time);
            }
        }

        public void Tick(AIManager.VillagerRuntime villager)
        {
            AI_Possession possession = villager.Profile.Possession;
            bool dead = villager.Status != null && villager.Status.IsDead;

            if (possession == null || dead)
            {
                TickEnd(villager);
                return;
            }

            float now = Time.time;
            if (!_states.TryGetValue(villager.Id, out State state))
            {
                state = new State { NextRoll = now + RandomRange(0.3f, 1f) * _config.SymptomCheckInterval };
                _states[villager.Id] = state;
            }

            EndExpired(villager.Core, state, now);

            if (now < state.NextRoll)
            {
                return;
            }

            state.NextRoll = now + RandomRange(0.7f, 1.3f) * _config.SymptomCheckInterval;

            float yin = possession.Yin;
            float factor = CorruptionPlanner.GetYinFactor(yin, _config);

            RollAppearance(villager.Core, state, yin, factor, now);
            RollBehaviour(villager, state, yin, factor, now);
        }

        private void RollAppearance(AI_Core core, State state, float yin, float factor, float now)
        {
            float baseChance = Mathf.Lerp(_config.AppearanceSymptomChanceAtZeroYin, _config.AppearanceSymptomChanceAtMaxYin, factor);

            foreach (SymptomRule rule in _config.SymptomRules)
            {
                if (rule == null || !rule.IsAppearance || yin < rule.MinYin || core.HasAppearance(rule.Type))
                {
                    continue;
                }

                if (state.RestUntil.TryGetValue(rule.Type, out float restUntil) && now < restUntil)
                {
                    continue;
                }

                if (_random.NextDouble() >= baseChance * Mathf.Max(0f, rule.Weight))
                {
                    continue;
                }

                float seconds = RandomRange(Mathf.Max(1f, rule.MinSeconds), Mathf.Max(rule.MinSeconds, rule.MaxSeconds));
                state.EpisodeEnd[rule.Type] = now + seconds;
                core.SetAppearance(rule.Type, true);

                // Seen for as long as it lasts
                SymptomType type = rule.Type;
                ObservableRegistry.Register(core.LogId, core.transform, ObservationService.SymptomVerb(type), null, seconds, true, () => core != null && core.HasAppearance(type));
            }
        }

        private void RollBehaviour(AIManager.VillagerRuntime villager, State state, float yin, float factor, float now)
        {
            if (now < state.BehaviourFreeAt)
            {
                return;
            }

            SymptomContext context = GetContext(villager);
            if (context == SymptomContext.None)
            {
                return;
            }

            float chance = Mathf.Lerp(_config.BehaviourSymptomChanceAtZeroYin, _config.BehaviourSymptomChanceAtMaxYin, factor);
            if (_random.NextDouble() >= chance)
            {
                return;
            }

            _candidates.Clear();
            float total = 0f;

            foreach (SymptomRule rule in _config.SymptomRules)
            {
                if (rule != null && rule.IsBehaviour && yin >= rule.MinYin && (rule.Context & context) != 0 && rule.Weight > 0f)
                {
                    _candidates.Add(rule);
                    total += rule.Weight;
                }
            }

            if (_candidates.Count == 0)
            {
                return;
            }

            double pick = _random.NextDouble() * total;
            SymptomRule chosen = _candidates[_candidates.Count - 1];

            foreach (SymptomRule rule in _candidates)
            {
                pick -= rule.Weight;
                if (pick <= 0d)
                {
                    chosen = rule;
                    break;
                }
            }

            state.BehaviourFreeAt = now + _config.BehaviourSymptomCooldown;
            _showBehaviour(villager.Core, chosen.Type);
        }

        // Walking, or at rest (a rest routine, free time, home); working, talking and sleeping show none
        private static SymptomContext GetContext(AIManager.VillagerRuntime villager)
        {
            if (villager.Core.IsAsleep || (villager.Interaction != null && villager.Interaction.IsInteracting))
            {
                return SymptomContext.None;
            }

            if (villager.Movement != null && villager.Movement.CurrentSpeed > MOVING_SPEED)
            {
                return SymptomContext.Moving;
            }

            ResolvedRoutine current = villager.Current;
            bool resting = villager.IsFree || villager.HasArrivedHome || current.Source == null || current.Category == RoutineCategory.Rest;
            return resting ? SymptomContext.Resting : SymptomContext.None;
        }

        private void EndExpired(AI_Core core, State state, float now)
        {
            if (state.EpisodeEnd.Count == 0)
            {
                return;
            }

            _ended.Clear();
            foreach (KeyValuePair<SymptomType, float> episode in state.EpisodeEnd)
            {
                if (now >= episode.Value)
                {
                    _ended.Add(episode.Key);
                }
            }

            foreach (SymptomType type in _ended)
            {
                state.EpisodeEnd.Remove(type);
                state.RestUntil[type] = now + _config.AppearanceSymptomRest;
                core.SetAppearance(type, false);
            }
        }

        private static void ClearAppearance(AI_Core core)
        {
            foreach (SymptomType type in Enum.GetValues(typeof(SymptomType)))
            {
                if (core.HasAppearance(type))
                {
                    core.SetAppearance(type, false);
                }
            }
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }
    }
}
