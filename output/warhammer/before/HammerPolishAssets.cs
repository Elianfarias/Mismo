using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    // Asset authoring only. No runtime rig solver, manager, or animation damage events.
    public static class HammerPolishAssets
    {
        const string AudioFolder="Assets/Art/Audio/Hammer";
        public static void Apply(WeaponDefinition weapon,WeaponAnimationSet animations)
        {
            Bake(weapon,animations,1);Bake(weapon,animations,2);Bake(weapon,animations,3);
            weapon.poseProfile.trailTip=new Vector3(0,1.06f,0);
            weapon.poseProfile.trailPreparationFraction=.30f;
            weapon.poseProfile.trailWidth=.30f;weapon.poseProfile.trailDuration=.14f;
            weapon.poseProfile.trailStartColor=new Color(.82f,.75f,.58f,.65f);
            weapon.poseProfile.trailEndColor=new Color(.54f,.47f,.33f,0);
            weapon.inventoryIcon=InventoryPresentationAssets.Icon(weapon.visualPrefab,"weapon-Hammer",new Vector3(0,25,-25));
            Icons();
            var swing=Sound("Hammer-Swing",.24f,0);var heavySwing=Sound("Hammer-HeavySwing",.34f,1);
            var impact=Sound("Hammer-Impact",.20f,2);var heavyImpact=Sound("Hammer-HeavyImpact",.28f,3);
            var slam=Sound("Hammer-Ground",.42f,4);var spin=Sound("Hammer-Spin",1.20f,5);
            for(int i=0;i<4;i++)
            {
                var a=weapon.abilities[i];a.preparationSfx=null;
                a.executionSfx=i==0?swing:i==1?heavySwing:i==3?spin:null;a.executionSfxVolume=i==3?.28f:.35f;
                if(a.actions[0] is RepeatedStrikeAction strike){strike.impactParticles=i==0?5:10;strike.impactSfx=i==0?impact:heavyImpact;strike.impactVolume=i==0?.42f:.55f;}
                if(a.actions[0] is HammerImpactAction area){area.groundImpact=i==2;area.impactSfx=i==2?slam:impact;area.impactVolume=i==2?.65f:.35f;}
                EditorUtility.SetDirty(a);
            }
            EditorUtility.SetDirty(weapon);EditorUtility.SetDirty(weapon.poseProfile);EditorUtility.SetDirty(animations);
        }
        static void Bake(WeaponDefinition weapon,WeaponAnimationSet animations,int index)
        {
            var character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
            try
            {
                var model=character.GetComponentInChildren<Animator>().gameObject;
                var bones=model.GetComponentsInChildren<Transform>();
                var left=bones.Single(t=>t.name=="Hand.L");var right=bones.Single(t=>t.name=="Hand.R");var root=bones.Single(t=>t.name=="Root");
                var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(HammerAssets.Clips+"/Hammer_Idle.anim");
                var source=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/HumanMelee/Human_Player_Attack2H01.anim");
                var clip=animations.actions[index].clip;float duration=index==3?1.65f:1.6f;
                var curves=new Dictionary<EditorCurveBinding,AnimationCurve>();
                // Copy all source transforms so fingers, legs and bind scales remain compatible.
                foreach(var b in AnimationUtility.GetCurveBindings(index==3?idle:source))curves[b]=new AnimationCurve();
                foreach(var t in new[]{left.parent.parent,left.parent,left,right.parent.parent,right.parent,right,root})
                    foreach(char axis in "xyzw")curves[EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(t,model.transform),typeof(Transform),"m_LocalRotation."+axis)]=new AnimationCurve();
                idle.SampleAnimation(model,0);
                Quaternion offset=Quaternion.Euler(weapon.poseProfile.equipped.rotation);
                Quaternion initialHammer=left.rotation*offset;
                Quaternion rightOffset=Quaternion.Inverse(initialHammer)*right.rotation;
                Quaternion rootStart=root.rotation;
                Vector3 restGrip=left.position;Vector3 restShaft=initialHammer*Vector3.up;
                for(int frame=0;frame<=Mathf.RoundToInt(duration*60);frame++)
                {
                    float time=frame/60f,n=time/duration;
                    (index==3?idle:source).SampleAnimation(model,index==3?idle.length*.5f:time);
                    if(index==3)root.rotation=rootStart;
                    Vector3 grip,shaft;float blend;
                    if(index==3)
                    {
                        blend=time<.2f?Mathf.SmoothStep(0,1,time/.2f):time<=1.4f?1:1-Mathf.SmoothStep(0,1,(time-1.4f)/.25f);
                        grip=Vector3.Lerp(restGrip,new Vector3(.12f,1.15f,.48f),blend);shaft=Vector3.Slerp(restShaft,new Vector3(1,0,.1f).normalized,blend);
                    }
                    else
                    {
                        Vector3 raised=new Vector3(0,1.48f,.28f),raisedShaft=new Vector3(0,.97f,-.24f).normalized;
                        Vector3 down=index==2?new Vector3(0,1.00f,.38f):new Vector3(.15f,1.12f,.42f);
                        Vector3 downShaft=index==2?new Vector3(0,-.80f,.60f):new Vector3(.22f,-.28f,.93f).normalized;
                        if(n<.34f){blend=Mathf.SmoothStep(0,1,n/.34f);grip=Vector3.Lerp(restGrip,raised,blend);shaft=Vector3.Slerp(restShaft,raisedShaft,blend);}
                        else if(n<.52f){blend=Mathf.Pow(Mathf.Clamp01((n-.34f)/.18f),2);grip=Vector3.Lerp(raised,down,blend);shaft=Vector3.Slerp(raisedShaft,downShaft,blend);}
                        else if(n<.62f){grip=down;shaft=downShaft;}
                        else {blend=Mathf.SmoothStep(0,1,(n-.62f)/.38f);grip=Vector3.Lerp(down,restGrip,blend);shaft=Vector3.Slerp(downShaft,restShaft,blend);}
                    }
                    Quaternion hammer=Quaternion.LookRotation(Vector3.Cross(Vector3.right,shaft).normalized,shaft);
                    Reach(left,grip);left.rotation=hammer*Quaternion.Inverse(offset);
                    Reach(right,grip+shaft*.25f);right.rotation=hammer*rightOffset;
                    if(index==3)root.rotation=Quaternion.AngleAxis(720*Mathf.Clamp01((time-.2f)/1.2f),Vector3.up)*rootStart;
                    foreach(var entry in curves)
                    {
                        var b=entry.Key;var t=model.transform.Find(b.path);if(t==null)continue;
                        string property=b.propertyName;int axis="xyzw".IndexOf(property[property.Length-1]);float value;
                        if(property.StartsWith("m_LocalRotation."))value=t.localRotation[axis];
                        else if(property.StartsWith("m_LocalPosition."))value=t.localPosition[axis];
                        else if(property.StartsWith("m_LocalScale."))value=t.localScale[axis];
                        else {var original=AnimationUtility.GetEditorCurve(index==3?idle:source,b);value=original!=null?original.Evaluate(time):0;}
                        entry.Value.AddKey(time,value);
                    }
                }
                foreach(var b in AnimationUtility.GetCurveBindings(clip))AnimationUtility.SetEditorCurve(clip,b,null);
                foreach(var entry in curves)
                {
                    for(int k=0;k<entry.Value.length;k++){AnimationUtility.SetKeyLeftTangentMode(entry.Value,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(entry.Value,k,AnimationUtility.TangentMode.Linear);}
                    AnimationUtility.SetEditorCurve(clip,entry.Key,entry.Value);
                }
                clip.EnsureQuaternionContinuity();AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;settings.stopTime=duration;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
                // Add exact contact samples, not an animation event: damage still belongs to AbilityRunner.
                if(index==2)
                {
                    clip.SampleAnimation(model,.52f*duration);
                    Vector3 head=left.position+left.rotation*offset*new Vector3(0,1.06f,0);
                    File.WriteAllText("HammerChecks/slam-bake.txt","Contact head="+head.ToString("F4")+" front grip error="+Vector3.Distance(right.position,left.position+(left.rotation*offset*Vector3.up)*.25f));
                }
            }
            finally{Object.DestroyImmediate(character);}
        }
        // Editor-only two-bone fitting preserves bone lengths. The result is ordinary clip curves.
        static void Reach(Transform hand,Vector3 target)
        {
            var lower=hand.parent;var upper=lower.parent;
            for(int i=0;i<18;i++)
            {
                lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
                upper.rotation=Quaternion.FromToRotation(hand.position-upper.position,target-upper.position)*upper.rotation;
            }
        }
        static AudioClip Sound(string name,float duration,int kind)
        {
            Directory.CreateDirectory(AudioFolder);string path=AudioFolder+"/"+name+".wav";
            if(!File.Exists(path))
            {
                const int rate=22050;int count=Mathf.CeilToInt(duration*rate);var random=new System.Random(173+kind);float low=0;
                using(var writer=new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                    for(int i=0;i<count;i++)
                    {
                        float t=i/(float)rate,p=t/duration,noise=(float)random.NextDouble()*2-1;low=Mathf.Lerp(low,noise,kind<2||kind==5?.08f:.30f);
                        float value;
                        if(kind<2||kind==5){float phase=kind==5?(p*2)%1:p;value=low*Mathf.Pow(Mathf.Sin(phase*Mathf.PI),2)*1.4f;}
                        else {float envelope=Mathf.Exp(-p*9)*Mathf.Min(1,t/.002f);float hz=kind==4?74:kind==3?96:135;value=(low*.75f+Mathf.Sin(t*hz*2*Mathf.PI)*.38f+Mathf.Sin(t*hz*3.17f*Mathf.PI)*.12f)*envelope;}
                        value*=Mathf.Clamp01((1-p)*50);writer.Write((short)(Mathf.Clamp(value,-.95f,.95f)*32767));
                    }
                }
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static void Icons()
        {
            // Monochrome vector-like silhouettes match QuietFantasy's 512px transparent icons.
            var preview=new Texture2D(1024,256,TextureFormat.RGB24,false);int column=0;
            foreach(string kind in new[]{"strike","heavy","slam","spin"})
            {
                var texture=new Texture2D(512,512,TextureFormat.RGBA32,false);var pixels=new Color32[512*512];
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                {
                    int hits=0;for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++)if(InIcon(new Vector2((x+(sx+.5f)/2)/512,(y+(sy+.5f)/2)/512),kind))hits++;
                    pixels[y*512+x]=new Color32((byte)(hits>0?255:0),(byte)(hits>0?255:0),(byte)(hits>0?255:0),(byte)(hits*255/4));
                }
                texture.SetPixels32(pixels);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;
                for(int py=0;py<256;py++)for(int px=0;px<256;px++)
                {
                    float u=(px-20)/216f,v=(py-20)/216f;
                    float alpha=u>=0&&u<=1&&v>=0&&v<=1?texture.GetPixelBilinear(u,v).a:0;
                    preview.SetPixel(column*256+px,py,Color.Lerp(new Color(.055f,.067f,.058f),QuietFantasyUI.Ink,alpha));
                }
                column++;string path="Assets/Resources/UI/QuietFantasy/Icons/hammer-"+kind+".png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            }
            preview.Apply();File.WriteAllBytes("HammerChecks/skill-icons.png",preview.EncodeToPNG());Object.DestroyImmediate(preview);
        }
        static bool InIcon(Vector2 p,string kind)
        {
            Vector2 h=kind=="slam"?new Vector2(.58f,.37f):new Vector2(.58f,.68f);
            float angle=kind=="slam"?-.55f:kind=="heavy"?-.30f:-.72f;
            Vector2 d=p-h;Vector2 q=new Vector2(d.x*Mathf.Cos(angle)-d.y*Mathf.Sin(angle),d.x*Mathf.Sin(angle)+d.y*Mathf.Cos(angle));
            bool hammer=Mathf.Abs(q.x)<.195f&&Mathf.Abs(q.y)<.105f&&Mathf.Abs(q.x)+Mathf.Abs(q.y)<.275f || Mathf.Abs(q.x)<.033f&&(kind=="slam"?q.y>.1f&&q.y<.47f:q.y<-.1f&&q.y>-.47f);
            if(hammer)return true;
            if(kind=="spin")
            {
                Vector2 v=p-new Vector2(.5f,.5f);float r=v.magnitude;float a=Mathf.Atan2(v.y,v.x);
                return r>.405f&&r<.45f&&(a>.35f&&a<2.85f||a<-.30f&&a>-2.80f)||Triangle(p,new Vector2(.085f,.67f),new Vector2(.19f,.77f),new Vector2(.21f,.59f))||Triangle(p,new Vector2(.915f,.33f),new Vector2(.81f,.23f),new Vector2(.79f,.41f));
            }
            if(kind=="slam")
            {
                Vector2 v=p-new Vector2(.58f,.23f);float r=new Vector2(v.x,v.y*2.8f).magnitude;
                return r>.23f&&r<.26f||Line(p,new Vector2(.37f,.24f),new Vector2(.21f,.11f),.018f)||Line(p,new Vector2(.68f,.20f),new Vector2(.79f,.085f),.018f)||Line(p,new Vector2(.47f,.18f),new Vector2(.43f,.07f),.014f);
            }
            if(kind=="heavy")return Line(p,new Vector2(.13f,.80f),new Vector2(.27f,.66f),.026f)||Line(p,new Vector2(.15f,.92f),new Vector2(.40f,.76f),.019f)||Triangle(p,new Vector2(.73f,.29f),new Vector2(.64f,.09f),new Vector2(.89f,.18f))||Line(p,new Vector2(.76f,.40f),new Vector2(.92f,.34f),.023f);
            return Line(p,new Vector2(.77f,.44f),new Vector2(.87f,.34f),.018f)||Line(p,new Vector2(.80f,.54f),new Vector2(.94f,.53f),.018f);
        }
        static bool Line(Vector2 p,Vector2 a,Vector2 b,float width){var v=b-a;return Vector2.Distance(p,a+v*Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude))<width;}
        static bool Triangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
        {float Cross(Vector2 u,Vector2 v)=>u.x*v.y-u.y*v.x;float x=Cross(b-a,p-a),y=Cross(c-b,p-b),z=Cross(a-c,p-c);return x>=0&&y>=0&&z>=0||x<=0&&y<=0&&z<=0;}
    }
}
