using Mismo.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    public abstract partial class EnemyController
    {
        private NavMeshPath tacticalPath;

        protected void PositionAt(Vector3 destination, float speed)
        {
            if (State != EnemyState.Position) Enter(EnemyState.Position);
            MoveTo(destination, speed);
        }

        protected void HoldPosition()
        {
            if (State != EnemyState.Position) Enter(EnemyState.Position);
            Stop();
        }

        protected bool TryTacticalDestination(Vector3 requested, bool direct, bool requireSight, out Vector3 position)
        {
            position = transform.position;
            if (!CanNavigate || !NavMesh.SamplePosition(requested, out var hit, .75f, agent.areaMask)) return false;
            if (Vector3.Distance(home, hit.position) > Mathf.Max(settings.leashRange, provokedLeashRange) - .5f) return false;
            if (direct && agent.Raycast(hit.position, out _)) return false;
            tacticalPath ??= new NavMeshPath();
            if (!agent.CalculatePath(hit.position, tacticalPath) || tacticalPath.status != NavMeshPathStatus.PathComplete) return false;
            if (requireSight && !EnemyPerception.HasSightFrom(hit.position, transform, target, settings.sightHeight)) return false;
            position = hit.position;
            return true;
        }

        private void Face(
            Vector3 direction,
            float dt)
        {
            if (
                direction.sqrMagnitude >
                0.001f
            )
            {
                transform.rotation =
                    Quaternion.RotateTowards(
                        transform.rotation,
                        Quaternion.LookRotation(
                            direction
                        ),
                        360f * dt
                    );
            }
        }

        private void MoveTo(
            Vector3 position,
            float speed)
        {
            if (
                !CanNavigate ||
                repath > 0f
            )
                return;

            repath = 0.2f;

            if (
                !NavMesh.SamplePosition(
                    position,
                    out NavMeshHit hit,
                    1.5f,
                    agent.areaMask
                )
            )
            {
                Stop();
                return;
            }

            agent.speed =
                speed *
                (
                    GetComponent<
                        Mismo.Gameplay.Combat.CombatAilment
                    >()?.SpeedMultiplier ?? 1
                );

            agent.isStopped = false;

            agent.SetDestination(
                hit.position
            );
        }

        private void Stop()
        {
            if (!CanNavigate)
                return;

            agent.ResetPath();
            // Apply the stop after clearing the path; clearing it can reset the native stop flag.
            agent.isStopped = true;
        }
    }
}
