using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Mismo.Gameplay.Enemies;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterDustChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void Run(SoulEaterPhaseOneController boss,GameObject target,Action<bool,string> check)
        {
            var dust=boss.GetComponent<SoulEaterEffects>().Dust;var s=boss.Settings;
            check(dust!=null&&dust.Capacity==2&&s.dustExplosionPrefab!=null&&s.dustExplosionPrefab.name=="DustExplosion","Polvo usa DustExplosion y dos instancias reutilizables");
            int count=dust.BurstCount;dust.Play(Vector3.zero,SoulEaterDustKind.Dive);var big=dust.LastCloud;
            var ps=big.GetComponentsInChildren<ParticleSystem>(true);
            check(ps.Length==5&&ps.All(p=>!p.main.loop&&!p.main.prewarm&&p.main.maxParticles<=s.dustMaxParticlesPerSystem),"Se conservan cinco emisores, sin bucles y con arena limitada");
            foreach(var p in ps)p.Simulate(.15f,false,false,false);
            check(ps.Sum(p=>p.particleCount)>0&&ps.All(p=>p.particleCount<=s.dustMaxParticlesPerSystem),"La explosión emite partículas dentro del presupuesto");
            check(Quaternion.Angle(big.transform.rotation,s.dustExplosionPrefab.transform.localRotation)<.01f,"Se conserva la orientación vertical del prefab");
            var anchor=big.transform.position;boss.transform.position+=Vector3.right*3;
            check(big.transform.position==anchor,"La nube permanece en el suelo cuando se mueve el dragón");boss.transform.position-=Vector3.right*3;
            dust.Play(Vector3.right*4,SoulEaterDustKind.Charge);var small=dust.LastCloud;
            check(small!=big&&small.transform.localScale.magnitude<big.transform.localScale.magnitude*.5f,"El apoyo de la carga produce una nube claramente menor que la picada");
            dust.Play(Vector3.zero,SoulEaterDustKind.Landing);check(dust.LastCloud==big&&dust.BurstCount==count+3,"El tercer impacto reutiliza una instancia del pool");
            dust.Clear();check(!big.activeSelf&&!small.activeSelf&&ps.All(p=>p.particleCount==0),"Cancelar o reiniciar limpia todo el polvo");
            var cleanupProbe=new GameObject("Dust unload probe").AddComponent<SoulEaterDustVfx>();
            try
            {
                cleanupProbe.Configure(s,1);cleanupProbe.Play(Vector3.zero,SoulEaterDustKind.Charge);
                Object.DestroyImmediate(cleanupProbe.LastCloud.transform.parent.gameObject);
                cleanupProbe.Clear();
                check(true,"Dust cleanup tolerates its scene objects being destroyed before the boss");
            }
            finally{Object.DestroyImmediate(cleanupProbe.gameObject);}
            var floor=GameObject.Find("Arena floor");var oldScale=floor.transform.localScale;floor.transform.localScale=new Vector3(256,oldScale.y,256);
            var walls=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("Arena boundary")).ToArray();foreach(var w in walls)w.enabled=false;
            float trigger=s.pursuitChargeTriggerDistance;bool pursuit=s.enablePursuitCharge;
            var trace=new List<string>();int contacts=0;float highest=0;bool charged=false,braked=false;
            try
            {
                s.enablePursuitCharge=true;s.pursuitChargeTriggerDistance=20;boss.ResetEncounter();target.transform.position=new Vector3(0,0,40);boss.BeginEncounter(target.transform);
                foreach(var name in new[]{"nextBite","nextBreath","nextTail"})typeof(SoulEaterPhaseOneController).GetField(name,Private).SetValue(boss,float.MaxValue);
                var support=typeof(SoulEaterPhaseOneController).GetField("footSupport",Private).GetValue(boss);
                var clearance=support.GetType().GetProperty("FrontClearance");int previous=dust.BurstCount;
                for(int i=0;i<900;i++)
                {
                    Physics.SyncTransforms();boss.Tick(1f/60);
                    float height=(float)clearance.GetValue(support);highest=Mathf.Max(highest,height);
                    charged|=boss.State==SoulEaterState.Charging;braked|=boss.State==SoulEaterState.Braking;
                    if(i%5==0)trace.Add(i+" "+boss.State+" "+boss.Progress+" front="+height+" bursts="+dust.BurstCount);
                    if(dust.BurstCount!=previous)
                    {
                        contacts++;check(dust.LastKind==SoulEaterDustKind.Charge&&height<=s.chargeDustContactHeight+.01f,"Polvo de carga sincronizado con el contacto real de las patas");previous=dust.BurstCount;
                    }
                    if(braked&&boss.State==SoulEaterState.Hunting&&boss.Progress>=1)break;
                }
                check(charged&&braked&&contacts>=1&&contacts<=2,"Carga y frenado levantan polvo una vez por apoyo, sin emitir en cada zancada (contactos="+contacts+", altura="+highest+")");
            }
            finally
            {
                Directory.CreateDirectory("output/soul-dust");File.WriteAllLines("output/soul-dust/charge-contact.txt",trace);
                s.pursuitChargeTriggerDistance=trigger;s.enablePursuitCharge=pursuit;floor.transform.localScale=oldScale;foreach(var w in walls)w.enabled=true;boss.ResetEncounter();
            }
        }
    }
}
