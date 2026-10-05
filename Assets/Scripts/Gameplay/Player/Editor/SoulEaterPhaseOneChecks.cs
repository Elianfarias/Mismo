using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    [InitializeOnLoad]
    public static class SoulEaterPhaseOneChecks
    {
        const string Key="SoulEaterPhaseOneChecks.Pending";
        const string Output="output/soul-eater-phase-one";
        static SoulEaterPhaseOneController boss;
        static GameObject target;
        static Health health;
        static SkinnedMeshRenderer captureSkin;
        static GameObject captureObject;
        static Mesh captureMesh;
        static readonly List<string> report=new List<string>();
        static int waitFrames;
        static bool originalPhaseTwo;
        static SoulEaterPhaseOneChecks(){EditorApplication.playModeStateChanged+=Mode;}
        public static void RunAndBuild(){SessionState.SetBool(Key+".Build",true);Run();}
        public static void RunAndWorld(){SessionState.SetBool(Key+".World",true);Run();}
        [MenuItem("Mismo/Bosses/Soul Eater/Verificar fase 1 en Play")]
        public static void Run()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory(Output);EditorSceneManager.OpenScene(SoulEaterPhaseOneSetup.ScenePath);
            SessionState.SetInt(Key,1);EditorApplication.EnterPlaymode();
        }
        static void Mode(PlayModeStateChange mode)
        {
            if(mode==PlayModeStateChange.EnteredPlayMode&&SessionState.GetInt(Key,0)==1){waitFrames=0;EditorApplication.update+=Wait;}
            if(mode==PlayModeStateChange.EnteredEditMode&&SessionState.GetInt(Key,0)>=2)
            {
                int exit=SessionState.GetInt(Key,0)==2?0:1;SessionState.EraseInt(Key);
                bool build=SessionState.GetBool(Key+".Build",false);SessionState.EraseBool(Key+".Build");
                bool world=SessionState.GetBool(Key+".World",false);SessionState.EraseBool(Key+".World");
                if(exit==0&&world){DragonArcChecks.Run();return;}
                if(exit==0&&build){DragonArcBuild.Run();return;}
                if(Application.isBatchMode)EditorApplication.Exit(exit);
            }
        }
        static void Wait()
        {
            if(++waitFrames<6)return;EditorApplication.update-=Wait;
            try{Check();SessionState.SetInt(Key,2);}
            catch(Exception e){report.Add("FAIL: "+e);Debug.LogException(e);SessionState.SetInt(Key,3);if(boss!=null&&target!=null)try{Capture();}catch(Exception captureError){Debug.LogException(captureError);}}
            finally{if(boss!=null&&boss.Settings!=null)boss.Settings.enablePhaseTwo=originalPhaseTwo;File.WriteAllLines(Output+"/checks.txt",report);EditorApplication.ExitPlaymode();}
        }
        static void Check()
        {
            report.Clear();AudioListener.volume=0;
            boss=Object.FindFirstObjectByType<SoulEaterPhaseOneController>();
            originalPhaseTwo=boss!=null&&boss.Settings!=null&&boss.Settings.enablePhaseTwo;
            Require(boss!=null&&boss.Health.Maximum>100,"Prefab, settings y rig inicializan en Play");
            foreach(var player in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))player.gameObject.SetActive(false);
            Object.FindFirstObjectByType<SoulEaterArena>().enabled=false;
            Object.DestroyImmediate(boss.GetComponent<NavMeshAgent>());
            target=GameObject.CreatePrimitive(PrimitiveType.Capsule);target.name="Combat test target";
            // Feet at y=0, capsule reaches y=2 like the player.
            var collider=target.GetComponent<CapsuleCollider>();collider.center=Vector3.up;
            target.GetComponent<Renderer>().enabled=false;
            health=target.AddComponent<Health>();health.ConfigureMaximum(5000);health.Revive();
            // Manual simulation stays in one Unity frame; defer cosmetic death cubes so their
            // temporary primitive (destroyed at end of frame) cannot obstruct the return test.
            target.GetComponent<Mismo.Gameplay.Player.Presentation.ActorCombatVisuals>().DelayDeathEffect(120);
            target.AddComponent<CombatState>();target.AddComponent<DefenseWindow>();target.AddComponent<DamageReceiver>();

            Reset(new Vector3(0,0,boss.Settings.biteRange*(4f/5.1f)));Attack(SoulEaterAction.Bite);Advance(boss.Settings.biteWindup*.65f);
            Require(health.Current==health.Maximum,"La anticipación de mordida no inflige daño");
            Advance(1);Require(health.Current<health.Maximum,"La mordida alcanza a un jugador a nivel del suelo");
            float after=health.Current;Advance(.3f);Require(health.Current==after,"Mordida: un impacto por objetivo");
            Reset(new Vector3(boss.Settings.biteRange,0,boss.Settings.biteRange*(4f/5.1f)));Attack(SoulEaterAction.Bite);Advance(1.5f);Require(health.Current==health.Maximum,"El flanco queda fuera de la mordida");

            Reset(new Vector3(0,0,boss.Settings.biteRange*(4f/5.1f)));target.transform.rotation=Quaternion.Euler(0,180,0);target.GetComponent<DefenseWindow>().OpenParry(5);
            Attack(SoulEaterAction.Bite);Advance(1.25f);
            Require(health.Current==health.Maximum&&boss.State==SoulEaterState.Staggered,"Parry de mordida evita daño e interrumpe al jefe");
            CombatTimeFeedback.CancelForPause();Time.timeScale=1;

            Reset(new Vector3(0,0,10));Attack(SoulEaterAction.Breath);Advance(1.25f);
            Vector3 preparing=boss.LockedDirection;target.transform.position=new Vector3(6,0,10);Advance(.2f);
            Require(boss.State==SoulEaterState.Windup&&Vector3.Angle(preparing,boss.LockedDirection)>15,"Aliento sigue al jugador hasta el final de la anticipación");
            Advance(.12f);Vector3 locked=boss.LockedDirection;target.transform.position=new Vector3(-7,0,10);Advance(.4f);
            Require(Vector3.Angle(locked,boss.LockedDirection)<.1f,"Fase 1 fija la dirección al comenzar el primer tick de fuego");
            Advance(1.5f);Require(health.Current==health.Maximum,"Salir lateralmente después de la primera llama evita el aliento");
            Reset(new Vector3(0,0,18));Attack(SoulEaterAction.Breath);Advance(3.6f);
            Require(health.Current<=health.Maximum-boss.Settings.breathTickDamage*3,"Aliento sostenido causa daño periódico dentro del cono");
            Advance(.2f);after=health.Current;Advance(1.2f);Require(health.Current==after&&!boss.GetComponent<SoulEaterEffects>().Flame.Emitting,"La recuperación corta llamas y daño");
            Reset(new Vector3(0,0,10));
            target.transform.position=Vector3.ProjectOnPlane(boss.MouthPosition,Vector3.up)+Vector3.forward*(boss.Settings.breathRange*.65f);
            float wallHeight=boss.MouthPosition.y+4;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,wallHeight*.5f,(boss.MouthPosition.z+target.transform.position.z)*.5f);wall.transform.localScale=new Vector3(10,wallHeight,.6f);Physics.SyncTransforms();
            Attack(SoulEaterAction.Breath);Advance(3.6f);Require(health.Current==health.Maximum,"El fuego no daña a través de paredes");Object.DestroyImmediate(wall);

            Reset(new Vector3(0,0,-boss.Settings.tailRange*.75f));Advance(1.5f);
            Require(boss.Action==SoulEaterAction.Tail,"Permanecer detrás provoca coletazo");
            Advance(2.1f);
            Require(health.Current<health.Maximum,"El barrido de la cola alcanza al jugador detrás");
            Require(health.Maximum-health.Current<=boss.Settings.tailDamage*1.6f,"Coletazo: un impacto por objetivo");
            Reset(new Vector3(0,0,boss.Settings.biteRange*(4f/5.1f)));Attack(SoulEaterAction.Tail);Advance(2);
            Require(health.Current==health.Maximum,"El frente queda fuera del barrido trasero");

            Reset(new Vector3(0,0,10));Attack(SoulEaterAction.Breath);DamageTo(.75f);Advance(1);
            Require(boss.State==SoulEaterState.Windup&&!boss.ChargeUsed,"75 % espera a que termine el ataque actual");
            Advance(5.1f);Require(boss.ChargeUsed,"75 % inicia su secuencia después de la recuperación");
            Until(SoulEaterState.RetreatJump,4);Vector3 start=boss.transform.position;Vector3 destination=boss.RetreatDestination;
            Require(Vector3.Distance(destination,target.transform.position)>Vector3.Distance(start,target.transform.position)+3,"El salto se aleja del jugador");
            Advance(.5f);Require(boss.transform.position.y>1,"El salto tiene arco, no teletransporte");
            Until(SoulEaterState.Charging,4);Vector3 direction=boss.LockedDirection;Vector3 origin=boss.transform.position;
            target.transform.position=origin+direction*8;Physics.SyncTransforms();after=health.Current;
            Until(SoulEaterState.Braking,3);Require(health.Current<after,"La carga inflige daño al contactar al jugador");
            float chargeDamage=after-health.Current;Require(chargeDamage<=boss.Settings.chargeDamage*1.6f,"La carga no repite daño por cada frame");
            Require(Vector3.Angle(direction,boss.transform.forward)<.1f,"La carga mantiene una trayectoria recta");
            Advance(2);Require(boss.ChargeUsed&&boss.State!=SoulEaterState.SpecialRoar,"La carga especial no se repite bajo 75 %");

            // This checks the leash, so extend the test floor and open its authored walls.
            // The complete torso correctly collides with those walls before its root reaches them.
            var arenaFloor=GameObject.Find("Arena floor");var originalFloorScale=arenaFloor.transform.localScale;
            var boundaries=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>c.name=="Arena boundary"&&c.enabled).ToArray();
            try
            {
                arenaFloor.transform.localScale=new Vector3(256,originalFloorScale.y,256);foreach(var boundary in boundaries)boundary.enabled=false;
                Reset(new Vector3(0,0,90));boss.transform.position=new Vector3(0,0,25);Advance(2);
                Require(boss.State!=SoulEaterState.Returning&&boss.State!=SoulEaterState.Dormant&&boss.Target==target.transform&&boss.transform.position.z>29,"Persigue fuera del radio original sin volver a casa ni reiniciar vida");
            }
            finally{arenaFloor.transform.localScale=originalFloorScale;foreach(var boundary in boundaries)boundary.enabled=true;}

            bool phaseTwoEnabled=boss.Settings.enablePhaseTwo;boss.Settings.enablePhaseTwo=false;
            Reset(new Vector3(0,0,10));DamageTo(.5f);int transitions=0;boss.PhaseTwoRequested+=Finished;
            void Finished()=>transitions++;
            Advance(4);Require(boss.PhaseOneComplete&&transitions==1,"50 % termina en rugido y entrega la segunda fase una vez");
            after=health.Current;Advance(4);Require(transitions==1&&health.Current==after,"La prueba completada no sigue atacando");boss.PhaseTwoRequested-=Finished;boss.Settings.enablePhaseTwo=phaseTwoEnabled;
            Reset(new Vector3(0,0,10));Attack(SoulEaterAction.Breath);Advance(2);float progress=boss.Progress;after=health.Current;boss.Tick(0);
            Require(boss.Progress==progress&&health.Current==after,"Pausa: no avanza el ataque ni el daño");
            boss.Health.ApplyDamage(new DamageInfo(10000,target,Vector3.zero,Vector3.forward));Advance(.5f);
            Require(boss.State==SoulEaterState.Dead&&!boss.GetComponent<SoulEaterEffects>().Flame.Emitting,"Morir cancela el ataque y el efecto");
            Reset(new Vector3(0,0,10));Require(!boss.ChargeUsed&&boss.Health.Current==boss.Health.Maximum,"Reiniciar restaura vida y umbrales");
            DamageTo(.75f);Until(SoulEaterState.RetreatJump,4);Advance(.3f);health.ApplyDamage(new DamageInfo(10000,boss.gameObject,Vector3.zero,Vector3.forward));Advance(.3f);
            Require(boss.transform.position.y<.1f&&boss.State==SoulEaterState.Returning,"Perder al jugador durante el salto aterriza y abandona el ataque");
            Until(SoulEaterState.Dormant,10);Require(boss.Health.Current==boss.Health.Maximum,"El regreso a la arena reinicia el encuentro");

            Reset(new Vector3(0,0,10));Advance(.1f);
            var groundedSkin=boss.GetComponentInChildren<SkinnedMeshRenderer>();var feetMesh=new Mesh();groundedSkin.BakeMesh(feetMesh,false);
            float floor=feetMesh.vertices.Select(v=>groundedSkin.transform.position+groundedSkin.transform.rotation*v).Min(v=>v.y);
            var originalMesh=groundedSkin.sharedMesh;var originalPoints=originalMesh.vertices;var originalWeights=originalMesh.boneWeights;
            var matrices=groundedSkin.bones.Select((b,i)=>b.localToWorldMatrix*originalMesh.bindposes[i]).ToArray();
            float calculated=float.PositiveInfinity;int lowIndex=0;var bakedVertices=feetMesh.vertices;
            for(int i=0;i<originalPoints.Length;i++)
            {
                var v=originalPoints[i];var w=originalWeights[i];var cp=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                calculated=Mathf.Min(calculated,cp.y);if(bakedVertices[i].y<bakedVertices[lowIndex].y)lowIndex=i;
            }
            report.Add("GROUND_DIAGNOSTIC baked="+floor+" matrix="+calculated+" lowestBone="+groundedSkin.bones[originalWeights[lowIndex].boneIndex0].name+" visual="+groundedSkin.transform.position+" scale="+groundedSkin.transform.lossyScale+" rootBone="+groundedSkin.rootBone.name);
            Require(floor>=-.03f,"Patas y suelas no quedan enterradas en la pose terrestre: "+floor);Object.DestroyImmediate(feetMesh);
            boss.ResetEncounter();Physics.SyncTransforms();
            var pedestrian=new GameObject("Ground collision probe");var controller=pedestrian.AddComponent<CharacterController>();
            controller.height=1.8f;controller.radius=.35f;controller.center=Vector3.up*.9f;controller.stepOffset=.25f;
            try
            {
                Require(boss.GroundBody!=null&&!boss.GroundBody.isTrigger,"El torso tiene un volumen sólido hasta el suelo");
                var block=boss.GroundBody;
                foreach(var approach in new[]{new Vector3(0,0,12),new Vector3(6,0,4.8f),new Vector3(-6,0,4.8f)})
                {
                    controller.enabled=false;pedestrian.transform.position=approach+Vector3.up*.04f;controller.enabled=true;Physics.SyncTransforms();
                    var probeDirection=Vector3.ProjectOnPlane(new Vector3(0,0,4.8f)-approach,Vector3.up).normalized;
                    CollisionFlags contacts=CollisionFlags.None;
                    for(int i=0;i<140;i++){contacts|=controller.Move(probeDirection*.1f-Vector3.up*.01f);Physics.SyncTransforms();}
                    var local=boss.transform.InverseTransformPoint(pedestrian.transform.position)-block.center;
                    bool stayedOutside=Mathf.Abs(approach.x)>.1f?Mathf.Sign(approach.x)*local.x>=block.size.x*.5f+.2f:local.z>=block.size.z*.5f+.2f;
                    Require(stayedOutside && (contacts&CollisionFlags.Sides)!=0,"CharacterController queda bloqueado del lado de entrada "+approach+"; posición final="+pedestrian.transform.position);
                }
            }
            finally{Object.DestroyImmediate(pedestrian);}
            Reset(new Vector3(0,0,18));Attack(SoulEaterAction.Breath);Advance(1.7f);
            Require(Mathf.Abs(Mathf.Asin(boss.LockedDirection.y)*Mathf.Rad2Deg+boss.Settings.breathPitch)<.1f,"Aliento frontal mantiene su inclinación suave incluso con un objetivo bajo");
            var flame=boss.GetComponent<SoulEaterEffects>().Flame;
            Require(Vector3.Angle(flame.transform.forward,boss.LockedDirection)<.1f,"VFX y daño comparten la dirección frontal del aliento");
            Require(boss.Settings.breathPrefab!=null&&boss.Settings.breathPrefab.name=="VFX_Fire_Green"&&flame.PrefabInstance!=null,"El prefab solicitado está referenciado e instanciado para el aliento");
            var stream=flame.PrefabInstance;var particles=stream.GetComponentsInChildren<ParticleSystem>(true);
            Require(stream.activeSelf&&particles.Sum(p=>p.particleCount)>0&&!flame.GetComponent<MeshRenderer>().enabled,"La llamarada usa partículas visibles y apaga las cintas antiguas");
            Require(particles.Sum(p=>p.main.maxParticles)<=boss.Settings.breathParticleBudget,"El VFX respeta el presupuesto total de partículas");
            Require(stream.GetComponentInChildren<ParticleSystemRenderer>().sharedMaterial==boss.Settings.breathPrefab.GetComponentInChildren<ParticleSystemRenderer>().sharedMaterial,"El aliento conserva el material del prefab del usuario");
            Advance(.2f);Require(flame.PrefabInstance==stream,"Se reutiliza una sola instancia durante el ataque");
            Advance(4);Require(!stream.activeSelf&&particles.All(p=>p.particleCount==0),"La recuperación limpia las partículas del prefab");
            SoulEaterPolishChecks.Run(boss,target,Require,report.Add);
            SoulEaterPursuitChecks.Run(boss,target,Require,report.Add);
            SoulEaterDustChecks.Run(boss,target,Require);
            Reset(new Vector3(0,0,10));DamageTo(.75f);Until(SoulEaterState.RetreatJump,4);
            SoulEaterWingChecks.CheckRetreat(boss,Require);
            Capture();report.Add("SOUL_PHASE_ONE_CHECKS_OK");Debug.Log(report.Last());
        }
        static void Reset(Vector3 position)
        {
            CombatTimeFeedback.CancelForPause();Time.timeScale=1;
            boss.GetComponent<Invulnerability>().Cancel();boss.ResetEncounter();
            health.Revive();target.GetComponent<DefenseWindow>().CloseParry();target.GetComponent<DefenseWindow>().CloseDodge();target.GetComponent<CombatState>().ResetCombat();
            target.transform.SetPositionAndRotation(position,Quaternion.Euler(0,180,0));Physics.SyncTransforms();boss.BeginEncounter(target.transform);
        }
        static void Attack(SoulEaterAction action)=>Require(boss.TryStartAttack(action),"Iniciar "+action);
        static void DamageTo(float normalized)=>boss.Health.ApplyDamage(new DamageInfo(boss.Health.Current-boss.Health.Maximum*normalized,target,Vector3.zero,Vector3.forward));
        static void Advance(float seconds)
        {
            for(float elapsed=0;elapsed<seconds-.0001f;elapsed+=1f/60)
            {Physics.SyncTransforms();boss.Tick(Mathf.Min(1f/60,seconds-elapsed));boss.Combat.Tick(1f/60);}
        }
        static void Until(SoulEaterState state,float timeout)
        {for(float t=0;t<timeout&&boss.State!=state;t+=1f/60)Advance(1f/60);Require(boss.State==state,"Estado "+state);}
        static void Require(bool condition,string text){if(!condition)throw new Exception(text+"; estado="+boss?.State+" raiz="+boss?.transform.position+" boca="+boss?.MouthPosition+" objetivo="+target?.transform.position);report.Add("PASS: "+text);}
        static void Capture()
        {
            // Several samples are rendered in one Unity frame. Bake each pose so GPU skinning
            // cannot reuse the first pose for all the subsequent camera.Render calls.
            captureSkin=boss.GetComponentInChildren<SkinnedMeshRenderer>();captureMesh=new Mesh();
            captureObject=new GameObject("Soul Eater capture pose");captureObject.AddComponent<MeshFilter>().sharedMesh=captureMesh;
            captureObject.AddComponent<MeshRenderer>().sharedMaterials=captureSkin.sharedMaterials;captureSkin.enabled=false;
            try
            {
            var camera=Object.FindFirstObjectByType<UnityEngine.Camera>();
            float framingScale=boss.GetComponent<CapsuleCollider>().radius/1.4f;
            camera.GetComponent<Mismo.Gameplay.Player.Camera.ThirdPersonCamera>().enabled=false;
            camera.transform.position=new Vector3(18,11,18);camera.transform.LookAt(new Vector3(0,2,3));camera.fieldOfView=43;
            Reset(new Vector3(0,0,11));Advance(.1f);camera.transform.position=new Vector3(7,2.6f,12)*framingScale;camera.transform.LookAt(boss.transform.position+new Vector3(0,2,1)*framingScale);camera.fieldOfView=46;Shot(camera,Output+"/cuello-y-patas.png");
            camera.transform.position=new Vector3(18,11,18);camera.transform.LookAt(new Vector3(0,2,3));camera.fieldOfView=43;
            Directory.CreateDirectory(Output+"/flame-frames");Directory.CreateDirectory(Output+"/charge-frames");
            Reset(new Vector3(0,0,11));Attack(SoulEaterAction.Breath);
            for(int i=0;i<42;i++){Advance(.12f);Shot(camera,Output+"/flame-frames/"+i.ToString("000")+".png");if(i==19)Shot(camera,Output+"/llamarada-verde.png");}
            Reset(new Vector3(0,0,12));DamageTo(.75f);
            camera.transform.position=new Vector3(25,19,27);camera.transform.LookAt(new Vector3(0,1,-2));camera.fieldOfView=52;
            for(int i=0;i<72;i++){Advance(.12f);Shot(camera,Output+"/charge-frames/"+i.ToString("000")+".png");}
            Directory.CreateDirectory(Output+"/tail-frames");Reset(new Vector3(0,0,-boss.Settings.tailRange*.75f));Attack(SoulEaterAction.Tail);
            camera.transform.position=new Vector3(11,8,-13);camera.transform.LookAt(new Vector3(0,1,0));camera.fieldOfView=43;
            for(int i=0;i<26;i++){Advance(.12f);Shot(camera,Output+"/tail-frames/"+i.ToString("000")+".png");}
            Reset(new Vector3(0,0,18));Advance(.05f);camera.transform.position=new Vector3(34,20,34);camera.transform.LookAt(new Vector3(0,3,2));camera.fieldOfView=55;
            var dust=boss.GetComponent<SoulEaterEffects>().Dust;Directory.CreateDirectory("output/soul-dust");
            foreach(var kind in new[]{SoulEaterDustKind.Dive,SoulEaterDustKind.Charge,SoulEaterDustKind.Landing})
            {
                dust.Clear();dust.Play(boss.transform.position+Vector3.forward*3,kind);
                foreach(var ps in dust.LastCloud.GetComponentsInChildren<ParticleSystem>())ps.Simulate(.18f,false,false,false);
                Shot(camera,"output/soul-dust/"+kind+".png");
            }
            dust.Clear();report.Add("Capturas Unity: aliento, polvo y secuencia del 75 %");
            }
            finally{captureSkin.enabled=true;Object.DestroyImmediate(captureObject);Object.DestroyImmediate(captureMesh);captureObject=null;captureMesh=null;captureSkin=null;}
        }
        static void Shot(UnityEngine.Camera camera,string path)
        {
            if(captureSkin!=null)
            {
                captureSkin.BakeMesh(captureMesh,false);captureMesh.RecalculateBounds();
                captureObject.transform.SetPositionAndRotation(captureSkin.transform.position,captureSkin.transform.rotation);
            }
            var rt=RenderTexture.GetTemporary(960,600,24);var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(texture);
        }
    }
}
