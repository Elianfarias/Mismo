using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Core;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Movement;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class HammerChecks
    {
        const string Pending="Mismo.WarhammerValidation";
        const string MotionOnly="Mismo.WarhammerMotionOnly";
        static IEnumerator routine;static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();static int frame=-1;static double deadline;
        const string PreviousStart="Mismo.WarhammerPreviousPlayScene";
        static readonly List<string> log=new List<string>();
        static readonly Dictionary<AudioClip,int> sounds=new Dictionary<AudioClip,int>();
        static void Heard(AudioClip clip,float volume){if(clip!=null&&volume>0)sounds[clip]=sounds.TryGetValue(clip,out var count)?count+1:1;}
        static int HeardCount(AudioClip clip)=>clip!=null&&sounds.TryGetValue(clip,out var count)?count:0;
        static void Check(bool pass,string message){if(!pass)throw new Exception(message);log.Add("PASS "+message);Debug.Log("WARHAMMER_CHECK "+message);}
        public static void RunBatch()
        {
            if(!Application.isBatchMode)throw new Exception("Ejecutar en batch; no reemplaza la escena abierta del usuario.");
            StaticChecks();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        public static void RunMotionInteractive(){SessionState.SetBool(MotionOnly,true);RunInteractive();}
        public static void RunInteractive()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Salir de Play Mode para validar.");
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Hay una escena con cambios sin guardar. La prueba no la guarda ni la reemplaza.");
            StaticChecks();const string path="Assets/Scenes/Validation/WarhammerValidation.unity";
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(path)==null)
            {
                HammerAssets.Folder("Assets/Scenes/Validation");var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene,path);EditorSceneManager.CloseScene(scene,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
            }
            SessionState.SetString(PreviousStart,AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]static void Resume(){if(!SessionState.GetBool(Pending,false))return;deadline=EditorApplication.timeSinceStartup+240;EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup>deadline){Finish(false,"Timeout");return;}
            if(!Application.isPlaying||Time.frameCount<5||Time.frameCount==frame)return;frame=Time.frameCount;
            try
            {
                if(routine==null){routine=Run();stack.Push(routine);}
                if(stack.Count==0){Finish(true,"Pruebas funcionales completas.");return;}
                var current=stack.Peek();if(!current.MoveNext())stack.Pop();else if(current.Current is IEnumerator nested)stack.Push(nested);
            }catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool pass,string message)
        {
            AudioEvents.OnPlayAbilitySFX-=Heard;SessionState.SetBool(Pending,false);EditorApplication.update-=Tick;Directory.CreateDirectory(HammerAssets.Output);
            File.WriteAllText(HammerAssets.Output+(SessionState.GetBool(MotionOnly,false)?"/locomotion.txt":"/playmode.txt"),(pass?"PASS":"FAIL")+"\n"+string.Join("\n",log)+"\n"+message);
            SessionState.SetBool(MotionOnly,false);
            if(Application.isBatchMode)EditorApplication.Exit(pass?0:1);
            else {EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(PreviousStart,""));EditorApplication.ExitPlaymode();}
        }
        static WeaponDefinition Weapon()=>AssetDatabase.LoadAssetAtPath<WeaponDefinition>(HammerAssets.Data+"/Hammer.asset");
        public static void StaticChecks()
        {
            var w=Weapon();var kit=HammerAssets.Kit(w);
            Check(kit.Length==6&&kit.All(a=>a!=null)&&kit.Select(a=>a.Id).Distinct().Count()==6,"Seis definiciones únicas, con IDs persistentes");
            Check(Enum.GetValues(typeof(AbilitySlot)).Length==4&&w.family.SkillCount==5,"Básico + cinco técnicas; cuatro entradas activas existentes");
            Check(kit.Select(a=>a.icon).Distinct().Count()==6&&kit.All(a=>a.icon!=null&&!string.IsNullOrWhiteSpace(a.description)),"Seis iconos y descripciones propios");
            foreach(var a in kit)
            {
                var binding=w.family.animations.Find(a);Check(binding!=null&&binding.clip!=null&&binding.clip.humanMotion,a.name+" usa Humanoid real");
                Check(binding.maskMode==ActionMaskMode.FullBody&&binding.blendSeconds>=.08f,a.name+" cuerpo completo y mezcla");
                Check(Mathf.Abs(binding.clip.length-a.Duration)<.002f&&Mathf.Abs(binding.activeStartsAt-a.preparation/a.Duration)<.001f,a.name+" comparte reloj de animación y ejecución");
                if(a.usesSwordCombo)
                {
                    Check(a==kit[0]&&a.comboSteps.Length==2&&binding.comboClips.Length==2,"Básico con dos etapas dentro de una sola habilidad");
                    for(int i=0;i<2;i++)Check(binding.comboClips[i]!=null&&binding.comboClips[i].humanMotion&&Mathf.Abs(binding.comboClips[i].length-a.comboSteps[i].Duration)<.002f&&a.comboSteps[i].impactSfx!=null,"Etapa básica "+i+" sincronizada con clip Humanoid y sonido");
                }
                else
                {
                    var action=a.actions.OfType<HammerImpactAction>().Single();Check(action.hitTimes.Length==(a.Id=="hammer.double-spin"?4:a.Id=="hammer.sweep"?3:1)&&action.hitTimes.All(t=>t>=0&&t<a.active),a.name+" ventanas exactas de impacto");
                    Check(a.executionSfx!=null&&action.impactSfx!=null,a.name+" swing e impacto asignados");
                }
            }
            Check(w.isTwoHanded&&w.visualPrefab!=null&&w.poseProfile.equipped.anchor==WeaponAnchor.RightHand&&w.poseProfile.holstered.anchor==WeaponAnchor.Character,"Modelo y ambas poses conectados");
            Check(w.poseProfile.maintainSupportGrip&&Vector3.Distance(w.poseProfile.supportGrip,new Vector3(0,.30f,0))<.001f,"Agarre secundario del Warhammer se conserva durante blending");
            var ids=w.family.repertoire.Select(a=>a.Id).ToArray();
            var old=new MasteryProgress{familyId="hammer",level=1,equippedAbilities=new[]{"hammer.heavy","hammer.slam","hammer.double-spin"}};
            Check(old.IsValid()&&old.equippedAbilities.All(id=>w.family.UnlockLevel(w.family.FindSkill(id))==1),"Selección antigua sigue válida en nivel 1");
            var mastery=new MasteryProgress{familyId="hammer",level=6};Check(mastery.TrySelectAbility(ids,4,1)&&mastery.TrySelectAbility(ids,3,0),"Avance y barrido seleccionables mediante el repertorio");
            var saved=JsonUtility.FromJson<MasteryProgress>(JsonUtility.ToJson(mastery));Check(saved.IsValid()&&saved.equippedAbilities.SequenceEqual(mastery.equippedAbilities),"Roundtrip de IDs de selección");
            foreach(var path in AssetDatabase.FindAssets("t:WeaponDefinition").Select(AssetDatabase.GUIDToAssetPath))
            {
                var other=AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                Check(other.GetAbility((AbilitySlot)99)==null,other.name+" ignora slots fuera de rango");
            }
            var legacy=ScriptableObject.CreateInstance<WeaponDefinition>();legacy.abilities=new[]{kit[0]};legacy.overrideFamilyAbilities=true;
            Check(legacy.GetAbility(AbilitySlot.Basic)==kit[0]&&legacy.GetAbility(AbilitySlot.R)==null,"Arma legacy con una habilidad sigue válida");Object.DestroyImmediate(legacy);
            File.WriteAllLines(HammerAssets.Output+"/static.txt",log);
        }
        sealed class Memory:IProfileRepository
        {
            public string payload;
            public ProfileReadResult Read(Func<string,bool> validate,out string value){value=payload;return value==null?ProfileReadResult.Missing:validate(value)?ProfileReadResult.Loaded:ProfileReadResult.Invalid;}
            public void Write(string value){payload=value;}
        }
        static GameObject Player(Memory memory)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Player/Player.prefab"));
            var respawn=go.GetComponent<RegionRespawn>();if(respawn!=null){respawn.enabled=false;Object.DestroyImmediate(respawn);}
            go.GetComponent<PlayerController>().enabled=false;
            var inventory=go.GetComponent<PlayerInventory>()??go.AddComponent<PlayerInventory>();inventory.Initialize(ProjectAssets.Load<ItemCatalog>("ItemCatalog"),memory);return go;
        }
        static Health Target(Vector3 position)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Capsule);go.transform.position=position;var health=go.AddComponent<Health>();go.AddComponent<DamageReceiver>();
            var child=new GameObject("Collider duplicado");child.transform.SetParent(go.transform,false);child.AddComponent<SphereCollider>().radius=.3f;Physics.SyncTransforms();return health;
        }
        static IEnumerator Run()
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(30,1,30);
            var camera=new GameObject("Validation camera").AddComponent<UnityEngine.Camera>();camera.gameObject.AddComponent<AudioListener>();camera.tag="MainCamera";camera.transform.position=new Vector3(3,2,4);camera.transform.LookAt(Vector3.up);
            var memory=new Memory();var player=Player(memory);var inv=player.GetComponent<PlayerInventory>();var w=Weapon();
            Check(inv.IsReady&&!inv.HasSaveProblem,"Inventario aislado; ninguna escritura en partidas del usuario");
            var recipe=AssetDatabase.LoadAssetAtPath<CraftingRecipe>(HammerAssets.Recipe);var settings=ProjectAssets.Load<GatheringSettings>("GatheringSettings");
            Check(ProjectAssets.Load<ItemCatalog>("ItemCatalog").Find(w.Id)==w&&settings.recipes.Contains(recipe),"Registro y receta descubiertos por catálogo actual");
            var station=new GameObject("Banco de prueba").AddComponent<CraftingStation>();station.recipes=settings.recipes;
            foreach(var ingredient in recipe.ingredients)Check(inv.TryGrantMaterial(ingredient.material.id,ingredient.quantity),"Material de receta disponible");
            Check(inv.TryCraft(recipe,null,station),"Obtención por crafting actual");Check(inv.TryEquipDefinition(0,w),"Equipar Warhammer fabricado");
            var profile=JsonUtility.FromJson<InventoryProfile>(memory.payload);profile.progression.GetOrCreate(w.MasteryId).level=6;memory.payload=JsonUtility.ToJson(profile);
            Object.Destroy(player);yield return null;player=Player(memory);inv=player.GetComponent<PlayerInventory>();var loadout=player.GetComponent<EquipmentLoadout>();var runner=loadout.Runner;
            Check(!inv.HasSaveProblem&&loadout.ActiveDefinition.Id==w.Id,"Save/load real del inventario conserva Warhammer equipado");
            yield return null;
            if(SessionState.GetBool(MotionOnly,false)){yield return TestLocomotion(player,loadout,camera);yield break;}
            var kit=HammerAssets.Kit(w);AudioEvents.OnPlayAbilitySFX+=Heard;
            new GameObject("Sol de validación").AddComponent<Light>().type=LightType.Directional;
            RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
            yield return TestBasicCombo(player,loadout,camera);
            foreach(var ability in kit.Skip(1))
            {
                runner.Cancel();yield return WaitCombat(loadout);
                if(ability!=kit[0])
                {
                    int index=w.family.FindSkill(ability.Id);if(inv.SelectedAbility(w,AbilitySlot.Q)!=ability)Check(inv.TrySelectAbility(w,AbilitySlot.Q,index),"Selección Q de "+ability.name);
                }
                var slot=ability==kit[0]?AbilitySlot.Basic:AbilitySlot.Q;
                var action=ability.actions.OfType<HammerImpactAction>().Single();var target=Target((action.singleHitPerCast?Quaternion.Euler(0,-60,0):Quaternion.identity)*new Vector3(0,1,action.singleHitPerCast?1.1f:Mathf.Max(1.1f,action.forward)));
                var rear=ability.Id=="hammer.double-spin"?Target(new Vector3(0,1,-1.1f)):null;
                float multiplier=inv.DamageMultiplier(loadout.ActiveDefinition);
                sounds.Clear();int groundBefore=player.GetComponent<CombatFeedback>().GroundImpactCount;
                Check(runner.TryUse(slot,Vector3.forward,Vector3.zero),ability.name+" inicia desde slot real");
                float speed=runner.Current.AttackSpeed,first=ability.preparation+action.hitTimes[0];
                runner.Tick((first-.002f)/speed);Check(Mathf.Approximately(target.Current,target.Maximum),ability.name+" no daña antes de contacto");
                runner.Tick(.004f/speed);Check(Mathf.Approximately(target.Maximum-target.Current,action.damage*multiplier),ability.name+" primer impacto una vez con múltiples colliders");
                runner.Tick((ability.Duration-first+.01f)/speed);
                Check(Mathf.Approximately(target.Maximum-target.Current,action.damage*(action.singleHitPerCast?1:action.hitTimes.Length)*multiplier),ability.name+" cantidad exacta de impactos con timestep grande");
                Check(HeardCount(ability.executionSfx)==1&&HeardCount(action.impactSfx)==(action.singleHitPerCast?1:action.hitTimes.Length),ability.name+" audio de swing e impactos exacto");
                if(action.groundImpact)Check(player.GetComponent<CombatFeedback>().GroundImpactCount-groundBefore==action.hitTimes.Length,ability.name+" daño y efecto de suelo comparten tick");
                Check(!runner.IsBusy&&runner.Remaining(ability)>0,ability.name+" termina sin perder cooldown");
                if(rear!=null){Check(Mathf.Approximately(rear.Maximum-rear.Current,action.damage*action.hitTimes.Length*multiplier),"Torbellino alcanza también a enemigos detrás del personaje, sin duplicar colliders");Object.Destroy(rear.gameObject);}
                Object.Destroy(target.gameObject);yield return null;
            }
            foreach(var ability in kit)
            {
                yield return WaitCombat(loadout);
                if(ability!=kit[0]&&inv.SelectedAbility(w,AbilitySlot.Q)!=ability)Check(inv.TrySelectAbility(w,AbilitySlot.Q,w.family.FindSkill(ability.Id)),"Selección visual "+ability.name);
                var slot=ability==kit[0]?AbilitySlot.Basic:AbilitySlot.Q;
                Check(runner.TryUse(slot,Vector3.forward,Vector3.zero),"Playback real "+ability.name);
                float elapsed=0;int capture=0;
                float impact=ability.preparation+(ability.usesSwordCombo?0:ability.actions.OfType<HammerImpactAction>().Single().hitTimes[0]);
                float[] times={.12f,ability.preparation*.65f,impact+.025f,ability.preparation+ability.active+.04f,ability.Duration-.04f};
                bool spinning=ability.Id=="hammer.double-spin";
                if(spinning)times=new[]{.12f,ability.preparation*.65f,ability.preparation+.4f,ability.preparation+.8f,ability.preparation+1.25f,ability.preparation+1.7f,ability.Duration-.04f};
                var animated=player.GetComponent<PlayerAnimationDriver>().Animator;float turn=0;Vector3 previousForward=animated.bodyRotation*Vector3.forward;
                while(runner.IsBusy)
                {
                    float dt=Time.deltaTime;runner.Tick(dt);elapsed+=dt;yield return null;
                    if(spinning){Vector3 forward=animated.bodyRotation*Vector3.forward;turn+=Vector3.SignedAngle(Vector3.ProjectOnPlane(previousForward,Vector3.up),Vector3.ProjectOnPlane(forward,Vector3.up),Vector3.up);previousForward=forward;}
                    if(capture<times.Length&&elapsed>=times[capture]){Capture(player,camera,ability.name+"-"+capture);capture++;}
                }
                if(spinning)Check(Mathf.Abs(turn)>600&&Mathf.Abs(turn)<820,"Torbellino gira el cuerpo completo dos vueltas en playback real: "+turn);
            }
            yield return TestInput(player,loadout);
            yield return TestLocomotion(player,loadout,camera);
            yield return WaitCombat(loadout);
            Check(inv.TrySwap(),"Guardar Warhammer cambiando al segundo conjunto");yield return null;Check(inv.TrySwap(),"Volver a equipar Warhammer");
            Check(loadout.ActiveDefinition.Id==w.Id,"Identidad del arma restaurada");
            // Verify collision-aware advance independently of damage samples.
            var charge=kit.Single(a=>a.Id=="hammer.charge");if(inv.SelectedAbility(w,AbilitySlot.Q)!=charge)Check(inv.TrySelectAbility(w,AbilitySlot.Q,w.family.FindSkill(charge.Id)),"Equipar avance");
            runner.Cancel();float ready=Time.time+runner.Remaining(charge)+.02f;while(Time.time<ready)yield return null;
            Vector3 start=player.transform.position;Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,Vector3.zero),"Avance inicia");
            while(runner.IsBusy){runner.Tick(.016f);runner.Motor.Tick(Vector3.zero,false,false,false,.016f);yield return null;}
            float distance=Vector3.ProjectOnPlane(player.transform.position-start,Vector3.up).magnitude;Check(distance>1.5f&&distance<2.05f,"Avance continuo por PlayerMotor: "+distance);
            yield return TestRunStrike(player,loadout);
            yield return WaitCombat(loadout);
            foreach(string name in new[]{"Sword","Bow"})
            {
                var other=AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/"+name+"/"+name+".asset");
                Check(inv.TryEquipDefinition(0,other),name+" equipa después de Warhammer");yield return null;
                var regressionTarget=Target(player.transform.position+Vector3.up+Vector3.forward*(name=="Bow"?4:1.1f));
                Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero,regressionTarget.transform.position),name+" básico ejecuta");
                while(runner.IsBusy){runner.Tick(.016f);yield return null;}
                float until=Time.time+.6f;while(Time.time<until)yield return null;
                Check(regressionTarget.Current<regressionTarget.Maximum,name+" vuelve a infligir daño tras cambiar desde Warhammer");Object.Destroy(regressionTarget.gameObject);
                Check(other.family.SkillCount==6,name+" conserva repertorio de seis técnicas");yield return WaitCombat(loadout);
            }
            Check(inv.TryEquipDefinition(0,w),"Volver al Warhammer después de Sword y Bow");
            string selected=inv.SelectedAbility(w,AbilitySlot.Q).Id;Object.Destroy(player);yield return null;player=Player(memory);
            Check(!player.GetComponent<PlayerInventory>().HasSaveProblem&&player.GetComponent<EquipmentLoadout>().GetAbility(AbilitySlot.Q).Id==selected,"Save/load conserva la selección nueva de habilidades");
            Object.Destroy(player);Object.Destroy(floor);Object.Destroy(camera.gameObject);Object.Destroy(station.gameObject);
        }
        static IEnumerator TestRunStrike(GameObject player,EquipmentLoadout loadout)
        {
            var inv=player.GetComponent<PlayerInventory>();var w=loadout.ActiveDefinition;var runner=loadout.Runner;
            var ability=HammerAssets.Kit(w).Single(a=>a.Id=="hammer.heavy");
            yield return WaitCombat(loadout);
            Check(inv.TrySelectAbility(w,AbilitySlot.Q,w.family.FindSkill(ability.Id)),"Q selecciona Carrera demoledora conservando el ID antiguo");
            foreach(bool blocked in new[]{false,true})
            {
                yield return WaitCombat(loadout);Vector3 start=player.transform.position;
                var target=Target(start+new Vector3(0,1,blocked?2.2f:4.2f));GameObject wall=null;
                if(blocked){wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=start+new Vector3(0,1,1.2f);wall.transform.localScale=new Vector3(3,2,.3f);Physics.SyncTransforms();}
                Check(runner.TryUse(AbilitySlot.Q,Vector3.forward,Vector3.zero),"Carrera inicia con obstáculo: "+blocked);
                while(runner.IsBusy){runner.Tick(.016f);runner.Motor.Tick(Vector3.zero,false,false,false,.016f);yield return null;}
                float moved=Vector3.ProjectOnPlane(player.transform.position-start,Vector3.up).magnitude;
                Check(blocked?moved<1:moved>2.7f&&moved<3.5f,"Carrera respeta distancia y obstáculos: "+moved);
                Check(blocked?Mathf.Approximately(target.Current,target.Maximum):target.Current<target.Maximum,"Carrera remata al objetivo y no golpea a través de una pared: "+blocked);
                Object.Destroy(target.gameObject);if(wall!=null)Object.Destroy(wall);yield return null;
            }
        }
        static IEnumerator TestBasicCombo(GameObject player,EquipmentLoadout loadout,UnityEngine.Camera camera)
        {
            var runner=loadout.Runner;var ability=loadout.GetAbility(AbilitySlot.Basic);
            var combo=player.GetComponentInChildren<BasicSwordCombo>();var hitbox=player.GetComponentInChildren<AttackHitbox>();
            float multiplier=player.GetComponent<PlayerInventory>().DamageMultiplier(loadout.ActiveDefinition);
            foreach(bool chain in new[]{false,true})
            {
                yield return WaitCombat(loadout);sounds.Clear();
                var target=Target(player.transform.position+new Vector3(0,1,1.38f));
                Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero),"Básico inicia para "+(chain?"dos clics":"un clic"));
                float speed=runner.Current.AttackSpeed;
                runner.Tick(.358f/speed);hitbox.EvaluateImpact();Check(Mathf.Approximately(target.Current,target.Maximum),"Combo no daña durante anticipación");
                runner.Tick(.004f/speed);hitbox.EvaluateImpact();
                Check(Mathf.Approximately(target.Maximum-target.Current,22*multiplier),"Primer golpe básico aplica 22 una vez pese a colliders duplicados");
                if(chain)Check(runner.TryUse(AbilitySlot.Basic,Vector3.forward,Vector3.zero),"Segundo clic se conserva en la ventana de encadenado");
                bool second=false;int captured=0;float[] captureAt={.24f,.39f,.62f,.92f};
                while(runner.IsBusy)
                {
                    runner.Tick(.016f/speed);hitbox.EvaluateImpact();
                    second|=combo.CurrentStepIndex==1;
                    yield return null;
                    if(chain&&captured<captureAt.Length&&combo.CurrentStepIndex==1&&combo.CurrentStepNormalized>captureAt[captured])
                    {
                        target.GetComponent<Renderer>().enabled=false;
                        Capture(player,camera,"Basic-return-"+captured);captured++;
                        if(combo.CurrentStepNormalized>.6f)Check(!player.GetComponent<SwordAnimationFeedback>().ImpactVisible,"La estela del combo se apaga durante recuperación");
                    }
                }
                Check(second==chain,"Un clic da un golpe; dos clics habilitan el segundo: "+chain);
                Check(Mathf.Approximately(target.Maximum-target.Current,(chain?48:22)*multiplier),"Daño exacto de la cadena básica: "+(chain?48:22));
                Check(HeardCount(ability.executionSfx)==(chain?2:1)&&HeardCount(ability.comboSteps[0].impactSfx)==(chain?2:1),"Swing e impacto una vez por etapa conectada");
                Object.Destroy(target.gameObject);yield return null;
            }
        }
        static IEnumerator TestLocomotion(GameObject player,EquipmentLoadout loadout,UnityEngine.Camera camera)
        {
            var input=player.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            var controller=player.GetComponent<PlayerController>();
            var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            var settings=UnityEngine.InputSystem.InputSystem.settings;
            var background=settings.backgroundBehavior;var editor=settings.editorInputBehaviorInPlayMode;
            bool run=Application.runInBackground;
            try
            {
                Application.runInBackground=true;
                settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                input.SwitchCurrentControlScheme("Keyboard&Mouse",keyboard,mouse);input.actions.FindActionMap("Player").Enable();
                player.GetComponent<Mismo.Gameplay.Player.Input.PlayerInputReader>().SendMessage("Start");
                controller.Configure(camera.transform);controller.enabled=true;
                foreach(bool sprint in new[]{false,true})
                {
                    yield return WaitCombat(loadout);
                    var held=sprint?new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.LeftShift):new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,held);
                    float until=Time.time+.8f;while(Time.time<until)yield return null;
                    Capture(player,camera,sprint?"Run-before":"Walk-before");
                    Check(loadout.Runner.Motor.Speed>1,"Locomoción mediante PlayerController e input simulado");
                    Check(loadout.Runner.TryUse(AbilitySlot.Basic,loadout.Runner.Motor.Facing,Vector3.zero),"Ataque desde "+(sprint?"Run":"Walk"));
                    while(loadout.Runner.IsBusy)yield return null;
                    until=Time.time+.3f;while(Time.time<until)yield return null;
                    Capture(player,camera,sprint?"Run-return":"Walk-return");
                    var driver=player.GetComponent<PlayerAnimationDriver>();
                    Check(driver.ActionClip==null,"Recuperación vuelve a locomoción sin mantener el clip de ataque");
                    float tilt=Vector3.Angle(driver.Animator.transform.up,Vector3.up); Check(tilt<30,"Inclinación de locomoción estable después del ataque: "+tilt);
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
                    until=Time.time+.6f;while(Time.time<until)yield return null;
                }
            }
            finally
            {
                controller.enabled=false;
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
                settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editor;Application.runInBackground=run;
            }
        }
        static IEnumerator TestInput(GameObject player,EquipmentLoadout loadout)
        {
            var input=player.GetComponent<UnityEngine.InputSystem.PlayerInput>();var controller=player.GetComponent<PlayerController>();
            var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var settings=UnityEngine.InputSystem.InputSystem.settings;var background=settings.backgroundBehavior;var editor=settings.editorInputBehaviorInPlayMode;
            bool run=Application.runInBackground;
            try
            {
                Application.runInBackground=true;settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                input.SwitchCurrentControlScheme("Keyboard&Mouse",keyboard,mouse);input.actions.FindActionMap("Player").Enable();
                player.GetComponent<Mismo.Gameplay.Player.Input.PlayerInputReader>().SendMessage("Start");
                controller.Configure(UnityEngine.Camera.main.transform);
                foreach(AbilitySlot slot in Enum.GetValues(typeof(AbilitySlot)))
                {
                    while(loadout.Runner.Remaining(loadout.GetAbility(slot))>0)yield return null;
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState());
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());UnityEngine.InputSystem.InputSystem.Update();
                    if(slot==AbilitySlot.Basic)UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                    else UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(slot==AbilitySlot.Q?UnityEngine.InputSystem.Key.Q:slot==AbilitySlot.E?UnityEngine.InputSystem.Key.E:UnityEngine.InputSystem.Key.R));
                    UnityEngine.InputSystem.InputSystem.Update();
                    typeof(PlayerController).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(controller,null);
                    Check(loadout.Runner.Current?.Definition==loadout.GetAbility(slot),"Input físico "+slot+" atraviesa PlayerController y EquipmentLoadout");
                    if(slot==AbilitySlot.Basic)
                    {
                        loadout.Runner.Tick(.4f/loadout.Runner.Current.AttackSpeed);
                        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState());UnityEngine.InputSystem.InputSystem.Update();
                        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));UnityEngine.InputSystem.InputSystem.Update();
                        typeof(PlayerController).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(controller,null);
                        var combo=player.GetComponentInChildren<BasicSwordCombo>();bool second=false;
                        while(loadout.Runner.IsBusy){loadout.Runner.Tick(.016f);second|=combo.CurrentStepIndex==1;yield return null;}
                        Check(second,"Dos pulsaciones físicas de clic izquierdo ejecutan las dos etapas");
                    }
                    loadout.Runner.Cancel();
                }
            }
            finally{UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);settings.backgroundBehavior=background;settings.editorInputBehaviorInPlayMode=editor;Application.runInBackground=run;}
        }
        static void Capture(GameObject player,UnityEngine.Camera camera,string name)
        {
            camera.transform.position=player.transform.position+new Vector3(3,2,4);
            camera.transform.LookAt(player.transform.position+Vector3.up);
            // EditorApplication.update is outside the player's LateUpdate sequence.
            // Align the visual with the sampled bones before rendering a moving character.
            player.GetComponent<WeaponPresentation>().SendMessage("LateUpdate");
            var visual=player.GetComponent<WeaponPresentation>().ActiveVisual;
            var animator=player.GetComponentInChildren<Animator>();
            if(visual!=null)
            {
                Vector3 head=visual.TransformPoint(new Vector3(0,1.22f,0));
                float grip=Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftHand).position,visual.TransformPoint(new Vector3(0,.30f,0)));
                File.AppendAllText(HammerAssets.Output+"/runtime-poses.txt",name+" head="+head.ToString("F3")+" grip="+grip.ToString("F3")+"\n");
                Check(grip<.08f,name+" ambas manos permanecen en el mango: "+grip);
            }
            var rt=new RenderTexture(640,640,24);var previous=RenderTexture.active;var target=camera.targetTexture;
            var image=new Texture2D(640,640,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();File.WriteAllBytes(HammerAssets.Output+"/runtime-"+name+".png",image.EncodeToPNG());}
            finally{camera.targetTexture=target;RenderTexture.active=previous;Object.Destroy(rt);Object.Destroy(image);}
        }
        static IEnumerator WaitCombat(EquipmentLoadout loadout){while(loadout.InCombat)yield return null;}
        public static void RenderBatch(){try{Render(false);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void AuditBatch(){try{Render(true);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void Render(bool audit)
        {
            Directory.CreateDirectory(HammerAssets.Output);var w=Weapon();var paths=new List<string>();
            if(audit)
            {
                paths.AddRange(new[]{HammerAssets.Clips+"/Hammer_Attack.anim",HammerAssets.Clips+"/Hammer_GroundSlam.anim",HammerAssets.Clips+"/Hammer_DoubleSpin.anim","Assets/Art/Animations/HumanoidAttacks/DualSwords/Dual_Torbellino.anim"});
                paths.AddRange(AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Art/Animations/WeaponCombat"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.Contains("2H")||p.Contains("Polearm")||p.Contains("Enemy_Attack_1_Run_InPlace")).Distinct());
            }
            else
            {
                paths.AddRange(HammerAssets.Kit(w).Select(a=>AssetDatabase.GetAssetPath(w.family.animations.Find(a).clip)));
                paths.Add(HammerAssets.Clips+"/Hammer_Attack_Return.anim");
                paths.AddRange(new[]{"Idle","Walk","Run"}.Select(m=>HammerAssets.Clips+"/Hammer_"+m+".anim"));
                paths.Add(QuaterniusHumanoidLocomotion.OutputFolder+"/Quaternius_Idle.anim");
            }
            var material=new Material(Shader.Find("Standard")){color=new Color(.65f,.69f,.72f)};
            using(var rig=new HumanoidAttackRig(AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusHumanoidLocomotion.ModelPath)))
            {
                var preview=new PreviewRenderUtility();var meshes=new List<Mesh>();Material hammerMaterial=null;
                try
                {
                    preview.AddSingleGO(rig.Container);var skins=rig.Container.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach(var skin in skins)
                    {
                        var snapshot=new GameObject("Snapshot",typeof(MeshFilter),typeof(MeshRenderer));snapshot.transform.SetParent(skin.transform,false);
                        Vector3 s=skin.transform.lossyScale;snapshot.transform.localScale=new Vector3(1/s.x,1/s.y,1/s.z);var mesh=new Mesh();meshes.Add(mesh);
                        snapshot.GetComponent<MeshFilter>().sharedMesh=mesh;snapshot.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials.Select(_=>material).ToArray();skin.enabled=false;
                    }
                    var weapon=Object.Instantiate(w.visualPrefab,rig.Container.transform);
                    // PreviewRenderUtility uses the built-in renderer; use a disposable preview material for URP assets.
                    hammerMaterial=new Material(material){color=new Color(.45f,.37f,.26f)};
                    foreach(var r in weapon.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(_=>hammerMaterial).ToArray();
                    preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.055f,.065f,.08f);preview.camera.orthographic=true;preview.camera.orthographicSize=1.85f;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=30;
                    preview.camera.transform.position=new Vector3(3.6f,2.3f,6);preview.camera.transform.LookAt(new Vector3(0,1.05f,0));preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);preview.lights[1].intensity=.8f;
                    var positions=new List<string>();
                    foreach(string path in paths)
                    {
                        var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__"));if(clip==null)continue;
                        var binding=w.family.animations.actions.FirstOrDefault(b=>b.clip==clip);var a=binding?.ability;
                        float[] times=a!=null&&!audit?new[]{0,a.preparation*.65f,a.preparation,a.preparation+a.active*.75f,a.preparation+a.active,a.Duration-.001f}:new[]{0,.2f*clip.length,.4f*clip.length,.6f*clip.length,.8f*clip.length,clip.length-.001f};
                        var sheet=new Texture2D(320*6,420,TextureFormat.RGB24,false);
                        try
                        {
                            for(int frame=0;frame<times.Length;frame++)
                            {
                                if(clip.humanMotion)rig.Sample(clip,times[frame]);else{rig.StopSampling();clip.SampleAnimation(rig.Animator.gameObject,times[frame]);}
                                for(int s=0;s<skins.Length;s++)skins[s].BakeMesh(meshes[s]);
                                var attachment=!audit&&clip.name=="Quaternius_Idle"?w.poseProfile.holstered:w.poseProfile.equipped;
                                preview.camera.transform.position=attachment==w.poseProfile.holstered?new Vector3(3.6f,2.3f,-6):new Vector3(3.6f,2.3f,6);
                                preview.camera.transform.LookAt(new Vector3(0,1.05f,0));
                                var anchor=attachment.Resolve(rig.Container.transform,rig.Animator);attachment.Apply(weapon.transform,anchor,rig.Animator);
                                positions.Add(clip.name+" t="+times[frame].ToString("F3")+" head="+weapon.transform.TransformPoint(w.poseProfile.trailTip).ToString("F3")+" left grip error="+Vector3.Distance(rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position,weapon.transform.TransformPoint(new Vector3(0,.30f,0))).ToString("F3"));
                                preview.BeginStaticPreview(new Rect(0,0,320,420));preview.Render();var image=preview.EndStaticPreview();sheet.SetPixels(frame*320,0,320,420,image.GetPixels());Object.DestroyImmediate(image);
                            }
                            sheet.Apply();File.WriteAllBytes(HammerAssets.Output+"/"+(audit?"audit-":"")+clip.name+".png",sheet.EncodeToPNG());
                        }
                        finally{Object.DestroyImmediate(sheet);}
                    }
                    File.WriteAllLines(HammerAssets.Output+"/"+(audit?"audit-":"")+"poses.txt",positions);
                }
                finally{preview.Cleanup();foreach(var mesh in meshes)Object.DestroyImmediate(mesh);if(hammerMaterial!=null)Object.DestroyImmediate(hammerMaterial);}
            }
            Object.DestroyImmediate(material);
        }
        public static void BuildBatch(){try{BuildContent();EditorApplication.Exit(0);}catch{EditorApplication.Exit(1);}}
        public static void BuildContent()
        {
            try
            {
                string output=HammerAssets.Output+"/content-build";Directory.CreateDirectory(output);
                var manifest=BuildPipeline.BuildAssetBundles(output,new[]{new AssetBundleBuild{assetBundleName="warhammer",assetNames=new[]{HammerAssets.Data+"/Hammer.asset",HammerAssets.Recipe}}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
                if(manifest==null)throw new Exception("Build fallida");var bundle=AssetBundle.LoadFromFile(output+"/warhammer");if(bundle==null)throw new Exception("No carga bundle");
                try{var w=bundle.LoadAsset<WeaponDefinition>(HammerAssets.Data+"/Hammer.asset");Check(w!=null&&KitValid(w),"Build carga las seis habilidades y dependencias");}finally{bundle.Unload(true);}
                File.WriteAllText(HammerAssets.Output+"/build.txt","PASS build de contenido Windows y carga de dependencias. No sustituye prueba de jugador completo.");
            }catch(Exception e){File.WriteAllText(HammerAssets.Output+"/build.txt",e.ToString());Debug.LogException(e);throw;}
        }
        static bool KitValid(WeaponDefinition w)=>HammerAssets.Kit(w).Length==6&&HammerAssets.Kit(w).All(a=>a.icon!=null&&w.family.animations.Find(a)?.clip!=null&&a.executionSfx!=null&&(!a.usesSwordCombo||w.family.animations.Find(a).comboClips.Length==2&&w.family.animations.Find(a).comboClips.All(c=>c!=null)));
    }
}


