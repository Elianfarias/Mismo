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
    public static class HammerPolishAssets
    {
        const string AudioFolder="Assets/Art/Audio/Hammer";
        public const string Projects="Assets/Data/AnimationAuthoring/HumanoidAttacks/Hammer";
        static readonly string[] ClipNames={"Hammer_Attack","Hammer_Heavy","Hammer_GroundSlam","Hammer_Sweep","Hammer_Charge","Hammer_DoubleSpin"};
        struct Key
        {
            public float time,twist,lean;public Vector3 grip,shaft;
            public Key(float t,Vector3 g,Vector3 s,float twist=0,float lean=0){time=t;grip=g;shaft=s.normalized;this.twist=twist;this.lean=lean;}
        }
        static readonly Vector3 Ready=new Vector3(.18f,1.08f,.16f),ReadyShaft=new Vector3(-.42f,.70f,.57f).normalized;
        static readonly Vector3 Loaded=new Vector3(.06f,1.38f,.12f),LoadedShaft=new Vector3(-.12f,.97f,-.20f).normalized;
        static readonly Vector3 Contact=new Vector3(.02f,1.10f,.13f),ContactShaft=new Vector3(.02f,-.12f,.99f).normalized;
        static readonly Vector3 Ground=new Vector3(.02f,.97f,.17f),GroundShaft=new Vector3(0,-.75f,.6614f).normalized;
        static Quaternion GripRotation,LeftRotation;
        public static void Apply(WeaponDefinition weapon,WeaponAnimationSet animations)
        {
            HammerAssets.Folder(Projects);var kit=HammerAssets.Kit(weapon);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusHumanoidLocomotion.ModelPath);
            using(var rig=new HumanoidAttackRig(model))
            {
                rig.Sample(AssetDatabase.LoadAssetAtPath<AnimationClip>(QuaterniusHumanoidLocomotion.OutputFolder+"/Quaternius_Idle.anim"),.3f);
                var basis=rig.Capture("Base",0);rig.StopSampling();
                // Calibrate grip once against the real humanoid hand orientation, then bake both hands.
                GripRotation=Quaternion.Inverse(rig.Animator.GetBoneTransform(HumanBodyBones.RightHand).rotation)*Quaternion.FromToRotation(Vector3.up,ReadyShaft);
                LeftRotation=Quaternion.Inverse(Quaternion.FromToRotation(Vector3.up,ReadyShaft))*rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand).rotation;
                weapon.poseProfile.equipped.rotation=GripRotation.eulerAngles;
                var bindings=new List<AbilityAnimationBinding>();
                for(int i=0;i<=kit.Length;i++)
                {
                    bool returning=i==kit.Length;
                    var a=returning?Object.Instantiate(kit[0]):kit[i];
                    if(returning){a.preparation=.40f;a.active=.22f;a.recovery=.46f;}
                    string clipName=returning?"Hammer_Attack_Return":ClipNames[i];
                    var recipe=HammerAssets.Asset<HumanoidAttackRecipe>(Projects+"/"+clipName+".asset");
                    recipe.model=model;recipe.previewWeapon=weapon;recipe.clipName=clipName;recipe.duration=a.Duration;recipe.frameRate=60;
                    recipe.activeStartsAt=a.preparation/a.Duration;recipe.recoveryStartsAt=(a.preparation+a.active)/a.Duration;recipe.poses.Clear();
                    var keys=Keys(i,a);int frames=Mathf.CeilToInt(a.Duration*60);
                    var times=new SortedSet<float>(keys.Select(k=>k.time));for(int f=0;f<=frames;f++)times.Add(a.Duration*f/frames);
                    foreach(float t in times)
                    {
                        int next=1;while(next<keys.Count-1&&keys[next].time<t)next++;
                        var from=keys[next-1];var to=keys[next];float n=Mathf.InverseLerp(from.time,to.time,t);
                        float contactTime=a.preparation+(i==1?.72f:i==3?.12f:i==4?.46f:0);
                        if(i!=2&&i!=5&&Mathf.Abs(to.time-contactTime)<.001f)n=n*n;
                        else if(i!=2&&i!=5&&Mathf.Abs(from.time-contactTime)<.001f)n=1-(1-n)*(1-n);
                        else n=n*n*(3-2*n);
                        var key=new Key(t,Vector3.Lerp(from.grip,to.grip,n),Vector3.Slerp(from.shaft,to.shaft,n),Mathf.Lerp(from.twist,to.twist,n),Mathf.Lerp(from.lean,to.lean,n));
                        var frameBasis=basis;
                        if((i==1||i==4)&&t>=a.preparation&&t<=a.preparation+a.active)
                        {
                            float progress=(t-a.preparation)/a.active;
                            var gait=AssetDatabase.LoadAssetAtPath<AnimationClip>(QuaterniusHumanoidLocomotion.OutputFolder+(i==1?"/Quaternius_Run.anim":"/Quaternius_Walk.anim"));
                            rig.Sample(gait,(progress*(i==1?2:1)%1)*gait.length);var walking=rig.Capture("Paso",0);rig.StopSampling();
                            float weight=Mathf.SmoothStep(0,1,Mathf.Min(progress/.15f,(1-progress)/.2f));
                            frameBasis=basis.Copy(0);frameBasis.bodyPosition=Vector3.Lerp(basis.bodyPosition,walking.bodyPosition,weight);
                            for(int m=0;m<frameBasis.muscles.Length;m++)if(HumanTrait.MuscleName[m].Contains("Leg")||HumanTrait.MuscleName[m].Contains("Foot"))frameBasis.muscles[m]=Mathf.Lerp(basis.muscles[m],walking.muscles[m],weight);
                        }
                        Pose(rig,frameBasis,key,i!=1&&i!=4&&i!=5);var pose=rig.Capture("Warhammer "+t.ToString("F3"),t/a.Duration);
                        if(i==5)
                        {
                            float progress=Mathf.Clamp01((t-a.preparation)/a.active);
                            Quaternion turn=Quaternion.AngleAxis(720*Mathf.SmoothStep(0,1,progress),Vector3.up);
                            pose.bodyRotation=turn*pose.bodyRotation;pose.bodyPosition=turn*pose.bodyPosition;
                        }
                        pose.blend=AttackPoseBlend.Linear;recipe.poses.Add(pose);
                    }
                    var clip=SaveClip(HumanoidAttackAuthoring.Bake(recipe,rig),HammerAssets.Clips+"/"+clipName+".anim");
                    if(returning){bindings[0].comboClips=new[]{bindings[0].clip,clip};Object.DestroyImmediate(a);}
                    else bindings.Add(new AbilityAnimationBinding{ability=a,clip=clip,maskMode=ActionMaskMode.FullBody,activeStartsAt=recipe.activeStartsAt,recoveryStartsAt=recipe.recoveryStartsAt,blendSeconds=.10f});
                    EditorUtility.SetDirty(recipe);
                }
                animations.actions=bindings.ToArray();
                var overrides=new Dictionary<string,AnimationClip>();
                foreach(string motion in new[]{"Idle","Walk","Run"})
                {
                    var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(QuaterniusHumanoidLocomotion.OutputFolder+"/Quaternius_"+motion+".anim");
                    var recipe=HammerAssets.Asset<HumanoidAttackRecipe>(Projects+"/Hammer_"+motion+".asset");recipe.model=model;recipe.previewWeapon=weapon;recipe.clipName="Hammer_"+motion;recipe.duration=source.length;recipe.frameRate=60;recipe.poses.Clear();
                    for(int frame=0;frame<=60;frame++)
                    {
                        float n=frame/60f;rig.Sample(source,n*source.length);var moving=rig.Capture("Movimiento",n);rig.StopSampling();
                        Pose(rig,moving,new Key(n,Ready,ReadyShaft),false);var pose=rig.Capture("Guardia",n);pose.blend=AttackPoseBlend.Linear;recipe.poses.Add(pose);
                    }
                    var clip=HumanoidAttackAuthoring.Bake(recipe,rig);var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
                    overrides[motion]=SaveClip(clip,HammerAssets.Clips+"/Hammer_"+motion+".anim");EditorUtility.SetDirty(recipe);
                }
                var controller=animations.locomotion;
                if(controller==null){controller=new AnimatorOverrideController(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(QuaterniusHumanoidLocomotion.ControllerPath));AssetDatabase.CreateAsset(controller,HammerAssets.Clips+"/HammerAnimations.overrideController");}
                var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(pairs);
                for(int j=0;j<pairs.Count;j++)foreach(var entry in overrides)if(pairs[j].Key.name=="Quaternius_"+entry.Key||pairs[j].Key.name==entry.Key)pairs[j]=new KeyValuePair<AnimationClip,AnimationClip>(pairs[j].Key,entry.Value);
                controller.ApplyOverrides(pairs);animations.locomotion=controller;EditorUtility.SetDirty(controller);
            }
            Icons();var kinds=new[]{"strike","heavy","slam","sweep","charge","spin"};
            var swing=Sound("Hammer-Swing",.24f,0);var heavy=Sound("Hammer-HeavySwing",.34f,1);
            var spin=Sound("Hammer-Spin",1.8f,5);
            var hit=Sound("Hammer-Impact",.20f,2);var heavyHit=Sound("Hammer-HeavyImpact",.28f,3);var ground=Sound("Hammer-Ground",.42f,4);
            for(int i=0;i<kit.Length;i++)
            {
                var a=kit[i];a.icon=AssetDatabase.LoadAssetAtPath<Texture2D>(HammerAssets.Icons+"/hammer-"+kinds[i]+".png");
                a.preparationSfx=null;a.executionSfx=i==5?spin:i==0||i==3?swing:heavy;a.executionSfxVolume=i==0?.22f:i==3?.3f:.4f;
                if(a.usesSwordCombo)
                {
                    for(int step=0;step<a.comboSteps.Length;step++){a.comboSteps[step].impactSfx=hit;a.comboSteps[step].impactVolume=step==0?.32f:.38f;}
                }
                else
                {
                    var impact=a.actions.OfType<HammerImpactAction>().Single();impact.impactSfx=impact.groundImpact?ground:i==3||i==5?hit:heavyHit;impact.impactVolume=i==5?.38f:.5f;
                }
            }
            InventoryPresentationAssets.RegenerateWeaponIcon(weapon);
        }
        static AnimationClip SaveClip(AnimationClip clip,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing==null){AssetDatabase.CreateAsset(clip,path);return clip;}
            EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);EditorUtility.SetDirty(existing);return existing;
        }
        static void Muscle(HumanoidAttackPose pose,string name,float value){int i=Array.IndexOf(HumanTrait.MuscleName,name);if(i>=0)pose.muscles[i]=value;}
        static void Pose(HumanoidAttackRig rig,HumanoidAttackPose basis,Key key,bool plant=true)
        {
            rig.Apply(basis);var rightFoot=rig.Animator.GetBoneTransform(HumanBodyBones.RightFoot).position;var leftFoot=rig.Animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            var p=basis.Copy(0);
            if(plant){p.bodyPosition.y-=Mathf.Max(0,key.lean)*.5f;p.bodyRotation=Quaternion.AngleAxis(key.twist*30,Vector3.up)*p.bodyRotation;}
            Muscle(p,"Chest Twist Left-Right",key.twist);Muscle(p,"Spine Twist Left-Right",key.twist*.45f);Muscle(p,"Chest Front-Back",key.lean*1.7f);Muscle(p,"Spine Front-Back",key.lean*.45f);
            Muscle(p,"Left Upper Leg Front-Back",.12f+Mathf.Max(0,key.lean)*.15f);Muscle(p,"Right Upper Leg Front-Back",-.06f+Mathf.Max(0,key.lean)*.15f);
            for(int m=0;m<p.muscles.Length;m++)if(HumanTrait.MuscleName[m].EndsWith("Stretched",StringComparison.Ordinal))p.muscles[m]=-.58f;
            if(!plant)for(int m=0;m<p.muscles.Length;m++)if(HumanTrait.MuscleName[m].Contains("Leg")||HumanTrait.MuscleName[m].Contains("Foot"))p.muscles[m]=basis.muscles[m];
            rig.Apply(p);
            if(plant){Solve(rig,HumanBodyBones.RightFoot,rightFoot,new Vector3(.4f,.5f,1));Solve(rig,HumanBodyBones.LeftFoot,leftFoot,new Vector3(-.4f,.5f,1));}
            Quaternion hammer=Quaternion.FromToRotation(Vector3.up,key.shaft);
            HumanoidAttackIK.TryGetLimb(rig.Animator,HumanBodyBones.RightHand,out var rightArm);
            HumanoidAttackIK.TryGetLimb(rig.Animator,HumanBodyBones.LeftHand,out var leftArm);
            Vector3 grip=key.grip;
            // Project the paired grip into both arms' reachable volumes before baking.
            for(int fit=0;fit<12;fit++)
            {
                grip=rightArm.upper.position+Vector3.ClampMagnitude(grip-rightArm.upper.position,rightArm.Length*.96f);
                Vector3 center=leftArm.upper.position-key.shaft*.30f;
                grip=center+Vector3.ClampMagnitude(grip-center,leftArm.Length*.96f);
            }
            Solve(rig,HumanBodyBones.RightHand,grip,new Vector3(.85f,.9f,-.2f));
            Solve(rig,HumanBodyBones.LeftHand,grip+key.shaft*.30f,new Vector3(-.85f,.9f,-.2f));
            rig.Animator.GetBoneTransform(HumanBodyBones.RightHand).rotation=hammer*Quaternion.Inverse(GripRotation);
            rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand).rotation=hammer*LeftRotation;
        }
        static void Solve(HumanoidAttackRig rig,HumanBodyBones bone,Vector3 target,Vector3 hint)
        {if(!HumanoidAttackIK.TryGetLimb(rig.Animator,bone,out var limb))throw new Exception("Falta "+bone);HumanoidAttackIK.Solve(limb,target,hint,false);}
        static List<Key> Keys(int i,AbilityDefinition a)
        {
            var keys=new List<Key>{new Key(0,Ready,ReadyShaft)};
            float hit=a.preparation+(i==1?.72f:i==3?.12f:i==4?.46f:0),end=a.preparation+a.active;
            if(i==5)
            {
                var extended=new Vector3(.12f,1.08f,.24f);var horizontal=new Vector3(.90f,.04f,.43f);
                keys.Add(new Key(a.preparation*.65f,new Vector3(.08f,1.08f,.16f),new Vector3(-.9f,.15f,.4f),-.24f,.06f));
                keys.Add(new Key(a.preparation,extended,horizontal,.08f,.08f));
                keys.Add(new Key(end,extended,horizontal,.08f,.08f));
                keys.Add(new Key(end+a.recovery*.5f,new Vector3(.02f,.94f,.24f),new Vector3(.25f,.5f,.85f),.04f,.05f));
                keys.Add(new Key(a.Duration,Ready,ReadyShaft));return keys;
            }
            if(i==6)
            {
                var loadReturn=new Vector3(-.12f,1.27f,.17f);var shaftReturn=new Vector3(.92f,.35f,-.10f);
                keys.Add(new Key(hit*.63f,loadReturn,shaftReturn,.27f,-.08f));
                keys.Add(new Key(hit*.84f,loadReturn,shaftReturn,.27f,-.08f));
                keys.Add(new Key(hit,Contact,ContactShaft,-.16f,.18f));
                keys.Add(new Key(end,new Vector3(.12f,1,.20f),new Vector3(-.85f,-.30f,.45f),-.26f,.18f));
                keys.Add(new Key(end+a.recovery*.55f,new Vector3(.02f,.92f,.31f),new Vector3(-.35f,.4f,.85f),-.07f,.1f));
                keys.Add(new Key(a.Duration,Ready,ReadyShaft));return keys;
            }
            bool overhead=i==2;
            Vector3 load=overhead?Loaded:new Vector3(.12f,i==3?1.08f:1.28f,.15f),shaft=overhead?LoadedShaft:new Vector3(-.92f,i==3?.1f:.4f,-.10f);
            Vector3 contact=i==2?Ground:Contact,contactShaft=i==2?GroundShaft:ContactShaft;
            keys.Add(new Key(hit*.63f,load,shaft,-.27f,-.08f));keys.Add(new Key(hit*.84f,load,shaft,-.27f,-.08f));
            keys.Add(new Key(hit,contact,contactShaft,.16f,.18f));
            {
                Vector3 follow=overhead?contact+new Vector3(-.08f,-.01f,.04f):new Vector3(-.12f,1.0f,.20f);
                Vector3 followShaft=overhead?contactShaft:new Vector3(.85f,i==3?-.05f:-.35f,.45f);
                keys.Add(new Key(end,follow,followShaft,i==3?.38f:.26f,.18f));
            }
            keys.Add(new Key(end+a.recovery*.55f,new Vector3(.02f,.92f,.31f),new Vector3(-.35f,.4f,.85f),.07f,.1f));
            keys.Add(new Key(a.Duration,Ready,ReadyShaft));return keys;
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
        public static void Icons()
        {
            // Monochrome vector-like silhouettes match QuietFantasy's 512px transparent icons.
            var preview=new Texture2D(1536,256,TextureFormat.RGB24,false);int column=0;
            foreach(string kind in new[]{"strike","heavy","slam","sweep","charge","spin"})
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
                column++;string path=HammerAssets.Icons+"/hammer-"+kind+".png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            }
            preview.Apply();File.WriteAllBytes(HammerAssets.Output+"/skill-icons.png",preview.EncodeToPNG());Object.DestroyImmediate(preview);
        }
        static bool InIcon(Vector2 p,string kind)
        {
            if(kind=="spin")
            {
                Vector2 radial=p-new Vector2(.5f,.5f);float radius=radial.magnitude;
                bool ring=radius>.33f&&radius<.37f&&!(radial.x>.12f&&radial.y>.12f);
                bool head=Mathf.Abs(p.x-.60f)<.14f&&Mathf.Abs(p.y-.65f)<.08f;
                return ring||head||Line(p,new Vector2(.34f,.30f),new Vector2(.60f,.59f),.028f)
                    ||Triangle(p,new Vector2(.82f,.73f),new Vector2(.94f,.61f),new Vector2(.72f,.59f));
            }
            Vector2 h=kind=="slam"?new Vector2(.58f,.37f):new Vector2(.58f,.68f);
            float angle=kind=="slam"?-.55f:kind=="heavy"?-.30f:-.72f;
            Vector2 d=p-h;Vector2 q=new Vector2(d.x*Mathf.Cos(angle)-d.y*Mathf.Sin(angle),d.x*Mathf.Sin(angle)+d.y*Mathf.Cos(angle));
            bool hammer=Mathf.Abs(q.x)<.195f&&Mathf.Abs(q.y)<.105f&&Mathf.Abs(q.x)+Mathf.Abs(q.y)<.275f || Mathf.Abs(q.x)<.033f&&(kind=="slam"?q.y>.1f&&q.y<.47f:q.y<-.1f&&q.y>-.47f);
            if(hammer)return true;
            if(kind=="charge")return Line(p,new Vector2(.1f,.23f),new Vector2(.7f,.23f),.027f)||Triangle(p,new Vector2(.68f,.12f),new Vector2(.91f,.23f),new Vector2(.68f,.34f));
            if(kind=="sweep") { var v=p-new Vector2(.5f,.57f); float r=new Vector2(v.x,v.y*1.5f).magnitude; return r>.36f&&r<.40f&&v.y<0; }
            if(kind=="slam")
            {
                Vector2 v=p-new Vector2(.58f,.23f);float r=new Vector2(v.x,v.y*2.8f).magnitude;
                return r>.23f&&r<.26f||Line(p,new Vector2(.37f,.24f),new Vector2(.21f,.11f),.018f)||Line(p,new Vector2(.68f,.20f),new Vector2(.79f,.085f),.018f)||Line(p,new Vector2(.47f,.18f),new Vector2(.43f,.07f),.014f);
            }
            if(kind=="heavy")return Line(p,new Vector2(.10f,.32f),new Vector2(.65f,.32f),.026f)||Line(p,new Vector2(.10f,.22f),new Vector2(.52f,.22f),.020f)||Triangle(p,new Vector2(.63f,.21f),new Vector2(.90f,.32f),new Vector2(.63f,.43f));
            return Line(p,new Vector2(.77f,.44f),new Vector2(.87f,.34f),.018f)||Line(p,new Vector2(.80f,.54f),new Vector2(.94f,.53f),.018f);
        }
        static bool Line(Vector2 p,Vector2 a,Vector2 b,float width){var v=b-a;return Vector2.Distance(p,a+v*Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude))<width;}
        static bool Triangle(Vector2 p,Vector2 a,Vector2 b,Vector2 c)
        {float Cross(Vector2 u,Vector2 v)=>u.x*v.y-u.y*v.x;float x=Cross(b-a,p-a),y=Cross(c-b,p-b),z=Cross(a-c,p-c);return x>=0&&y>=0&&z>=0||x<=0&&y<=0&&z<=0;}
    }
}
