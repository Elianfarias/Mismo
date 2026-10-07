using System;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    // Data-level rules of the Hacha sola family; runs without Play mode.
    public static class OneHandAxeChecks
    {
        private static void Require(bool ok,string message) { if(!ok)throw new Exception(message); Debug.Log("ONE_HAND_AXE_CHECK "+message); }
        [MenuItem("Mismo/Armas/Verificar Hacha sola")]
        public static void RunBatch()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Data/Inventory/ItemCatalog.asset");
            Require(catalog!=null,"Item catalog loads");
            var axe=Array.Find(catalog.weapons,w=>w!=null&&w.Id=="axe.basic");
            Require(axe!=null&&axe.family!=null&&axe.family.progressionId=="axe.onehand","Axe uses the axe.onehand family");
            var family=axe.family;
            var combo=Enumerable.Range(0,family.SkillCount).Select(family.Skill).FirstOrDefault(a=>a!=null&&a.Id=="AxeFuriousCombo");
            Require(combo!=null&&AssetDatabase.GetAssetPath(combo)==OneHandAxeAttackAuthoring.AbilityPath,"Combo furioso stays in the repertoire with its saved id");
            Require(!combo.usesSwordCombo&&!combo.chargeable&&combo.RecastCount==3&&Mathf.Approximately(combo.recastWindow,4),"Combo furioso is pressed three times with a 4 s window");
            Require(Mathf.Approximately(combo.cooldown,7.1f)&&combo.focusCost==0&&Mathf.Approximately(combo.staminaCost,20)&&combo.unstoppable,"Cooldown 7.1 s, no Focus, 20 stamina, unstoppable");

            // A compile error wipes [SerializeReference] data; these catch an emptied or reverted action list.
            Require(!combo.actions.OfType<FuriousComboAction>().Any(),"The single-press FuriousComboAction is gone");
            Require(!combo.actions.OfType<MoveCasterAction>().Any(),"Every press strikes in place, like the other abilities");
            var strike=combo.actions.OfType<RecastStrikeAction>().SingleOrDefault();
            Require(strike!=null&&strike.strikes!=null&&strike.strikes.Length==3,"One strike per press");
            Require(strike.strikes.All(s=>Mathf.Abs(strike.damage*s.multiplier-18.9f)<.001f),"Every strike deals 135 % of 14");
            Require(strike.strikes[0].breakPostureSeconds==0&&strike.strikes[1].breakPostureSeconds==0&&Mathf.Approximately(strike.strikes[2].breakPostureSeconds,1),"Only the third strike breaks posture, for 1 s");

            // The three presses split the old 3.18 s execution: every strike still lands where the clip hits.
            Require(Mathf.Abs(combo.recastStages.Sum(s=>s.Duration)-3.18f)<.002f,"The presses keep the original 3.18 s");
            float[] hits={.6637f,1.5445f,2.2506f};
            for(int i=0;i<3;i++)
            {
                var stage=combo.recastStages[i];
                float hit=OneHandAxeAttackAuthoring.StageStart(combo,i)+stage.preparation+strike.strikes[i].at*stage.active;
                Require(Mathf.Abs(hit-hits[i])<.002f,"Strike "+(i+1)+" lands at the original "+hits[i]+" s");
            }

            var binding=family.animations!=null?family.animations.Find(combo):null;
            Require(binding!=null&&binding.comboClips!=null&&binding.comboClips.Length==3,"The binding has one combo clip per press");
            for(int i=0;i<3;i++)
                Require(binding.comboClips[i]!=null&&binding.comboClips[i].humanMotion&&Mathf.Abs(binding.comboClips[i].length-combo.recastStages[i].Duration)<.001f,"Press "+(i+1)+" plays a Humanoid slice as long as its stage");
            OneHandAxeAttackAuthoring.VerifySlices();
            Require(true,"Slices match the original clip at the old rhythm and join without jumps");

            // Evolutions and authored numbers: the Inspector is the balance sheet, so these catch a wiped or reverted asset.
            Require(Mathf.Approximately(family.basicDamageBonus,.15f),"The family trait makes basics 15 % heavier");
            var basic=family.GetAbility(AbilitySlot.Basic);
            AbilityDefinition Skill(string id)=>Enumerable.Range(0,family.SkillCount).Select(family.Skill).First(a=>a!=null&&a.Id==id);
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
            void Bleed(AbilityModifierDefinition m,float seconds)=>
                Require(Close(m.BleedDamage(0),3)&&Close(m.BleedDamage(3),3.5f)&&Close(m.BleedSeconds(0),seconds)&&Close(m.BleedSeconds(3),seconds),m.id+" bleeds 3 per second, 3.5 at rank 3, for "+seconds+" s");

            Modifiers(basic,"fourth_cut");
            var fourth=basic.FindModifier("fourth_cut");
            Require(fourth.behavior==AbilityModifierBehavior.FourthCut&&fourth.hitsRequired==4&&Mathf.Approximately(fourth.streakResetSeconds,3),"Cuarto corte needs 4 basics in a row, reset after 3 s");
            Bleed(fourth,3);

            var swing=Skill("AxeForwardSwing");Modifiers(swing,"reopen","sweep");Retired(swing,"power","recovery");
            Evolution(swing,"reopen",AbilityModifierBehavior.Reopen,3,3.5f);
            Evolution(swing,"sweep",AbilityModifierBehavior.Sweep,.6f,.7f);
            Require(Close(swing.FindModifier("sweep").width,2),"Barrido doubles the width of the swing");
            Require(Mathf.Approximately(swing.actions.OfType<MeleeAction>().Single().damage,20),"Hachazo hits for 20");

            var rend=Skill("AxeArmorRend");Modifiers(rend,"deep_rend","raw_flesh");
            Evolution(rend,"deep_rend",AbilityModifierBehavior.DeepRend,.39f,.45f);
            Evolution(rend,"raw_flesh",AbilityModifierBehavior.RawFlesh,0,0);Bleed(rend.FindModifier("raw_flesh"),3);
            var rendAction=rend.actions.OfType<ArmorRendStrikeAction>().Single();
            Require(Mathf.Approximately(rendAction.damage,17.5f)&&Mathf.Approximately(rendAction.armorMultiplier,.7f)&&Mathf.Approximately(rendAction.armorDuration,4),"Desgarre hits for 125 % of 14 and removes 30 % of the armor for 4 s");

            var cut=Skill("AxeBleedingCut");Modifiers(cut,"rusty_edge");
            Evolution(cut,"rusty_edge",AbilityModifierBehavior.RustyEdge,.5f,.75f);
            var prime=cut.actions.OfType<PrimeBleedAction>().Single();
            Require(Mathf.Approximately(prime.damagePerSecond,3)&&Mathf.Approximately(prime.bleedSeconds,5)&&Mathf.Approximately(prime.markSeconds,4),"Tajo sangrante bleeds 3 per second for 5 s on a 4 s mark");

            Modifiers(combo,"escalation","shatter");
            Evolution(combo,"escalation",AbilityModifierBehavior.Escalation,.1f,.15f);
            Evolution(combo,"shatter",AbilityModifierBehavior.Shatter,.3f,.45f);

            var cruel=Skill("AxeCruelEdge");var executioner=Skill("AxeExecutioner");
            Modifiers(cruel,"cold_blood");Retired(cruel,"power");Modifiers(executioner,"execution");Retired(executioner,"power");
            Evolution(cruel,"cold_blood",AbilityModifierBehavior.ColdBlood,3,3.5f);
            Evolution(executioner,"execution",AbilityModifierBehavior.Execution,.3f,.35f);
            Require(Close(executioner.FindModifier("execution").bonus,.4f),"Ejecución raises the bonus to 40 %");

            // The Inspector drawer must resolve every field it shows: a typo in a name would hide that field silently.
            var unresolved=new System.Collections.Generic.List<string>();
            foreach(var ability in Enumerable.Range(0,family.SkillCount).Select(family.Skill).Append(basic))
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
            Debug.Log("ONE_HAND_AXE_PASS");
        }
    }
}
