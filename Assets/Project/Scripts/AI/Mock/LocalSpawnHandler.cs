using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI.Mock
{
    public class LocalSpawnHandler : ISpawnHandler
    {
        public string HandlerName => "Local";
        public bool CanSpawn => true;

        public void Spawn(GameObject instance)
        {
            AI_Movement movement = instance.GetComponent<AI_Movement>();
            AI_Core core = instance.GetComponent<AI_Core>();

            if (movement != null)
            {
                movement.SetAgentActive(true);
            }

            if (core != null)
            {
                core.SetBehaviorGraphActive(true);
            }

            ProjectShaman.AI.Ghost.AI_GhostEntity ghost = instance.GetComponent<ProjectShaman.AI.Ghost.AI_GhostEntity>();
            if (ghost != null)
            {
                ghost.SetSimulationActive(true);
            }

            AILog.Log(AILog.FACTORY, $"Local spawn (no network), Agent/BT enabled: {instance.name}");
        }

        public void Despawn(GameObject instance)
        {
            if (instance != null)
            {
                AILog.Log(AILog.FACTORY, $"Local despawn: {instance.name}");
                Object.Destroy(instance);
            }
        }
    }
}
