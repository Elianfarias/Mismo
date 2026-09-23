using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Feedback provisional del boss. Lee el estado, pero nunca decide ni aplica daño.</summary>
    [DisallowMultipleComponent]
    public sealed class BossPresentation : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private Transform healthFill;
        [SerializeField] private Transform billboard;
        [SerializeField] private TextMesh label;

        private BossController boss;
        private Health health;
        private Renderer[] skin;
        private MaterialPropertyBlock tint;
        private float flash;
        [SerializeField] private bool authoredAnimation;
        [SerializeField] private EnemyGroundSupport groundSupport=new EnemyGroundSupport();
        private Transform animatedModel;
        public void UseAuthoredAnimation() => authoredAnimation = true;

        public void Configure(Transform body, Transform sword, Transform fill, Transform board, TextMesh text)
        {
            visual = body;
            weaponPivot = sword;
            healthFill = fill;
            billboard = board;
            label = text;
        }

        public void HideLegacyStatus()
        {
            if (billboard != null) billboard.gameObject.SetActive(false);
            if (healthFill != null) healthFill.gameObject.SetActive(false);
            if (label != null) label.gameObject.SetActive(false);
        }

        private void Awake()
        {
            boss = GetComponent<BossController>();
            animatedModel=GetComponentInChildren<Animator>()?.transform;
            health = GetComponent<Health>();
            tint = new MaterialPropertyBlock();
            skin = visual != null ? visual.GetComponentsInChildren<Renderer>() : GetComponentsInChildren<Renderer>();
        }

        private void OnEnable()
        {
            if (health == null) health = GetComponent<Health>();
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo _) => flash = 0.12f;

        private void LateUpdate()
        {
            if (boss == null || health == null) return;
            float deltaTime = Time.deltaTime;
            flash = Mathf.Max(0f, flash - deltaTime);
            BossState currentState = boss.State;
            BossAttackDefinition currentAttack = boss.CurrentAttack;
            float progress = boss.StateProgress;
            bool charge = currentAttack != null && currentAttack.id == BossAttackId.StraightCharge;
            bool overhead = currentAttack != null && currentAttack.id == BossAttackId.OverheadSmash;

            float lean = 0f;
            float weaponAngle = 20f;
            float bob = 0f;
            if (currentState == BossState.Approach || currentState == BossState.Position || currentState == BossState.Return)
                bob = Mathf.Sin(Time.time * 11f) * 0.035f;
            if (currentState == BossState.Telegraph)
            {
                lean = charge ? -20f : overhead ? -4f : -10f;
                weaponAngle = charge ? 72f : overhead ? -150f : Mathf.Lerp(20f, -115f, progress);
            }
            if (currentState == BossState.Attack)
            {
                lean = charge ? 32f : overhead ? 18f : 14f;
                weaponAngle = charge ? 72f : Mathf.Lerp(-115f, 105f, progress);
            }
            if (currentState == BossState.Recovery)
            {
                lean = Mathf.Lerp(22f, 0f, progress);
                weaponAngle = Mathf.Lerp(105f, 20f, progress);
            }
            if (currentState == BossState.Stagger)
            {
                lean = -28f;
                bob = Mathf.Sin(Time.time * 40f) * 0.025f;
            }
            if (currentState == BossState.Dead) bob = 0.32f;

            if (visual != null)
            {
                visual.localPosition = authoredAnimation ? (currentState == BossState.Dead ? Vector3.up*.45f : Vector3.zero) : Vector3.up * bob;
                visual.localRotation = authoredAnimation ? Quaternion.Euler(0,0,currentState == BossState.Dead ? 90 : 0) : Quaternion.Euler(lean, 0f, currentState == BossState.Dead ? 85f : 0f);
                if(authoredAnimation&&currentState!=BossState.Dead&&animatedModel!=null)groundSupport.Apply(transform,visual,animatedModel.lossyScale.y);
            }
            if (!authoredAnimation && weaponPivot != null) weaponPivot.localRotation = Quaternion.Euler(weaponAngle, 0f, -15f);


            if (healthFill != null) healthFill.localScale = new Vector3(health.Normalized, 1f, 1f);
            if (billboard != null && Camera.main != null) billboard.rotation = Camera.main.transform.rotation;
            if (label != null) label.text = "BOSS";

        }
    }
}
