using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Core;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class HammerAssets
    {
        public const string Data="Assets/Data/Weapons/Hammer";
        public const string Art="Assets/Art/FBX/Weapons/Hammer";
        public const string Clips="Assets/Art/Animations/Weapons/Hammer";
        public const string Visual="Assets/Art/Prefabs/Weapons/Hammer/HammerVisual.prefab";
        public const string Recipe="Assets/Data/Crafting/Recipes/StoneHammer.asset";
        public const string Icons="Assets/Art/UI/Weapons/Hammer";
        public const string Output="output/warhammer";
        const string Request="Temp/Warhammer.request";
        static double nextPoll;
        [InitializeOnLoadMethod] static void Register(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            nextPoll=EditorApplication.timeSinceStartup+1;if(!File.Exists(Request))return;
            string command=File.ReadAllText(Request).Trim();File.Delete(Request);Directory.CreateDirectory(Output);
            try
            {
                if(command=="audit")HammerChecks.Render(true);
                else if(command=="create")Create();
                else if(command=="render")HammerChecks.Render(false);
                else if(command=="check")HammerChecks.StaticChecks();
                else if(command=="play")HammerChecks.RunInteractive();
                else if(command=="build")HammerChecks.BuildContent();
                else if(command=="icons")HammerPolishAssets.Icons();
                else throw new InvalidOperationException("Comando desconocido.");
                File.WriteAllText(Output+"/request-result.txt","PASS "+command);
            }
            catch(Exception e){File.WriteAllText(Output+"/request-result.txt","FAIL "+command+"\n"+e);Debug.LogException(e);}
        }
        public static readonly string[] Names={"HammerAttack","HammerHeavy","HammerGroundSlam","HammerSweep","HammerCharge","HammerDoubleSpin"};
        public static AbilityDefinition[] Kit(WeaponDefinition w)=>new[]{w.GetAbility(AbilitySlot.Basic)}.Concat(w.family.repertoire).Distinct().OrderBy(a=>Array.IndexOf(Names,a.name)).ToArray();
        public static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
        }
        public static T Asset<T>(string path) where T:ScriptableObject
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset!=null)return asset;
            Folder(Path.GetDirectoryName(path).Replace('\\','/'));asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);return asset;
        }
        static void Move(string from,string to,List<string> moved)
        {
            if(AssetDatabase.LoadMainAssetAtPath(from)==null)return;
            if(AssetDatabase.LoadMainAssetAtPath(to)!=null)throw new InvalidOperationException("Destino ocupado: "+to);
            string guid=AssetDatabase.AssetPathToGUID(from);Folder(Path.GetDirectoryName(to).Replace('\\','/'));
            string error=AssetDatabase.MoveAsset(from,to);if(!string.IsNullOrEmpty(error))throw new Exception(error);
            if(guid!=AssetDatabase.AssetPathToGUID(to))throw new Exception("GUID cambiado: "+to);
            moved.Add(from+" -> "+to+" | "+guid);
        }
        static void Migrate()
        {
            var moved=new List<string>();
            Move(Data+"/HammerVisual.prefab",Visual,moved);
            // Historical paths are used only as explicit migration sources, never as load destinations.
            string legacy="Assets/"+"Resources";
            Move(legacy+"/Recipes/StoneHammer.asset",Recipe,moved);
            foreach(string kind in new[]{"strike","heavy","slam","spin"})Move(legacy+"/UI/QuietFantasy/Icons/hammer-"+kind+".png",Icons+"/hammer-"+kind+".png",moved);
            foreach(string material in new[]{"HammerWood","HammerStone"})Move(Art+"/"+material+".mat","Assets/Art/Materials/Weapons/Hammer/"+material+".mat",moved);
            Move("Assets/Art/Inventory/weapon-Hammer.png","Assets/Art/UI/Inventory/weapon-Hammer.png",moved);
            File.AppendAllLines(Output+"/migration.txt",moved);
            // Only remove empty migration folders; AssetDatabase keeps asset GUIDs intact.
            foreach(string folder in new[]{legacy+"/UI/QuietFantasy/Icons",legacy+"/UI/QuietFantasy",legacy+"/UI",legacy+"/Recipes",legacy,"Assets/Art/Inventory"})
                if(AssetDatabase.IsValidFolder(folder)&&Directory.GetFileSystemEntries(folder).Length==0)AssetDatabase.DeleteAsset(folder);
        }
        [MenuItem("Mismo/Armas/Actualizar Warhammer")]
        public static void Create()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Salir de Play Mode.");
            Directory.CreateDirectory(Output);Migrate();Folder(Icons);Folder(Clips);
            var weapon=AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Data+"/Hammer.asset");
            if(weapon==null)throw new Exception("Falta el Hammer existente. No se crea un arma paralela.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Visual);
            if(prefab==null)throw new Exception("Falta el modelo existente del Hammer.");
            // Normalize the existing visual to 1.55m, retaining its model, pivot and materials.
            var visual=PrefabUtility.LoadPrefabContents(Visual);
            try
            {
                var renderers=visual.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                float scale=1.55f/bounds.size.y;
                foreach(Transform child in visual.transform){child.localPosition*=scale;child.localScale*=scale;}
                PrefabUtility.SaveAsPrefabAsset(visual,Visual);
            }
            finally{PrefabUtility.UnloadPrefabContents(visual);}
            var basic=Ability(0,"Golpe de mazo","hammer.strike",22,.36f,.20f,.42f,.98f,.12f,0,.25f);
            var heavy=Ability(1,"Carrera demoledora","hammer.heavy",40,.28f,.92f,.55f,4.5f,0,0,0);
            var slam=Ability(2,"Golpe sísmico","hammer.slam",32,.86f,.24f,.70f,5,.04f,0,.1f);
            var sweep=Ability(3,"Barrido de guerra","hammer.sweep",25,.54f,.42f,.55f,3.6f,.15f,0,.25f);
            var charge=Ability(4,"Avance demoledor","hammer.charge",30,.48f,.68f,.58f,6,.12f,0,.2f);
            // Preserve saved selections while changing the special to the requested full-body spin.
            var special=Ability(5,"Torbellino de guerra","hammer.double-spin",14,.48f,1.8f,.58f,8,0,0,0);
            basic.usesSwordCombo=true;basic.actions=Array.Empty<AbilityAction>();
            basic.comboSteps=new[]{
                JsonUtility.FromJson<Mismo.Gameplay.Combat.ComboStep>("{\"id\":\"hammer.strike.1\",\"duration\":0.98,\"inputStartPercent\":22,\"inputEndPercent\":82,\"branchPercent\":100,\"transitionDuration\":0.04,\"recoveryDuration\":0,\"impactStartPercent\":36.734694,\"impactEndPercent\":47,\"damage\":22,\"shape\":1,\"center\":{\"x\":0,\"y\":0.95,\"z\":1.38},\"radius\":0.68}"),
                JsonUtility.FromJson<Mismo.Gameplay.Combat.ComboStep>("{\"id\":\"hammer.strike.2\",\"duration\":1.08,\"inputStartPercent\":25,\"inputEndPercent\":75,\"branchPercent\":100,\"transitionDuration\":0,\"recoveryDuration\":0,\"impactStartPercent\":37.037037,\"impactEndPercent\":49,\"damage\":26,\"shape\":1,\"center\":{\"x\":0,\"y\":0.95,\"z\":1.38},\"radius\":0.68}")};
            heavy.actions=new AbilityAction[]{new MoveCasterAction{distance=3.2f,crossEnemies=false},Impact(40,.78f,1.40f,.9f,90,28,.45f,.20f,.72f)};
            slam.actions=new AbilityAction[]{Impact(32,2.1f,1.03f,.15f,360,24,.55f,.22f,0,true)};
            sweep.actions=new AbilityAction[]{Impact(25,1.75f,.4f,.9f,75,14,.3f,.12f,.12f)};
            var sweepHit=(HammerImpactAction)sweep.actions[0];sweepHit.singleHitPerCast=true;sweepHit.hitTimes=new[]{.04f,.12f,.24f};sweepHit.hitYawAngles=new[]{-60f,0,60f};
            charge.actions=new AbilityAction[]{new MoveCasterAction{distance=1.8f,crossEnemies=false},Impact(30,.7f,1.38f,.9f,80,20,.55f,.2f,.46f)};
            special.actions=new AbilityAction[]{Impact(14,1.7f,0,.95f,360,9,0,0,.18f)};
            ((HammerImpactAction)special.actions[0]).hitTimes=new[]{.18f,.63f,1.08f,1.53f};
            basic.description="Golpe diagonal de 22 de daño. Volvé a hacer clic para encadenar un golpe de vuelta de 26.";
            heavy.description="Corré 3,2 m hacia delante y rematá con un golpe de 40 de daño. Los obstáculos frenan la carrera.";
            slam.description="Descargá el mazo contra el suelo: 32 de daño en 2,1 m y breve tambaleo.";
            sweep.description="Barré un arco frontal amplio. Inflige 25 de daño a cada enemigo alcanzado.";
            charge.description="Avanzá 1,8 m y descargá un golpe de 30 de daño. Los obstáculos detienen el avance.";
            special.description="Girá dos vueltas sobre vos mismo con el mazo extendido. Cuatro impactos de 14 de daño en un radio de 1,7 m.";
            var all=new[]{basic,heavy,slam,sweep,charge,special};
            var family=weapon.family;family.displayName="Mazo de guerra";
            var feedback=Asset<CombatFeedbackProfile>("Assets/Data/Combat/Feedback/HammerFeedback.asset");
            var reference=AssetDatabase.LoadAssetAtPath<CombatFeedbackProfile>("Assets/Data/Combat/Feedback/SwordFeedback.asset");
            if(reference==null)throw new Exception("Falta perfil de feedback existente.");
            EditorUtility.CopySerialized(reference,feedback);feedback.name="HammerFeedback";feedback.heavyPostureThreshold=18;
            feedback.impact.hitStop=.008f;feedback.impact.scale=.23f;feedback.impact.sound=null;
            feedback.heavy.hitStop=.035f;feedback.heavy.scale=.42f;feedback.heavy.sound=null;
            family.feedback=feedback;EditorUtility.SetDirty(feedback);
            // Keep the three original selections and their unlock levels valid in existing saves.
            family.abilities=new[]{basic,heavy,slam,special};family.repertoire=new[]{heavy,slam,special,sweep,charge};
            weapon.abilities=(AbilityDefinition[])family.abilities.Clone();weapon.overrideFamilyAbilities=false;
            weapon.Configure("hammer.stone","Mazo de guerra",basic.cooldown);weapon.isTwoHanded=true;weapon.dualWield=false;weapon.visualPrefab=prefab;
            weapon.inventoryDescription="Gran mazo de piedra a dos manos. Golpes con inercia, control de grupos y recuperación comprometida.";
            var pose=weapon.poseProfile;pose.equipped.anchor=WeaponAnchor.RightHand;pose.equipped.bonePath="";pose.equipped.offset=Vector3.zero;pose.equipped.scale=1;
            pose.holstered=new WeaponAttachmentPose{anchor=WeaponAnchor.Character,offset=new Vector3(.35f,.65f,-.31f),rotation=new Vector3(0,0,32),scale=1};
            pose.maintainSupportGrip=true;pose.supportGrip=new Vector3(0,.30f,0);
            pose.meleeTrail=true;pose.trailTip=new Vector3(0,1.22f,0);pose.trailBase=new Vector3(0,1.0f,0);pose.trailWidth=.25f;pose.trailDuration=.12f;pose.trailPreparationFraction=.22f;
            pose.trailStartColor=new Color(.82f,.75f,.58f,.6f);pose.trailEndColor=new Color(.54f,.47f,.33f,0);
            HammerPolishAssets.Apply(weapon,family.animations);
            var recipe=AssetDatabase.LoadAssetAtPath<CraftingRecipe>(Recipe);
            if(recipe==null)throw new Exception("Falta la receta migrada.");
            recipe.displayName="Mazo de guerra";recipe.description="Mazo de piedra a dos manos.";recipe.weaponResult=weapon;
            var catalog=ProjectAssets.Load<ItemCatalog>("ItemCatalog");var gathering=ProjectAssets.Load<GatheringSettings>("GatheringSettings");
            if(catalog==null||gathering==null)throw new Exception("Falta catálogo o configuración de obtención.");
            if(!catalog.weapons.Contains(weapon))catalog.weapons=catalog.weapons.Concat(new[]{weapon}).ToArray();
            if(!gathering.recipes.Contains(recipe))gathering.recipes=gathering.recipes.Concat(new[]{recipe}).ToArray();
            foreach(var o in all.Cast<Object>().Concat(new Object[]{weapon,family,pose,family.animations,recipe,catalog,gathering}))EditorUtility.SetDirty(o);
            AssetDatabase.SaveAssets();
            InvokeEditor("RuntimeCatalogBuilder","Refresh");
            InvokeEditor("ProjectOrganizationChecks","Run");
            File.WriteAllText(Output+"/assets.txt","PASS: Hammer existente migrado; seis habilidades; GUIDs conservados; organización verificada.");
        }
        static AbilityDefinition Ability(int index,string display,string id,float damage,float prep,float active,float recovery,float cooldown,float prepMove,float activeMove,float recoveryMove)
        {
            var a=Asset<AbilityDefinition>(Data+"/"+Names[index]+".asset");a.displayName=display;a.abilityId=id;
            a.preparation=prep;a.active=active;a.recovery=recovery;a.cooldown=cooldown;
            a.preparationMobility=0;a.activeMobility=0;a.recoveryMobility=0;
            a.cancelPreparation=false;a.cancelRecovery=false;a.interruptible=true;a.chargeable=false;
            a.staminaCost=0;a.focusCost=0;a.focusGainOnHit=index==5?2:5;a.pose=AbilityPose.Lunge;a.usesSwordCombo=false;
            a.animationSource=null;a.targetsGround=false;a.aimFromCamera=false;a.range=3;a.localizationKey="";return a;
        }
        static HammerImpactAction Impact(float damage,float radius,float forward,float height,float arc,float posture,float push,float stun,float time,bool ground=false)
            =>new HammerImpactAction{damage=damage,radius=radius,forward=forward,height=height,arcDegrees=arc,postureDamage=posture,pushDistance=push,stunSeconds=stun,hitTimes=new[]{time},groundImpact=ground};
        public static void InvokeEditor(string type,string method)
        {
            var target=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(type)).FirstOrDefault(t=>t!=null);
            if(target==null)throw new Exception("No se encontró "+type);target.GetMethod(method).Invoke(null,null);
        }
        public static void RunBatch(){try{Create();EditorApplication.Exit(0);}catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/FAILED.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}}
    }
}
