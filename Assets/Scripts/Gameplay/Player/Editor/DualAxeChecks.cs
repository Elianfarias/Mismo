using System;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
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
            Require(family.Skill(0).actions.OfType<MoveCasterAction>().Any(m=>Mathf.Approximately(m.distance,3)),"Hachazo doble leaps 3 m");
            Require(leap!=null&&leap.hits.Length==1&&leap.hits[0].at<1&&Mathf.Approximately(leap.damage*leap.hits[0].multiplier,basicDamage*2),"Hachazo doble lands one hit at +100 % of the basic");
            Require(family.Skill(1).actions.OfType<AxeThrowAction>().Any()&&Mathf.Approximately(family.Skill(1).range,6),"Lanzamiento de hacha throws up to 6 m");
            var spin=family.Skill(2).actions.OfType<FuriousComboAction>().SingleOrDefault();
            Require(spin!=null&&spin.hits.Length==2&&spin.hits.All(h=>Mathf.Approximately(spin.damage*h.multiplier,basicDamage*1.3f)),"Giro mortal hits once per axe at 130 % of the basic");
            var berserk=family.Skill(3).actions.OfType<BerserkAction>().SingleOrDefault();
            Require(berserk!=null&&Mathf.Approximately(berserk.duration,9)&&Mathf.Approximately(berserk.armorMultiplier,.45f),"Modo Berserker lasts 9 s and scales armor by 0.45");
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
            Debug.Log("DUAL_AXE_PASS");
        }
    }
}
