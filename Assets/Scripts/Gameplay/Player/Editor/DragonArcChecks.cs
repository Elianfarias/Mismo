using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Core;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Quests;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    [InitializeOnLoad]
    public static class DragonArcChecks
    {
        const string Key="Mismo.DragonArcChecks",Output="output/dragon-arc";
        static readonly List<string> report=new List<string>();
        static readonly List<string> runtimeErrors=new List<string>();
        static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        static int frame=-1;
        static double deadline;
        static Memory storage;
        static SoulEaterAnimationFrameCheck activeFrameCheck;
        sealed class Memory:IProfileRepository
        {
            public string data;public bool fail;
            public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=data;return data==null?ProfileReadResult.Missing:validate(data)?ProfileReadResult.Loaded:ProfileReadResult.Invalid;}
            public void Write(string value){if(fail)throw new IOException("Expected dragon arc save failure");data=value;}
        }
        static DragonArcChecks(){EditorApplication.playModeStateChanged+=Mode;}
        static void Require(bool value,string label){if(!value)throw new Exception(label);report.Add("PASS "+label);}
        public static void RunAndBuild(){SessionState.SetBool(Key+".Build",true);Run();}
        [MenuItem("Mismo/Historia/Verificar arco del dragón")]
        public static void Run()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory(Output);report.Clear();Rules();
            File.WriteAllLines(Output+"/rules.txt",report);
            EditorSceneManager.OpenScene("Assets/Scenes/VoxelRegion_7319.unity");SessionState.SetInt(Key,1);EditorApplication.EnterPlaymode();
        }
        static void Rules()
        {
            var d=DragonArcSetup.Load<DragonArcDefinition>(DragonArcSetup.DataPath);
            Require(d.Valid && DragonArcDefinition.BossPhases==2 && d.beacon==null,"Datos: dos fases; arte de faros pendiente");
            var orders=new[]{new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
            foreach(var order in orders)
            {
                var p=new InventoryProfile();
                Require(!DragonArcRules.Apply(p,d,DragonArcStep.Forest)&&!DragonArcRules.CanSummon(p,d),"No hay faros ni ritual antes de la audiencia");
                Require(DragonArcRules.Apply(p,d,DragonArcStep.Arrival)&&!DragonArcRules.Apply(p,d,DragonArcStep.Arrival),"Llegada persistente y única");
                Require(!DragonArcRules.Apply(p,d,DragonArcStep.Audience),"El vigía explica la amenaza antes del rey");
                Require(DragonArcRules.Apply(p,d,DragonArcStep.Warning)&&DragonArcRules.Apply(p,d,DragonArcStep.Audience),"Audiencia completa y misión principal en una transacción");
                for(int j=0;j<3;j++)
                {
                    var step=DragonArcStep.Forest+order[j];Require(DragonArcRules.Apply(p,d,step),"Activación en orden "+string.Join(",",order)+" / "+j);
                    Require(!DragonArcRules.Apply(p,d,step),"Repetir un faro no suma progreso");
                    p=JsonUtility.FromJson<InventoryProfile>(JsonUtility.ToJson(p));
                    Require(QuestRules.ValidSave(p)&&DragonArcRules.CanSummon(p,d)==(j==2),"Guardar/continuar conserva activaciones y valida tres requisitos");
                }
                Require(DragonArcRules.Apply(p,d,DragonArcStep.Summoned)&&QuestRules.State(p,d.awakening).completed,"La misión termina en el ritual, sin acreditar victoria del jefe");
                Require(!DragonArcRules.Apply(p,d,DragonArcStep.Summoned)&&DragonArcRules.CanSummon(p,d),"Reintento disponible sin duplicar misión ni recompensas");
                Require(p.defeatedEnemies.Count==0&&p.questCoins==0,"Invocar no concede derrota ni botín");
            }
            var s=Object.Instantiate(ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));s.generationVersion=2;s.introductionVersion=1;s.preserveAuthoredCenter=false;
            try
            {
                for(int seed=0;seed<32;seed++)
                {
                    s.seed=seed*9187+71;var terrain=new ExplorationTerrain(s);var layout=terrain.DragonArc;
                    foreach(var p in layout.Beacons.Concat(new[]{layout.Arena,layout.Altar,layout.Retry}))
                    {
                        if(!terrain.Plan.Contains(p.x,p.z,80)||terrain.Plan.Stage(p.x,p.z)!=0||terrain.Plan.BarrierDistance(p.x,p.z)<60 || !terrain.Reserved(p.x,p.z) || Mathf.Abs(terrain.Height(p.x,p.z)-12)>.01f)throw new Exception("Unusable story location seed="+s.seed+" point="+p);
                        var content=new ExplorationContent(s,terrain);
                        var chunk=ExplorationChunks.Coordinate(p);
                        for(int z=-2;z<=2;z++)for(int x=-2;x<=2;x++)foreach(var encounter in content.Encounters(chunk+new Vector2Int(x,z)))
                            if(layout.Reserved(encounter.position.x,encounter.position.z,6))throw new Exception("Encounter occupies story clearing");
                    }
                }
                Require(true,"32 semillas: destinos accesibles en primer bioma, terreno reservado y plano");
                s.introductionVersion=0;Require(new ExplorationTerrain(s).DragonArc==null,"No se inyecta el arco en mundos anteriores al tutorial");
            }
            finally{Object.DestroyImmediate(s);}
            Require(d.flyingVisual.GetComponentInChildren<Health>()==null && d.encounterPrefab.GetComponent<IDragonEncounter>()?.Ready==true,"Sobrevuelo visual sin IA/daño; invocación referencia el boss correcto");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if(SessionState.GetInt(Key,0)!=1)return;
            storage=new Memory();
            typeof(PlayerInventory).GetField("BuildCheckRepository",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,storage);
            typeof(WorldSession).GetField("VerificationDirectory",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Path.GetFullPath(".validation/DragonArcProfiles/"+Guid.NewGuid().ToString("N")));
            if(!WorldSession.NewGame())throw new Exception(WorldSession.LastError);
            var s=WorldSession.Settings(ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings"));var terrain=new ExplorationTerrain(s);var intro=terrain.Introduction;
            WorldSession.SaveIntroduction(5+intro.Definition.encounters.Length,0,intro.Arrival,intro.Arrival,intro.Facing.eulerAngles.y);Object.Destroy(s);
            Application.runInBackground=true;
            runtimeErrors.Clear();Application.logMessageReceived+=RecordError;
        }
        static void RecordError(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeErrors.Add(message+"\n"+trace);}
        static void Mode(PlayModeStateChange mode)
        {
            if(mode==PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(Key,0)==1)
            {report.Clear();stack.Clear();stack.Push(Play());frame=-1;deadline=EditorApplication.timeSinceStartup+450;EditorApplication.update+=Tick;}
            if(mode==PlayModeStateChange.EnteredEditMode && SessionState.GetInt(Key,0)>=2)
            {
                int code=SessionState.GetInt(Key,0)==2?0:1;SessionState.EraseInt(Key);
                bool build=SessionState.GetBool(Key+".Build",false);SessionState.EraseBool(Key+".Build");
                if(code==0&&build){DragonArcBuild.Run();return;}
                if(Application.isBatchMode)EditorApplication.Exit(code);
            }
        }
        static void Tick()
        {
            if(!Application.isPlaying || Time.frameCount==frame)return;frame=Time.frameCount;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Arc check timeout");
                if(stack.Count==0){Finish(true);return;}
                var routine=stack.Peek();if(!routine.MoveNext())stack.Pop();else if(routine.Current is IEnumerator child)stack.Push(child);
            }
            catch(Exception e){report.Add("FAIL "+e);Debug.LogException(e);Finish(false);}
        }
        static void Finish(bool ok)
        {
            activeFrameCheck?.Dispose();activeFrameCheck=null;
            EditorApplication.update-=Tick;File.WriteAllLines(Output+"/play.txt",report);
            Debug.Log(ok?"DRAGON_ARC_CHECKS_OK":"DRAGON_ARC_CHECKS_FAILED");SessionState.SetInt(Key,ok?2:3);EditorApplication.ExitPlaymode();
        }
        static IEnumerator Until(Func<bool> condition,string label,float seconds=45)
        {
            double end=EditorApplication.timeSinceStartup+seconds;
            while(!condition()){if(EditorApplication.timeSinceStartup>end)throw new Exception("Timeout: "+label);yield return null;}
        }
        static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        static void Warp(DragonArcCoordinator arc,Vector3 p)
        {arc.GetComponent<PlayerController>().enabled=false;arc.GetComponent<Movement.PlayerMotor>().ResetPosition(p+Vector3.up*.2f);}
        static IEnumerator Play()
        {
            yield return Until(()=>Object.FindFirstObjectByType<DragonArcCoordinator>()!=null,"arco en escena real");
            var arc=Object.FindFirstObjectByType<DragonArcCoordinator>();var d=arc.Data;var inventory=arc.GetComponent<PlayerInventory>();var arrival=arc.GetComponent<DragonArrivalSequence>();
            Require(arc!=null && !arc.ArrivalSeen,"Nueva partida entra al arco después de completar la introducción");
            yield return Until(()=>arrival.Running,"sobrevuelo");
            Require(Time.timeScale==1 && GameplayPause.BlocksInput && GameplayPause.InterfaceHidden && !GameplayPause.IsPaused,"Sobrevuelo bloquea input sin pausar mundo");
            var camera=UnityEngine.Camera.main;var originalPosition=camera.transform.position;
            yield return CaptureSequence(()=>arrival.Running,"vuelo",.16f);
            yield return Frames(3);
            Require(arc.ArrivalSeen && !GameplayPause.BlocksInput && !GameplayPause.InterfaceHidden && Vector3.Distance(originalPosition,camera.transform.position)<1,"Terminar restaura cámara/control y guarda llegada");
            yield return Until(()=>!arrival.Departing,"salida de vista del sobrevuelo",25);
            var canvas=new GameObject("Cinematic HUD check").AddComponent<Canvas>();
            var inactiveCanvas=new GameObject("Disabled HUD check").AddComponent<Canvas>();inactiveCanvas.enabled=false;
            float fov=camera.fieldOfView;
            Require(arrival.Begin(d,arc.Layout.Lookout,Quaternion.identity,null),"Presentación puede iniciar con input libre");
            Require(GameplayPause.InterfaceHidden&&!canvas.enabled&&!inactiveCanvas.enabled,"Cinemática oculta Canvas sin activar UI previamente deshabilitada");
            GameplayPause.ReleaseInput(new object());
            Require(GameplayPause.InterfaceHidden&&!GameplayPause.TryPause(null),"Otro dueño no libera la cinemática; Escape no abre pausa debajo");
            yield return Frames(3);var beforeSkip=arrival.FlightPosition;arrival.Skip();
            Require(arrival.Departing && Vector3.Distance(arrival.FlightPosition,beforeSkip)<.01f,"Devolver el control no elimina ni teletransporta al dragón");
            yield return Frames(2);
            Require(arrival.Departing && Vector3.Distance(arrival.FlightPosition,beforeSkip)>0,"El dragón continúa volando después de omitir la cámara");
            Require(!GameplayPause.BlocksInput&&!GameplayPause.InterfaceHidden&&canvas.enabled&&!inactiveCanvas.enabled&&Mathf.Approximately(camera.fieldOfView,fov),"Omitir restaura lente y control");
            yield return Until(()=>!arrival.Departing,"salida de vista después de omitir",25);
            arrival.Begin(d,arc.Layout.Lookout,Quaternion.identity,null);yield return Frames(2);arrival.enabled=false;yield return Frames(2);
            Require(!GameplayPause.BlocksInput&&!GameplayPause.InterfaceHidden&&canvas.enabled&&Mathf.Approximately(camera.fieldOfView,fov),"Interrumpir restaura lente y control");arrival.enabled=true;
            Object.Destroy(canvas.gameObject);Object.Destroy(inactiveCanvas.gameObject);
            yield return CheckAnimationFrames(d.flyingVisual);
            Require(!inventory.TryAdvanceDragonArc(d,DragonArcStep.Forest),"No se activa un faro antes de la audiencia");
            Warp(arc,arc.Layout.Lookout);yield return Frames(2);
            Require(arc.Interact(DragonArcRole.Lookout),"Conversación con vigía");
            storage.fail=true;Require(!arc.Confirm()&&!arc.WarningHeard&&arc.DialogueOpen,"Fallo de escritura no adelanta la historia ni cierra el diálogo");
            storage.fail=false;Require(arc.Confirm()&&arc.WarningHeard,"Reintento del guardado de la explicación");yield return Frames(2);
            Warp(arc,arc.Layout.King+introductionFacing(arc)*new Vector3(0,0,-3));yield return Frames(4);Require(arc.Interact(DragonArcRole.King),"Audiencia disponible después del vigía");
            Capture("02-audiencia");Require(arc.Confirm()&&arc.AudienceComplete,"Rey inicia misión de los tres faros");yield return Frames(2);
            Require(Object.FindObjectsByType<DragonArcInteractable>(FindObjectsSortMode.None).Length==6,"Dos NPCs, tres activaciones y un altar; sin guardianes");
            Warp(arc,arc.Layout.Beacons[1]);Require(arc.Interact(DragonArcRole.Ruins)&&arc.Confirm()&&arc.LitCount==1,"Activación de ruinas primero");yield return Frames(2);
            Require(!arc.TrySummon(),"Ritual bloqueado con solo un faro");
            WorldSession.Checkpoint(arc.Layout.Beacons[1]+Vector3.up*.2f,0);
            SceneManager.LoadScene("Assets/Scenes/VoxelRegion_7319.unity");yield return Frames(8);
            arc=Object.FindFirstObjectByType<DragonArcCoordinator>();inventory=arc.GetComponent<PlayerInventory>();arrival=arc.GetComponent<DragonArrivalSequence>();
            Require(arc.ArrivalSeen&&arc.AudienceComplete&&arc.IsLit(1)&&!arrival.Running,"Recargar escena recupera misión/faro sin repetir sobrevuelo");
            Require(Object.FindObjectsByType<DragonArcInteractable>(FindObjectsSortMode.None).Length==6,"Streaming/recarga no duplica contactos");
            foreach(int i in new[]{2,0})
            {Warp(arc,arc.Layout.Beacons[i]);yield return Frames(2);Require(arc.Interact(DragonArcRole.Forest+i)&&arc.Confirm(),"Activación independiente "+i);yield return Frames(2);}
            Require(arc.CanSummon&&arc.LitCount==3,"Tres faros habilitan el altar");
            Warp(arc,arc.Layout.Altar+Vector3.left*2);
            yield return Until(()=>Object.FindFirstObjectByType<ExplorationChunks>().NavigationReady && Physics.Raycast(arc.transform.position+Vector3.up*5,Vector3.down,10),"terreno del altar");
            yield return Frames(5);Require(arc.Encounter==null,"Entrar en arena no invoca automáticamente");
            Require(arc.Interact(DragonArcRole.Altar),"Confirmación explícita del ritual");
            storage.fail=true;Require(!arc.Confirm()&&arc.Encounter==null,"Fallo de guardado impide crear un jefe");storage.fail=false;
            var landingObstacle=new GameObject("Arrival tree collision regression");landingObstacle.transform.position=arc.Layout.Arena+Vector3.forward*4;
            var trunk=GameObject.CreatePrimitive(PrimitiveType.Cube);trunk.transform.SetParent(landingObstacle.transform,false);trunk.transform.localPosition=Vector3.up*5;trunk.transform.localScale=new Vector3(2,10,2);
            WorldDestructible.Attach(landingObstacle,WorldAssetKind.Tree);
            double invocationRequested=Time.realtimeSinceStartupAsDouble;
            Require(arc.Confirm()&&arc.Encounter.IsActive,"Invocación y punto de reintento guardados");
            Require(!arc.TrySummon(),"No hay invocación duplicada");
            yield return Until(()=>GameplayPause.BlocksInput&&!GameplayPause.IsPaused,"entrada animada");
            double invocationLatency=Time.realtimeSinceStartupAsDouble-invocationRequested;
            Require(invocationLatency<1,"Inicio de invocación sin bloqueo de preparación: "+(invocationLatency*1000).ToString("F1")+" ms");
            Require(Time.timeScale==1&&GameplayPause.InterfaceHidden,"Vuelo/aterrizaje/rugido animan durante el bloqueo de control");
            yield return CaptureSequence(()=>GameplayPause.InterfaceHidden&&!GameplayPause.IsPaused,"invocacion",.16f);
            yield return Until(()=>Object.FindFirstObjectByType<SoulEaterPhaseOneController>()?.Target!=null,"inicio combate",30);
            var boss=Object.FindFirstObjectByType<SoulEaterPhaseOneController>();
            Require(Object.FindObjectsByType<SoulEaterPhaseOneController>(FindObjectsSortMode.None).Length==1&&!GameplayPause.BlocksInput&&!GameplayPause.InterfaceHidden,"Un jefe, IA activa después del aterrizaje y control restaurado");
            var arrivalDust=((DragonRegionalEncounter)arc.Encounter).GetComponent<SoulEaterDustVfx>();
            Require(arrivalDust!=null&&arrivalDust.BurstCount==1&&arrivalDust.LastKind==SoulEaterDustKind.Dive,"Invocación emite una sola nube grande de DustExplosion al aterrizar");
            Require(!landingObstacle.activeSelf && WorldImpactRecord.Destroyed(landingObstacle.transform.position),"El aterrizaje rompe el árbol que bloqueaba el pecho y guarda su destrucción");
            Require(WorldSession.Current.dragonImpacts.Any(i=>Vector2.Distance(new Vector2(i.x,i.z),new Vector2(arc.Layout.Arena.x,arc.Layout.Arena.z))<.1f),"La invocación deja un impacto persistente antes de habilitar la IA");
            Require(inventory.QuestState(d.awakening).completed&&arc.CanSummon,"Ritual completado permite reintentos");
            arc.ReturnToAltar();yield return Frames(2);
            Warp(arc,arc.Layout.Altar+Vector3.left*2);yield return Frames(4);
            yield return Until(()=>Object.FindFirstObjectByType<ExplorationChunks>().NavigationReady,"reintento del altar");
            Require(arc.Interact(DragonArcRole.Altar)&&arc.Confirm(),"Reinvocación para probar Omitir");
            yield return Until(()=>GameplayPause.BlocksInput&&!GameplayPause.IsPaused,"entrada antes de omitir");
            var skipped=(DragonRegionalEncounter)arc.Encounter;int impactCount=WorldSession.Current.dragonImpacts.Count;
            double skipRequested=Time.realtimeSinceStartupAsDouble;skipped.Skip();
            yield return Until(()=>skipped.Boss?.Target!=null&&!GameplayPause.BlocksInput,"Omitir restaura combate",5);
            double skipLatency=Time.realtimeSinceStartupAsDouble-skipRequested;
            Require(skipped.GetComponent<SoulEaterDustVfx>().BurstCount==1,"Omitir conserva un único impacto de polvo, sin duplicarlo al crear el boss");
            Require(skipLatency<1,"Omitir devuelve control con jefe activo sin congelamiento: "+(skipLatency*1000).ToString("F1")+" ms");
            Require(!GameplayPause.InterfaceHidden&&Object.FindObjectsByType<SoulEaterPhaseOneController>(FindObjectsSortMode.None).Length==1&&WorldSession.Current.dragonImpacts.Count==impactCount,"Omitir restaura HUD, conserva destrucción y no duplica jefe/impacto");
            boss=skipped.Boss;
            yield return CombatPresentation(arc,boss);
            yield return SoulEaterTreeChecks.Play(arc,boss,Capture);
            yield return SoulEaterPhaseTwoChecks.Play(arc,boss,Capture);
            boss.enabled=false;var old=arc;arc.GetComponent<Health>().ApplyDamage(new DamageInfo(100000,null,arc.transform.position,Vector3.zero));
            yield return Until(()=>Object.FindFirstObjectByType<DragonArcCoordinator>()!=null && Object.FindFirstObjectByType<DragonArcCoordinator>()!=old,"muerte y recarga");
            arc=Object.FindFirstObjectByType<DragonArcCoordinator>();
            Require(arc.CanSummon&&arc.LitCount==3&&Vector3.Distance(arc.transform.position,arc.Layout.Retry)<3&&arc.Encounter==null,"Muerte: retorno al altar, faros conservados y sin jefe duplicado");
            SoulEaterPhaseTwoChecks.VerifyReload(Object.FindFirstObjectByType<ExplorationChunks>());
            SoulEaterTreeChecks.VerifyReload();
            Require(!GameplayPause.BlocksInput&&Time.timeScale==1,"Recarga no deja bloqueados cámara ni controles");
            Require(runtimeErrors.Count==0,"Sin excepciones de ejecución: "+string.Join("\n",runtimeErrors));
            report.Add("DRAGON_ARC_CHECKS_OK");
        }
        static Quaternion introductionFacing(DragonArcCoordinator arc)=>Object.FindFirstObjectByType<WorldIntroduction>().Layout.Facing;
        static IEnumerator CaptureSequence(Func<bool> running,string folder,float interval)
        {
            Directory.CreateDirectory(Output+"/"+folder);float next=0;int index=0;
            Quaternion? firstWing=null;float wingMotion=0;bool screenCaptured=false;
            float closestFlyby=float.PositiveInfinity,measuredSpeed=0;Vector3? previousFlight=null;float previousFlightTime=0;
            double previousYield=0,maxFrameGap=0;
            while(running())
            {
                if(previousYield>0)maxFrameGap=Math.Max(maxFrameGap,Time.realtimeSinceStartupAsDouble-previousYield);
                RequireHiddenInterface();
                if(folder=="vuelo")
                {
                    var arrival=Object.FindFirstObjectByType<DragonArrivalSequence>();var intro=Object.FindFirstObjectByType<WorldIntroduction>();
                    closestFlyby=Mathf.Min(closestFlyby,Vector3.ProjectOnPlane(arrival.FlightPosition-intro.Layout.Village,Vector3.up).magnitude);
                    if(previousFlight.HasValue && arrival.Elapsed>previousFlightTime)measuredSpeed=Vector3.Distance(previousFlight.Value,arrival.FlightPosition)/(arrival.Elapsed-previousFlightTime);
                    previousFlight=arrival.FlightPosition;previousFlightTime=arrival.Elapsed;
                }
                var skins=Object.FindObjectsByType<VoxelRigInstance>(FindObjectsSortMode.None);
                foreach(var rig in skins.Where(r=>r.name.Contains("SoulEater")))
                {
                    var encounter=Object.FindFirstObjectByType<DragonRegionalEncounter>();
                    if(folder=="invocacion" && encounter!=null && encounter.PresentationTime>=2.5f)continue;
                    var wing=rig.surface.bones.First(b=>b.name=="Wing01_Left");
                    if(!firstWing.HasValue)firstWing=wing.localRotation;
                    wingMotion=Mathf.Max(wingMotion,Quaternion.Angle(firstWing.Value,wing.localRotation));
                    if(rig.animator.runtimeAnimatorController!=null)throw new Exception("Sample controller overwrites cinematic animation");
                }
                if(!screenCaptured && index>=8){ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/"+folder+"-hud.png"));screenCaptured=true;}
                if(Time.time>=next){Capture(folder+"/"+(index++).ToString("D3"));next=Time.time+interval;}
                if(folder=="invocacion")
                {
                    var encounter=Object.FindFirstObjectByType<DragonRegionalEncounter>();
                    if(encounter!=null && encounter.PresentationTime>5.25f)
                    {
                        var skin=encounter.GetComponentInChildren<SkinnedMeshRenderer>();
                        if(skin!=null)
                        {
                            // BakeMesh already contains this exported rig's scale; TransformPoint would apply it twice.
                            var mesh=new Mesh();skin.BakeMesh(mesh,false);var lowest=mesh.vertices.Select(v=>skin.transform.position+skin.transform.rotation*v).OrderBy(v=>v.y).First();Object.Destroy(mesh);
                            float ground=Object.FindFirstObjectByType<ExplorationChunks>().SurfaceHeight(lowest);
                            if(lowest.y<ground-.18f)throw new Exception("El dragón atraviesa el suelo durante el rugido: "+lowest.y+" / suelo="+ground);
                        }
                    }
                }
                // Exclude this test's screenshot encoding/BakeMesh work from the gameplay frame gap.
                previousYield=Time.realtimeSinceStartupAsDouble;yield return null;
            }
            if(previousYield>0)maxFrameGap=Math.Max(maxFrameGap,Time.realtimeSinceStartupAsDouble-previousYield);
            if(folder=="invocacion")Require(maxFrameGap<1,"Invocación y final sin congelamientos: pausa máxima entre fotogramas="+(maxFrameGap*1000).ToString("F1")+" ms");
            if(folder=="vuelo")Require(closestFlyby>80 && measuredSpeed>35,"Primer vuelo lejos del pueblo y rápido: distancia mínima="+closestFlyby+" m, velocidad="+measuredSpeed+" m/s");
            Require(wingMotion>25,"Aleteo visible en fotogramas reales: "+folder+" / "+wingMotion.ToString("F1")+" grados");
        }
        static void RequireHiddenInterface()
        {if(!GameplayPause.InterfaceHidden)throw new Exception("Cinematic exposes gameplay interface");}
        static IEnumerator CheckAnimationFrames(GameObject prefab)
        {
            var go=Object.Instantiate(prefab,new Vector3(0,-1000,0),Quaternion.identity);
            var probe=activeFrameCheck=new SoulEaterAnimationFrameCheck(go.GetComponent<VoxelRigInstance>());
            yield return Until(()=>probe.Complete||probe.Failure!=null,"clips survive complete rendered frames");
            string failure=probe.Failure;probe.Dispose();activeFrameCheck=null;Object.Destroy(go);
            Require(failure==null,"17 clips mantienen su pose de Update a LateUpdate sin controlador que los sobrescriba: "+failure);
            yield return Frames(2);
        }
        static IEnumerator CombatPresentation(DragonArcCoordinator arc,SoulEaterPhaseOneController boss)
        {
            var rig=boss.GetComponentInChildren<VoxelRigInstance>();
            Require(rig.animator.runtimeAnimatorController==null,"Combate usa exclusivamente la animación de sus ataques");
            var shield=arc.GetComponent<Invulnerability>()??arc.gameObject.AddComponent<Invulnerability>();shield.StartWindow(30);
            boss.ResetEncounter();Warp(arc,boss.transform.position+boss.transform.forward*10);boss.BeginEncounter(arc.transform);
            Require(boss.TryStartAttack(SoulEaterAction.Breath),"Aliento real después de invocación");
            var camera=UnityEngine.Camera.main;camera.transform.position=boss.transform.position+boss.transform.forward*16+boss.transform.right*12+Vector3.up*6;
            camera.transform.LookAt(boss.transform.position+Vector3.up*2);camera.fieldOfView=48;
            Directory.CreateDirectory(Output+"/combate");int i=0;float end=Time.time+6,next=0;
            while(Time.time<end)
            {
                if(GameplayPause.InterfaceHidden)throw new Exception("Combat HUD remains hidden");
                if(Time.time>=next){Capture("combate/"+(i++).ToString("D3"));next=Time.time+.16f;}
                if(i==12)ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output+"/combate-hud.png"));
                yield return null;
            }
            shield.Cancel();
            Require(true,"Aliento y recuperación en fotogramas normales del encuentro real; estado del HUD restaurado");
        }
        static void Capture(string name)
        {
            var c=UnityEngine.Camera.main;if(c==null)return;var old=c.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());}
            finally{c.targetTexture=old;RenderTexture.active=active;rt.Release();Object.Destroy(rt);Object.Destroy(texture);}
        }
    }
}
