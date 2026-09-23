using UnityEngine;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
namespace Mismo.Gameplay.Player.Presentation
{
    [DefaultExecutionOrder(300),DisallowMultipleComponent]
    public sealed class WeaponTrailPresentation:MonoBehaviour
    {
        EquipmentLoadout loadout;WeaponPresentation presentation;BasicSwordCombo combo;
        WeaponTrailRibbon main,second;Material material;
        long execution;int segment=-1;WeaponPoseProfile previous;
        void Awake(){loadout=GetComponent<EquipmentLoadout>();presentation=GetComponent<WeaponPresentation>();combo=GetComponentInChildren<BasicSwordCombo>();}
        void LateUpdate()
        {
            var weapon=loadout!=null?loadout.ActiveDefinition:null;var profile=weapon!=null?weapon.poseProfile:null;
            var health=GetComponent<Health>();
            if(profile==null||!profile.proceduralTrail||!profile.meleeTrail||weapon.isBow||health!=null&&health.IsDead||presentation==null)
            {main?.Clear();second?.Clear();previous=null;return;}
            if(GameplayPause.IsPaused)return;
            if(main==null){material=RuntimeParticleMaterial.Create("Melee ribbon",Color.white);main=new WeaponTrailRibbon(transform,material);second=new WeaponTrailRibbon(transform,material);}
            var cast=loadout.Runner!=null?loadout.Runner.Current:null;
            long id=cast!=null?cast.AttackId:0;int index=combo!=null?combo.CurrentStepIndex:-1;
            if(previous!=profile||cast!=null&&(execution!=id||segment!=index)){main.Clear();second.Clear();}
            previous=profile;if(cast!=null){execution=id;segment=index;}
            bool emit=false;
            if(cast!=null&&cast.Definition.usesSwordCombo&&combo!=null&&combo.IsActive&&combo.CurrentStep!=null)
            {float p=combo.CurrentStepNormalized;emit=p>=combo.CurrentStep.ImpactStart&&p<combo.CurrentStep.ImpactEnd;}
            else if(cast!=null)emit=cast.Began&&!cast.Ended;
            main.SampleBlade(presentation.ActiveVisual,profile,Time.time,emit);
            if(weapon.dualWield&&profile.secondaryTrail)second.SampleBlade(presentation.ActiveSecondVisual,profile,Time.time,emit,true);else second.Clear();
        }
        void OnDisable(){main?.Clear();second?.Clear();}
        void OnDestroy(){main?.Dispose();second?.Dispose();if(material!=null)Destroy(material);}
    }
}
