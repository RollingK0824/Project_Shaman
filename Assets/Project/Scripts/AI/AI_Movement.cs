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
        // Callers set the speed they want; a slow effect scales it without them knowing
        private float baseSpeed;
        private float speedMultiplier = 1f;

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
        public float DefaultSpeed => defaultSpeed;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            defaultSpeed = agent != null ? agent.speed : 0f;
            baseSpeed = defaultSpeed;
        }

        public void SetSpeed(float speed)
        {
            baseSpeed = Mathf.Max(0.1f, speed);
            ApplySpeed();
        }

        public void ResetSpeed()
        {
            baseSpeed = defaultSpeed;
            ApplySpeed();
        }

        // 1 = normal. Kept across SetSpeed/ResetSpeed until set back
        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
            ApplySpeed();
        }

        // Jump to a point on the NavMesh without walking there (the caller has checked it is reachable)
        public bool Warp(Vector3 position)
        {
            return agent != null && agent.isActiveAndEnabled && agent.Warp(position);
        }

        private void ApplySpeed()
        {
            if (agent != null)
            {
                agent.speed = Mathf.Max(0.1f, baseSpeed * speedMultiplier);
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
