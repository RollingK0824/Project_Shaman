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

            AILog.Log(AILog.FACTORY, $"Local spawn (no network), Agent/BT enabled: {instance.name}");
        }
    }
}
