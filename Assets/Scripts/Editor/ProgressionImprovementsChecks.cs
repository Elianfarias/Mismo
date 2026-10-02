using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Core;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static partial class ProgressionImprovementsChecks
{
    const string Output="output/progression-improvements";
    static readonly List<string> results=new List<string>();
    static readonly List<Object> owned=new List<Object>();
    [InitializeOnLoadMethod] static void Register(){EditorApplication.update-=PollContent;EditorApplication.update+=PollContent;}
    static void PollContent()
    {
        const string request="Temp/ProgressionContentChecks.request";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;
        string command;
        try{command=File.ReadAllText(request).Trim();File.Delete(request);}catch(IOException){return;}
        if(command=="preview")Preview();else RunContent();
    }
    [MenuItem("Mismo/Progresión/Verificar contenido y organización")]
    public static void RunContent()
    {
        Directory.CreateDirectory(Output);var lines=new List<string>();
        try
        {
            try{ProjectOrganizationChecks.Run();File.WriteAllText(Output+"/organization-checks.txt","PASS ProjectOrganizationChecks.Run");}
            catch(Exception e){File.WriteAllText(Output+"/organization-checks.txt",e.ToString());}
            var rules=AssetDatabase.LoadAssetAtPath<ProgressionRules>("Assets/Data/Progression/ProgressionRules.asset");
            if(rules==null||rules.masteryLevelsPerAbilityPoint!=3||rules.masteryExperiencePerDamage<=0)throw new Exception("Progression rule assets");
            var stamina=AssetDatabase.LoadAssetAtPath<StaminaSettings>("Assets/Data/Player/DefaultStaminaSettings.asset");
            if(stamina==null||stamina.Maximum!=80)throw new Exception("Initial stamina asset");
            lines.Add("PASS Rules: unlock points at mastery 3, 6, 9..., damage XP, 80 starting stamina");
            int skillCount=0;
            foreach(string guid in AssetDatabase.FindAssets("t:WeaponFamilyDefinition",new[]{"Assets/Data/WeaponFamilies"}))
            {
                var family=AssetDatabase.LoadAssetAtPath<WeaponFamilyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var ids=new HashSet<string>();
                for(int i=0;i<family.SkillCount;i++)
                {
                    var skill=family.Skill(i);if(skill==null||string.IsNullOrEmpty(skill.Id)||!ids.Add(skill.Id)||skill.masteryUsesRequired<1)throw new Exception("Invalid repertoire in "+family.name);
                    var modifiers=new HashSet<string>();
                    foreach(var modifier in skill.masteryModifiers)
                        if(modifier==null||string.IsNullOrEmpty(modifier.id)||!modifiers.Add(modifier.id)||modifier.maxLevel<1||modifier.effectiveUsesPerLevel<1)throw new Exception("Invalid modifiers for "+skill.name);
                    if(modifiers.Count==0)throw new Exception("Missing optional modifiers for "+skill.name);
                    skillCount++;
                }
                lines.Add("PASS "+family.DisplayName+": repertoire and optional modifier IDs");
            }
            if(skillCount!=24)throw new Exception("Expected 24 configured skills, got "+skillCount);
            ParryRegressionPlayChecks.CheckImportedContent();
            lines.Add("PASS Imported Parry retains its defense action, authored timing, animation pose and VFX");
            foreach(var name in new[]{"Arrival","WorldArrival","Parry","Weapons","Focus","WorldFocus","Interface","Combine"})
            {
                var sequence=AssetDatabase.LoadAssetAtPath<TutorialSequence>("Assets/Data/Tutorial/"+name+".asset");
                if(sequence==null||!sequence.IsValid)throw new Exception("Invalid tutorial "+name);
                if(sequence.pages.Any(p=>p.body.Contains("Ahora tenés {q}")||p.body.Contains("E activa la parada")))throw new Exception("Obsolete default-skill tutorial "+name);
                if(new[]{"Arrival","WorldArrival","Interface"}.Contains(name)&&!sequence.pages.Any(p=>p.body.Contains("doble salto")&&p.body.Contains("Espacio")))throw new Exception("Missing double jump tutorial "+name);
                if(new[]{"Weapons","Interface"}.Contains(name)&&!sequence.pages.Any(p=>p.body.Contains("cada 3 niveles")))throw new Exception("Missing unlock milestones "+name);
            }
            lines.Add("PASS Updated tutorial lessons deserialize and do not require a default skill");
            lines.Add("PASS ALL CONTENT CHECKS");
        }
        catch(Exception e){lines.Add("FAIL "+e);Debug.LogException(e);}
        File.WriteAllLines(Output+"/content-checks.txt",lines);
    }
    [MenuItem("Mismo/Progresión/Vista previa de atributos y habilidades")]
    public static void Preview()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Data/Inventory/ItemCatalog.asset");
        if(catalog==null)catalog=ProjectAssets.Load<ItemCatalog>("ItemCatalog");
        var weapons=ProjectAssets.Load<WeaponSetDefinition>("StartingWeapons");
        int firstFixture=owned.Count;
        var inventory=Player(weapons,catalog,new Store());
        var value=(InventoryProfile)typeof(PlayerInventory).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(inventory);
        value.progression.level=8;value.progression.staminaPoints=3;value.progression.lifePoints=2;
        foreach(var weapon in new[]{weapons.primary,weapons.secondary})
        {
            var mastery=value.progression.GetOrCreate(weapon.MasteryId);mastery.level=20;mastery.cooldownPoints=2;mastery.equippedAbilities=new string[3];
            for(int i=0;i<weapon.family.SkillCount;i++)
            {
                var skill=weapon.family.Skill(i);mastery.unlockedAbilities.Add(skill.Id);
                if(i<3)mastery.equippedAbilities[i]=skill.Id;
                var progress=mastery.GetSkillProgress(skill.Id);progress.effectiveUses=45;
                if(skill.masteryModifiers.Length>0){progress.TrySelectModifier(skill.masteryModifiers[0].id,skill.masteryUsesRequired);progress.modifiers[0].level=2;}
            }
        }
        Call(inventory,"ApplyStats");
        var panel=inventory.gameObject.AddComponent<InventoryPanel>();Call(panel,"Awake");
        var window=ScriptableObject.CreateInstance<ProgressionPreviewWindow>();window.panel=panel;window.actor=inventory.gameObject;
        window.fixtures=owned.Skip(firstFixture).ToArray();owned.RemoveRange(firstFixture,owned.Count-firstFixture);
        window.titleContent=new GUIContent("Progresión · vista previa");window.position=new Rect(70,70,1280,830);window.ShowUtility();
    }
    static void Check(bool condition,string text){if(!condition)throw new Exception(text);results.Add("PASS "+text);}
    static void Near(float a,float b,string text)=>Check(Mathf.Abs(a-b)<.001f,text+" ("+a+" / "+b+")");
    static T Data<T>()where T:ScriptableObject{var value=ScriptableObject.CreateInstance<T>();owned.Add(value);return value;}
    static GameObject Actor(string name){var value=new GameObject(name){hideFlags=HideFlags.HideAndDontSave};owned.Add(value);return value;}
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(target,value);
    static void Call(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(target,null);
    sealed class Store:IProfileRepository
    {
        public string json;public bool fail;
        public ProfileReadResult Read(Func<string,bool> validate,out string payload)
        {payload=json;return json==null?ProfileReadResult.Missing:validate(json)?ProfileReadResult.Loaded:ProfileReadResult.Invalid;}
        public void Write(string payload){if(fail)throw new IOException("Expected test failure");json=payload;}
    }
    public static void RunBatch()
    {
        if(!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Run fixtures in an isolated Unity validation project.");
        Directory.CreateDirectory(Output);
        int code=0;
        try{ModelChecks();RuntimeChecks();CheatChecks();EvolutionChecks();results.Add("PASS ALL "+results.Count+" checks");}
        catch(Exception e){code=1;results.Add("FAIL "+e);Debug.LogException(e);}
        finally
        {
            for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)Object.DestroyImmediate(owned[i]);
            owned.Clear();File.WriteAllLines(Output+"/checks.txt",results);
        }
        EditorApplication.Exit(code);
    }
    static void ModelChecks()
    {
        string[] skills={"lunge","parry","step","breaker","two","tip"};
        var mastery=new MasteryProgress{familyId="sword"};
        Check(mastery.IsValid()&&mastery.AbilityPoints()==0&&mastery.equippedAbilities==null,"New family starts without skills or unlock points");
        Check(!mastery.TryUnlockAbility(skills,5)&&!mastery.TrySelectAbility(skills,0,0),"Cannot unlock/equip without earning a point");
        mastery.level=2;
        Check(mastery.AbilityPoints()==0&&!mastery.TryUnlockAbility(skills,5),"Level 2 does not unlock a skill");
        mastery.level=3;
        Check(mastery.TryUnlockAbility(skills,5)&&mastery.AbilityPoints()==0&&mastery.Available==2,"Level 3 unlocks any skill; attribute points remain independent");
        Check(!mastery.TryUnlockAbility(skills,5)&&!mastery.TryUnlockAbility(skills,0),"No duplicate or overspent unlocks");
        Check(mastery.TrySelectAbility(skills,5,2)&&string.IsNullOrEmpty(mastery.equippedAbilities[0]),"Unlocked skill equips into an empty slot");
        mastery.level=5;Check(mastery.AbilityPoints()==0,"Levels 4 and 5 do not add unlock points");
        mastery.level=6;mastery.TryUnlockAbility(skills,0);mastery.TrySelectAbility(skills,0,0);
        Check(mastery.TrySelectAbility(skills,5,0)&&mastery.equippedAbilities[2]=="lunge","Equipping an existing selection swaps the slots");
        var copy=mastery.Copy();copy.unlockedAbilities.Clear();copy.equippedAbilities[0]=null;
        Check(mastery.IsUnlocked("tip")&&mastery.equippedAbilities[0]=="tip","Transaction copy cannot mutate original unlocks or slots");
        var legacy=new MasteryProgress{familyId="sword",level=6,equippedAbilities=new[]{"breaker","parry","lunge"}};
        legacy.MigrateAbilities(skills);
        Check(legacy.unlockedAbilities.Count==5&&legacy.equippedAbilities[0]=="breaker"&&legacy.AbilityPoints()==0,"Legacy selection and all earned legacy unlocks survive migration");
        var freshLegacy=new MasteryProgress{familyId="bow"};freshLegacy.MigrateAbilities(skills);
        Check(freshLegacy.legacyAbilityCredit==3&&freshLegacy.equippedAbilities[1]=="parry"&&freshLegacy.AbilityPoints()==0,"Legacy level 1 retains three skills without gaining free spending points");
        freshLegacy.level=2;Check(freshLegacy.AbilityPoints()==0,"Legacy player follows the new interval");
        freshLegacy.level=3;Check(freshLegacy.AbilityPoints()==1,"Legacy player earns a new point at level 3");
        Check(new MasteryProgress{level=9}.AbilityPoints()==3&&new MasteryProgress{level=20}.AbilityPoints()==6,"Milestones reach three points at level 9 and six at level 20");
        var skill=mastery.GetSkillProgress("tip");
        Check(!skill.TrySelectModifier("power",2),"Modifier cannot be selected before skill mastery");
        skill.RecordUse(2,3,2);skill.RecordUse(2,3,2);
        Check(skill.TrySelectModifier("power",2)&&skill.Modifier("power").level==0,"Mastery unlocks optional modifier training at rank zero");
        skill.RecordUse(2,3,2);Check(skill.Modifier("power").level==0,"One use does not complete a two-use modifier rank");
        skill.RecordUse(2,3,2);Check(skill.Modifier("power").level==1,"Effective use trains the chosen modifier");
        skill.TrySelectModifier("recovery",2);skill.RecordUse(2,3,2);
        Check(skill.Modifier("power").level==1&&skill.Modifier("recovery").effectiveUses==1,"Alternative modifiers retain independent progress");
        skill.selectedModifierId=null;skill.RecordUse(2,3,2);
        Check(skill.Modifier("recovery").effectiveUses==1,"No equipped modifier means no modifier training");
        var roundtrip=JsonUtility.FromJson<MasteryProgress>(JsonUtility.ToJson(mastery));
        Check(roundtrip.IsValid()&&roundtrip.IsUnlocked("tip")&&roundtrip.SkillProgress("tip").Modifier("power").level==1,"Unity JSON roundtrip retains unlocks, slots and modifier ranks");
        copy=mastery.Copy();copy.SkillProgress("tip").modifiers[0].level=99;
        Check(mastery.SkillProgress("tip").modifiers[0].level==1,"Transaction copy isolates nested modifier progress");
        roundtrip.unlockedAbilities.Add("tip");Check(!roundtrip.IsValid(),"Reject duplicate persisted unlocks");
        mastery.damageExperienceRemainder=double.NaN;Check(!mastery.IsValid(),"Reject nonfinite persisted damage remainder");
        var p=new ProgressionData{level=3,staminaPoints=2};
        Check(p.IsValid()&&p.Available==0&&p.Copy().staminaPoints==2,"Stamina participates in the character point budget and copy");
        p.staminaPoints++;Check(!p.IsValid(),"Reject overspending character attributes");
        var spent=new MasteryProgress{familyId="test",level=4,damagePoints=1,speedPoints=1,cooldownPoints=1};
        Check(spent.IsValid()&&spent.Available==0&&spent.Copy().cooldownPoints==1,"Cooldown shares the weapon mastery point budget");
        spent.cooldownPoints++;Check(!spent.IsValid(),"Reject overspending mastery cooldown");
    }
    static void RuntimeChecks()
    {
        var registry=Data<RuntimeAssetCatalog>();var rules=Data<ProgressionRules>();rules.dropChance=0;
        registry.entries=new[]{new RuntimeAssetCatalog.Entry{key="ProgressionRules",assets=new Object[]{rules}}};registry.Invalidate();
        var damageProgress=new MasteryProgress{familyId="test"};
        for(int i=0;i<10;i++)rules.GrantDamage(damageProgress,.5);
        Check(damageProgress.experience==1&&damageProgress.damageExperienceRemainder<.00001,"Fractional damage XP survives repeated small hits");
        rules.GrantDamage(damageProgress,double.PositiveInfinity);Check(damageProgress.experience==1,"Ignore nonfinite damage");
        Near(rules.CooldownReduction(1000),.4f,"Cooldown attribute is capped at 40 percent");
        var family=Data<WeaponFamilyDefinition>();family.progressionId="test.sword";
        var bowFamily=Data<WeaponFamilyDefinition>();bowFamily.progressionId="test.bow";
        var basic=Data<AbilityDefinition>();basic.name="basic";basic.cooldown=4;
        var skill=Data<AbilityDefinition>();skill.name="test.skill";skill.cooldown=10;skill.masteryUsesRequired=2;
        skill.masteryModifiers=new[]{new AbilityModifierDefinition{id="power",maxLevel=3,effectiveUsesPerLevel=2,damagePerLevel=.05f},new AbilityModifierDefinition{id="recovery",maxLevel=3,effectiveUsesPerLevel=2,cooldownReductionPerLevel=.05f}};
        var other=Data<AbilityDefinition>();other.name="other";
        family.abilities=new[]{basic,skill,other,other};family.repertoire=new[]{skill,other};
        bowFamily.abilities=new[]{basic,skill,other,other};bowFamily.repertoire=new[]{skill,other};
        var sword=Data<WeaponDefinition>();sword.Configure("test.sword","Test sword",.5f);sword.family=family;
        var bow=Data<WeaponDefinition>();bow.Configure("test.bow","Test bow",.5f);bow.family=bowFamily;bow.isBow=true;
        var weapons=Data<WeaponSetDefinition>();weapons.primary=sword;weapons.secondary=bow;
        var catalog=Data<ItemCatalog>();catalog.weapons=new[]{sword,bow};
        var store=new Store();var inventory=Player(weapons,catalog,store);var actor=inventory.gameObject;
        var loadout=actor.GetComponent<EquipmentLoadout>();var stamina=actor.GetComponent<Stamina>();
        Check(inventory.IsReady&&!inventory.HasSaveProblem&&loadout.GetAbility(AbilitySlot.Basic)==basic&&loadout.GetAbility(AbilitySlot.Q)==null,"Real inventory starts with basic only");
        Check(JsonUtility.FromJson<InventoryProfile>(store.json).version==9,"Fresh profile persists schema 9");
        Near(stamina.Maximum,80,"Starting stamina is 80");
        inventory.TryGrantVictory(500,new Dictionary<string,int>{{sword.MasteryId,100}},null);
        Check(inventory.AvailableAbilityPoints(sword)==1&&inventory.AvailableAbilityPoints(bow)==0,"Only the contributing family gains a skill point");
        Check(inventory.TrySpendAttributes(0,0,0,2),"Stamina attributes can be committed");
        Near(inventory.AbilityCooldown(sword,skill),10,"Character upgrades do not reduce skill cooldown");
        Check(inventory.TrySpendMasteryPoints(sword,0,0,1),"Cooldown spends a weapon mastery point");
        Near(inventory.AbilityCooldown(bow,skill),10,"Sword cooldown investment does not affect bow skills");
        Check(!inventory.TrySpendMasteryPoints(sword,0,0,int.MaxValue)&&!inventory.TrySpendMasteryPoints(sword,0,0,-1),"Mastery batches reject overflow and negative cooldown");
        store.fail=true;Check(!inventory.TrySpendMastery(sword,MasteryAttribute.Cooldown)&&inventory.Mastery(sword).cooldownPoints==1,"Failed save cannot spend a cooldown point");store.fail=false;
        Near(stamina.Maximum,90,"Stamina capacity increases by allocated points");Near(stamina.Current,80,"Investing in capacity gives no free refill");
        stamina.Tick(false,5);Near(stamina.Current,90,"Regeneration reaches the upgraded capacity");
        Check(PlayerHUD.StaminaWidth(380,90)>PlayerHUD.StaminaWidth(380,80),"HUD length increases with capacity");
        Near(inventory.AbilityCooldown(sword,skill),9.8f,"Cooldown attribute affects skill cooldown");
        Near(inventory.AbilityCooldown(sword,basic,true),4,"Basic attack cooldown keeps attack-speed rules");
        Check(inventory.TryUnlockAbility(sword,0)&&inventory.TrySelectAbility(sword,AbilitySlot.R,0),"Unlock and equip through real inventory");
        Check(loadout.GetAbility(AbilitySlot.R)==skill&&loadout.GetAbility(AbilitySlot.Q)==null,"Empty slots never fall back to default skills");
        Check(!inventory.TrySelectModifier(sword,0,"power"),"Runtime selection rejects unmastered modifier");
        var victim=Monster("Target",100,out var health,out var receiver);
        long cast=AttackIdentity.Next();Hit(receiver,actor,sword,skill,10,cast);
        inventory.FlushCombatProgress();
        Check(!health.IsDead&&inventory.Mastery(sword).experience==2,"Nonlethal real damage grants weapon XP immediately after flush");
        Check(inventory.Mastery(sword).SkillProgress(skill.Id).effectiveUses==1,"Confirmed monster hit trains skill mastery");
        Hit(receiver,actor,sword,skill,10,cast);inventory.FlushCombatProgress();
        Check(inventory.Mastery(sword).SkillProgress(skill.Id).effectiveUses==1&&inventory.Mastery(sword).experience==4,"Repeated pulses add damage XP but count one skill use");
        var target2=Monster("Area second target",100,out _,out var secondReceiver);
        Hit(secondReceiver,actor,sword,skill,5,cast);inventory.FlushCombatProgress();
        Check(inventory.Mastery(sword).SkillProgress(skill.Id).effectiveUses==1,"Multiple monsters in one cast count as one use");
        Hit(receiver,actor,sword,skill,5,AttackIdentity.Next());inventory.FlushCombatProgress();
        ClearCombat(loadout);Check(inventory.TrySelectModifier(sword,0,"power"),"Mastered skill offers an optional modifier");
        Near(inventory.AbilityDamageMultiplier(sword,skill),1,"Untrained modifier has no free effect");
        Hit(receiver,actor,sword,skill,5,AttackIdentity.Next());Hit(receiver,actor,sword,skill,5,AttackIdentity.Next());inventory.FlushCombatProgress();
        Near(inventory.AbilityDamageMultiplier(sword,skill),1.05f,"Two effective casts train rank one and apply power");
        ClearCombat(loadout);Check(inventory.TryClearModifier(sword,0),"Modifier is optional and can be disabled outside combat");
        Near(inventory.AbilityDamageMultiplier(sword,skill),1,"Disabling a modifier removes its effect");
        Check(inventory.TrySelectModifier(sword,0,"power")&&inventory.Mastery(sword).SkillProgress(skill.Id).Modifier("power").level==1,"Reequipping retains trained rank");
        var invulnerable=victim.AddComponent<Invulnerability>();Call(invulnerable,"Awake");Set(receiver,"invulnerability",invulnerable);invulnerable.StartWindow(10);
        int uses=inventory.Mastery(sword).SkillProgress(skill.Id).effectiveUses;int xp=inventory.Mastery(sword).experience;
        Hit(receiver,actor,sword,skill,50,AttackIdentity.Next());inventory.FlushCombatProgress();
        Check(inventory.Mastery(sword).experience==xp&&inventory.Mastery(sword).SkillProgress(skill.Id).effectiveUses==uses,"Invulnerable/rejected hits grant neither XP nor training");invulnerable.Cancel();
        var dummy=Actor("Dummy");var dummyHealth=dummy.AddComponent<Health>();dummyHealth.Revive();var dummyReceiver=dummy.AddComponent<DamageReceiver>();Call(dummyReceiver,"Awake");
        Hit(dummyReceiver,actor,sword,skill,10,AttackIdentity.Next());inventory.FlushCombatProgress();
        Check(inventory.Mastery(sword).experience==xp,"Ordinary damage receivers without monster rewards do not grant progression");
        store.fail=true;Hit(receiver,actor,sword,skill,5,AttackIdentity.Next());
        Check(!inventory.FlushCombatProgress()&&inventory.Mastery(sword).experience==xp,"Failed save retains pending damage without mutating committed XP");
        store.fail=false;Check(inventory.FlushCombatProgress()&&inventory.Mastery(sword).experience==xp+1,"Retry saves pending damage exactly once");
        inventory.FlushCombatProgress();Check(inventory.Mastery(sword).experience==xp+1,"Second flush cannot duplicate XP");
        ClearCombat(loadout);inventory.TrySwap();
        Hit(receiver,actor,sword,skill,5,AttackIdentity.Next());inventory.FlushCombatProgress();
        Check(inventory.Mastery(sword).experience==xp+2&&inventory.Mastery(bow).experience==0,"Delayed hit credits originating family after swap: sword="+inventory.Mastery(sword).experience+", expected="+(xp+2)+", bow="+inventory.Mastery(bow).experience+", health="+health.Current+", outcome="+receiver.LastResult.Outcome);
        float remaining=health.Current;int before=inventory.Mastery(sword).experience;
        Hit(receiver,actor,sword,skill,10000,AttackIdentity.Next());inventory.FlushCombatProgress();
        Check(health.IsDead&&inventory.Mastery(sword).experience-before==(int)(remaining*.2f),"Lethal overkill grants only remaining health damage, with no fixed mastery reward");
        int after=inventory.Mastery(sword).experience;Hit(receiver,actor,sword,skill,10000,AttackIdentity.Next());inventory.FlushCombatProgress();
        Check(inventory.Mastery(sword).experience==after,"Dead monsters cannot grant duplicate XP");
        var loaded=Player(weapons,catalog,new Store{json=store.json});
        Check(!loaded.HasSaveProblem&&loaded.Mastery(sword).SkillProgress(skill.Id).Modifier("power").level==inventory.Mastery(sword).SkillProgress(skill.Id).Modifier("power").level&&loaded.Progression.staminaPoints==2,"Protected inventory load validates and restores new progression");
        var previous=JsonUtility.FromJson<InventoryProfile>(store.json);previous.version=6;
        foreach(var m in previous.progression.masteries){m.unlockedAbilities=null;m.abilityProgress=null;m.equippedAbilities=null;}
        var oldStore=new Store{json=JsonUtility.ToJson(previous)};var migrated=Player(weapons,catalog,oldStore);
        Check(!migrated.HasSaveProblem&&migrated.SelectedAbility(sword,AbilitySlot.Q)==skill&&JsonUtility.FromJson<InventoryProfile>(oldStore.json).version==9,"Real version 6 migration persists and restores implicit skills");
        previous=JsonUtility.FromJson<InventoryProfile>(store.json);previous.version=7;previous.progression.cooldownPoints=1;
        var priorMastery=previous.progression.Find(sword.MasteryId);priorMastery.level=2;priorMastery.experience=0;priorMastery.cooldownPoints=0;
        int previousAvailable=previous.progression.Available;
        var versionSevenStore=new Store{json=JsonUtility.ToJson(previous)};var seven=Player(weapons,catalog,versionSevenStore);
        Check(!seven.HasSaveProblem&&seven.Progression.cooldownPoints==0&&seven.Progression.Available==previousAvailable+1,"Schema 7 refunds character cooldown points");
        Check(seven.SelectedAbility(sword,AbilitySlot.R)==skill&&seven.Mastery(sword).legacyAbilityCredit==1&&seven.AvailableAbilityPoints(sword)==0,"Schema 7 keeps a level-2 unlock and selection without free additional points");
        Check(seven.Mastery(sword).SkillProgress(skill.Id).Modifier("power").level==priorMastery.SkillProgress(skill.Id).Modifier("power").level,"Migration preserves optional modifier training");
        Near(seven.AbilityCooldown(sword,skill),10,"Legacy character cooldown has no effect on weapons");
        var sevenReload=Player(weapons,catalog,new Store{json=versionSevenStore.json});
        Check(!sevenReload.HasSaveProblem&&sevenReload.Progression.Available==seven.Progression.Available&&sevenReload.Mastery(sword).legacyAbilityCredit==1,"Schema 8 reload does not duplicate refunds or migration credit");
        ClearCombat(loadout);loadout.TrySwap();
        float original=skill.cooldown;
        actor.GetComponent<CombatState>().Reward(100,"test");
        Check(loadout.Runner.TryUse(AbilitySlot.R,Vector3.forward,Vector3.zero),"Unlocked equipped skill executes through AbilityRunner");
        Near(loadout.Runner.CooldownDuration(skill),9.8f,"Runtime captures reduced cooldown for HUD");
        Near(skill.cooldown,original,"Cooldown calculation never mutates shared ability assets");
        loadout.Runner.Cancel();Check(loadout.Runner.Remaining(skill)>0,"Cancel cannot clear the captured cooldown");
        ClearCombat(loadout);
        Check(inventory.TrySelectModifier(sword,0,"recovery"),"Alternative modifier is selectable after mastery");
        inventory.RecordMonsterSkillUse(sword.MasteryId,skill.Id,AttackIdentity.Next());
        inventory.RecordMonsterSkillUse(sword.MasteryId,skill.Id,AttackIdentity.Next());inventory.FlushCombatProgress();
        Near(inventory.AbilityCooldown(sword,skill),9.31f,"Trained recovery modifier combines multiplicatively with weapon mastery cooldown reduction");
        Check(inventory.Mastery(sword).SkillProgress(skill.Id).Modifier("power").level>=1,"Choosing recovery preserves power training");
        inventory.TryGrantVictory(0,new Dictionary<string,int>{{sword.MasteryId,300}},null);
        Check(inventory.TryUnlockAbility(sword,1)&&inventory.TrySelectAbility(sword,AbilitySlot.E,1),"Next three-level milestone allows an independently chosen skill");
        other.actions=new AbilityAction[]{new GuardAction()};other.preparation=0;other.active=1;other.cooldown=0;
        var playerReceiver=actor.AddComponent<DamageReceiver>();Call(playerReceiver,"Awake");
        Check(loadout.Runner.TryUse(AbilitySlot.E,Vector3.forward,Vector3.zero),"Defensive skill can be activated");loadout.Runner.Tick(.1f);
        inventory.FlushCombatProgress();Check(inventory.Mastery(sword).SkillProgress(other.Id)==null,"Casting a defense in the air gives no mastery");
        int defenseXP=inventory.Mastery(sword).experience;
        var blocked=playerReceiver.Resolve(new DamageInfo(5,target2,actor.transform.position,Vector3.back,AttackIdentity.Next()));inventory.FlushCombatProgress();
        Check(blocked.Outcome==HitOutcome.Block&&inventory.Mastery(sword).SkillProgress(other.Id).effectiveUses==1,"Blocking a monster attack trains the defensive skill");
        Check(inventory.Mastery(sword).experience==defenseXP,"Defense does not manufacture weapon damage XP");
        playerReceiver.Resolve(new DamageInfo(5,target2,actor.transform.position,Vector3.back,AttackIdentity.Next()));inventory.FlushCombatProgress();
        Check(inventory.Mastery(sword).SkillProgress(other.Id).effectiveUses==1,"Repeated blocks in one activation count once");
        ClearCombat(loadout);
        var execution=new AbilityExecution(loadout.Runner,sword,skill,Vector3.forward,new Vector3(100,0,0));
        var projectileAction=new ProjectileAction();projectileAction.Begin(execution);
        var projectile=Object.FindObjectsByType<ProjectileInstance>().Single();owned.Add(projectile.gameObject);
        Check(projectile.AbilityId==skill.Id&&projectile.AbilityUseId==execution.AttackId,"Projectile retains the source skill and cast identity");
        var areaObject=Actor("Area fixture");areaObject.transform.position=new Vector3(100,0,0);var area=areaObject.AddComponent<AreaInstance>();
        Set(area,"owner",actor);Set(area,"family",sword.MasteryId);Set(area,"abilityId",skill.Id);Set(area,"abilityUseId",execution.AttackId);Set(area,"radius",3f);Set(area,"damage",2f);
        var areaTarget=Monster("Area target",100,out _,out var areaReceiver);areaTarget.transform.position=new Vector3(100,0,1);areaTarget.AddComponent<BoxCollider>();Physics.SyncTransforms();
        loadout.TrySwap();int beforeAreaUses=inventory.Mastery(sword).SkillProgress(skill.Id).effectiveUses;
        area.Pulse();area.Pulse();inventory.FlushCombatProgress();
        Check(inventory.Mastery(sword).SkillProgress(skill.Id).effectiveUses==beforeAreaUses+1,"Real area pulses after swapping credit the original skill once");
        CombatAilment.Poison(areaTarget,actor,sword.MasteryId,2,5,skill.Id,execution.AttackId);
        var poison=areaTarget.GetComponent<CombatAilment>();Set(poison,"nextPoison",-1f);Call(poison,"Update");inventory.FlushCombatProgress();
        Check(inventory.Mastery(sword).SkillProgress(skill.Id).effectiveUses==beforeAreaUses+1,"Poison preserves cast identity and cannot duplicate area/direct-hit training");
        ClearCombat(loadout);
        var recoveryBefore=inventory.Mastery(sword).SkillProgress(skill.Id).Modifier("recovery");
        int trainedBefore=recoveryBefore.level*2+recoveryBefore.effectiveUses;
        inventory.RecordMonsterSkillUse(sword.MasteryId,skill.Id,AttackIdentity.Next());
        Check(inventory.TrySelectModifier(sword,0,"power"),"Modifier switch flushes pending combat progress");
        var recoveryAfter=inventory.Mastery(sword).SkillProgress(skill.Id).Modifier("recovery");
        Check(recoveryAfter.level*2+recoveryAfter.effectiveUses==trainedBefore+1,"Pending use trains the previous modifier, never the newly chosen modifier");
        inventory.RecordMonsterSkillUse(sword.MasteryId,skill.Id,AttackIdentity.Next());store.fail=true;
        Check(!inventory.TryClearModifier(sword,0)&&inventory.Mastery(sword).SkillProgress(skill.Id).selectedModifierId=="power","Failed pending save cannot change the selected modifier");
        store.fail=false;
        Check(inventory.TryClearModifier(sword,0),"Retry flushes pending uses before disabling the modifier");
        WeaponVfxChecks(inventory,loadout,sword,skill);
    }
    static void CheatChecks()
    {
        var family=Data<WeaponFamilyDefinition>();family.progressionId="cheat.sword";
        var dual=Data<WeaponFamilyDefinition>();dual.progressionId="cheat.dual";
        var shield=Data<WeaponFamilyDefinition>();shield.progressionId="cheat.shield";
        var sword=Data<WeaponDefinition>();sword.Configure("cheat.sword","Sword",.5f);sword.family=family;sword.dualSwordFamily=dual;sword.swordShieldFamily=shield;
        var bow=Data<WeaponDefinition>();bow.Configure("cheat.bow","Bow",.5f);bow.isBow=true;
        var unowned=Data<WeaponDefinition>();unowned.Configure("cheat.unowned","Unowned",.5f);
        var weapons=Data<WeaponSetDefinition>();weapons.primary=sword;weapons.secondary=bow;
        var catalog=Data<ItemCatalog>();catalog.weapons=new[]{sword,bow,unowned};
        var store=new Store();var inventory=Player(weapons,catalog,store);var actor=inventory.gameObject;
        inventory.TryGrantVictory(2000,new Dictionary<string,int>{{sword.MasteryId,100}},null);
        Check(inventory.TrySpendAttributes(9,0,0),"Cheat fixture increases maximum health through actual character attributes");
        inventory.TrySpendMasteryPoints(sword,0,0,1);
        int characterLevel=inventory.Level;
        store.fail=true;
        Check(!inventory.TryMaxCheatWeaponMasteries()&&inventory.Mastery(sword).level==3&&inventory.Mastery(bow).level==1,"Failed max-mastery save leaves every family unchanged");
        store.fail=false;
        Check(inventory.TryMaxCheatWeaponMasteries(),"Max-mastery cheat commits successfully");
        Check(new[]{sword.MasteryId,bow.MasteryId,dual.progressionId,shield.progressionId}.All(id=>inventory.Progression.Find(id)?.level==inventory.Rules.masteryMaxLevel),"Cheat maximizes owned weapon and combination families");
        Check(inventory.Progression.Find(unowned.MasteryId)==null&&inventory.Level==characterLevel,"Max mastery leaves unowned weapons and character level unchanged");
        var mastery=inventory.Mastery(sword);
        Check(mastery.cooldownPoints==1&&mastery.Available==mastery.level-2&&mastery.AbilityPoints()==6&&mastery.unlockedAbilities.Count==0,"Max mastery preserves allocations and leaves skill choices unspent");
        string maximumSave=store.json;inventory.TryMaxCheatWeaponMasteries();
        Check(store.json==maximumSave,"Repeated max-mastery cheat is idempotent");
        var reloaded=Player(weapons,catalog,new Store{json=store.json});
        Check(!reloaded.HasSaveProblem&&reloaded.Mastery(sword).level==inventory.Rules.masteryMaxLevel,"Cheat mastery persists on reload");
        var cheats=actor.AddComponent<Mismo.Gameplay.Player.PlayerCheats>();Call(cheats,"Awake");
        var health=actor.GetComponent<Health>();health.Revive();health.ApplyDamage(new DamageInfo(70,actor,Vector3.zero,Vector3.forward));
        Check(!cheats.RestoreHealth()&&!cheats.MaxWeaponMasteries()&&health.Current==75,"New commands require F8 cheat mode");
        // Exercise the real controller commands in EditMode; keyboard transitions require a running player loop.
        Check(cheats.SetActive(true)&&cheats.Active&&actor.GetComponent<PlayerMotor>().IsFlying&&inventory.Count==3,"Enabling cheat mode retains flight and full-arsenal behavior");
        Check(cheats.MaxWeaponMasteries()&&inventory.Mastery(unowned).level==inventory.Rules.masteryMaxLevel,"Max command reaches newly obtained weapons through PlayerCheats");
        Check(cheats.RestoreHealth(),"Heal command succeeds while cheat mode is active");
        Near(health.Current,145,"Heal command restores the current full health capacity");
        Check(cheats.SetActive(false)&&!cheats.Active&&!actor.GetComponent<PlayerMotor>().IsFlying,"Disabling cheat mode disables flight");
        health.ApplyDamage(new DamageInfo(20,actor,Vector3.zero,Vector3.forward));
        Check(!cheats.RestoreHealth()&&health.Current==125,"Heal command is inert after disabling cheat mode");
        cheats.SetActive(true);health.ApplyDamage(new DamageInfo(1000,actor,Vector3.zero,Vector3.forward));
        Check(!cheats.RestoreHealth()&&health.IsDead,"Healing cannot bypass the death and respawn flow");
        Call(cheats,"Update");Check(!cheats.Active,"Death switches off cheat mode");
    }
    static void WeaponVfxChecks(PlayerInventory inventory,EquipmentLoadout loadout,WeaponDefinition weapon,AbilityDefinition skill)
    {
        ClearCombat(loadout);if(loadout.ActiveDefinition.MasteryId!=weapon.MasteryId)inventory.TrySwap();
        var actor=inventory.gameObject;var runner=loadout.Runner;var vfx=actor.GetComponent<WeaponAbilityVfx>();Call(vfx,"Awake");
        var presentation=actor.GetComponent<WeaponPresentation>();
        var model=Actor("VFX main weapon");var offhand=Actor("VFX offhand");
        Set(presentation,"activeVisual",model);Set(presentation,"activeSecondVisual",offhand);
        var mainPoint=Actor("Main emission");mainPoint.transform.SetParent(model.transform,false);mainPoint.transform.localPosition=new Vector3(0,0,1);
        mainPoint.AddComponent<WeaponVfxSocket>().socketId="tip";
        var offPoint=Actor("Offhand emission");offPoint.transform.SetParent(offhand.transform,false);offPoint.AddComponent<WeaponVfxSocket>().socketId="tip";
        var prefab=Actor("Charge particles");var particles=prefab.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=particles.main;main.playOnAwake=false;main.loop=true;main.useUnscaledTime=true;
        var modifierPrefab=Actor("Modifier particles");modifierPrefab.AddComponent<ParticleSystem>().Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var preparation=new WeaponVfxDefinition{prefab=prefab,socketId="tip",fullChargeScale=2,localOffset=new Vector3(.1f,0,0)};
        skill.weaponVfx=new[]{preparation,new WeaponVfxDefinition{prefab=prefab,socketId="tip",phase=WeaponVfxPhase.Execution,lifetime=2},new WeaponVfxDefinition{prefab=prefab,socketId="tip",phase=WeaponVfxPhase.Active}};
        skill.masteryModifiers[0].weaponVfx=new[]{new WeaponVfxDefinition{prefab=modifierPrefab,socketId="tip",hand=WeaponVfxHand.Offhand}};
        skill.chargeable=true;skill.preparation=.1f;skill.maximumCharge=1;skill.active=.2f;skill.recovery=.15f;skill.cooldown=0;skill.actions=Array.Empty<AbilityAction>();
        ((System.Collections.IDictionary)typeof(AbilityRunner).GetField("readyAt",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(runner)).Clear();
        Check(WeaponVfxSocket.Resolve(model.transform,"tip")==mainPoint.transform&&WeaponVfxSocket.Resolve(model.transform,"")==model.transform,"Weapon VFX resolves explicit sockets and the model-root fallback");
        Check(WeaponVfxSocket.Resolve(model.transform,"missing")==null,"Missing socket never emits an effect at the character origin");
        Check(runner.TryUse(AbilitySlot.R,Vector3.forward,Vector3.zero,held:true)&&mainPoint.transform.childCount==1&&offPoint.transform.childCount==0,"Charged skill creates preparation VFX only on the chosen weapon");
        var charge=mainPoint.transform.GetChild(0);var chargeParticle=charge.GetComponent<ParticleSystem>();
        Check(chargeParticle.isPlaying&&!chargeParticle.main.useUnscaledTime,"Particle prefab starts even with Play On Awake disabled and uses gameplay time");
        model.transform.position=new Vector3(7,2,3);
        Check(Vector3.Distance(charge.position,mainPoint.transform.TransformPoint(preparation.localOffset))<.001f,"Charge effect follows the weapon socket and configured offset");
        runner.Tick(.55f);Near(charge.localScale.x,1.5f,"Charge controls visual scale without changing the shared prefab");Near(prefab.transform.localScale.x,1,"Source prefab scale remains unchanged");
        try
        {
            GameplayPause.Pause();Call(vfx,"LateUpdate");Check(chargeParticle.isPaused,"Pause freezes charge particles");
        }
        finally{GameplayPause.Resume();}
        Call(vfx,"LateUpdate");Check(chargeParticle.isPlaying,"Resume continues existing particles without spawning copies");
        runner.SetHeld(false);runner.Tick(.01f);
        Check(charge==null&&mainPoint.transform.childCount==2,"Release removes charging VFX and starts execution plus active VFX");
        runner.Tick(.22f);Check(mainPoint.transform.childCount==1,"Active VFX ends with the active phase while the release burst remains");
        runner.Tick(.2f);Check(runner.Current==null&&mainPoint.transform.childCount==1,"Release burst can finish after the ability completes");
        var live=(System.Collections.IList)typeof(WeaponAbilityVfx).GetField("effects",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vfx);
        Set(live[0],"remaining",-1f);Call(vfx,"LateUpdate");Check(mainPoint.transform.childCount==0,"Release burst expires and removes its runtime object");
        ClearCombat(loadout);Check(inventory.TrySelectModifier(weapon,0,"power"),"Trained modifier can supply its own weapon VFX");
        runner.TryUse(AbilitySlot.R,Vector3.forward,Vector3.zero,held:true);
        Check(mainPoint.transform.childCount==1&&offPoint.transform.childCount==1,"Ability and selected modifier VFX coexist on their configured hands");
        runner.Cancel();Check(mainPoint.transform.childCount==0&&offPoint.transform.childCount==0,"Cancellation clears VFX from both weapons");
        ClearCombat(loadout);inventory.TryClearModifier(weapon,0);runner.TryUse(AbilitySlot.R,Vector3.forward,Vector3.zero,held:true);
        Check(offPoint.transform.childCount==0,"Disabling the optional modifier removes its VFX from subsequent casts");
        runner.Cancel();preparation.hand=WeaponVfxHand.Both;runner.TryUse(AbilitySlot.R,Vector3.forward,Vector3.zero,held:true);
        Check(mainPoint.transform.childCount==1&&offPoint.transform.childCount==1,"Both-hands binding emits once on each weapon");
        runner.Tick(1.01f);Check(runner.Current.Began&&mainPoint.transform.childCount==2&&offPoint.transform.childCount==0,"Maximum charge auto-releases and clears both charging effects");
        ClearCombat(loadout);runner.TryUse(AbilitySlot.R,Vector3.forward,Vector3.zero,held:true);
        skill.interruptible=true;Check(runner.Interrupt()&&mainPoint.transform.childCount==0&&offPoint.transform.childCount==0,"Interruption removes charging effects");
        // Equipment changes clear remaining bursts even when the cast has already finished.
        runner.TryUse(AbilitySlot.R,Vector3.forward,Vector3.zero,held:true);runner.Tick(2);
        Check(inventory.TrySwap()&&mainPoint.transform.childCount==0&&offPoint.transform.childCount==0,"Weapon swap clears outstanding execution VFX");
        ClearCombat(loadout);inventory.TrySwap();runner.TryUse(AbilitySlot.R,Vector3.forward,Vector3.zero,held:true);
        actor.GetComponent<Health>().ApplyDamage(new DamageInfo(100000,actor,Vector3.zero,Vector3.forward));
        Check(mainPoint.transform.childCount==0&&offPoint.transform.childCount==0,"Death immediately removes all weapon VFX");
        actor.GetComponent<Health>().Revive();runner.Cancel();
    }
    static PlayerInventory Player(WeaponSetDefinition weapons,ItemCatalog catalog,Store store)
    {
        var actor=Actor("Progression player");var health=actor.AddComponent<Health>();health.Revive();
        var stamina=actor.AddComponent<Stamina>();stamina.Configure(Data<StaminaSettings>());
        var motor=actor.AddComponent<PlayerMotor>();Call(motor,"Awake");
        var loadout=actor.AddComponent<EquipmentLoadout>();Set(loadout,"startingWeapons",weapons);loadout.Initialize();
        Call(actor.GetComponent<WeaponSkillEffects>(),"Awake");
        var inventory=actor.AddComponent<PlayerInventory>();inventory.Initialize(catalog,store);return inventory;
    }
    static GameObject Monster(string name,float maximum,out Health health,out DamageReceiver receiver)
    {
        var actor=Actor(name);health=actor.AddComponent<Health>();health.ConfigureMaximum(maximum);health.Revive();
        receiver=actor.AddComponent<DamageReceiver>();Call(receiver,"Awake");Set(receiver,"invulnerabilityAfterHit",0f);
        var reward=actor.AddComponent<EnemyProgressionReward>();Call(reward,"Awake");Call(reward,"OnEnable");return actor;
    }
    static void Hit(DamageReceiver receiver,GameObject actor,WeaponDefinition weapon,AbilityDefinition ability,float damage,long cast)=>
        receiver.Resolve(new DamageInfo(damage,actor,receiver.transform.position,Vector3.forward,AttackIdentity.Next(),weaponFamilyId:weapon.MasteryId,abilityId:ability.Id,abilityUseId:cast));
    static void ClearCombat(EquipmentLoadout loadout){loadout.Runner.Cancel();Set(loadout,"combatUntil",-1f);}
}

public sealed class ProgressionPreviewWindow:EditorWindow
{
    public InventoryPanel panel;public GameObject actor;public Object[] fixtures;int tab;
    void OnGUI()
    {
        if(panel==null)return;
        tab=GUILayout.Toolbar(tab,new[]{"Atributos","Habilidades","Habilidad bloqueada","Maestría"});
        var old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3(0,28,0),Quaternion.identity,Vector3.one*Mathf.Min(position.width/1280,(position.height-28)/800));
        PlayerHUD.Fill(new Rect(0,0,1280,800),new Color(.043f,.059f,.071f));
        if(tab==2)
        {
            var inventory=actor.GetComponent<PlayerInventory>();
            var value=(InventoryProfile)typeof(PlayerInventory).GetField("profile",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(inventory);
            var mastery=value.progression.Find(actor.GetComponent<EquipmentLoadout>().ActiveDefinition.MasteryId);
            var id=actor.GetComponent<EquipmentLoadout>().ActiveDefinition.family.Skill(0).Id;mastery.unlockedAbilities.Remove(id);
            Draw("DrawSkillCollection");mastery.unlockedAbilities.Insert(0,id);
        }
        else Draw(tab==0?"DrawCharacterSheet":tab==3?"DrawWeaponSheet":"DrawSkillCollection");
        GUI.matrix=old;
    }
    void Draw(string name)=>typeof(InventoryPanel).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);
    void OnDisable(){if(fixtures!=null)for(int i=fixtures.Length-1;i>=0;i--)if(fixtures[i]!=null)Object.DestroyImmediate(fixtures[i]);}
}
