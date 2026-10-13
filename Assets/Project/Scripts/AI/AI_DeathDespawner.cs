using System.Collections;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI
{
    [RequireComponent(typeof(AI_Status))]
    public class AI_DeathDespawner : MonoBehaviour
    {
        private AI_Status _status;
        private ISpawnHandler _spawnHandler;
        private bool _isDespawning;

        private void Awake()
        {
            _status = GetComponent<AI_Status>();
        }

        private void OnEnable()
        {
            _isDespawning = false;
            _status.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            _status.OnDied -= HandleDied;
        }

        public void SetSpawnHandler(ISpawnHandler spawnHandler)
        {
            _spawnHandler = spawnHandler;
        }

        private void HandleDied(AI_Status status, GameObject source)
        {
            if (_isDespawning)
            {
                return;
            }

            _isDespawning = true;
            StartCoroutine(DespawnAfterDelay(status.DeathDespawnDelay));
        }

        private IEnumerator DespawnAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            if (_spawnHandler == null)
            {
                AILog.Error(AILog.CORE, $"{name} died without spawn handler, despawn skipped (call SetSpawnHandler on spawn)");
                yield break;
            }

            _spawnHandler.Despawn(gameObject);
        }
    }
}
