using System;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    // Data-level rules of the Dos hachas family; runs without Play mode.
    public static class DualAxeChecks
    {
        private static void Require(bool ok,string message) { if(!ok)throw new Exception(message); Debug.Log("DUAL_AXE_CHECK "+message); }
        [MenuItem("Mismo/Armas/Verificar Dos hachas")]
        public static void RunBatch()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Data/Inventory/ItemCatalog.asset");
            Require(catalog!=null,"Item catalog loads");
            var axe=Array.Find(catalog.weapons,w=>w!=null&&w.Id=="axe.basic");
            var sword=Array.Find(catalog.weapons,w=>w!=null&&w.Id=="sword.basic");
            var shield=Array.Find(catalog.weapons,w=>w!=null&&w.isShield);
            Require(axe!=null&&sword!=null&&shield!=null,"Axe, sword and shield exist in the catalog");

            var compatible=typeof(PlayerInventory).GetMethod("Compatible",BindingFlags.Static|BindingFlags.NonPublic);
            bool Pair(WeaponDefinition main,WeaponDefinition off)=>(bool)compatible.Invoke(null,new object[]{main,off});
            Require(Pair(axe,axe),"Axe accepts another axe in the off hand");
            Require(!Pair(sword,axe)&&!Pair(axe,sword),"Sword and axe do not mix");
            Require(!Pair(axe,shield),"Axe does not take a shield");
            Require(Pair(sword,sword)&&Pair(sword,shield),"Sword pairings are unchanged");

            var family=axe.dualAxeFamily;
            Require(family!=null&&family.progressionId=="axe.dual"&&family.DisplayName=="Dos hachas","Axe links the axe.dual family");
            Require(family!=axe.family,"Dos hachas keeps its own mastery apart from Hacha sola");
            Require(family.SkillCount==6,"Family has six selectable abilities");
            int[] unlocks={1,1,1,1,2,3};
            for(int i=0;i<6;i++)Require(family.UnlockLevel(i)==unlocks[i],"Ability "+i+" unlocks at mastery "+unlocks[i]);

            var basic=family.GetAbility(AbilitySlot.Basic);
            Require(basic!=null&&basic.Id=="DualAxeBasic"&&!basic.IsPassive,"Basic is DualAxeBasic");
            for(int slot=1;slot<=3;slot++)Require(family.GetAbility((AbilitySlot)slot)==family.Skill(slot-1),"Initial slot "+(AbilitySlot)slot+" uses repertoire entry "+(slot-1));
            string[] ids={"DualAxeLeap","DualAxeThrow","DualAxeDeathSpin","DualAxeBerserk","DualAxeDoubleEdge","DualAxeBloodthirst"};
            for(int i=0;i<6;i++)Require(family.Skill(i)!=null&&family.Skill(i).Id==ids[i],"Repertoire "+i+" is "+ids[i]);
            Require(family.Skill(4).passive==WeaponPassive.DoubleEdge&&family.Skill(5).passive==WeaponPassive.Bloodthirst,"Passives map to their effects");
            Require(Enumerable.Range(0,4).All(i=>!family.Skill(i).IsPassive),"The four actives have no passive tag");
            Require(family.Skill(4).actions==null||family.Skill(4).actions.Length==0,"Doble filo has no actions: it changes the basic chain");

            // A compile error wipes [SerializeReference] data and combo steps; these catch an emptied basic or action list.
            Require(basic.usesSwordCombo&&basic.comboOrder==ComboOrder.AlternateHands&&basic.comboSteps!=null&&basic.comboSteps.Length==4,"Basic alternates hands over four combo steps");
            var strikers=new[]{ComboStriker.MainHand,ComboStriker.OffHand,ComboStriker.EachHand,ComboStriker.EachHand};
            for(int i=0;i<4;i++)Require(basic.comboSteps[i]!=null&&basic.comboSteps[i].Striker==strikers[i]&&basic.comboSteps[i].Shape==ComboHitShape.Blade,"Basic step "+i+" strikes with "+strikers[i]);
            float basicDamage=basic.comboSteps[0].Damage;
            Require(basicDamage>0&&basic.comboSteps.All(s=>Mathf.Approximately(s.Damage,basicDamage)),"Every basic swing deals the same damage per axe");
            var leap=family.Skill(0).actions.OfType<FuriousComboAction>().SingleOrDefault();
            Require(family.Skill(0).actions.OfType<MoveCasterAction>().Any(m=>Mathf.Approximately(m.distance,9)),"Hachazo doble leaps 9 m");
            Require(leap!=null&&leap.hits.Length==1&&leap.hits[0].at<1&&Mathf.Approximately(leap.damage*leap.hits[0].multiplier,basicDamage*2),"Hachazo doble lands one hit at +100 % of the basic");
            Require(family.Skill(1).actions.OfType<AxeThrowAction>().Any()&&family.Skill(1).range>=1,"Lanzamiento de hacha throws an axe (its range is a balance value)");
            var spin=family.Skill(2).actions.OfType<FuriousComboAction>().SingleOrDefault();
            Require(spin!=null&&spin.hits.Length==2&&spin.hits.All(h=>Mathf.Approximately(spin.damage*h.multiplier,basicDamage*1.3f)),"Giro mortal hits once per axe at 130 % of the basic");
            var berserk=family.Skill(3).actions.OfType<BerserkAction>().SingleOrDefault();
            Require(berserk!=null&&berserk.duration>0&&Mathf.Approximately(berserk.armorMultiplier,.45f),"Modo Berserker has a duration (a balance value) and scales armor by 0.45");
            Require(Mathf.Approximately(berserk.lifeSteal,.15f),"Modo Berserker heals 15 % of basic damage");

            // Steps: 0 right, 1 left, 2 double led by the right, 3 double led by the left; -1 is idle.
            int Next(int previous,bool both,bool thrown=false)=>BasicSwordCombo.NextStep(ComboOrder.AlternateHands,previous,4,both,thrown);
            int[,] table={{-1,0,2},{0,1,2},{1,0,3},{2,0,3},{3,1,2}};
            for(int i=0;i<5;i++)
                Require(Next(table[i,0],false)==table[i,1]&&Next(table[i,0],true)==table[i,2],"After step "+table[i,0]+": single "+table[i,1]+", double "+table[i,2]);
            Require(Next(1,true,true)==0&&Next(0,false,true)==0,"A thrown axe forces a right-hand single");
            Require(BasicSwordCombo.NextStep(ComboOrder.Sequential,-1,2,true,false)==0&&BasicSwordCombo.NextStep(ComboOrder.Sequential,0,2,true,false)==1&&BasicSwordCombo.NextStep(ComboOrder.Sequential,1,2,false,false)==-1,"Sequential combos still advance one step and end");

            var animations=family.animations;
            Require(animations!=null,"Family has an animation set");
            foreach(var ability in family.repertoire.Where(a=>!a.IsPassive))
                Require(animations.actions.Any(b=>b.ability==ability&&b.clip!=null),ability.Id+" has an animation clip");
            var basicBinding=animations.Find(basic);
            Require(basicBinding!=null&&basicBinding.comboClips!=null&&basicBinding.comboClips.Length==4&&basicBinding.comboClips.All(c=>c!=null&&c.humanMotion),"Basic has four Humanoid combo clips");

            // Evolutions: the Inspector is the balance sheet, so these catch a wiped or reverted asset.
            // Every evolution works from rank 0 and each rank only improves its numbers: value = base + perRank × rank.
            void Modifiers(AbilityDefinition ability,params string[] expected)
            {
                var offered=ability.masteryModifiers.Where(m=>m!=null&&!m.retired).ToArray();
                Require(offered.Select(m=>m.id).SequenceEqual(expected)&&offered.All(m=>m.maxLevel==3&&m.effectiveUsesPerLevel==10),ability.Id+" offers exactly "+string.Join(", ",expected));
            }
            // Retired modifiers keep their id so a save that trained them stays valid.
            void Retired(AbilityDefinition ability,params string[] ids)=>
                Require(ids.All(id=>ability.FindModifier(id)?.retired==true),ability.Id+" keeps "+string.Join(", ",ids)+" retired");
            bool Close(float a,float b)=>Mathf.Abs(a-b)<2e-4f;
            void Evolution(AbilityDefinition ability,string id,AbilityModifierBehavior behavior,float rank0,float rank3)
            {
                var m=ability.FindModifier(id);
                Require(m!=null&&!m.retired&&m.behavior==behavior&&Close(m.Amount(0),rank0)&&Close(m.Amount(3),rank3),ability.Id+" "+id+" evolves "+behavior+" from "+rank0+" at rank 0 to "+rank3+" at rank 3");
            }
            Modifiers(basic,"burst");Evolution(basic,"burst",AbilityModifierBehavior.Burst,.4f,.4667f);
            var burst=basic.FindModifier("burst");
            Require(burst.hitsRequired==5&&Mathf.Approximately(burst.streakResetSeconds,3)&&burst.count==2&&Close(burst.interval,.12f),"Ráfaga needs 5 basics in a row, reset after 3 s, and adds 2 extra strikes");
            Modifiers(family.Skill(0),"landing_strike");Evolution(family.Skill(0),"landing_strike",AbilityModifierBehavior.LandingStrike,1,1.1667f);
            Modifiers(family.Skill(1),"wandering_axe");Evolution(family.Skill(1),"wandering_axe",AbilityModifierBehavior.WanderingAxe,.3f,.25f);
            var wandering=family.Skill(1).FindModifier("wandering_axe");
            // The distance is a balance value the Inspector changes freely: only its sanity is checked.
            Require(wandering.count==2&&wandering.distance>=1,"Hacha errante rebounds to at most 2 enemies, each hitting 30 % softer at rank 0");
            Modifiers(family.Skill(2),"whirlwind");Evolution(family.Skill(2),"whirlwind",AbilityModifierBehavior.Whirlwind,.15f,.175f);
            Require(family.Skill(2).FindModifier("whirlwind").maxRepeats==1,"Torbellino repeats the spin once");
            Modifiers(family.Skill(3),"slaughter","rising_fury");
            Evolution(family.Skill(3),"slaughter",AbilityModifierBehavior.Slaughter,.05f,.0583f);Evolution(family.Skill(3),"rising_fury",AbilityModifierBehavior.RisingFury,.015f,.0175f);
            Modifiers(family.Skill(4),"cross_cut");Retired(family.Skill(4),"power");Evolution(family.Skill(4),"cross_cut",AbilityModifierBehavior.CrossCut,.2f,.2333f);
            Modifiers(family.Skill(5),"insatiable");Retired(family.Skill(5),"power");Evolution(family.Skill(5),"insatiable",AbilityModifierBehavior.Insatiable,.05f,.0583f);
            Require(Close(family.Skill(5).FindModifier("insatiable").threshold,.5f),"Sed insaciable acts below 50 % of the life");
            Require(family.Skill(4).UsesPassiveValue&&Mathf.Approximately(family.Skill(4).passiveValue,.5f),"Doble filo is authored at 50 %");
            Require(family.Skill(5).UsesPassiveValue&&Mathf.Approximately(family.Skill(5).passiveValue,.2f),"Sed de sangre is authored at 20 %");

            // The Inspector drawer must resolve every field it shows: a typo in a name would hide that field silently.
            var unresolved=new System.Collections.Generic.List<string>();
            foreach(var ability in family.repertoire.Append(basic))
            {
                var serialized=new SerializedObject(ability);
                if(ability.UsesPassiveValue&&serialized.FindProperty("passiveValue")==null)unresolved.Add(ability.Id+".passiveValue");
                var list=serialized.FindProperty("masteryModifiers");
                for(int i=0;i<list.arraySize;i++)
                {
                    var element=list.GetArrayElementAtIndex(i);
                    foreach(var name in AbilityModifierDefinitionDrawer.FieldNames(element))
                        if(element.FindPropertyRelative(name)==null)unresolved.Add(ability.Id+"["+i+"]."+name);
                }
            }
            Require(unresolved.Count==0,"The modifier drawer resolves every field it shows"+(unresolved.Count>0?": "+string.Join(", ",unresolved):""));

            // Torbellino replays the active window of the spin clip: its start and end poses should match.
            SpinSeam(animations.Find(family.Skill(2)).clip,animations.Find(family.Skill(2)));
            Debug.Log("DUAL_AXE_PASS");
        }

        // Compares the clip at both ends of the replayed window: the muscles and the root orientation.
        static void SpinSeam(AnimationClip clip,AbilityAnimationBinding binding)
        {
            float a=binding.activeStartsAt*clip.length,b=binding.recoveryStartsAt*clip.length;
            float worst=0;string worstName=null;float[] q1=new float[4],q2=new float[4];
            foreach(var curveBinding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve=AnimationUtility.GetEditorCurve(clip,curveBinding);
                float v1=curve.Evaluate(a),v2=curve.Evaluate(b);
                string name=curveBinding.propertyName;
                if(name.StartsWith("RootQ."))
                {
                    int axis="xyzw".IndexOf(name[name.Length-1]);if(axis>=0){q1[axis]=v1;q2[axis]=v2;}continue;
                }
                if(name.StartsWith("RootT.")||name.StartsWith("MotionT.")||name.StartsWith("MotionQ."))continue;
                float diff=Mathf.Abs(v1-v2);if(diff>worst){worst=diff;worstName=name;}
            }
            float angle=Quaternion.Angle(new Quaternion(q1[0],q1[1],q1[2],q1[3]),new Quaternion(q2[0],q2[1],q2[2],q2[3]));
            Debug.Log("DUAL_AXE_SPIN_SEAM muscles worst="+worst.ToString("0.000")+" ("+worstName+") rootAngle="+angle.ToString("0.0")+" deg over "+(b-a).ToString("0.000")+" s");
            Require(worst<.35f&&angle<25,"The spin clip's active window starts and ends on a similar pose, so a repeat does not jump");
        }
    }
}
