using System;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Combat;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterPolishChecks
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run(SoulEaterPhaseOneController boss,GameObject target,Action<bool,string> check,Action<string> record)
        {
            var s=boss.Settings;var field=boss.Battlefield;
            boss.ResetEncounter();target.transform.position=new Vector3(0,0,22);boss.BeginEncounter(target.transform);
            check(s.groundFirePrefab!=null && s.groundFirePrefab.name=="VFX_GroundFire_Circle_Green","Los charcos usan el prefab verde del usuario");
            var positions=Enumerable.Range(0,s.maximumGroundFires).Select(i=>new Vector3(-12+i*3,0,5)).ToArray();
            foreach(var p in positions)field.AddFire(p);
            check(field.FireCount==s.maximumGroundFires && field.FirePoolCount==0,"Pool acotado: se reutilizan los charcos sin crear mallas de aliento");
            var visuals=Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.transform.root.name=="Soul Eater ground fire pool").ToArray();
            check(visuals.Length==s.maximumGroundFires*3 && visuals.All(p=>p.main.maxParticles<=20),"Cada charco conserva los 3 sistemas y hasta 60 partículas del prefab");
            var ids=new System.Collections.Generic.HashSet<ParticleSystem>(visuals);var first=visuals[0].transform.position;boss.transform.position+=Vector3.right*4;
            check(Vector3.Distance(first,visuals[0].transform.position)<.001f,"Mover el dragón no arrastra el fuego del suelo");
            var watch=new Stopwatch();long bytes=GC.GetAllocatedBytesForCurrentThread();watch.Start();
            for(int i=0;i<300;i++)field.Tick(.0001f);
            watch.Stop();long allocation=GC.GetAllocatedBytesForCurrentThread()-bytes;
            record("GROUND_FIRE_CPU: 8 charcos, 300 Tick = "+watch.Elapsed.TotalMilliseconds+" ms; asignación C#="+allocation+" bytes (no incluye render/partículas nativas)");
            check(allocation<1024,"Los charcos no asignan memoria C# ni reconstruyen geometría por fotograma");
            // Optional baseline exists only in the isolated verification copy, never in player builds.
            var legacy=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Mismo.Gameplay.Enemies.SoulEaterLegacyBattlefieldProfile")).FirstOrDefault(t=>t!=null);
            if(legacy!=null)
            {
                var go=new GameObject("Legacy fire measurement");var old=go.AddComponent(legacy);
                legacy.GetMethod("Initialize").Invoke(old,new object[]{boss,s.breathMaterial});
                foreach(var p in positions)legacy.GetMethod("AddFire").Invoke(old,new object[]{p});
                var tick=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),old,legacy.GetMethod("Tick"));
                tick(.0001f);watch.Restart();for(int i=0;i<300;i++)tick(.0001f);watch.Stop();
                record("GROUND_FIRE_OLD_CPU: mismos 8 charcos, 300 Tick = "+watch.Elapsed.TotalMilliseconds+" ms");Object.DestroyImmediate(go);
            }
            field.Clear();foreach(var p in positions)field.AddFire(p);
            check(visuals.All(p=>p!=null)&&ids.SetEquals(Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.transform.root.name=="Soul Eater ground fire pool")),"Expirar y volver a generar fuego conserva las mismas instancias");
            field.Tick(s.groundFireLifetime+.1f);check(field.FireCount==0 && field.FirePoolCount==s.maximumGroundFires,"Al caducar, todos los charcos vuelven al pool");
            boss.ResetEncounter();target.transform.position=new Vector3(0,0,10);target.GetComponent<Health>().Revive();boss.BeginEncounter(target.transform);boss.TryStartAttack(SoulEaterAction.Breath);
            for(int i=0;i<115;i++){Physics.SyncTransforms();boss.Tick(1f/60);}
            target.transform.position=new Vector3(boss.MouthPosition.x,0,boss.MouthPosition.z-.5f);Physics.SyncTransforms();float hp=target.GetComponent<Health>().Current;
            for(int i=0;i<28;i++){Physics.SyncTransforms();boss.Tick(1f/60);}
            check(target.GetComponent<Health>().Current<hp,"El aliento alcanza al jugador a ras del suelo bajo la boca");
            var flame=boss.GetComponent<SoulEaterEffects>().Flame;var mesh=flame.GetComponent<MeshFilter>().sharedMesh;
            check(mesh.vertices.Min(v=>flame.transform.TransformPoint(v).y)<.3f,"Las llamas visibles también alcanzan el suelo");
            Navigation(boss,target,check);
        }
        static void Navigation(SoulEaterPhaseOneController boss,GameObject target,Action<bool,string> check)
        {
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Pathfinding tree test";wall.transform.position=new Vector3(0,4,14);wall.transform.localScale=new Vector3(3,8,3);
            var floor=GameObject.Find("Arena floor");var scale=floor.transform.localScale;floor.transform.localScale=new Vector3(256,scale.y,256);
            var boundaries=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("Arena boundary")).ToArray();foreach(var c in boundaries)c.enabled=false;
            try
            {
                boss.ResetEncounter();target.transform.position=new Vector3(0,0,26);boss.BeginEncounter(target.transform);
                foreach(string name in new[]{"nextBite","nextTail","nextBreath"})typeof(SoulEaterPhaseOneController).GetField(name,Private).SetValue(boss,10000f);
                bool crossed=false,penetrated=false;float lateral=0;
                for(int i=0;i<3600;i++)
                {
                    Physics.SyncTransforms();boss.Tick(1f/60);lateral=Mathf.Max(lateral,Mathf.Abs(boss.transform.position.x));
                    if(Physics.ComputePenetration(boss.GroundBody,boss.transform.position,boss.transform.rotation,wall.GetComponent<Collider>(),wall.transform.position,wall.transform.rotation,out _,out var depth)&&depth>.05f)penetrated=true;
                    if(Vector3.Distance(boss.transform.position,target.transform.position)<10 && boss.transform.position.z>16){crossed=true;break;}
                }
                check(crossed && lateral>5 && !penetrated,"A* rodea un árbol con el cuerpo completo y llega al jugador sin atravesarlo; lateral="+lateral+", posición="+boss.transform.position+", penetración="+penetrated);
            }
            finally{Object.DestroyImmediate(wall);floor.transform.localScale=scale;foreach(var c in boundaries)c.enabled=true;boss.ResetEncounter();}
        }
    }
}
