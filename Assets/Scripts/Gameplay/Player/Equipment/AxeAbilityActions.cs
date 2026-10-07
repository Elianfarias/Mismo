using System;
using Mismo.Gameplay.Combat;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    // Solo activa el buff de "próximo golpe primado"; el sangrado lo aplica el propio básico (ver WeaponSkillEffects.BasicHit).
    [Serializable]
    public sealed class PrimeBleedAction : AbilityAction
    {
        [InspectorName("Daño de sangrado por segundo"),Tooltip("Antes de multiplicar por el nivel del arma y la Potencia de la habilidad. Filo oxidado lo aumenta por rango.")]
        [Min(0)] public float damagePerSecond=3;
        [InspectorName("Duración del sangrado (segundos)"),Min(0)] public float bleedSeconds=5;
        [InspectorName("Duración de la marca (segundos)"),Tooltip("Tiempo para pegar el básico que aplica el sangrado.")]
        [Min(.1f)] public float markSeconds=4;
        public override void Begin(AbilityExecution c)=>c.Owner.GetComponent<WeaponSkillEffects>()?.PrimeBleed(c,damagePerSecond,bleedSeconds,markSeconds);
    }

    [Serializable]
    public sealed class ArmorRendStrikeAction : AbilityAction
    {
        public float damage=14,postureDamage=-1,radius=.6f,forward=.8f;
        [Tooltip("Multiplicador de armadura mientras dura el debuff (0.8 = -20%). No acumulable.")]
        public float armorMultiplier=.8f,armorDuration=4;
        public override void Tick(AbilityExecution c,float dt)
        {
            Vector3 origin=c.Owner.transform.position+Vector3.up+c.Direction*forward;
            foreach(var other in Physics.OverlapSphere(origin,radius,~0,QueryTriggerInteraction.Ignore))
            {
                var receiver=other.GetComponentInParent<IDamageReceiver>();
                if(!(receiver is Component target)||target.transform.root==c.Owner.transform.root||!c.HitTargets.Add(target))continue;
                Vector3 point=other.ClosestPoint(origin);
                if(Physics.Linecast(origin,point,out var wall,~0,QueryTriggerInteraction.Ignore)&&wall.transform.root!=c.Owner.transform.root&&wall.collider.GetComponentInParent<IDamageReceiver>()!=receiver)continue;
                if(!receiver.ReceiveDamage(new DamageInfo(damage*c.DamageMultiplier,c.Owner,point,(target.transform.position-c.Owner.transform.position).normalized,c.AttackId,postureDamage,weaponFamilyId:c.WeaponFamilyId,focusGainOnHit:c.Definition.focusGainOnHit,abilityId:c.Definition.Id,abilityUseId:c.AttackId)))continue;
                var evolution=c.Modifier;float multiplier=armorMultiplier;
                // Desgarro profundo: reduce la armadura más que la habilidad (Amount es la reducción total), sin alargar el debuff.
                if(evolution?.behavior==AbilityModifierBehavior.DeepRend)multiplier=Mathf.Clamp01(1-evolution.Amount(c.ModifierRank));
                CombatAilment.WeakenArmor(target.gameObject,multiplier,armorDuration);
                if(evolution?.behavior==AbilityModifierBehavior.RawFlesh)
                    CombatAilment.Poison(target.gameObject,c.Owner,c.WeaponFamilyId,evolution.BleedDamage(c.ModifierRank)*c.DamageMultiplier,evolution.BleedSeconds(c.ModifierRank),c.Definition.Id,c.AttackId);
            }
        }
    }

    [Serializable]
    public struct ComboHit
    {
        [Tooltip("Momento del golpe dentro de la fase activa (0 = inicio, 1 = final).")]
        [Range(0,1)] public float at;
        [Tooltip("Multiplicador del daño base de la acción para este golpe.")]
        public float multiplier;
    }

    // Como RepeatedStrikeAction pero con los golpes en momentos explícitos y con multiplicador propio;
    // el aturdimiento sólo se aplica al enemigo que recibió todos los golpes del combo.
    [Serializable]
    public sealed class FuriousComboAction : AbilityAction
    {
        public float damage=15,postureDamage=-1,radius=.7f,forward=1,stunSeconds=.8f;
        public ComboHit[] hits={new ComboHit{at=.105f,multiplier=1},new ComboHit{at=.559f,multiplier=1.2f},new ComboHit{at=.923f,multiplier=1.5f}};
        // Estado por ejecución: Unity no serializa Dictionary, así que no ensucia el asset.
        readonly System.Collections.Generic.Dictionary<Component,int> landed=new System.Collections.Generic.Dictionary<Component,int>();
        public override void Begin(AbilityExecution c){c.ActionTimes[this]=0;landed.Clear();}
        public override void Tick(AbilityExecution c,float dt)
        {
            float previous=c.ActionTimes[this],next=previous+dt;c.ActionTimes[this]=next;
            if(hits==null)return;
            for(int i=0;i<hits.Length;i++)
            {
                float at=hits[i].at*c.Definition.active;
                if(previous<at&&at<=next)Strike(c,hits[i].multiplier,i==hits.Length-1);
            }
        }
        void Strike(AbilityExecution c,float multiplier,bool last)
        {
            bool landedAny=false;
            long id=AttackIdentity.Next();var seen=new System.Collections.Generic.HashSet<Component>();
            var origin=c.Owner.transform.position+Vector3.up+c.Direction*forward;
            foreach(var collider in Physics.OverlapSphere(origin,radius,~0,QueryTriggerInteraction.Ignore))
            {
                if(!(collider.GetComponentInParent<IDamageReceiver>() is Component target)||target.transform.IsChildOf(c.Owner.transform)||!seen.Add(target))continue;
                Vector3 point=collider.ClosestPoint(origin);
                if(Physics.Linecast(origin,point,out var wall,~0,QueryTriggerInteraction.Ignore)&&!wall.transform.IsChildOf(c.Owner.transform)&&wall.collider.GetComponentInParent<IDamageReceiver>()!=(target as IDamageReceiver))continue;
                if(!((IDamageReceiver)target).ReceiveDamage(new DamageInfo(damage*multiplier*c.DamageMultiplier,c.Owner,point,c.Direction,id,postureDamage,weaponFamilyId:c.WeaponFamilyId,focusGainOnHit:c.Definition.focusGainOnHit,abilityId:c.Definition.Id,abilityUseId:c.AttackId)))continue;
                landedAny=true;
                landed.TryGetValue(target,out int count);
                landed[target]=++count;
                if(stunSeconds>0&&count>=hits.Length)target.GetComponent<CombatState>()?.Stagger(stunSeconds);
            }
            // Remate: the landing connected, so a basic follows at once (the runner starts it after this tick).
            if(last&&landedAny&&c.Modifier?.behavior==AbilityModifierBehavior.LandingStrike)c.Chain=true;
        }
    }

    [Serializable]
    public struct RecastStrike
    {
        [Tooltip("Momento del golpe dentro de la fase activa de su pulsación (0 = inicio, 1 = final).")]
        [Range(0,1)] public float at;
        [Tooltip("Multiplicador del daño base de la acción para este golpe.")]
        public float multiplier;
        [Tooltip("Segundos de postura rota para cada enemigo que alcanza este golpe. 0 = no la rompe.")]
        [Min(0)] public float breakPostureSeconds;
    }

    // Como FuriousComboAction, pero para habilidades con reactivación: cada pulsación ejecuta sólo el golpe de su etapa.
    [Serializable]
    public sealed class RecastStrikeAction : AbilityAction
    {
        public float damage=14,postureDamage=-1,radius=.7f,forward=1;
        [Tooltip("Un golpe por etapa de reactivación, en el mismo orden que las etapas de la habilidad.")]
        public RecastStrike[] strikes={new RecastStrike{at=.5f,multiplier=1},new RecastStrike{at=.5f,multiplier=1},new RecastStrike{at=.5f,multiplier=1,breakPostureSeconds=1}};
        public override void Begin(AbilityExecution c)=>c.ActionTimes[this]=0;
        public override void Tick(AbilityExecution c,float dt)
        {
            // -1 marks the strike as done; a fired flag still lands at = 0 and at = 1 despite accumulated float error.
            if(strikes==null||strikes.Length==0||!c.ActionTimes.TryGetValue(this,out float elapsed)||elapsed<0)return;
            var strike=strikes[Mathf.Clamp(c.RecastStage,0,strikes.Length-1)];elapsed+=dt;
            if(elapsed<strike.at*c.Active-.0001f){c.ActionTimes[this]=elapsed;return;}
            c.ActionTimes[this]=-1;Strike(c,strike);
        }
        void Strike(AbilityExecution c,RecastStrike strike)
        {
            long id=AttackIdentity.Next();var seen=new System.Collections.Generic.HashSet<Component>();
            var origin=c.Owner.transform.position+Vector3.up+c.Direction*forward;
            var evolution=c.Modifier;
            // Escalada: each earlier strike of this chain that landed adds Amount to this one, on top of its base multiplier.
            float multiplier=strike.multiplier;
            if(evolution?.behavior==AbilityModifierBehavior.Escalation&&c.Progress!=null)multiplier+=evolution.Amount(c.ModifierRank)*c.Progress.landed;
            // Quebranto lengthens the posture break only where the strike already breaks it.
            float breakSeconds=strike.breakPostureSeconds;
            if(breakSeconds>0&&evolution?.behavior==AbilityModifierBehavior.Shatter)breakSeconds+=evolution.Amount(c.ModifierRank);
            bool landedAny=false;
            foreach(var collider in Physics.OverlapSphere(origin,radius,~0,QueryTriggerInteraction.Ignore))
            {
                if(!(collider.GetComponentInParent<IDamageReceiver>() is Component target)||target.transform.IsChildOf(c.Owner.transform)||!seen.Add(target))continue;
                Vector3 point=collider.ClosestPoint(origin);
                if(Physics.Linecast(origin,point,out var wall,~0,QueryTriggerInteraction.Ignore)&&!wall.transform.IsChildOf(c.Owner.transform)&&wall.collider.GetComponentInParent<IDamageReceiver>()!=(target as IDamageReceiver))continue;
                if(!((IDamageReceiver)target).ReceiveDamage(new DamageInfo(damage*multiplier*c.DamageMultiplier,c.Owner,point,c.Direction,id,postureDamage,weaponFamilyId:c.WeaponFamilyId,focusGainOnHit:c.Definition.focusGainOnHit,abilityId:c.Definition.Id,abilityUseId:c.UseId)))continue;
                landedAny=true;
                var state=breakSeconds>0?target.GetComponent<CombatState>():null;
                if(state==null)continue;
                bool opens=!state.Broken;state.BreakPosture(breakSeconds);
                // A hit that already broke posture got its cue from the receiver; this marks the break without another hit stop.
                var cue=c.Weapon!=null&&c.Weapon.FeedbackProfile!=null?c.Weapon.FeedbackProfile.postureBreak:null;
                if(opens&&state.Broken&&cue!=null&&Application.isPlaying)Presentation.CombatImpactPool.Instance.Play(cue,target.transform.position+Vector3.up,c.Direction);
            }
            // One landed strike counts once, however many targets it reached.
            if(landedAny&&c.Progress!=null)c.Progress.landed++;
        }
    }

    // Lanza la pieza de la mano secundaria; vuelve a la mano al instante al impactar o al agotar el alcance (Range de la habilidad).
    [Serializable]
    public sealed class AxeThrowAction : AbilityAction
    {
        public float damage=14,postureDamage=-1,speed=20,radius=.25f;
        [Tooltip("Vueltas por segundo del hacha en vuelo. 0 = sin giro.")]
        public float spinsPerSecond=3;
        public override void Begin(AbilityExecution c)
        {
            var effects=c.Owner.GetComponent<WeaponSkillEffects>();
            Vector3 origin=c.Owner.transform.position+Vector3.up*1.2f;
            Vector3 direction=c.AimPoint.HasValue?(c.AimPoint.Value-origin).normalized:c.Direction;
            var projectile=ProjectileInstance.Spawn(c.Owner,origin,direction,damage*c.DamageMultiplier,speed,c.Definition.range,radius,c.Weapon.SecondaryVisualPrefab,c.AttackId,postureDamage,c.WeaponFamilyId,c.Definition.focusGainOnHit);
            projectile.AbilityId=c.Definition.Id;projectile.AbilityUseId=c.AttackId;
            // Weapon prefabs carry colliders; the projectile would hit its own visual.
            foreach(var collider in projectile.GetComponentsInChildren<Collider>())collider.enabled=false;
            if(spinsPerSecond>0)projectile.gameObject.AddComponent<SpinningVisual>().turnsPerSecond=spinsPerSecond;
            effects?.ThrowOffhand(c.Definition.range/Mathf.Max(.1f,speed)+.5f);
            projectile.OnImpact=(target,point)=>
            {
                effects?.ReturnOffhand();
                // Hacha errante: the axe that connected jumps on to up to `count` other enemies, each rebound hitting
                // softer than the last; the rank only trims that loss.
                var evolution=c.Modifier;
                if(target!=null&&evolution?.behavior==AbilityModifierBehavior.WanderingAxe)
                    Rebound(c,evolution,target,point,evolution.count,new System.Collections.Generic.HashSet<Component>(),1);
            };
        }
        // Each rebound flies to the nearest enemy not hit yet; its damage lands on arrival, then it may jump again.
        // `carried` is the share of the throw's damage the previous hit kept.
        void Rebound(AbilityExecution c,AbilityModifierDefinition evolution,Component from,Vector3 point,int remaining,System.Collections.Generic.HashSet<Component> hit,float carried)
        {
            hit.Add(from);
            Component next=null;float best=float.MaxValue;
            foreach(var collider in Physics.OverlapSphere(point,Mathf.Max(.5f,evolution.distance),~0,QueryTriggerInteraction.Ignore))
            {
                if(!(collider.GetComponentInParent<IDamageReceiver>() is Component candidate)||hit.Contains(candidate)||candidate.transform.IsChildOf(c.Owner.transform))continue;
                if(candidate.GetComponent<Health>()?.IsDead==true)continue;
                float distance=Vector3.Distance(point,candidate.transform.position);
                if(distance<best){best=distance;next=candidate;}
            }
            if(next==null)return;
            Vector3 target=next.transform.position+Vector3.up;
            float kept=carried*(1-Mathf.Clamp01(evolution.Amount(c.ModifierRank)));
            float amount=damage*kept*c.DamageMultiplier;
            ReboundFlight.Launch(c.Weapon.SecondaryVisualPrefab,point,target,speed,spinsPerSecond,()=>
            {
                if(next==null)return;
                var direction=(target-point).normalized;
                ((IDamageReceiver)next).ReceiveDamage(new DamageInfo(amount,c.Owner,target,direction,AttackIdentity.Next(),postureDamage,weaponFamilyId:c.WeaponFamilyId,abilityId:c.Definition.Id,abilityUseId:c.AttackId));
                if(remaining>1)Rebound(c,evolution,next,target,remaining-1,hit,kept);
            });
        }
    }

    // Visual of an axe flying between two points; runs a callback on arrival. It carries no colliders and no gameplay.
    public sealed class ReboundFlight : MonoBehaviour
    {
        Vector3 from,to;float duration,age,turns;System.Action arrive;
        public static void Launch(GameObject prefab,Vector3 from,Vector3 to,float speed,float turnsPerSecond,System.Action arrive)
        {
            var go=prefab!=null?Instantiate(prefab):new GameObject("Axe rebound");
            foreach(var collider in go.GetComponentsInChildren<Collider>())collider.enabled=false;
            var flight=go.AddComponent<ReboundFlight>();
            flight.from=from;flight.to=to;flight.turns=turnsPerSecond;flight.arrive=arrive;
            flight.duration=Mathf.Max(.05f,Vector3.Distance(from,to)/Mathf.Max(.1f,speed));
            go.transform.position=from;
        }
        void Update()
        {
            age+=Time.deltaTime;
            transform.position=Vector3.Lerp(from,to,Mathf.Clamp01(age/duration));
            transform.Rotate(Vector3.right,360*turns*Time.deltaTime,Space.Self);
            if(age>=duration){var done=arrive;arrive=null;Destroy(gameObject);done?.Invoke();}
        }
    }

    public sealed class SpinningVisual : MonoBehaviour
    {
        public float turnsPerSecond=3;
        void Update()=>transform.Rotate(Vector3.right,360*turnsPerSecond*Time.deltaTime,Space.Self);
    }

    // Bonificación temporal del jugador; el estado vive en WeaponSkillEffects y se limpia al morir o al cambiar de equipo.
    [Serializable]
    public sealed class BerserkAction : AbilityAction
    {
        public float duration=9;
        [Tooltip("Daño de ataque adicional (0.11 = +11 %).")]
        public float damageBonus=.11f;
        [Tooltip("Velocidad de ataque adicional (0.22 = +22 %). Respeta el tope de las reglas de progresión.")]
        public float attackSpeedBonus=.22f;
        [Tooltip("Focus adicional por cada básico acertado.")]
        public float focusPerBasic=1;
        [Tooltip("Multiplica todo gasto de stamina (0.34 = 66 % menos).")]
        public float staminaCostMultiplier=.34f;
        [Tooltip("Multiplica la armadura positiva mientras dura (0.45 = -55 %).")]
        public float armorMultiplier=.45f;
        [Tooltip("Fracción del daño de los básicos que cura mientras dura (0.15 = 15 %). Habilidades y sangrado no curan.")]
        [Range(0,1)] public float lifeSteal=.15f;
        public override void Begin(AbilityExecution c)=>c.Owner.GetComponent<WeaponSkillEffects>()?.Berserk(this,c);
    }
}
