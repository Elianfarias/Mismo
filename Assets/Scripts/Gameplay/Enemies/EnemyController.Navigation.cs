using Mismo.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Enemies
{
    public abstract partial class EnemyController
    {
        private NavMeshPath tacticalPath;
        private float backstepRemaining, previousAcceleration, previousSpeed;
        private bool previousBraking;
        private Vector3 backstepDestination;
        public bool IsBackstepping { get; private set; }

        private bool TryStartBackstep()
        {
            if (!CanNavigate || target == null || settings.comboBackstepDistance <= 0f) return false;
            Vector3 away = Vector3.ProjectOnPlane(transform.position - target.position, Vector3.up).normalized;
            if (away.sqrMagnitude < .001f) away = -transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, away) * orbitSign;
            if (!TryBackstepDirection(away) &&
                !TryBackstepDirection((away + side * .8f).normalized) &&
                !TryBackstepDirection((away - side * .8f).normalized)) return false;

            previousAcceleration = agent.acceleration;
            previousSpeed = agent.speed;
            previousBraking = agent.autoBraking;
            float travelTime = Mathf.Max(.1f, settings.comboBackstepDuration);
            agent.acceleration = Mathf.Max(previousAcceleration, 120f);
            agent.autoBraking = false;
            agent.speed = settings.comboBackstepDistance / travelTime *
                (GetComponent<CombatAilment>()?.SpeedMultiplier ?? 1f);
            // Allow acceleration and the native navigation update to start before the hard timeout.
            // Arrival still ends the step immediately; do not cut it short inside the melee/ranged gap.
            backstepRemaining = travelTime + agent.speed / agent.acceleration + .1f;
            agent.isStopped = false;
            IsBackstepping = true;
            // Install the already validated path synchronously; a short burst cannot wait for repathing.
            if (agent.SetPath(tacticalPath)) return true;
            CancelBackstep();
            return false;
        }

        private bool TryBackstepDirection(Vector3 direction)
        {
            return TryTacticalDestination(transform.position + direction * settings.comboBackstepDistance,
                       true, false, out backstepDestination) && BackstepPathClear(backstepDestination);
        }

        private bool BackstepPathClear(Vector3 destination)
        {
            Vector3 offset = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
            if (offset.sqrMagnitude < .001f) return true;
            float radius = agent.radius * .9f;
            foreach (var hit in Physics.CapsuleCastAll(
                transform.position + Vector3.up * (agent.radius + .1f),
                transform.position + Vector3.up * Mathf.Max(agent.radius + .1f, agent.height - agent.radius),
                radius, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) return false;
            return true;
        }

        private void TickBackstep(float dt)
        {
            backstepRemaining -= dt;
            if (backstepRemaining > 0f && Vector3.Distance(transform.position, backstepDestination) > .15f &&
                BackstepPathClear(backstepDestination)) return;
            CancelBackstep();
            ComboResponseFinished();
        }

        private void CancelBackstep()
        {
            if (!IsBackstepping) return;
            IsBackstepping = false;
            backstepRemaining = 0f;
            if (agent != null)
            {
                agent.acceleration = previousAcceleration;
                agent.speed = previousSpeed;
                agent.autoBraking = previousBraking;
            }
            Stop();
        }

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
