using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.AI.Defense
{
    public readonly struct DefenseVerdict
    {
        public readonly int Day;
        public readonly string VillagerId;
        public readonly string GhostId;
        public readonly float Yin;
        public readonly bool IsCorrect;

        public DefenseVerdict(int day, string villagerId, string ghostId, float yin, bool isCorrect)
        {
            Day = day;
            VillagerId = villagerId;
            GhostId = ghostId;
            Yin = yin;
            IsCorrect = isCorrect;
        }

        public override string ToString()
        {
            return IsCorrect ? $"Day{Day} {VillagerId} correct (ghost {GhostId}, yin {Yin:F0})" : $"Day{Day} {VillagerId} wrong";
        }
    }

    public class DefenseVoteResolver : MonoBehaviour
    {
        [SerializeField] private AIFactory _factory;
        [SerializeField] private AIManager _manager;
        [SerializeField] private NightHuntDirector _huntDirector;

        [Header("Runtime (server only)")]
        [SerializeField] private string _lastVerdict;

        private VoteManager _voteManager;
        private DefenseManager _defense;
        private bool _hasVerdict;
        private DefenseVerdict _verdict;

        public bool HasCorrectVerdict => _hasVerdict && _verdict.IsCorrect;
        public DefenseVerdict LastVerdict => _verdict;

        public event Action<DefenseVerdict> OnResolved;

        private void Awake()
        {
            if (_factory != null)
            {
                _factory.OnVillagersCreated += HandleVillagersCreated;
                _factory.OnVillagersCleared += HandleVillagersCleared;
            }
        }

        private void OnDestroy()
        {
            if (_factory != null)
            {
                _factory.OnVillagersCreated -= HandleVillagersCreated;
                _factory.OnVillagersCleared -= HandleVillagersCleared;
            }

            UnsubscribeVote();
        }

        private void HandleVillagersCreated(IReadOnlyList<AI_Core> cores)
        {
            UnsubscribeVote();

            _defense = DefenseManager.Existing != null ? DefenseManager.Existing : FindFirstObjectByType<DefenseManager>();
            if (_defense != null)
            {
                _defense.DefenseCompleted += HandleDefenseCompleted;
            }
            else
            {
                AILog.Warn(AILog.DEFENSE, "DefenseManager not in scene, a won defense will not exorcise the host");
            }

            _voteManager = VoteManager.Existing != null ? VoteManager.Existing : FindFirstObjectByType<VoteManager>();

            if (_voteManager == null)
            {
                AILog.Warn(AILog.DEFENSE, "VoteManager not in scene, vote results will not reach AI (use Resolve directly for tests)");
                return;
            }

            _voteManager.NpcVotedOut += HandleNpcVotedOut;
        }

        private void HandleVillagersCleared()
        {
            UnsubscribeVote();
            _hasVerdict = false;
            _lastVerdict = string.Empty;
        }

        private void UnsubscribeVote()
        {
            if (_defense != null)
            {
                _defense.DefenseCompleted -= HandleDefenseCompleted;
                _defense = null;
            }

            if (_voteManager != null)
            {
                _voteManager.NpcVotedOut -= HandleNpcVotedOut;
                _voteManager = null;
            }
        }

        private void HandleNpcVotedOut(string villagerId)
        {
            if (string.IsNullOrEmpty(villagerId))
            {
                AILog.Log(AILog.DEFENSE, "Vote finished without a target, nothing to judge");
                return;
            }

            // "Went to drive them out and found them already dead": the death comes to light, the vote judged nobody
            if (_factory != null && _factory.Registry != null && _factory.Registry.TryGet(villagerId, out Interfaces.IVillagerView view) && !view.IsAlive)
            {
                Vector3 where = _manager != null && _manager.TryGetHomePosition(villagerId, out Vector3 home) ? home : Vector3.zero;
                AILog.Log(AILog.DEFENSE, villagerId, "Voted out but already dead");
                _manager?.RevealDeath(villagerId, where, "voted out, found already dead");
                return;
            }

            Resolve(villagerId);
        }

        public DefenseVerdict Resolve(string villagerId)
        {
            int day = TimeManager.Existing != null ? TimeManager.Existing.DayCount : 0;
            AI_Possession possession = FindPossession(villagerId);
            bool isCorrect = possession != null;

            _verdict = isCorrect
                ? new DefenseVerdict(day, villagerId, possession.GhostId, possession.Yin, true)
                : new DefenseVerdict(day, villagerId, string.Empty, 0f, false);
            _hasVerdict = true;
            _lastVerdict = _verdict.ToString();

            AILog.Log(AILog.DEFENSE, $"Vote verdict {_verdict}");

            if (isCorrect)
            {
                BeginDefense(_verdict);
            }
            else
            {
                HandleWrongVote(_verdict);
            }

            OnResolved?.Invoke(_verdict);
            return _verdict;
        }

        private AI_Possession FindPossession(string villagerId)
        {
            if (string.IsNullOrEmpty(villagerId) || _manager == null)
            {
                return null;
            }

            foreach (AIManager.VillagerRuntime villager in _manager.Villagers)
            {
                if (villager.Id == villagerId && villager.Core != null && villager.Profile.Possession != null)
                {
                    return villager.Profile.Possession;
                }
            }

            return null;
        }

        private void BeginDefense(DefenseVerdict verdict)
        {
            if (_huntDirector != null)
            {
                _huntDirector.ExcludeGhostTonight(verdict.GhostId);
            }
            else
            {
                AILog.Warn(AILog.DEFENSE, "NightHuntDirector missing, exorcised ghost is not excluded tonight");
            }

            //DefenseManager defense = DefenseManager.Existing != null ? DefenseManager.Existing : FindFirstObjectByType<DefenseManager>();
            //if (defense == null)
            //{
            //    AILog.Warn(AILog.DEFENSE, "DefenseManager not in scene, defense not started");
            //    return;
            //}
            //
            //defense.StartDefense();
            //AILog.Log(AILog.DEFENSE, verdict.VillagerId, "DefenseManager.StartDefense requested");
        }

        // Holding out until morning on a correct verdict exorcises the host; losing leaves the ghost as it was
        private void HandleDefenseCompleted(bool won)
        {
            if (!_hasVerdict || !_verdict.IsCorrect)
            {
                return;
            }

            _hasVerdict = false;
            AILog.Log(AILog.DEFENSE, _verdict.VillagerId, $"Defense {(won ? "won" : "lost")} against {_verdict.GhostId}");

            if (won)
            {
                _manager?.Exorcise(_verdict.VillagerId);
            }
        }

        private void HandleWrongVote(DefenseVerdict verdict)
        {
            AILog.Log(AILog.STUB, verdict.VillagerId, "Wrong vote: no defense (ghost-side penalty not designed yet)");
        }
    }
}
