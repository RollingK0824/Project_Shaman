using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defense;
using ProjectShaman.AI.Minion;

namespace ProjectShaman.AI.Mock
{
    public class MockDefenseTrigger : MonoBehaviour
    {
        [SerializeField] private AIManager _manager;
        [SerializeField] private DefenseVoteResolver _voteResolver;
        [SerializeField] private MinionSpawner _spawner;
        [SerializeField] private float _directWaveYin = 50f;

        [ContextMenu("Vote Host (correct)")]
        private void VoteHost()
        {
            if (TryFindVillager(true, out string villagerId))
            {
                _voteResolver.Resolve(villagerId);
            }
        }

        [ContextMenu("Vote Innocent (wrong)")]
        private void VoteInnocent()
        {
            if (TryFindVillager(false, out string villagerId))
            {
                _voteResolver.Resolve(villagerId);
            }
        }

        [ContextMenu("Begin Waves Directly (skip DefenseManager)")]
        private void BeginWavesDirectly()
        {
            if (_spawner != null)
            {
                _spawner.BeginDefense(_directWaveYin);
            }
        }

        [ContextMenu("Stop Waves")]
        private void StopWaves()
        {
            if (_spawner != null)
            {
                _spawner.StopDefense("mock stop");
            }
        }

        private bool TryFindVillager(bool possessed, out string villagerId)
        {
            villagerId = string.Empty;

            if (_manager == null || _voteResolver == null)
            {
                AILog.Warn(AILog.DEFENSE, "MockDefenseTrigger needs AIManager and DefenseVoteResolver");
                return false;
            }

            foreach (AIManager.VillagerRuntime villager in _manager.Villagers)
            {
                if (villager.Core != null && (villager.Profile.Possession != null) == possessed)
                {
                    villagerId = villager.Id;
                    return true;
                }
            }

            AILog.Warn(AILog.DEFENSE, $"No {(possessed ? "possessed" : "innocent")} villager to vote");
            return false;
        }
    }
}
