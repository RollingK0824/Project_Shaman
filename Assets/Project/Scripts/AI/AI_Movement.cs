using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class AI_Movement : MonoBehaviour, INetworkMovable
    {
        private NavMeshAgent agent;
        private float defaultSpeed;

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
            defaultSpeed = agent != null ? agent.speed : 0f;
        }

        public void SetSpeed(float speed)
        {
            if (agent != null)
            {
                agent.speed = Mathf.Max(0.1f, speed);
            }
        }

        public void ResetSpeed()
        {
            if (agent != null)
            {
                agent.speed = defaultSpeed;
            }
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
