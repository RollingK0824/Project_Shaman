using UnityEngine;
using Unity.Behavior;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCMovement : MonoBehaviour
{

    [SerializeField]
    private Transform _testTarget;

    private NavMeshAgent agent;

    public bool HasReachedDestination =>
        !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;

    public float CurrentNormailizedSpeed =>
        agent.speed > 0 ? agent.velocity.magnitude / agent.speed : 0f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        MoveTo(_testTarget.position);
    }

    public void MoveTo(Vector3 targetPosition)
    {
        if(agent.enabled)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPosition);
        }
    }

    public void Stop()
    {
        if(agent.enabled && agent.hasPath)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }
}
