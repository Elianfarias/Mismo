using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Quests;
using Mismo.Gameplay.Player.Camera;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class CameraCheatChecks
    {
        const string Key = "Mismo.CameraCheatChecks";
        static IEnumerator routine;
        static double deadline;
        static int frame = -1, count;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void RunBatch() => StartBatch(true);
        public static void RunGameplayBatch() => StartBatch(false);
        static void StartBatch(bool checkOrganization)
        {
            if (!Application.isBatchMode || !Directory.GetCurrentDirectory().Replace('\\', '/').Contains("/.validation/"))
                throw new InvalidOperationException("Run in the isolated validation project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));
            Object.DestroyImmediate(player.GetComponent<World.RegionRespawn>());
            player.transform.position = new Vector3(0, 100, 0);
            new GameObject("Camera").AddComponent<UnityEngine.Camera>().tag = "MainCamera";
            SessionState.SetBool(Key + ".Organization", checkOrganization);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(Key, false)) return;
            deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update += Step;
        }
        static void Step()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout"); return; }
            if (!Application.isPlaying || Time.frameCount < 10 || frame == Time.frameCount) return;
            frame = Time.frameCount;
            try { if (routine == null) routine = Run(); if (!routine.MoveNext()) Finish(true, count + " checks"); }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        static void Finish(bool pass, string message)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Step;
            Directory.CreateDirectory("output/camera-cheats");
            File.WriteAllText("output/camera-cheats/checks.txt", (pass ? "PASS " : "FAIL ") + message);
            Debug.Log("CAMERA_CHEATS_" + (pass ? "PASS " : "FAIL ") + message);
            EditorApplication.Exit(pass ? 0 : 1);
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); count++; Debug.Log("CAMERA_CHEATS_CHECK " + message); }
        static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static GameObject Box(string name, Vector3 position, Vector3 scale, Transform parent = null)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.SetParent(parent); box.transform.position = position; box.transform.localScale = scale;
            return box;
        }
        sealed class Memory : IProfileRepository
        {
            public string Payload; public bool Fail; public int Writes;
            public ProfileReadResult Read(Func<string, bool> validate, out string payload)
            { payload = Payload; return payload == null ? ProfileReadResult.Missing : validate(payload) ? ProfileReadResult.Loaded : ProfileReadResult.Invalid; }
            public void Write(string payload) { if (Fail) throw new IOException("Expected write failure"); Payload = payload; Writes++; }
        }
        static IEnumerator Run()
        {
            var player = Object.FindAnyObjectByType<PlayerController>(); player.enabled = false;
            var motor = player.GetComponent<PlayerMotor>();
            var cheats = player.GetComponent<PlayerCheats>();
            Check(cheats != null && !cheats.Active && !motor.IsFlying, "Cheats attach to the existing player and default off");
            var loadout = player.GetComponent<EquipmentLoadout>(); loadout.Runner.Cancel(); loadout.Belt?.Cancel();

            var target = new GameObject("Camera test target"); target.transform.position = new Vector3(1000, 1000, 1000);
            var cameraObject = new GameObject("Camera collision check");
            var view = cameraObject.AddComponent<UnityEngine.Camera>(); view.nearClipPlane = .1f;
            var camera = cameraObject.AddComponent<ThirdPersonCamera>(); camera.Configure(target.transform, null);
            camera.transform.rotation = Quaternion.identity;
            Vector3 origin = target.transform.position + Vector3.up * 1.3f;
            var weapon = Box("Own equipped collider on Default layer", origin + Vector3.back * .8f, Vector3.one * .3f, target.transform);
            Physics.SyncTransforms();
            Check(Physics.SphereCast(origin, .25f, Vector3.back, out var oldHit, 9, ~(1 << 2), QueryTriggerInteraction.Ignore) && oldHit.distance < .5f,
                "Reproduces the original camera collapse against equipped geometry");
            float Distance() => (float)Invoke(camera, "ObstacleDistance", origin, Vector3.back);
            Check(Mathf.Abs(Distance() - 9) < .01f, "Own colliders do not shorten the camera distance");
            var wall = Box("Real wall", origin + Vector3.back * 5, new Vector3(8, 8, .5f));
            Physics.SyncTransforms();
            Check(Distance() > 4 && Distance() < 4.5f, "A wall behind an ignored own collider still stops the camera");
            wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms();
            Check(Mathf.Abs(Distance() - 9) < .01f, "Triggers do not zoom the camera");
            wall.GetComponent<Collider>().isTrigger = false;
            for (int i = 0; i < 36; i++) Box("Crowded own collider", origin + Vector3.back * (1 + i * .08f), Vector3.one * .1f, target.transform);
            Physics.SyncTransforms();
            Check(Distance() > 4 && Distance() < 4.5f, "Growing the cast buffer preserves real obstacles in crowded equipment");
            Invoke(camera, "LateUpdate"); float close = Vector3.Distance(camera.transform.position, origin);
            wall.SetActive(false); Physics.SyncTransforms();
            Invoke(camera, "LateUpdate");
            Check(Vector3.Distance(camera.transform.position, origin) >= close && Vector3.Distance(camera.transform.position, origin) < 9,
                "Camera recovers outward smoothly after the wall clears");
            wall.SetActive(true); wall.transform.position = origin + Vector3.back; Physics.SyncTransforms();
            Invoke(camera, "LateUpdate");
            Check(Vector3.Distance(camera.transform.position, origin) < .5f, "New close obstacles take priority over smoothing");
            var visible = weapon.GetComponent<Renderer>();
            var alreadyHidden = target.transform.GetChild(1).GetComponent<Renderer>(); alreadyHidden.forceRenderingOff = true;
            Invoke(camera, "BeforeCamera", default(ScriptableRenderContext), view);
            Check(visible.forceRenderingOff, "A wall forcing the camera inside the avatar hides that avatar for this render");
            Invoke(camera, "AfterCamera", default(ScriptableRenderContext), view);
            Check(!visible.forceRenderingOff && alreadyHidden.forceRenderingOff, "Renderer state is restored without unhiding other hidden objects");
            Invoke(camera, "BeforeCamera", default(ScriptableRenderContext), UnityEngine.Camera.main);
            Check(!visible.forceRenderingOff, "The minimap and other cameras keep the character visible");
            Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(target); Object.DestroyImmediate(wall);

            motor.ResetPosition(new Vector3(0, 100, 0));
            motor.MovementConstraint = (from, to) => from;
            motor.SetFlight(true); motor.TickFlight(Vector3.up, false, .1f);
            Check(player.transform.position.y > 101 && !motor.IsGrounded, "Flight ascends and bypasses the progression movement constraint");
            Vector3 hovering = player.transform.position;
            for (int i = 0; i < 30; i++) motor.Tick(Vector3.zero, false, false, false, .02f);
            Check(Vector3.Distance(player.transform.position, hovering) < .001f, "Inventory/menu locomotion ticks do not apply gravity during flight");
            motor.TickFlight(Vector3.forward, false, .1f); float slow = player.transform.position.z;
            motor.TickFlight(Vector3.forward, true, .1f);
            Check(player.transform.position.z - slow > slow * 2, "Sprint accelerates flight");
            motor.ResetPosition(new Vector3(0, 100, 0)); motor.SetFlight(true);
            var floor = Box("Flight terrain", new Vector3(0, 99, 0), new Vector3(20, 1, 20)); Physics.SyncTransforms();
            for (int i = 0; i < 20; i++) motor.TickFlight(Vector3.down, false, .02f);
            Check(player.transform.position.y >= 99.4f && player.GetComponent<CharacterController>().enabled, "Flight descent respects terrain collision");
            motor.ResetPosition(new Vector3(0, 105, 0)); motor.MovementConstraint = null;
            motor.SetFlight(true); motor.SetFlight(false); motor.Tick(Vector3.zero, false, false, false, .1f);
            Check(player.transform.position.y < 105 && !motor.IsFlying, "Leaving flight restores normal gravity");

            var inventory = player.gameObject.AddComponent<PlayerInventory>();
            var storage = new Memory();
            var catalog = Mismo.Core.ProjectAssets.Load<ItemCatalog>("ItemCatalog");
            inventory.Initialize(catalog, storage);
            var initial = JsonUtility.FromJson<InventoryProfile>(storage.Payload);
            int initialCount = inventory.Count;
            storage.Fail = true;
            Check(!inventory.TryGrantCheatWeapons() && inventory.Count == initialCount, "Failed saves do not partially grant weapons");
            storage.Fail = false;
            Check(cheats.SetActive(true) && motor.IsFlying && cheats.Active, "Enabling cheats starts flight and grants the arsenal");
            var all = JsonUtility.FromJson<InventoryProfile>(storage.Payload);
            Check(catalog.weapons.All(w => all.weapons.Any(owned => owned.definitionId == w.Id)), "Every catalog weapon, including the boss weapon, is saved");
            int writes = storage.Writes;
            Check(inventory.TryGrantCheatWeapons() && storage.Writes == writes, "Repeated grants are idempotent");
            Check(cheats.SetActive(false) && !motor.IsFlying && inventory.Count >= initialCount, "Disabling cheats restores walking and retains granted weapons");
            Check(all.equipped.SequenceEqual(initial.equipped) && all.claimedRewards.SequenceEqual(initial.claimedRewards), "The arsenal preserves equipped items and boss reward progress");

            var profileField = typeof(PlayerInventory).GetField("profile", Private);
            Check(!cheats.ToggleOneHitKills() && !cheats.OneHitKills && !cheats.ActivateDragonAltars() && !cheats.AdvanceSoulEaterPhase(), "New cheats require F8 mode");
            Check(cheats.SetActive(true) && !cheats.OneHitKills, "One-hit kills start off even in cheat mode");
            Check(!cheats.ActivateDragonAltars(), "Missing dragon arc is handled without changing progress");
            var arc = player.gameObject.AddComponent<DragonArcCoordinator>(); arc.enabled = false;
            var data = AssetDatabase.LoadAssetAtPath<DragonArcDefinition>("Assets/Data/World/DragonArc/DragonArc.asset");
            typeof(DragonArcCoordinator).GetField("<Data>k__BackingField", Private).SetValue(arc, data);
            typeof(DragonArcCoordinator).GetField("inventory", Private).SetValue(arc, inventory);
            string beforeAltars = JsonUtility.ToJson(profileField.GetValue(inventory));
            string savedBeforeAltars = storage.Payload;
            storage.Fail = true;
            Check(!cheats.ActivateDragonAltars() && arc.LitCount == 0 && !arc.CanSummon &&
                storage.Payload == savedBeforeAltars && JsonUtility.ToJson(profileField.GetValue(inventory)) == beforeAltars,
                "Failed altar save leaves all quest progress unchanged");
            storage.Fail = false; writes = storage.Writes;
            Check(cheats.ActivateDragonAltars() && arc.LitCount == 3 && arc.CanSummon && storage.Writes == writes + 1,
                "Three altars and missing story prerequisites commit in one save");
            var savedAltars = JsonUtility.FromJson<InventoryProfile>(storage.Payload);
            Check(DragonArcRules.CanSummon(savedAltars, data) && QuestRules.ValidSave(savedAltars) &&
                !DragonArcRules.Has(savedAltars, data.awakening, 3) && arc.Encounter == null,
                "Saved altars survive serialization and still require manual summoning");
            var preserved = JsonUtility.FromJson<InventoryProfile>(beforeAltars);
            Check(savedAltars.equipped.SequenceEqual(preserved.equipped) && savedAltars.claimedRewards.SequenceEqual(preserved.claimedRewards) &&
                savedAltars.weapons.Count == preserved.weapons.Count && savedAltars.questCoins == preserved.questCoins,
                "Altar cheat preserves equipment, inventory and rewards");
            writes = storage.Writes;
            Check(cheats.ActivateDragonAltars() && storage.Writes == writes, "Repeated altar activation does not write or duplicate progress");
            profileField.SetValue(inventory, JsonUtility.FromJson<InventoryProfile>(beforeAltars));
            Check(inventory.TryAdvanceDragonArc(data, DragonArcStep.Arrival) && inventory.TryAdvanceDragonArc(data, DragonArcStep.Warning) &&
                inventory.TryAdvanceDragonArc(data, DragonArcStep.Audience) && inventory.TryAdvanceDragonArc(data, DragonArcStep.Forest), "Prepare partially activated altars");
            writes = storage.Writes;
            Check(cheats.ActivateDragonAltars() && arc.LitCount == 3 && storage.Writes == writes + 1, "Altar cheat resumes partial quest progress");
            Check(inventory.TryAdvanceDragonArc(data, DragonArcStep.Summoned), "Normal summoning completes the cheat-unlocked quest");
            writes = storage.Writes;
            Check(cheats.ActivateDragonAltars() && storage.Writes == writes && inventory.QuestState(data.awakening).completed,
                "Altar cheat preserves an already completed ritual");
            cheats.SetActive(false); Object.DestroyImmediate(arc);
            var full = initial.Copy();
            while (full.weapons.Count < 256) full.weapons.Add(new OwnedWeapon { instanceId = Guid.NewGuid().ToString("N"), definitionId = initial.weapons[0].definitionId });
            profileField.SetValue(inventory, full);
            Check(inventory.TryGrantCheatWeapons(), "A full inventory still accepts the arsenal through persistent pending loot");
            var overflow = JsonUtility.FromJson<InventoryProfile>(storage.Payload);
            Check(overflow.weapons.Count == 256 && overflow.pendingLoot.Count > 0 && catalog.weapons.All(w =>
                overflow.weapons.Any(owned => owned.definitionId == w.Id) || overflow.pendingLoot.Any(p => p.HasWeapon && p.weapon.definitionId == w.Id)),
                "Overflow preserves existing possessions and every granted definition");
            Check(overflow.pendingLoot.All(p => p.y < 100), "Weapons requested in flight fall back to loot at ground level");
            writes = storage.Writes;
            Check(inventory.TryGrantCheatWeapons() && storage.Writes == writes, "Pending weapons are not duplicated by repeat grants");
            Check(typeof(IEnemyDamageTarget).IsAssignableFrom(typeof(EnemyController)) &&
                typeof(IEnemyDamageTarget).IsAssignableFrom(typeof(BossController)) &&
                typeof(IEnemyDamageTarget).IsAssignableFrom(typeof(DragonBossController)), "All enemy controller families support one-hit kills");
            var bossObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/DragonBosses/SoulEater_PhaseOne.prefab"), new Vector3(800, 100, 800), Quaternion.identity);
            yield return null;
            var boss = bossObject.GetComponent<SoulEaterPhaseOneController>();
            var receiver = boss.GetComponent<DamageReceiver>(); var life = boss.GetComponent<Health>();
            int deaths = 0, defeated = 0; GameObject credited = null;
            life.Died += hit => { deaths++; credited = hit.Source; }; boss.Defeated += () => defeated++;
            life.GetComponent<Presentation.ActorCombatVisuals>().DelayDeathEffect(120);
            var attackSource = new GameObject("Player weapon source"); attackSource.transform.SetParent(player.transform, false);
            DamageInfo Strike(bool ranged = false, bool area = false, GameObject source = null, float amount = 1) =>
                new DamageInfo(amount, source != null ? source : attackSource, boss.transform.position, Vector3.forward, AttackIdentity.Next(), ranged: ranged, area: area);
            Check(receiver.Resolve(Strike()).HealthDamage < 10 && !life.IsDead, "Ordinary attacks retain normal damage with cheats off");
            Check(cheats.SetActive(true) && cheats.ToggleOneHitKills() && cheats.OneHitKills, "One-hit kills can be enabled explicitly");
            Check(!cheats.AdvanceSoulEaterPhase() && !life.IsDead, "Phase cheat does not activate a dormant boss");
            boss.BeginEncounter(player.transform);
            var inputOwner=new object();
            Check(Presentation.GameplayPause.TryBlockInput(inputOwner), "Cinematic input lock acquired for phase cheat check");
            Check(!cheats.AdvanceSoulEaterPhase() && boss.Phase==1, "Phase cheat respects cinematic input ownership");
            Presentation.GameplayPause.ReleaseInput(inputOwner);yield return null;
            int transitions=0;boss.PhaseTwoRequested+=()=>transitions++;
            Check(cheats.AdvanceSoulEaterPhase() && boss.State==SoulEaterState.PhaseTransition && !life.IsDead &&
                Mathf.Approximately(life.Normalized,boss.Settings.phaseThreshold) && deaths==0 && defeated==0,
                "Phase cheat starts the roar at the configured health threshold without triggering one-hit death or rewards");
            Check(!cheats.AdvanceSoulEaterPhase(), "Repeated input cannot restart the transition");
            for(int i=0;i<600 && boss.Phase!=2;i++)boss.Tick(1f/60);
            Check(boss.Phase==2 && transitions==1 && boss.Target==player.transform && !life.IsDead,
                "Normal roar completion starts phase two once and preserves the combat target");
            float phaseLife=life.Current;
            Check(!cheats.AdvanceSoulEaterPhase() && life.Current==phaseLife && transitions==1, "Phase two input does not reset health or repeat the transition");
            boss.ResetEncounter();boss.BeginEncounter(player.transform);
            life.ApplyDamage(new DamageInfo(life.Maximum*.7f,null,boss.transform.position,Vector3.zero));float lowLife=life.Current;
            Check(boss.TryStartAttack(SoulEaterAction.Breath) && cheats.AdvanceSoulEaterPhase() &&
                boss.State==SoulEaterState.PhaseTransition && boss.Action==SoulEaterAction.None && life.Current==lowLife,
                "Phase cheat interrupts an attack without healing an already weakened boss");
            boss.ResetEncounter();
            var invulnerable = boss.GetComponent<Invulnerability>() ?? boss.gameObject.AddComponent<Invulnerability>();
            typeof(DamageReceiver).GetField("invulnerability", Private).SetValue(receiver, invulnerable);
            invulnerable.StartWindow(60);
            Check(receiver.Resolve(Strike(amount: 0)).Outcome == HitOutcome.Ignored && !life.IsDead, "Zero-damage contacts cannot kill");
            var attack = Strike();
            float remaining = life.Current;
            var result = receiver.Resolve(attack);
            Check(life.IsDead && result.HealthDamage == remaining && deaths == 1 && defeated == 1 && credited == attackSource && boss.State == SoulEaterState.Dead,
                "One melee hit defeats the real Soul Eater through invulnerability with normal death attribution");
            Check(receiver.Resolve(attack).Outcome == HitOutcome.Ignored && deaths == 1, "Repeated contacts cannot duplicate death or boss rewards");
            boss.enabled = false;
            foreach (bool area in new[] { false, true })
            { life.Revive(); Check(receiver.Resolve(Strike(ranged: !area, area: area)).Outcome == HitOutcome.Hit && life.IsDead, "Ranged/area hits honor one-hit mode"); }
            life.Revive();
            Check(cheats.ToggleOneHitKills() && !cheats.OneHitKills && receiver.Resolve(Strike()).Outcome == HitOutcome.Invulnerable && !life.IsDead,
                "Turning one-hit off restores ordinary defenses immediately");
            cheats.ToggleOneHitKills();
            var neutral = new GameObject("Non-enemy health"); neutral.AddComponent<Health>(); var neutralReceiver = neutral.AddComponent<DamageReceiver>();
            Check(neutralReceiver.Resolve(Strike()).HealthDamage < 10 && !neutral.GetComponent<Health>().IsDead, "One-hit cheat does not amplify damage against non-enemies");
            Check(receiver.Resolve(Strike(source: neutral)).Outcome == HitOutcome.Invulnerable && !life.IsDead, "Other attack sources do not inherit player cheats");
            cheats.SetActive(false); cheats.SetActive(true);
            Check(!cheats.OneHitKills, "Leaving F8 mode clears one-hit kills");
            cheats.ToggleOneHitKills(); cheats.enabled = false; cheats.enabled = true;
            Check(!cheats.Active && !cheats.OneHitKills, "Disabling the player resets the cheat");
            cheats.SetActive(true); cheats.ToggleOneHitKills();
            var playerHealth = player.GetComponent<Health>(); player.GetComponent<Presentation.ActorCombatVisuals>().DelayDeathEffect(120);
            playerHealth.ApplyDamage(new DamageInfo(playerHealth.Current, neutral, player.transform.position, Vector3.zero));
            Check(!cheats.OneHitKills && !cheats.ToggleOneHitKills(), "Death immediately disables one-hit damage before the next Update");
            Invoke(cheats, "Update"); playerHealth.Revive();
            Check(!cheats.Active && !cheats.OneHitKills && !motor.IsFlying, "Respawn starts with cheats disabled");
            Object.DestroyImmediate(bossObject); Object.DestroyImmediate(neutral); Object.DestroyImmediate(attackSource);
            Object.DestroyImmediate(floor);
            if (SessionState.GetBool(Key + ".Organization", true))
                Type.GetType("ProjectOrganizationChecks, Assembly-CSharp-Editor", true).GetMethod("Run").Invoke(null, null);
            yield break;
        }
    }
}
