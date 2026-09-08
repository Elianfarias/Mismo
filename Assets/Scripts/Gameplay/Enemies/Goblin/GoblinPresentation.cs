using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Feedback provisional legible sin depender de un Animator o assets externos.</summary>
    public sealed class GoblinPresentation : MonoBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private Transform healthFill;
        [SerializeField] private Transform billboard;
        [SerializeField] private TextMesh label;
        private GoblinController goblin;
        private GoblinVisualStyle style;
        private Health health;
        private Renderer[] skin;
        private MaterialPropertyBlock tint;
        private float flash;
        [SerializeField] private bool authoredAnimation;
        public void UseAuthoredAnimation() => authoredAnimation=true;

        public void Configure(Transform body, Transform sword, Transform fill, Transform board, TextMesh text)
        { visual = body; weaponPivot = sword; healthFill = fill; billboard = board; label = text; }

        public void HideLegacyStatus()
        {
            if (billboard != null) billboard.gameObject.SetActive(false);
            if (healthFill != null) healthFill.gameObject.SetActive(false);
            if (label != null) label.gameObject.SetActive(false);
        }

        private void Awake()
        {
            goblin = GetComponent<GoblinController>(); health = GetComponent<Health>();
            style = GetComponent<GoblinVisualStyle>();
            tint = new MaterialPropertyBlock();
            skin = visual != null ? visual.GetComponentsInChildren<Renderer>() : GetComponentsInChildren<Renderer>();
        }
        private void OnEnable() { if(health != null) health.Damaged += OnDamaged; }
        private void OnDisable() { if(health != null) health.Damaged -= OnDamaged; }
        private void OnDamaged(DamageInfo _) => flash = 0.12f;

        private void LateUpdate()
        {
            if(goblin == null || health == null || visual == null) return;
            float dt = Time.deltaTime;
            flash = Mathf.Max(0f, flash - dt);
            GoblinState state = goblin.State;
            bool charge = goblin.CurrentAttack != null && goblin.Settings != null && goblin.CurrentAttack == goblin.Settings.charge;
            float progress = goblin.StateProgress;
            float lean = 0f, swordAngle = 20f, bob = 0f;
            if (state == GoblinState.Chase || state == GoblinState.Position || state == GoblinState.Return)
                bob = Mathf.Sin(Time.time * 12f) * 0.035f;
            if (state == GoblinState.Telegraph) { lean = charge ? -18f : -8f; swordAngle = Mathf.Lerp(20f, -110f, progress); }
            if (state == GoblinState.Attack) { lean = charge ? 28f : 12f; swordAngle = Mathf.Lerp(-110f, 100f, progress); }
            if (state == GoblinState.Recovery) { lean = Mathf.Lerp(20f, 0f, progress); swordAngle = Mathf.Lerp(100f, 20f, progress); }
            if (state == GoblinState.Stagger) { lean = -25f; bob = Mathf.Sin(Time.time * 40f) * 0.025f; }
            if (state == GoblinState.Dead) bob = 0.35f;
            if(!authoredAnimation)
            {
                visual.localPosition = Vector3.up * bob;
                visual.localRotation = Quaternion.Euler(lean, 0f, state == GoblinState.Dead ? 85f : 0f);
                if(weaponPivot != null) weaponPivot.localRotation = Quaternion.Euler(swordAngle, 0f, -15f);
            }
            else
            {
                visual.localPosition=state==GoblinState.Dead?Vector3.up*.24f:Vector3.zero;
                visual.localRotation=state==GoblinState.Dead?Quaternion.Euler(0,0,90):Quaternion.identity;
            }
            
            if (healthFill != null) healthFill.localScale = new Vector3(health.Normalized, 1f, 1f);
            if (billboard != null && UnityEngine.Camera.main != null) billboard.rotation = UnityEngine.Camera.main.transform.rotation;
            if (label != null)
            {
                string prefix = style != null && !string.IsNullOrEmpty(style.LabelPrefix) ? style.LabelPrefix + " " : string.Empty;
                label.text = prefix + (state == GoblinState.Telegraph ? (charge ? "CARGA" : "GOLPE") :
                    state == GoblinState.Stagger ? "ATURDIDO" : state == GoblinState.Dead ? "DERROTADO" : "GOBLIN");
            }
            
        }
    }
}
