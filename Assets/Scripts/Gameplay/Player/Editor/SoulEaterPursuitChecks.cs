using System;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterPursuitChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(SoulEaterPhaseOneController b,string name,object value)=>typeof(SoulEaterPhaseOneController).GetField(name,Private).SetValue(b,value);
        static AnimationClip Clip(SoulEaterPhaseOneController b)
        {
            var animation=typeof(SoulEaterPhaseOneController).GetField("animation",Private).GetValue(b);
            return (AnimationClip)typeof(SoulEaterAnimation).GetField("selected",Private).GetValue(animation);
        }
        static void Step(SoulEaterPhaseOneController b,int frames=1)
        {for(int i=0;i<frames;i++){Physics.SyncTransforms();b.Tick(1f/60);}}
        static void Reset(SoulEaterPhaseOneController b,GameObject target,Vector3 position)
        {
            b.ResetEncounter();target.GetComponent<Health>().Revive();target.transform.position=position;b.BeginEncounter(target.transform);
            foreach(string f in new[]{"nextBite","nextTail","nextBreath"})Set(b,f,10000f);
        }
        static void Until(SoulEaterPhaseOneController b,SoulEaterState state,float seconds=12)
        {for(int i=0;i<seconds*60&&b.State!=state;i++)Step(b);if(b.State!=state)throw new Exception("Pursuit timeout: "+state+"; actual="+b.State+"; position="+b.transform.position);}
        public static void Run(SoulEaterPhaseOneController b,GameObject target,Action<bool,string> check,Action<string> record)
        {
            var s=b.Settings;bool enabled=s.enablePursuitCharge;float windup=s.pursuitChargeWindup,limit=s.pursuitChargeMaxDistance;
            var floor=GameObject.Find("Arena floor");var scale=floor.transform.localScale;
            var bounds=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>c.name=="Arena boundary"&&c.enabled).ToArray();
            GameObject tree=null;
            try
            {
                floor.transform.localScale=new Vector3(512,scale.y,512);foreach(var c in bounds)c.enabled=false;s.enablePursuitCharge=true;
                Reset(b,target,new Vector3(0,0,100));Until(b,SoulEaterState.ChargeWindup);
                check(!b.ChargeUsed&&b.Health.Normalized==1,"La distancia activa carga a vida completa sin consumir el especial del 75 %");
                var start=b.transform.position;Step(b,Mathf.FloorToInt(s.pursuitChargeWindup*.5f*60));
                check(b.State==SoulEaterState.ChargeWindup&&Vector3.Distance(start,b.transform.position)<.01f,"Carga de persecución anticipa antes de desplazarse");
                Until(b,SoulEaterState.Charging);start=b.transform.position;Step(b,12);
                check(Mathf.Abs(Vector3.Distance(start,b.transform.position)-s.chargeSpeed*.2f)<.1f&&Clip(b)==s.run,"La persecución carga con velocidad configurada y animación Run continua");
                Until(b,SoulEaterState.Braking);
                check(Vector3.Distance(start,b.transform.position)>s.chargeDistance&&Vector3.Distance(start,b.transform.position)<=s.pursuitChargeMaxDistance+.6f,"Carga lejana usa su recorrido de 60 m y no el límite corto del especial");
                Until(b,SoulEaterState.Hunting);target.transform.position=b.transform.position+Vector3.forward*100;
                bool repeated=false;for(int i=0;i<s.pursuitChargeCooldown*.8f*60;i++){Step(b);repeated|=b.State==SoulEaterState.ChargeWindup;}
                check(!repeated,"La carga respeta el enfriamiento después de frenar aunque el jugador siga lejos");
                Until(b,SoulEaterState.ChargeWindup,6);check(true,"Puede repetir carga al seguir lejos, sin quedar limitada al 75 %");

                s.pursuitChargeWindup=1.2f;s.pursuitChargeMaxDistance=18;
                Reset(b,target,new Vector3(0,0,70));Until(b,SoulEaterState.ChargeWindup);Step(b,54);
                check(b.State==SoulEaterState.ChargeWindup,"El tiempo de reacción editado en settings se aplica a la persecución");
                Until(b,SoulEaterState.Charging);start=b.transform.position;var direction=b.LockedDirection;target.transform.position+=Vector3.right*25;
                Until(b,SoulEaterState.Braking);float travel=Vector3.Distance(start,b.transform.position);
                check(travel>=17.9f&&travel<18.6f&&Vector3.Angle(direction,b.transform.forward)<.1f,"Recorrido configurable y dirección fijada: se puede esquivar hacia un costado");
                s.pursuitChargeWindup=windup;s.pursuitChargeMaxDistance=limit;

                Reset(b,target,new Vector3(0,0,65));b.Health.ApplyDamage(new DamageInfo(b.Health.Maximum*.51f,target,Vector3.zero,Vector3.forward));
                for(int i=0;i<600&&b.Phase!=2;i++)Step(b);
                Set(b,"nextAerial",10000f);Set(b,"nextRepeatCharge",10000f);
                Until(b,SoulEaterState.ChargeWindup);check(b.Phase==2,"Cerrar distancia con carga también funciona en fase 2");
                Reset(b,target,new Vector3(0,0,20));Step(b,120);
                check(b.State==SoulEaterState.Hunting&&!b.ChargeUsed,"En rango cercano mantiene la persecución normal");

                tree=GameObject.CreatePrimitive(PrimitiveType.Cube);tree.name="Moving target pursuit tree";tree.transform.position=new Vector3(0,4,16);tree.transform.localScale=new Vector3(3,8,3);
                Reset(b,target,new Vector3(0,0,65));Step(b,60);
                check(b.State==SoulEaterState.Hunting,"Un árbol delante impide iniciar una carga contra el obstáculo");
                s.enablePursuitCharge=false;
                Reset(b,target,new Vector3(0,0,42));int idleSwitches=0;bool walking=false,penetrated=false,reached=false;float lateral=0;
                for(int i=0;i<2400;i++)
                {
                    target.transform.position=new Vector3(Mathf.Sin(i/60f*.7f)*8,0,42);Step(b);
                    bool now=Clip(b)==s.walk;if(walking&&!now)idleSwitches++;walking=now;
                    lateral=Mathf.Max(lateral,Mathf.Abs(b.transform.position.x));
                    if(Physics.ComputePenetration(b.GroundBody,b.transform.position,b.transform.rotation,tree.GetComponent<Collider>(),tree.transform.position,tree.transform.rotation,out _,out var depth)&&depth>.05f)penetrated=true;
                    if(Vector3.Distance(b.transform.position,target.transform.position)<11){reached=true;break;}
                }
                record("MOVING_TARGET_PATH: walk-to-idle="+idleSwitches+"; lateral="+lateral+"; reached="+reached+"; penetration="+penetrated+"; position="+b.transform.position);
                check(reached&&lateral>5&&!penetrated,"A* rodea el árbol y alcanza a un objetivo que cambia de posición mientras recalcula");
                check(idleSwitches<=2,"Replanificar hacia un objetivo móvil no alterna caminar/Idle continuamente");
                Object.DestroyImmediate(tree);tree=null;
                Reset(b,target,new Vector3(0,0,s.biteRange*.85f));Step(b,30);int changes=0;walking=Clip(b)==s.walk;
                for(int i=0;i<120;i++)
                {
                    target.transform.position=b.transform.position+Vector3.forward*(s.biteRange*.85f+(i%2==0?.1f:-.1f));Step(b);
                    bool now=Clip(b)==s.walk;if(now!=walking)changes++;walking=now;
                }
                check(changes==0,"Oscilar 10 cm en el borde del alcance no provoca espasmos de locomoción");
            }
            finally
            {
                if(tree!=null)Object.DestroyImmediate(tree);floor.transform.localScale=scale;foreach(var c in bounds)c.enabled=true;
                s.enablePursuitCharge=enabled;s.pursuitChargeWindup=windup;s.pursuitChargeMaxDistance=limit;b.ResetEncounter();
            }
        }
    }
}
