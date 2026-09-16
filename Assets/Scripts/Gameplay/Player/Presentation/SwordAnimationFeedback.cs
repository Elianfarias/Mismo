using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Movement;
using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>
    /// Animación procedural temporal del arma. Lee eventos de las habilidades de espada y
    /// aplica poses aditivas al visual, dejando libre el Animator definitivo.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(300)]
    public sealed class SwordAnimationFeedback : MonoBehaviour
    {
        private enum Motion
        {
            None,
            Slash,
            HeavySlash,
            Parry,
            Spin,
            Lunge
        }

        [Header("Sword visual")]
        [SerializeField] private Transform swordVisual;
        [SerializeField] private Transform rigHand;
        [SerializeField] private bool useAuthoredAnimations;
        public void UseAuthoredAnimations() => useAuthoredAnimations = true;
        public void ConfigureRig(Transform hand)
        {
            rigHand = hand;
            swordVisual = hand;
        }
        [SerializeField, Min(0.01f)] private float slashDuration = 0.38f;
        [SerializeField, Min(0.01f)] private float heavySlashDuration = 0.52f;
        [SerializeField, Min(0.01f)] private float spinDuration = 0.55f;
        [SerializeField, Min(0.01f)] private float lungeDuration = 0.24f;

        private BasicSwordCombo combo;
        private SwordParry parry;
        private SwordSpinAttack spin;
        private SwordLunge lunge;
        private PlayerMotor motor;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Motion motion;
        private float elapsed;
        private float duration;
        private int priority;
        private int comboStep;
        private AttackHitbox hitbox;
        private TrailRenderer trail;
        private Material trailMaterial;
        private Transform lastTrailVisual;
        private SkinnedMeshRenderer bladeRenderer;
        private Mesh bladeSnapshot;
        private int bladeTipIndex=-1;
        private readonly System.Collections.Generic.List<Vector3> bladeVertices=new System.Collections.Generic.List<Vector3>();
        public Transform SwordVisual => swordVisual;
        public bool ImpactVisible => trail != null && trail.emitting;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            if (swordVisual == null) swordVisual = FindChild(transform, "BasicSword");
            // Only animate the weapon root, never an arbitrary body mesh.
            if (swordVisual != null && rigHand == null)
            {
                // The imported hand has a scale of 100. Keep the weapon outside the rig;
                // poses below are in world metres, oriented by the motor's facing.
                swordVisual.SetParent(transform, true);
                Vector3 parentScale = transform.lossyScale;
                swordVisual.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);
                basePosition = new Vector3(.43f, .93f, .05f);
                baseRotation = Quaternion.Euler(-58f, 12f, -10f);
                SetPose(basePosition, baseRotation);
            }
            if (rigHand != null)
            {
                basePosition = new Vector3(.43f, .93f, .05f);
                baseRotation = Quaternion.Euler(-58f, 12f, -10f);
            }
            combo = GetComponentInChildren<BasicSwordCombo>();
            parry = GetComponentInChildren<SwordParry>();
            spin = GetComponentInChildren<SwordSpinAttack>();
            lunge = GetComponentInChildren<SwordLunge>();
            hitbox = GetComponentInChildren<AttackHitbox>();
            if(swordVisual != null)
            {
                var tip=new GameObject("Sword trail");tip.layer=2;tip.transform.SetParent(swordVisual,false);tip.transform.localPosition=Vector3.forward*(rigHand != null ? .0096f : .96f);
                trail=tip.AddComponent<TrailRenderer>();trail.time=.10f;trail.minVertexDistance=.015f;
                trail.startWidth=.28f;trail.endWidth=0;trail.numCapVertices=3;
                trailMaterial=RuntimeParticleMaterial.Create("Sword trail", Color.white);trail.sharedMaterial=trailMaterial;
                trail.startColor=new Color(1,.88f,.50f,.8f);trail.endColor=new Color(1,.7f,.2f,0);trail.emitting=false;
            }
        }

        private void FollowBladeTip()
        {
            if(rigHand==null)return;
            if(bladeRenderer==null)
            {
                foreach(var candidate in GetComponentsInChildren<SkinnedMeshRenderer>())
                    if(candidate.name=="Sword_E_RightHand") {bladeRenderer=candidate;break;}
                if(bladeRenderer==null)return;
                bladeSnapshot=new Mesh();
            }
            // Follow the evaluated blade, including imported skinning and scale corrections.
            bladeRenderer.BakeMesh(bladeSnapshot);
            bladeSnapshot.GetVertices(bladeVertices);
            if(bladeTipIndex<0)
            {
                float farthest=-1;
                for(int i=0;i<bladeVertices.Count;i++)
                {
                    float distance=(bladeRenderer.transform.TransformPoint(bladeVertices[i])-rigHand.position).sqrMagnitude;
                    if(distance<=farthest)continue;
                    farthest=distance;bladeTipIndex=i;
                }
            }
            if(bladeTipIndex>=0)trail.transform.position=bladeRenderer.transform.TransformPoint(bladeVertices[bladeTipIndex]);
        }

        private void OnEnable()
        {
            if (combo != null)
            {
                combo.AttackStarted += OnComboStarted;
                combo.AttackFinished += OnComboFinished;
            }
            if (parry != null)
            {
                parry.WindowStarted += OnParryStarted;
                parry.WindowClosed += OnParryClosed;
            }
            if (spin != null)
            {
                spin.AttackStarted += OnSpinStarted;
                spin.AttackFinished += OnSpinFinished;
            }
            if (lunge != null)
            {
                lunge.AttackStarted += OnLungeStarted;
                lunge.AttackFinished += OnLungeFinished;
            }
        }

        private void OnDisable()
        {
            if (combo != null)
            {
                combo.AttackStarted -= OnComboStarted;
                combo.AttackFinished -= OnComboFinished;
            }
            if (parry != null)
            {
                parry.WindowStarted -= OnParryStarted;
                parry.WindowClosed -= OnParryClosed;
            }
            if (spin != null)
            {
                spin.AttackStarted -= OnSpinStarted;
                spin.AttackFinished -= OnSpinFinished;
            }
            if (lunge != null)
            {
                lunge.AttackStarted -= OnLungeStarted;
                lunge.AttackFinished -= OnLungeFinished;
            }
            ResetPose();
        }

        private void LateUpdate()
        {
            if (swordVisual == null) return;
            var equipment = GetComponent<Equipment.EquipmentLoadout>();
            if (equipment != null && equipment.ActiveDefinition != null && equipment.ActiveDefinition.poseProfile != null)
            {
                // Authored profiles own the attachment and body pose; legacy procedural
                // offsets and the embedded sword mesh must not drive this weapon.
                var profile = equipment.ActiveDefinition.poseProfile;
                var presentation = GetComponent<Equipment.WeaponPresentation>();
                if (trail != null)
                {
                    var visual = presentation != null ? presentation.ActiveVisual : null;
                    if(lastTrailVisual!=visual){trail.Clear();lastTrailVisual=visual;}
                    trail.time=Mathf.Max(.01f,profile.trailDuration);
                    trail.startWidth=Mathf.Max(0,profile.trailWidth);trail.endWidth=0;
                    trail.startColor=profile.trailStartColor;trail.endColor=profile.trailEndColor;
                    trail.sharedMaterial=profile.trailMaterial!=null?profile.trailMaterial:trailMaterial;
                    if (visual != null) trail.transform.position = visual.TransformPoint(profile.trailTip);
                    var cast = equipment.Runner != null ? equipment.Runner.Current : null;
                    trail.emitting = profile.meleeTrail && visual != null &&
                        ((hitbox != null && hitbox.IsWindowOpen) || (spin != null && spin.IsActive) || (lunge != null && lunge.IsActive) ||
                        (cast != null && cast.Began && !cast.Ended && (cast.Definition.pose == Equipment.AbilityPose.Lunge || cast.Definition.pose == Equipment.AbilityPose.Spin)));
                    if (!profile.meleeTrail || visual==null) trail.Clear();
                }
                return;
            }
            if (equipment != null && equipment.ActiveDefinition != null && equipment.ActiveDefinition.isBow)
            { if (trail != null) { trail.emitting = false; trail.Clear(); } return; }
            if(trail!=null)FollowBladeTip();
            if (useAuthoredAnimations || (equipment != null && equipment.ActiveDefinition != null && equipment.ActiveDefinition.family != null))
            {
                if(trail != null) trail.emitting = (hitbox != null && hitbox.IsWindowOpen) || (spin != null && spin.IsActive) || (lunge != null && lunge.IsActive);
                var cast = equipment != null && equipment.Runner != null ? equipment.Runner.Current : null;
                if (trail != null && cast != null && cast.Began && !cast.Ended &&
                    (cast.Definition.pose == Equipment.AbilityPose.Lunge || cast.Definition.pose == Equipment.AbilityPose.Spin)) trail.emitting = true;
                return;
            }
            if (motion == Motion.None) { SetPose(basePosition, baseRotation); return; }
            elapsed += Time.deltaTime;
            float normalized = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            ApplyPose(normalized);
            if(trail != null) trail.emitting = (hitbox != null && hitbox.IsWindowOpen) || (spin != null && spin.IsActive) || (lunge != null && lunge.IsActive);
            if (elapsed >= duration) ResetPose();
        }

        private void OnComboStarted(int step, string id)
        {
            comboStep = step;
            Begin(step >= 2 ? Motion.HeavySlash : Motion.Slash,
                step >= 2 ? heavySlashDuration : slashDuration, nextPriority: 10);
        }

        private void OnComboFinished(int step, string id)
        {
            if (priority <= 10) ResetPose();
        }

        private void OnParryStarted(float window) => Begin(Motion.Parry, Mathf.Max(0.12f, window), nextPriority: 30);
        private void OnParryClosed() { if (priority <= 30) ResetPose(); }
        private void OnSpinStarted() => Begin(Motion.Spin, spinDuration, nextPriority: 20);
        private void OnSpinFinished() { if (priority <= 20) ResetPose(); }
        private void OnLungeStarted() => Begin(Motion.Lunge, lungeDuration, nextPriority: 20);
        private void OnLungeFinished() { if (priority <= 20) ResetPose(); }

        private void Begin(Motion next, float nextDuration, int nextPriority)
        {
            if (nextPriority < priority && motion != Motion.None) return;
            motion = next;
            duration = Mathf.Max(0.01f, nextDuration);
            elapsed = 0f;
            priority = nextPriority;
        }

        private void ApplyPose(float normalized)
        {
            if(motion == Motion.Slash || motion == Motion.HeavySlash)
            {
                var window=hitbox != null ? hitbox.GetWindow(comboStep) : null;
                float start=window != null ? window.ActiveStart : .06f;
                float active=window != null ? window.ActiveDuration : .14f;
                float t=combo != null ? combo.CurrentStepElapsed : elapsed;
                float sweep=Mathf.Clamp01((t-start)/active);
                float direction=comboStep==1 ? -1f : 1f;
                Vector3 pose=comboStep>=2 ? Vector3.Lerp(new Vector3(.1f,1.65f,.1f),new Vector3(.1f,.7f,.65f),sweep) : new Vector3(Mathf.Lerp(-.38f*direction,.38f*direction,sweep),1.02f,.25f);
                Quaternion aim=comboStep>=2 ? Quaternion.Euler(Mathf.Lerp(-100,35,sweep),0,0) : Quaternion.Euler(-8,Mathf.Lerp(-75*direction,75*direction,sweep),-12*direction);
                float blend=t<start ? Mathf.Clamp01(t/Mathf.Max(.01f,start)) : 1-Mathf.Clamp01((t-start-active)/.18f);
                SetPose(Vector3.Lerp(basePosition,pose,blend), Quaternion.Slerp(baseRotation,aim,blend));
                return;
            }
            float smooth = Mathf.SmoothStep(0f, 1f, normalized);
            Vector3 offset = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            switch (motion)
            {
                case Motion.Slash:
                    float slashYaw = Mathf.Lerp(comboStep == 0 ? -58f : 58f, comboStep == 0 ? 58f : -58f, smooth);
                    float slashPitch = Mathf.Sin(smooth * Mathf.PI) * -14f;
                    rotation = Quaternion.Euler(slashPitch, slashYaw, 0f);
                    offset = Vector3.Lerp(new Vector3(comboStep == 0 ? -0.12f : 0.12f, 0f, -0.03f),
                        new Vector3(comboStep == 0 ? 0.12f : -0.12f, 0.02f, 0.08f), smooth);
                    break;
                case Motion.HeavySlash:
                    float heavyYaw = Mathf.Lerp(-82f, 92f, smooth);
                    float heavyPitch = Mathf.Lerp(22f, -24f, smooth);
                    rotation = Quaternion.Euler(heavyPitch, heavyYaw, 0f);
                    offset = Vector3.Lerp(new Vector3(-0.16f, -0.08f, -0.16f), new Vector3(0.16f, 0.06f, 0.16f), smooth);
                    break;
                case Motion.Parry:
                    float parryPulse = Mathf.Sin(smooth * Mathf.PI);
                    rotation = Quaternion.Euler(-28f - parryPulse * 16f, -22f, 0f);
                    offset = new Vector3(0f, 0.08f + parryPulse * 0.04f, 0.06f);
                    break;
                case Motion.Spin:
                    rotation = Quaternion.Euler(0f, Mathf.Lerp(-35f, 325f, smooth), 0f);
                    offset = new Vector3(0f, 0.03f, 0.12f);
                    break;
                case Motion.Lunge:
                    rotation = Quaternion.Euler(-10f, 8f, 0f);
                    offset = new Vector3(0f, 0f, Mathf.Sin(smooth * Mathf.PI) * 0.34f);
                    break;
            }

            SetPose(basePosition + offset, rotation);
        }

        private void ResetPose()
        {
            if(trail != null) {trail.emitting=false;trail.Clear();}
            motion = Motion.None;
            elapsed = 0f;
            duration = 0f;
            priority = 0;
            if (swordVisual != null)
            {
                SetPose(basePosition, baseRotation);
            }
        }

        private void SetPose(Vector3 position, Quaternion rotation)
        {
            if (useAuthoredAnimations) return;
            Vector3 facing = Vector3.ProjectOnPlane(motor != null ? motor.Facing : transform.forward, Vector3.up);
            Quaternion frame = Quaternion.LookRotation(facing.sqrMagnitude > .001f ? facing : Vector3.forward, Vector3.up);
            if (rigHand != null)
            {
                // Animator supplies the base pose each frame; attacks offset the hand so
                // the skinned sword and arm remain connected instead of detaching the mesh.
                if (motion == Motion.None) return;
                Transform shoulder = rigHand.parent != null ? rigHand.parent.parent : null;
                if (shoulder == null) shoulder = rigHand;
                Quaternion offset = frame * rotation * Quaternion.Inverse(baseRotation) * Quaternion.Inverse(frame);
                shoulder.rotation = offset * shoulder.rotation;
                return;
            }
            swordVisual.SetPositionAndRotation(transform.position + frame * position, frame * rotation);
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == childName) return child;
            return null;
        }
        private void OnDestroy(){if(trailMaterial != null)Destroy(trailMaterial);if(bladeSnapshot!=null)Destroy(bladeSnapshot);}
    }
}
