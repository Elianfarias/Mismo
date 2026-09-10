using System;
using System.Linq;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;

namespace Mismo.Gameplay.Player.Editor
{
    public static class WeaponFamilyMigration
    {
        public const string Folder="Assets/Data/WeaponFamilies";
        public static void RunBatch()
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Data","WeaponFamilies");
            var catalog=Resources.Load<ItemCatalog>("ItemCatalog");
            var sword=catalog.weapons.Single(w=>w.Id=="sword.basic");
            var bow=catalog.weapons.Single(w=>w.Id=="bow.basic");
            var family=Create(sword,"OneHandSword",true);
            var reward=catalog.bossReward;
            if(reward.family==null && reward.abilities.SequenceEqual(sword.abilities))
            {reward.family=family;EditorUtility.SetDirty(reward);}
            Create(bow,"Bow",false);
            AssetDatabase.SaveAssets();
            Debug.Log("WEAPON_FAMILY_MIGRATION_PASS");
        }
        private static WeaponFamilyDefinition Create(WeaponDefinition weapon,string name,bool sword)
        {
            if(weapon.family!=null)return weapon.family;
            var family=ScriptableObject.CreateInstance<WeaponFamilyDefinition>();family.abilities=(AbilityDefinition[])weapon.abilities.Clone();
            var animations=ScriptableObject.CreateInstance<WeaponAnimationSet>();
            if(sword)
            {
                var clips=AssetDatabase.LoadAllAssetsAtPath(VoxelCharacterIntegration.ModelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview")).ToArray();
                AnimationClip Clip(string clipName)
                {
                    var clip=clips.Single(c=>c.name.Split('|').Last()==clipName);
                    var overrides=weapon.poseProfile!=null?weapon.poseProfile.animations:null;
                    return overrides!=null && overrides[clip]!=null ? overrides[clip] : clip;
                }
                animations.actions=new[]
                {
                    new AbilityAnimationBinding {ability=weapon.GetAbility(AbilitySlot.Basic),comboClips=new[]{Clip("Attack1"),Clip("Attack2"),Clip("Attack3")},blendSeconds=.035f},
                    Binding(weapon.GetAbility(AbilitySlot.Q),Clip("Lunge")),
                    Binding(weapon.GetAbility(AbilitySlot.E),Clip("Parry")),
                    Binding(weapon.GetAbility(AbilitySlot.R),Clip("Spin"))
                };
            }
            else
            {
                // No authored bow attack clips exist yet: expose the slots, never substitute sword attacks.
                animations.actions=weapon.abilities.Where(a=>a!=null).Select(a=>new AbilityAnimationBinding{ability=a}).ToArray();
            }
            family.animations=animations;
            AssetDatabase.CreateAsset(animations,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+name+"Animations.asset"));
            AssetDatabase.CreateAsset(family,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+name+".asset"));
            weapon.family=family;EditorUtility.SetDirty(weapon);return family;
        }
        private static AbilityAnimationBinding Binding(AbilityDefinition ability,AnimationClip clip)
        {
            // Preserve the existing whole-action animation clock for migrated non-charged clips.
            return new AbilityAnimationBinding{ability=ability,clip=clip,activeStartsAt=ability.preparation/ability.Duration,
                recoveryStartsAt=(ability.preparation+ability.active)/ability.Duration,blendSeconds=.035f};
        }
    }
}
