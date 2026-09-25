using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Player.Quests
{
    /// <summary>Walks between authored village destinations once streamed navigation is ready.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class VillageNpcRoutine : MonoBehaviour
    {
        public Animator animator;
        public float walkingSpeed = 1.35f;
        public Vector2 pauseSeconds = new Vector2(5, 12);
        Vector3[] destinations = System.Array.Empty<Vector3>();
        NavMeshAgent agent;
        PlayerInventory player;
        QuestGiver giver;
        float nextDecision, retryAt, tripDeadline;
        int nextStop;
        bool travelling;
        static readonly int Speed = Animator.StringToHash("Speed");

        public void Initialize(Vector3[] stops, int phase)
        {
            destinations = stops ?? System.Array.Empty<Vector3>();
            nextStop = destinations.Length == 0 ? 0 : phase % destinations.Length;
            nextDecision = Time.time + 2 + phase * 1.7f;
        }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            giver = GetComponent<QuestGiver>();
            if(GetComponent<NpcGrounding>()==null)gameObject.AddComponent<NpcGrounding>();
            // The world creates residents before its asynchronous navigation bake finishes.
            agent.enabled = false;
        }

        void Update()
        {
            if (Time.timeScale == 0) return;
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                if (animator != null) animator.SetFloat(Speed, 0);
                if (Time.time < retryAt) return;
                retryAt = Time.time + 1;
                agent.enabled = false;
                if (!NavMesh.SamplePosition(transform.position, out var hit, 2, agent.areaMask)) return;
                transform.position = hit.position;
                agent.enabled = true;
                if (!agent.isOnNavMesh) return;
                travelling = false;
            }
            if (player == null && Time.time >= retryAt)
            {
                player = FindAnyObjectByType<PlayerInventory>();
                retryAt = Time.time + 2;
            }
            bool talking = player != null && giver != null && giver.InRange(player);
            agent.isStopped = talking;
            if (talking)
            {
                tripDeadline = Time.time + 45;
                var direction = player.transform.position - transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude > .01f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 100 * Time.deltaTime);
            }
            else if (travelling && !agent.pathPending &&
                (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + .15f || Time.time > tripDeadline))
            {
                agent.ResetPath();
                travelling = false;
                nextDecision = Time.time + Random.Range(pauseSeconds.x, pauseSeconds.y);
            }
            else if (!travelling && Time.time >= nextDecision && destinations.Length > 0)
            {
                var target = destinations[nextStop];
                nextStop = (nextStop + 1) % destinations.Length;
                nextDecision = Time.time + 3;
                var path = new NavMeshPath();
                if (NavMesh.SamplePosition(target, out var hit, 1.5f, agent.areaMask) &&
                    agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    agent.speed = walkingSpeed;
                    travelling = agent.SetPath(path);
                    tripDeadline = Time.time + 45;
                }
            }
            if (animator != null)
                animator.SetFloat(Speed, talking ? 0 : agent.velocity.magnitude / Mathf.Max(.1f, walkingSpeed), .18f, Time.deltaTime);
        }
    }
}
