using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class ProgressionChecks
    {
        const string Pending="Mismo.ProgressionChecks";
        static IEnumerator routine;static double deadline;static int lastFrame=-1,count;
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/GoblinEliteArena.unity");
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
            SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Isolate()
        {if(SessionState.GetBool(Pending,false))foreach(var respawn in Object.FindObjectsByType<World.RegionRespawn>())Object.DestroyImmediate(respawn);}
        [InitializeOnLoadMethod]
        static void Resume(){if(!SessionState.GetBool(Pending,false))return;deadline=EditorApplication.timeSinceStartup+150;EditorApplication.update+=Step;}
        static void Step()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
            if(!Application.isPlaying||Time.frameCount<10||lastFrame==Time.frameCount)return;
            lastFrame=Time.frameCount;
            try{if(routine==null)routine=Run();if(!routine.MoveNext())Finish(true,count+" checks");}catch(Exception e){Finish(false,e.ToString());}
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;Debug.Log("PROGRESSION_CHECK "+message);}
        static bool Near(float a,float b)=>Mathf.Abs(a-b)<.02f;
        static void Finish(bool ok,string message)
        {
            SessionState.SetBool(Pending,false);EditorApplication.update-=Step;
            Directory.CreateDirectory("Docs/Validation");File.WriteAllText("Docs/Validation/Progression-checks.txt",(ok?"PASS ":"FAIL ")+message);
            Debug.Log("PROGRESSION_"+(ok?"PASS ":"FAIL ")+message);EditorApplication.Exit(ok?0:1);
        }
        sealed class MemoryStore:IProfileRepository
        {
            public string json;public bool fail;
            public ProfileReadResult Read(Func<string,bool> validate,out string payload)
            {payload=json;return json==null?ProfileReadResult.Missing:validate(json)?ProfileReadResult.Loaded:ProfileReadResult.Invalid;}
            public void Write(string payload){if(fail)throw new IOException("Simulated write failure");json=payload;}
        }
        static IEnumerator Run()
        {
            foreach(var g in Object.FindObjectsByType<GoblinController>())g.enabled=false;
            foreach(var b in Object.FindObjectsByType<BossController>())b.enabled=false;
            var player=Object.FindAnyObjectByType<PlayerController>();player.enabled=false;
            var loadout=player.GetComponent<EquipmentLoadout>();loadout.Runner.Cancel();loadout.Belt?.Cancel();
            var health=player.GetComponent<Health>();health.Revive();
            float until=Time.time+7;while(loadout.InCombat&&Time.time<until)yield return null;
            var catalog=Resources.Load<ItemCatalog>("ItemCatalog");
            var old=new InventoryProfile{version=1,progression=null};
            for(int i=0;i<2;i++){var item=new OwnedWeapon{instanceId=Guid.NewGuid().ToString("N"),definitionId=loadout.GetSlot(i).Id,tier=0};old.weapons.Add(item);old.equipped[i]=item.instanceId;}
            var storage=new MemoryStore{json=JsonUtility.ToJson(old)};
            var inventory=player.gameObject.AddComponent<PlayerInventory>();inventory.Initialize(catalog,storage);
            Check(inventory.IsReady&&!inventory.HasSaveProblem&&inventory.Level==1,"Old profile loads and migrates");
            Check(JsonUtility.FromJson<InventoryProfile>(storage.json).version==2&&inventory.EquippedId(0)==old.equipped[0],"Migration is persisted and retains instance IDs");
            var sword=Array.Find(catalog.weapons,w=>w.Id=="sword.basic");var bow=Array.Find(catalog.weapons,w=>w.Id=="bow.basic");
            Check(sword.MasteryId==catalog.bossReward.MasteryId&&sword.MasteryId!=bow.MasteryId,"Families share mastery across exemplars only");
            Check(!inventory.TrySpend(CharacterAttribute.Attack),"No free character points");
            Check(inventory.TryGrantVictory(270,new Dictionary<string,int>{{sword.MasteryId,100}},null),"Victory experience is saved");
            Check(inventory.Level==4&&inventory.Progression.Available==3,"Multiple character levels awarded without losing points");
            Check(inventory.Mastery(sword).level==3&&inventory.Mastery(bow).level==1,"Only contributing family gains mastery");
            Check(inventory.TrySpend(CharacterAttribute.Life)&&inventory.TrySpend(CharacterAttribute.Attack)&&inventory.TrySpend(CharacterAttribute.Armor),"All three character attributes can be selected");
            Check(Near(health.Maximum,105)&&Near(health.Current,100)&&Near(inventory.Armor,2),"Life increase does not give free healing and armor applies");
            Check(!inventory.TrySpend(CharacterAttribute.Life),"Spent character points cannot be reused");
            Check(inventory.TrySpendMastery(sword,MasteryAttribute.Damage)&&inventory.TrySpendMastery(sword,MasteryAttribute.Speed),"Maestría offers damage and speed");
            Check(Near(inventory.DamageMultiplier(sword),1.045f)&&Near(inventory.AttackSpeed(sword),1.015f),"Character and family bonuses affect offense");
            Check(!inventory.TrySpendMastery(sword,MasteryAttribute.Speed),"Spent mastery points cannot be reused");
            var heavy=new OwnedWeapon{instanceId=Guid.NewGuid().ToString("N"),definitionId=sword.Id,tier=3,variant=WeaponVariant.Colossus};
            Check(inventory.TryGrantVictory(0,null,heavy)&&inventory.TryEquip(0,heavy.instanceId),"Rolled exemplar can be saved and equipped");
            Check(Near(health.Maximum,120)&&inventory.AttackSpeed(sword)<1&&inventory.DamageMultiplier(sword)>1.3f,"Tier and colossus positives and negatives apply");
            var guard=new OwnedWeapon{instanceId=Guid.NewGuid().ToString("N"),definitionId=bow.Id,tier=2,variant=WeaponVariant.Guardian};
            Check(inventory.TryGrantVictory(0,null,guard)&&inventory.TryEquip(1,guard.instanceId),"Second equipped exemplar retains its roll");
            float max=health.Maximum,armor=inventory.Armor,current=health.Current;
            Check(inventory.TrySwap()&&Near(max,health.Maximum)&&Near(armor,inventory.Armor)&&Near(current,health.Current),"Alternating preserves both weapons' life and armor");
            Check(Near(inventory.AttackSpeed(bow),1)&&Near(inventory.DamageMultiplier(bow),1.0375f),"Offensive modifiers apply only to corresponding weapon");
            var receiver=player.GetComponent<DamageReceiver>();player.GetComponent<Invulnerability>()?.Cancel();
            float before=health.Current;
            receiver.Resolve(new DamageInfo(10,null,player.transform.position,Vector3.forward,AttackIdentity.Next()));
            Check(Near(before-health.Current,10*inventory.IncomingDamageMultiplier),"Armor mitigates received health damage");
            Check(!inventory.TryEquip(0,old.equipped[0])&&!inventory.TrySpend(CharacterAttribute.Life),"Combat blocks equipment and point changes");
            until=Time.time+7;while(loadout.InCombat&&Time.time<until)yield return null;
            Check(inventory.TrySwap(),"Return to sword after combat");
            var cast=new AbilityExecution(loadout.Runner,sword,sword.GetAbility(AbilitySlot.Q),Vector3.forward,Vector3.zero);
            float captured=cast.DamageMultiplier;
            Check(inventory.TrySwap()&&Near(cast.DamageMultiplier,captured)&&cast.WeaponFamilyId==sword.MasteryId,"Execution retains weapon attribution and offense after swap");
            int level=inventory.Level,items=inventory.Count;string saved=storage.json;storage.fail=true;
            Check(!inventory.TryGrantVictory(1000,new Dictionary<string,int>{{bow.MasteryId,200}},inventory.RollDrop()),"Failed save rejects whole victory transaction");
            Check(inventory.Level==level&&inventory.Count==items&&storage.json==saved,"Failed transaction changes neither stats nor inventory");storage.fail=false;
            var victim=new GameObject("Progression reward fixture");victim.transform.position=new Vector3(200,0,0);
            var victimHealth=victim.AddComponent<Health>();victimHealth.ConfigureMaximum(10);victimHealth.Revive();
            var victimReceiver=victim.AddComponent<DamageReceiver>();victim.AddComponent<EnemyProgressionReward>();
            int xp=inventory.Progression.experience;
            victimReceiver.Resolve(new DamageInfo(10,player.gameObject,victim.transform.position,Vector3.forward,AttackIdentity.Next(),weaponFamilyId:bow.MasteryId));
            Check(inventory.Progression.experience==xp+inventory.Rules.enemyExperience,"Real enemy defeat awards experience");
            int once=inventory.Progression.experience;
            victimReceiver.Resolve(new DamageInfo(10,player.gameObject,victim.transform.position,Vector3.forward,AttackIdentity.Next(),weaponFamilyId:bow.MasteryId));
            Check(inventory.Progression.experience==once,"Dead enemy cannot award victory twice");Object.Destroy(victim);
            var roundtrip=JsonUtility.FromJson<InventoryProfile>(storage.json);
            Check(roundtrip.Find(heavy.instanceId).tier==3&&roundtrip.Find(heavy.instanceId).variant==WeaponVariant.Colossus&&roundtrip.progression.lifePoints==1,"Save retains rolls and allocated points");
            Check(inventory.Mastery(catalog.bossReward).damagePoints==1,"Reward exemplar inherits family mastery");
            health.Revive();until=Time.time+7;while(loadout.InCombat&&Time.time<until)yield return null;
            var panel=player.gameObject.AddComponent<InventoryPanel>();Check(panel.TryOpen(),"Progression screen opens through inventory");
            typeof(InventoryPanel).GetField("showProgression",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(panel,true);
            Directory.CreateDirectory("Docs/Validation");for(int i=0;i<5;i++)yield return null;
            ScreenCapture.CaptureScreenshot("Docs/Validation/Progression-screen.png");for(int i=0;i<20;i++)yield return null;
            typeof(InventoryPanel).GetField("showProgression",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(panel,false);
            ScreenCapture.CaptureScreenshot("Docs/Validation/Progression-items.png");for(int i=0;i<20;i++)yield return null;
        }
    }
}
