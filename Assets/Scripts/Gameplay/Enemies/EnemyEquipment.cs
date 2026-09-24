using System;
using UnityEngine;
using Mismo.Gameplay.Player.Equipment;
namespace Mismo.Gameplay.Enemies
{
    [Serializable]
    public sealed class EnemyWeaponSlot
    {
        [Tooltip("Opcional: reutiliza el modelo de un arma del juego. El agarre se configura aquí para este enemigo.")]
        public WeaponDefinition weapon;
        [Tooltip("Opcional: modelo visual propio, tiene prioridad sobre el arma seleccionada.")]
        public GameObject visualPrefab;
        public WeaponAttachmentPose pose=new WeaponAttachmentPose();
        public GameObject Prefab=>visualPrefab!=null?visualPrefab:weapon!=null?weapon.visualPrefab:null;
    }

    /// <summary>Optional visual equipment; enemy AI remains the owner of attacks and damage.</summary>
    [DefaultExecutionOrder(250),DisallowMultipleComponent]
    public sealed class EnemyEquipment : MonoBehaviour
    {
        public Animator animator;
        [Tooltip("Opcional: controller compatible con los parámetros del enemigo. Vacío conserva el actual.")]
        public RuntimeAnimatorController controllerOverride;
        [Header("Animaciones base opcionales")]
        public AnimationClip idleClip,walkClip,runClip,hitClip;
        [Tooltip("Cuerpo completo, sincronizado con la duración de la postura rota. Vacío conserva la reacción actual.")]
        public AnimationClip postureBreakClip;
        [Tooltip("Vacío reutiliza Hit. Nunca utiliza el clip de postura rota.")]
        public AnimationClip parryClip;
        [Min(.01f)] public float parryDuration=.36f;
        float parryAt=float.NegativeInfinity;
        AnimationClip defaultParryClip;
        public AnimationClip ParryClip=>parryClip!=null?parryClip:hitClip!=null?hitClip:defaultParryClip;
        public float ParryProgress=>Mathf.Clamp01((Time.time-parryAt)/Mathf.Max(.01f,parryDuration));
        public bool PlayingParry=>ParryClip!=null&&health!=null&&!health.IsDead&&combat?.Broken!=true&&Time.time-parryAt<Mathf.Max(.01f,parryDuration);
        public void NotifyParried()
        {
            if(!isActiveAndEnabled||combat?.Broken==true||health==null||health.IsDead)return;
            if(parryClip==null&&hitClip==null&&defaultParryClip==null&&GetComponent<GoblinAnimationDriver>()!=null)
                defaultParryClip=Mismo.Core.ProjectAssets.Load<AnimationClip>("CombatPresentation/Human_Goblin_CombatDamage01");
            parryAt=Time.time;
        }
        [Min(.01f)] public float hitDuration=.36f;
        [Tooltip("Vacío: reacción de cuerpo completo.")] public AvatarMask hitMask;
        [Min(.1f)] public float walkReferenceSpeed=1.6f,runReferenceSpeed=3.2f;
        Mismo.Gameplay.Combat.Health health;
        Mismo.Gameplay.Combat.CombatState combat;
        float breakAt=float.NegativeInfinity,breakDuration=1;
        public float PostureBreakProgress=>Mathf.Clamp01((Time.time-breakAt)/Mathf.Max(.01f,breakDuration));
        public bool PlayingPostureBreak=>postureBreakClip!=null&&combat!=null&&combat.Broken&&health!=null&&!health.IsDead;
        void OnPostureBroken(float seconds){breakAt=Time.time;breakDuration=seconds;}
        float hitAt=float.NegativeInfinity;
        public float HitProgress=>(Time.time-hitAt)/Mathf.Max(.01f,hitDuration);
        public bool PlayingHit=>hitClip!=null&&health!=null&&!health.IsDead&&HitProgress<1;
        public void CancelHitReaction(){hitAt=float.NegativeInfinity;}
        void OnHit(Mismo.Gameplay.Combat.DamageInfo damage){hitAt=Time.time;}
        public EnemyWeaponSlot primary=new EnemyWeaponSlot();
        public EnemyWeaponSlot secondary=new EnemyWeaponSlot{pose=new WeaponAttachmentPose{anchor=WeaponAnchor.LeftHand}};
        [Tooltip("Rutas de renderers relativas al Animator. Permite ocultar armas integradas en el modelo.")]
        public string[] hiddenRendererPaths=Array.Empty<string>();
        GameObject primaryInstance,secondaryInstance;
        Renderer[] hidden=Array.Empty<Renderer>();bool[] wasEnabled=Array.Empty<bool>();
        RuntimeAnimatorController originalController,appliedController;
        Animator boundAnimator;
        public Transform PrimaryVisual=>primaryInstance!=null?primaryInstance.transform:null;
        public Transform SecondaryVisual=>secondaryInstance!=null?secondaryInstance.transform:null;
        public Animator ResolveAnimator()=>animator!=null?animator:GetComponentInChildren<Animator>(true);
        void OnEnable(){health=GetComponent<Mismo.Gameplay.Combat.Health>();if(health!=null)health.Damaged+=OnHit;combat=GetComponent<Mismo.Gameplay.Combat.CombatState>();if(combat!=null)combat.PostureBroken+=OnPostureBroken;Rebuild();}
        void LateUpdate(){ApplyPose();}
        public void Rebuild()
        {
            ClearInstances();RestoreHidden();RestoreController();
            boundAnimator=ResolveAnimator();
            if(boundAnimator!=null)
            {
                originalController=boundAnimator.runtimeAnimatorController;
                appliedController=controllerOverride;
                if(appliedController!=null)boundAnimator.runtimeAnimatorController=appliedController;
                var renderers=new System.Collections.Generic.List<Renderer>();
                if(hiddenRendererPaths!=null)foreach(var path in hiddenRendererPaths)
                {
                    var bone=string.IsNullOrEmpty(path)?boundAnimator.transform:boundAnimator.transform.Find(path);
                    if(bone!=null)foreach(var r in bone.GetComponents<Renderer>())if(!renderers.Contains(r))renderers.Add(r);
                }
                hidden=renderers.ToArray();wasEnabled=new bool[hidden.Length];
                for(int i=0;i<hidden.Length;i++){wasEnabled[i]=hidden[i].enabled;hidden[i].enabled=false;}
            }
            primaryInstance=CreateVisual(primary?.Prefab);secondaryInstance=CreateVisual(secondary?.Prefab);ApplyPose();
        }
        GameObject CreateVisual(GameObject prefab)
        {
            if(prefab==null)return null;
            var visual=Instantiate(prefab,transform);visual.name=prefab.name+" (equipped)";visual.hideFlags=HideFlags.DontSave;
            foreach(var collider in visual.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var body in visual.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;}
            foreach(var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))behaviour.enabled=false;
            return visual;
        }
        public void ApplyPose()
        {
            Apply(primaryInstance,primary);Apply(secondaryInstance,secondary);
        }
        void Apply(GameObject visual,EnemyWeaponSlot slot)
        {
            if(visual==null||slot==null||slot.pose==null)return;
            var rig=ResolveAnimator();var anchor=slot.pose.Resolve(transform,rig);
            visual.SetActive(anchor!=null);
            if(anchor!=null)slot.pose.Apply(visual.transform,anchor,rig);
        }
        void RestoreHidden(){for(int i=0;i<hidden.Length;i++)if(hidden[i]!=null)hidden[i].enabled=wasEnabled[i];hidden=Array.Empty<Renderer>();}
        void RestoreController(){if(boundAnimator!=null&&appliedController!=null&&boundAnimator.runtimeAnimatorController==appliedController)boundAnimator.runtimeAnimatorController=originalController;boundAnimator=null;appliedController=null;}
        void ClearInstances(){Dispose(primaryInstance);Dispose(secondaryInstance);primaryInstance=secondaryInstance=null;}
        static void Dispose(GameObject value){if(value==null)return;value.SetActive(false);if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        public void Release(){ClearInstances();RestoreHidden();RestoreController();}
        void OnDisable(){if(health!=null)health.Damaged-=OnHit;if(combat!=null)combat.PostureBroken-=OnPostureBroken;breakAt=parryAt=float.NegativeInfinity;hitAt=float.NegativeInfinity;Release();}
        void OnDestroy(){ClearInstances();RestoreHidden();RestoreController();}
    }
}
