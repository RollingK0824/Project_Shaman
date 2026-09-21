using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class AI_Movement : MonoBehaviour, INetworkMovable
    {
        private NavMeshAgent agent;

        public bool IsStopped => agent != null && agent.isStopped;
        public bool HasReachedDestination
        {
            get
            {
                if (agent == null || !agent.isActiveAndEnabled) return false;
                if (agent.pathPending) return false;
                if (agent.remainingDistance > agent.stoppingDistance) return false;
                return agent.hasPath || Vector3.Distance(transform.position, agent.destination) <= agent.stoppingDistance;
            }
        }
        public float CurrentSpeed => agent != null ? agent.velocity.magnitude : 0f;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        public void SetAgentActive(bool active)
        {
            if (agent != null)
            {
                agent.enabled = active;
            }
        }

        public void MoveTo(Vector3 destination)
        {
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = false;
                agent.SetDestination(destination);
            }
        }

        public void Stop()
        {
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = true;
            }
        }
    }
}
