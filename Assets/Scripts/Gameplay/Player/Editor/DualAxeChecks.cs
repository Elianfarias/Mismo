using System;
using System.Linq;
using System.Reflection;
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
            int[] unlocks={1,1,1,3,6,9};
            for(int i=0;i<6;i++)Require(family.UnlockLevel(i)==unlocks[i],"Ability "+i+" unlocks at mastery "+unlocks[i]);

            var basic=family.GetAbility(AbilitySlot.Basic);
            Require(basic!=null&&basic.Id=="DualAxeBasic"&&!basic.IsPassive,"Basic is DualAxeBasic");
            for(int slot=1;slot<=3;slot++)Require(family.GetAbility((AbilitySlot)slot)==family.Skill(slot-1),"Initial slot "+(AbilitySlot)slot+" uses repertoire entry "+(slot-1));
            string[] ids={"DualAxeLeap","DualAxeThrow","DualAxeDeathSpin","DualAxeBerserk","DualAxeBloodthirst","DualAxeDoubleEdge"};
            for(int i=0;i<6;i++)Require(family.Skill(i)!=null&&family.Skill(i).Id==ids[i],"Repertoire "+i+" is "+ids[i]);
            Require(family.Skill(4).passive==WeaponPassive.Bloodthirst&&family.Skill(5).passive==WeaponPassive.DoubleEdge,"Passives map to their effects");
            Require(Enumerable.Range(0,4).All(i=>!family.Skill(i).IsPassive),"The four actives have no passive tag");

            // A compile error wipes [SerializeReference] data; these catch an emptied action list.
            var melee=basic.actions.OfType<MeleeAction>().SingleOrDefault();
            Require(melee!=null,"Basic keeps its melee action");
            var leap=family.Skill(0).actions.OfType<FuriousComboAction>().SingleOrDefault();
            Require(family.Skill(0).actions.OfType<MoveCasterAction>().Any(m=>Mathf.Approximately(m.distance,3)),"Hachazo doble leaps 3 m");
            Require(leap!=null&&leap.hits.Length==1&&leap.hits[0].at<1&&Mathf.Approximately(leap.damage*leap.hits[0].multiplier,melee.damage*2),"Hachazo doble lands one hit at +100 % of the basic");
            Require(family.Skill(1).actions.OfType<AxeThrowAction>().Any()&&Mathf.Approximately(family.Skill(1).range,6),"Lanzamiento de hacha throws up to 6 m");
            var spin=family.Skill(2).actions.OfType<FuriousComboAction>().SingleOrDefault();
            Require(spin!=null&&spin.hits.Length==2&&spin.hits.All(h=>Mathf.Approximately(spin.damage*h.multiplier,melee.damage*1.3f)),"Giro mortal hits once per axe at 130 % of the basic");
            var berserk=family.Skill(3).actions.OfType<BerserkAction>().SingleOrDefault();
            Require(berserk!=null&&Mathf.Approximately(berserk.duration,9)&&Mathf.Approximately(berserk.armorMultiplier,.45f),"Modo Berserker lasts 9 s and scales armor by 0.45");

            var animations=family.animations;
            Require(animations!=null,"Family has an animation set");
            foreach(var ability in family.repertoire.Append(basic).Where(a=>!a.IsPassive))
                Require(animations.actions.Any(b=>b.ability==ability&&b.clip!=null),ability.Id+" has an animation clip");
            Debug.Log("DUAL_AXE_PASS");
        }
    }
}
