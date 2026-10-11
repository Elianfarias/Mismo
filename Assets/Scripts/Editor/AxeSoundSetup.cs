using System;
using System.Collections.Generic;
using System.IO;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEngine;

/// <summary>Synthesizes the axe sounds chosen from the browser samples (same recipe, generator and seeds) and wires them in.</summary>
public static class AxeSoundSetup
{
    const int SR=44100;
    const string Axe="Assets/Art/Audio/Weapons/Axe",Dual="Assets/Art/Audio/Weapons/Double Axe";
    public const string Mark=Axe+"/Axe_Bleeding_Cut_Mark.wav";
    public const string Entrance=Dual+"/DualAxe_Berserk_Entrance.wav",Heartbeat=Dual+"/DualAxe_Berserk_Heartbeat.wav";
    public const string Embers=Dual+"/DualAxe_Berserk_Embers.wav",End=Dual+"/DualAxe_Berserk_End.wav";
    // Shared basic of both axe families, sample A "Filo ligero": a short whoosh; the dual axes' left strike is the same
    // recipe with another seed, a 8 % higher sweep and a 4 % faster layer, so the two axes sound apart.
    public const string BasicRight=Axe+"/Axe_Attack_Basic_Light.wav",BasicLeft=Axe+"/Axe_Attack_Basic_Light_Left.wav";
    const string BasicSource=Axe+"/Axe_Attack_Basic.mp3",DualBasic="Assets/Data/Weapons/Double Axe/DualAxeBasic.asset",AxeBasic="Assets/Data/Weapons/Axe/AxeBasic.asset";
    // Where the old basic's cut was: the double strikes' 0.41 s gap was tuned with it.
    const double BasicImpact=.34;
    const string BleedingCut="Assets/Data/Weapons/Axe/AxeBleedingCut.asset",Berserk="Assets/Data/Weapons/Double Axe/DualAxeBerserk.asset";
    static readonly Dictionary<string,float> Seconds=new Dictionary<string,float>{{Mark,.75f},{Entrance,1.6f},{Heartbeat,.62f},{Embers,4},{End,1.1f}};
    // Combo furioso: one clip per press, chosen set B "Swing al viento". A and C were liked too and stay as unused alternatives.
    const string FuriousCombo="Assets/Data/Weapons/Axe/AxeFuriousCombo.asset",SwingSource=Axe+"/Axe_Attack_Swing.mp3",Alternatives=Axe+"/Alternatives";
    public static readonly string[] Combo={Axe+"/Axe_Furious_Combo_1.wav",Axe+"/Axe_Furious_Combo_2.wav",Axe+"/Axe_Furious_Combo_3.wav"};
    static readonly string[] ComboBasicWind={Alternatives+"/Axe_Furious_Combo_BasicWind_1.wav",Alternatives+"/Axe_Furious_Combo_BasicWind_2.wav",Alternatives+"/Axe_Furious_Combo_BasicWind_3.wav"};
    // Hachazo and Desgarre, sample A of each: the combo's whoosh, slower; Desgarre adds the shield-breaking hit.
    public const string ForwardSwing=Axe+"/Axe_Forward_Swing_Heavy.wav",ArmorRend=Axe+"/Axe_Armor_Rend_Break.wav";
    const string ForwardSwingAbility="Assets/Data/Weapons/Axe/AxeForwardSwing.asset",ArmorRendAbility="Assets/Data/Weapons/Axe/AxeArmorRend.asset";
    // Giro mortal, sample B "Remolino": wind that keeps turning and opens up on each axe's cut.
    public const string DeathSpin=Dual+"/DualAxe_Death_Spin.wav";
    const string DeathSpinAbility="Assets/Data/Weapons/Double Axe/DualAxeDeathSpin.asset";
    // Lanzamiento de hacha: release "Viento suave", flight A "Hacha giratoria" (a 1 s loop on the axe) and impact "Corte limpio".
    // The return to the hand has no sound.
    public const string ThrowRelease=Dual+"/DualAxe_Throw_Release.wav",ThrowFlight=Dual+"/DualAxe_Throw_Flight.wav",ThrowImpact=Dual+"/DualAxe_Throw_Impact.wav";
    const string ThrowAbility="Assets/Data/Weapons/Double Axe/DualAxeThrow.asset";
    // Hachazo doble, sample A "Salto y doble tajo".
    public const string Leap=Dual+"/DualAxe_Leap_Slam.wav";
    const string LeapAbility="Assets/Data/Weapons/Double Axe/DualAxeLeap.asset";
    // Seconds from the active phase's start (when the sound plays) to the hit, as in the sounds they replace.
    const double ForwardSwingImpact=.17,ArmorRendImpact=.22;
    static readonly string[] ComboBodyWind={Alternatives+"/Axe_Furious_Combo_BodyWind_1.wav",Alternatives+"/Axe_Furious_Combo_BodyWind_2.wav",Alternatives+"/Axe_Furious_Combo_BodyWind_3.wav"};

    [MenuItem("Mismo/Audio/Generar sonidos de hachas")]
    public static void Install()=>Install(false);
    [MenuItem("Mismo/Audio/Regenerar sonidos de hachas")]
    public static void Regenerate()=>Install(true);
    static void Install(bool force)
    {
        ProjectAssetOrganizer.EnsureFolder(Axe);ProjectAssetOrganizer.EnsureFolder(Dual);
        float[] beat=HeartbeatBeat(out float beatScale);
        Write(Mark,TajoMark(),force);Write(Entrance,BerserkEntrance(),force);Write(Heartbeat,beat,force);
        Write(Embers,EmberLoop(beatScale),force);Write(End,EmbersOut(),force);
        var furious=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(FuriousCombo);
        if(furious==null)throw new InvalidOperationException("AxeFuriousCombo missing");
        var impacts=ComboImpacts(furious);
        ProjectAssetOrganizer.EnsureFolder(Alternatives);
        WriteCombo(ComboSets[0],ComboBasicWind,impacts,force);WriteCombo(ComboSets[1],Combo,impacts,force);WriteCombo(ComboSets[2],ComboBodyWind,impacts,force);
        if(force||!File.Exists(ForwardSwing)||!File.Exists(ArmorRend)||!File.Exists(BasicRight)||!File.Exists(BasicLeft))
        {
            var swing=Mono(SwingSource,out int swingRate);
            Write(ForwardSwing,HeavySwing(swing,swingRate),force);Write(ArmorRend,ShieldBreak(swing,swingRate),force);
            Write(BasicRight,LightBasic(swing,swingRate,false),force);Write(BasicLeft,LightBasic(swing,swingRate,true),force);
        }
        var deathSpin=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(DeathSpinAbility)??throw new InvalidOperationException("DualAxeDeathSpin missing");
        if(force||!File.Exists(DeathSpin)){var swing=Mono(SwingSource,out int swingRate);Write(DeathSpin,Whirlwind(swing,swingRate,ActionHits(deathSpin)),true);}
        var leap=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(LeapAbility)??throw new InvalidOperationException("DualAxeLeap missing");
        if(force||!File.Exists(Leap)){var swing=Mono(SwingSource,out int swingRate);Write(Leap,LeapSlam(swing,swingRate,ActionHits(leap)[0]),true);}
        Write(ThrowRelease,ThrowReleaseWind(),force);Write(ThrowFlight,ThrowWhirl(),force);Write(ThrowImpact,ThrowCut(),force);
        AssetDatabase.Refresh();
        var throwing=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(ThrowAbility)??throw new InvalidOperationException("DualAxeThrow missing");
        var throwAction=Array.Find(throwing.actions,a=>a is AxeThrowAction) as AxeThrowAction??throw new InvalidOperationException("DualAxeThrow has no AxeThrowAction");
        throwing.executionSfx=Clip(ThrowRelease);throwAction.flightSfx=Clip(ThrowFlight);throwAction.impactSfx=Clip(ThrowImpact);EditorUtility.SetDirty(throwing);
        deathSpin.executionSfx=Clip(DeathSpin);EditorUtility.SetDirty(deathSpin);
        leap.executionSfx=Clip(Leap);EditorUtility.SetDirty(leap);
        var forwardSwing=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(ForwardSwingAbility);var armorRend=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(ArmorRendAbility);
        if(forwardSwing==null||armorRend==null)throw new InvalidOperationException("Hachazo or Desgarre missing");
        forwardSwing.executionSfx=Clip(ForwardSwing);armorRend.executionSfx=Clip(ArmorRend);
        EditorUtility.SetDirty(forwardSwing);EditorUtility.SetDirty(armorRend);
        // Each press sounds its own clip, indexed by recast stage; the clip starts when the press's active phase begins.
        // Existing entries keep their Inspector volume.
        if(furious.comboStepSfx==null||furious.comboStepSfx.Length<Combo.Length)
        {
            var steps=new ComboStepSound[Combo.Length];
            for(int i=0;i<steps.Length;i++)steps[i]=furious.comboStepSfx!=null&&i<furious.comboStepSfx.Length&&furious.comboStepSfx[i]!=null?furious.comboStepSfx[i]:new ComboStepSound();
            furious.comboStepSfx=steps;
        }
        for(int i=0;i<Combo.Length;i++)furious.comboStepSfx[i].clip=Clip(Combo[i]);
        EditorUtility.SetDirty(furious);
        var dualBasic=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(DualBasic);
        if(dualBasic==null||dualBasic.comboStepSfx==null||dualBasic.comboStepSfx.Length<4)throw new InvalidOperationException("DualAxeBasic needs sounds for its four combo steps");
        var right=Clip(BasicRight);var left=Clip(BasicLeft);
        var axeBasic=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AxeBasic)??throw new InvalidOperationException("AxeBasic missing");
        axeBasic.executionSfx=right;dualBasic.executionSfx=right;EditorUtility.SetDirty(axeBasic);
        dualBasic.comboStepSfx[0].clip=right;dualBasic.comboStepSfx[1].clip=left;
        // Doble filo only turns a basic into a double strike: the lead axe sounds its basic, the other one its own 0.41 s later
        // (in the clip the cuts land at 0.31 and 0.62 of 1.3 s).
        Double(dualBasic.comboStepSfx[2],right,left);Double(dualBasic.comboStepSfx[3],left,right);
        EditorUtility.SetDirty(dualBasic);
        var cut=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(BleedingCut);var berserk=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Berserk);
        if(cut==null||berserk==null)throw new InvalidOperationException("Axe abilities missing");
        cut.executionSfx=Clip(Mark);berserk.executionSfx=Clip(Entrance);
        var mode=Array.Find(berserk.actions,a=>a is BerserkAction) as BerserkAction;
        if(mode==null)throw new InvalidOperationException("DualAxeBerserk has no BerserkAction");
        mode.heartbeatSfx=Clip(Heartbeat);mode.embersSfx=Clip(Embers);mode.endSfx=Clip(End);
        EditorUtility.SetDirty(cut);EditorUtility.SetDirty(berserk);AssetDatabase.SaveAssets();
    }
    const float DoubleStrikeGap=.41f;
    static void Double(ComboStepSound step,AudioClip lead,AudioClip second){step.clip=lead;step.followUpClip=second;step.followUpDelay=DoubleStrikeGap;}
    static AudioClip Clip(string path)=>AssetDatabase.LoadAssetAtPath<AudioClip>(path)??throw new InvalidOperationException("Missing clip "+path);

    public static void Check()
    {
        var failures=new List<string>();
        void Require(bool pass,string message){Debug.Log((pass?"AXE_SOUND_CHECK ":"AXE_SOUND_FAIL ")+message);if(!pass)failures.Add(message);}
        foreach(var pair in Seconds)
        {
            var samples=File.Exists(pair.Key)?Read(pair.Key):null;
            Require(samples!=null,pair.Key+" exists");if(samples==null)continue;
            Require(Mathf.Abs(samples.Length/(float)SR-pair.Value)<.02f,pair.Key+" lasts "+pair.Value+" s");
            float peak=0;foreach(var s in samples)peak=Mathf.Max(peak,Mathf.Abs(s));
            Require(pair.Key==Embers?peak>.05f:Mathf.Abs(peak-(pair.Key==Heartbeat?.8f:.85f))<.02f,pair.Key+" is audible at its level (peak "+peak.ToString("0.00")+")");
            Require(Mathf.Abs(samples[samples.Length-1])<.01f,pair.Key+" ends in silence");
        }
        var embers=File.Exists(Embers)?Read(Embers):null;
        Require(embers!=null&&Mathf.Abs(embers[0])<.01f&&Mathf.Abs(embers[embers.Length-1])<.01f,"The embers loop has no click at its seam");
        var cut=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(BleedingCut);var berserk=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Berserk);
        Require(cut!=null&&AssetDatabase.GetAssetPath(cut.executionSfx)==Mark,"Tajo sangrante plays its own cut");
        Require(berserk!=null&&AssetDatabase.GetAssetPath(berserk.executionSfx)==Entrance,"The Berserker plays its own entrance");
        var mode=berserk!=null?Array.Find(berserk.actions,a=>a is BerserkAction) as BerserkAction:null;
        Require(mode!=null&&AssetDatabase.GetAssetPath(mode.heartbeatSfx)==Heartbeat&&AssetDatabase.GetAssetPath(mode.embersSfx)==Embers&&AssetDatabase.GetAssetPath(mode.endSfx)==End,"The Berserker carries its heartbeat, embers and end");
        foreach(var path in new[]{BasicRight,BasicLeft})
        {
            var samples=File.Exists(path)?Read(path):null;
            Require(samples!=null,path+" exists");if(samples==null)continue;
            int at=0;float peak=0;for(int k=0;k<samples.Length;k++)if(Mathf.Abs(samples[k])>peak){peak=Mathf.Abs(samples[k]);at=k;}
            Require(Mathf.Abs(peak-.85f)<.02f&&Mathf.Abs(samples[samples.Length-1])<.01f,path+" is audible at its level and ends in silence");
            Require(Math.Abs(at/(double)SR-BasicImpact)<.04,path+" cuts where the old basic did (expected "+BasicImpact.ToString("0.00")+" s, got "+(at/(double)SR).ToString("0.000")+")");
        }
        var dualBasic=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(DualBasic);var axeBasic=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AxeBasic);
        Require(axeBasic!=null&&dualBasic!=null&&AssetDatabase.GetAssetPath(axeBasic.executionSfx)==BasicRight&&AssetDatabase.GetAssetPath(dualBasic.executionSfx)==BasicRight,"Both axe families share the light basic");
        Require(dualBasic!=null&&dualBasic.comboStepSfx.Length>=2&&AssetDatabase.GetAssetPath(dualBasic.comboStepSfx[0].clip)==BasicRight
            &&AssetDatabase.GetAssetPath(dualBasic.comboStepSfx[1].clip)==BasicLeft,"Dual axes: the right strike sounds like the one-hand basic, the left one like its variant");
        bool Pair(int step,string lead,string second)=>dualBasic!=null&&dualBasic.comboStepSfx.Length>step&&AssetDatabase.GetAssetPath(dualBasic.comboStepSfx[step].clip)==lead
            &&AssetDatabase.GetAssetPath(dualBasic.comboStepSfx[step].followUpClip)==second&&Mathf.Approximately(dualBasic.comboStepSfx[step].followUpDelay,DoubleStrikeGap);
        Require(Pair(2,BasicRight,BasicLeft)&&Pair(3,BasicLeft,BasicRight),"Doble filo's double strikes sound both basics, the lead axe first");
        Require(dualBasic!=null&&dualBasic.comboStepSfx[0].followUpClip==null&&dualBasic.comboStepSfx[1].followUpClip==null,"Single strikes keep a single sound");
        var furious=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(FuriousCombo);var impacts=furious!=null?ComboImpacts(furious):new double[Combo.Length];
        for(int i=0;i<Combo.Length;i++)
        {
            Require(furious!=null&&furious.comboStepSfx!=null&&furious.comboStepSfx.Length>i&&AssetDatabase.GetAssetPath(furious.comboStepSfx[i].clip)==Combo[i]
                &&furious.comboStepSfx[i].followUpClip==null,"Combo furioso press "+(i+1)+" plays its own clip");
            var samples=File.Exists(Combo[i])?Read(Combo[i]):null;
            Require(samples!=null,Combo[i]+" exists");if(samples==null)continue;
            int at=0;float peak=0;for(int k=0;k<samples.Length;k++)if(Mathf.Abs(samples[k])>peak){peak=Mathf.Abs(samples[k]);at=k;}
            Require(Mathf.Abs(peak-.85f)<.02f&&Mathf.Abs(samples[samples.Length-1])<.01f,Combo[i]+" is audible at its level and ends in silence");
            // The loudest moment is the cut: it must land on the strike as tuned now (regenerate after retiming the presses).
            Require(Math.Abs(at/(double)SR-impacts[i])<.04,Combo[i]+" peaks on its strike (expected "+impacts[i].ToString("0.000")+" s, got "+(at/(double)SR).ToString("0.000")+")");
        }
        foreach(var (path,ability,impact,name) in new[]{(ForwardSwing,ForwardSwingAbility,ForwardSwingImpact,"Hachazo"),(ArmorRend,ArmorRendAbility,ArmorRendImpact,"Desgarre")})
        {
            var definition=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(ability);
            Require(definition!=null&&AssetDatabase.GetAssetPath(definition.executionSfx)==path,name+" plays its own sound");
            var samples=File.Exists(path)?Read(path):null;
            Require(samples!=null,path+" exists");if(samples==null)continue;
            int at=0;float peak=0;for(int k=0;k<samples.Length;k++)if(Mathf.Abs(samples[k])>peak){peak=Mathf.Abs(samples[k]);at=k;}
            Require(Mathf.Abs(peak-.85f)<.02f&&Mathf.Abs(samples[samples.Length-1])<.01f,path+" is audible at its level and ends in silence");
            Require(Math.Abs(at/(double)SR-impact)<.04,path+" peaks on its hit (expected "+impact.ToString("0.00")+" s, got "+(at/(double)SR).ToString("0.000")+")");
        }
        var spin=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(DeathSpinAbility);
        Require(spin!=null&&AssetDatabase.GetAssetPath(spin.executionSfx)==DeathSpin,"Giro mortal plays its own whirl");
        var whirl=File.Exists(DeathSpin)?Read(DeathSpin):null;
        Require(whirl!=null,DeathSpin+" exists");
        if(whirl!=null&&spin!=null)
        {
            var hits=ActionHits(spin);int at=0;float top=0;for(int k=0;k<whirl.Length;k++)if(Mathf.Abs(whirl[k])>top){top=Mathf.Abs(whirl[k]);at=k;}
            Require(Mathf.Abs(top-.85f)<.02f&&Mathf.Abs(whirl[whirl.Length-1])<.01f,DeathSpin+" is audible at its level and ends in silence");
            Require(Array.Exists(hits,h=>Math.Abs(at/(double)SR-h)<.04),DeathSpin+" peaks on one of the axes' cuts (got "+(at/(double)SR).ToString("0.000")+" s)");
            // The whirl opens up on every cut: each one is louder than the gap between them.
            float Rms(double t){int s=(int)(t*SR);double e=0;for(int k=s-1000;k<s+1000;k++)e+=whirl[k]*whirl[k];return (float)Math.Sqrt(e/2000);}
            Require(hits.Length<2||(Rms(hits[0])>Rms((hits[0]+hits[1])/2)*1.3f&&Rms(hits[1])>Rms((hits[0]+hits[1])/2)*1.3f),"Each axe's cut stands out of the whirl");
        }
        var leapAbility=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(LeapAbility);
        Require(leapAbility!=null&&AssetDatabase.GetAssetPath(leapAbility.executionSfx)==Leap,"Hachazo doble plays its own leap");
        var slam=File.Exists(Leap)?Read(Leap):null;
        Require(slam!=null,Leap+" exists");
        if(slam!=null&&leapAbility!=null)
        {
            double land=ActionHits(leapAbility)[0];int at=0;float top=0;for(int k=0;k<slam.Length;k++)if(Mathf.Abs(slam[k])>top){top=Mathf.Abs(slam[k]);at=k;}
            Require(Mathf.Abs(top-.85f)<.02f&&Mathf.Abs(slam[slam.Length-1])<.01f,Leap+" is audible at its level and ends in silence");
            Require(Math.Abs(at/(double)SR-land)<.04,Leap+" peaks on the landing (expected "+land.ToString("0.00")+" s, got "+(at/(double)SR).ToString("0.000")+")");
        }
        var throwAbility=AssetDatabase.LoadAssetAtPath<AbilityDefinition>(ThrowAbility);
        var thrown=throwAbility!=null?Array.Find(throwAbility.actions,a=>a is AxeThrowAction) as AxeThrowAction:null;
        Require(throwAbility!=null&&AssetDatabase.GetAssetPath(throwAbility.executionSfx)==ThrowRelease,"Lanzamiento de hacha plays its soft release");
        Require(thrown!=null&&AssetDatabase.GetAssetPath(thrown.flightSfx)==ThrowFlight&&AssetDatabase.GetAssetPath(thrown.impactSfx)==ThrowImpact,"The thrown axe carries its whirl and its cut");
        foreach(var (path,level,seconds) in new[]{(ThrowRelease,.85f,.5f),(ThrowFlight,.7f,1f),(ThrowImpact,.85f,.4f)})
        {
            var samples=File.Exists(path)?Read(path):null;
            Require(samples!=null,path+" exists");if(samples==null)continue;
            float top=0;foreach(var s in samples)top=Mathf.Max(top,Mathf.Abs(s));
            Require(Mathf.Abs(top-level)<.02f&&Mathf.Abs(samples.Length/(float)SR-seconds)<.01f,path+" lasts "+seconds+" s at its level");
        }
        var whirlLoop=File.Exists(ThrowFlight)?Read(ThrowFlight):null;
        if(whirlLoop!=null)
        {
            // The loop closes without a click: the jump across the seam is no bigger than an ordinary step inside it.
            double step=0;for(int k=1;k<whirlLoop.Length;k++)step+=Math.Abs(whirlLoop[k]-whirlLoop[k-1]);step/=whirlLoop.Length-1;
            Require(Math.Abs(whirlLoop[whirlLoop.Length-1]-whirlLoop[0])<=step*3+.002,"The flight loop has no click at its seam");
        }
        foreach(var path in ComboBasicWind)Require(File.Exists(path),"Alternative kept: "+path);
        foreach(var path in ComboBodyWind)Require(File.Exists(path),"Alternative kept: "+path);
        Debug.Log(failures.Count==0?"AXE_SOUND_PASS":"AXE_SOUND_FAIL "+failures.Count+" failures");
        if(failures.Count>0)throw new Exception(string.Join("; ",failures));
    }

    // ---- Recipes: a direct port of the browser samples. ----
    // Set 2 · C "Corte con filo": a rising and falling hiss, a bright edge, a short metallic ring and a wet, slower hit.
    static float[] TajoMark()
    {
        const double dur=.22,wetAt=.15,slow=1.25;var a=Buffer(.75);var r=new Rng(307);double lp=0,lp2=0;
        for(int i=0;i<(int)Math.Floor(dur*SR);i++)
        {
            double u=i/(dur*SR),x=r.Next()*2-1,cut=.08+.55*Math.Sin(Math.PI*Math.Min(1,u*1.3));
            lp+=cut*(x-lp);lp2+=.04*(lp-lp2);a[i]+=(float)((lp-lp2)*Math.Pow(Math.Sin(Math.PI*u),1.4)*.75);
        }
        Noise(a,dur*.55,.06,.95,.45,t=>Math.Exp(-t*45),.45,r);
        int ring=(int)Math.Floor(dur*.55*SR);
        for(int i=0;i<(int)Math.Floor(.25*SR);i++){double t=i/(double)SR;if(ring+i<a.Length)a[ring+i]+=(float)((Math.Sin(2*Math.PI*2350*t)+.6*Math.Sin(2*Math.PI*3720*t))*Math.Exp(-t*28)*.12);}
        Thump(a,wetAt,240,85,40/slow,20/slow,.75);
        Noise(a,wetAt+.01,.28,.055,0,t=>Math.Exp(-t*20/slow)*(1+.6*Math.Sin(2*Math.PI*38*t)),.55,r);
        return Finish(a,.85f);
    }
    // Set 2 · B "Más grave": two deep drums with a sub tail, the growing heat wave and embers.
    static float[] BerserkEntrance()
    {
        var a=Buffer(1.6);var r=new Rng(103);double[] drums={0,.34};const double f0=76,f1=32;
        foreach(var at in drums){Thump(a,at,f0,f1,20,6,1);Noise(a,at,.14,.22,.04,t=>Math.Exp(-t*26),.4,r);Thump(a,at,f1*1.1,f1*.75,6,3.5,.55);}
        double l1=0,l2=0,w0=drums[drums.Length-1]+.05;
        for(int i=(int)Math.Floor(w0*SR);i<a.Length;i++)
        {
            double u=(i/(double)SR-w0)/(1.6-w0),x=r.Next()*2-1;l1+=(.01+.3*u*u)*(x-l1);l2+=.01*(l1-l2);
            double env=u<.85?u/.85:Math.Max(0,(1-u)/.15);a[i]+=(float)((l1-l2)*env*.9);
        }
        Crackles(a,w0*.6,1.55,t=>24*(.4+t),.55,r);
        return Finish(a,.85f);
    }
    // Set 3 · A "Latido y brasas": one very deep lub-dub; the embers go in their own loop at the same relative level.
    static float[] HeartbeatBeat(out float scale)
    {
        var a=Buffer(.62);var r=new Rng(203);
        Thump(a,0,44,29,26,15,1);Thump(a,.22,44*.85,29*.85,26,17,.72);Noise(a,0,.05,.04,0,t=>Math.Exp(-t*60),.12,r);
        float peak=Peak(a);scale=peak>0?.8f/peak:1;return Finish(a,.8f);
    }
    static float[] EmberLoop(float scale)
    {
        var a=Buffer(4);var r=new Rng(209);
        Crackles(a,.01,3.98,t=>25,.22,r);
        for(int i=0;i<a.Length;i++)a[i]*=scale;
        return a;
    }
    // Set 1 · B "Brasas que se apagan".
    static float[] EmbersOut()
    {
        var a=Buffer(1.1);var r=new Rng(71);
        Crackles(a,0,1.1,t=>40*Math.Exp(-t*3.5),.6,r);Thump(a,0,90,35,4,3.5,.6);Noise(a,0,1.1,.03,0,t=>Math.Exp(-t*4),.4,r);
        return Finish(a,.85f);
    }

    // Combo furioso, fourth set: the wind of sample B "Corte de viento" plus the existing axe whoosh without its low end.
    sealed class Wind{public double lead,tail,f0,peak,f1,q,air,slice,doubled;}
    static readonly Wind[] ComboWind=
    {
        new Wind{lead=.176,tail=.25,f0=500,peak=1800,f1=600,q=1.6,air=1.4,slice=.45},
        new Wind{lead=.3,tail=.25,f0=560,peak=2000,f1=700,q=1.6,air=1.4,slice=.45},
        new Wind{lead=.38,tail=.35,f0=300,peak=1200,f1=380,q=1.3,air=1.6,slice=.5,doubled=.07},
    };
    sealed class ComboSet{public int seed;public string layer;public double[] rate;public double highpass,layerGain,wind,dry;public bool body;}
    static readonly ComboSet[] ComboSets=
    {
        // A "Básico al viento".
        new ComboSet{seed=700,layer=BasicSource,rate=new[]{1,1.06,.9},highpass=380,layerGain=1,wind=.7,dry=.1},
        // B "Swing al viento": the chosen one.
        new ComboSet{seed=710,layer=SwingSource,rate=new[]{1.1,1.16,.96},highpass=330,layerGain=1,wind=.6,dry=.1},
        // C "Viento con cuerpo".
        new ComboSet{seed=720,layer=BasicSource,rate=new[]{1,1.06,.9},highpass=450,layerGain=.35,wind=1,dry=0,body=true},
    };
    // Seconds from the start of each press's active phase (when its sound plays) to its strike, as tuned in the Inspector.
    static double[] ComboImpacts(AbilityDefinition furious)
    {
        var strikes=Array.Find(furious.actions,a=>a is RecastStrikeAction) as RecastStrikeAction;
        if(strikes==null||furious.recastStages==null||furious.recastStages.Length<Combo.Length||strikes.strikes.Length<Combo.Length)
            throw new InvalidOperationException("AxeFuriousCombo needs three recast stages and strikes");
        var impacts=new double[Combo.Length];
        for(int i=0;i<impacts.Length;i++)impacts[i]=strikes.strikes[i].at*furious.recastStages[i].active;
        return impacts;
    }
    static void WriteCombo(ComboSet set,string[] paths,double[] impacts,bool force)
    {
        float[] source=null;int sourceRate=0;
        for(int i=0;i<paths.Length;i++)
        {
            if(File.Exists(paths[i])&&!force)continue;
            if(source==null)source=Mono(set.layer,out sourceRate);
            Write(paths[i],ComboHit(set,i,impacts[i],source,sourceRate),true);
        }
    }
    static float[] ComboHit(ComboSet set,int i,double impact,float[] source,int sourceRate)
    {
        var o=ComboWind[i];var r=new Rng(set.seed+i);var a=Buffer(impact+o.tail+.35);
        Blade(a,impact,o,o.peak,o.air,r);
        if(o.doubled>0)Blade(a,impact-o.doubled,o,o.peak*1.15,o.air*.6,r);
        if(set.body)Blade(a,impact,new Wind{lead=o.lead,tail=o.tail,f0=220,peak=650,f1=260,q=.8},650,o.air*.8,r);
        Scale(a,(float)set.wind);Slice(a,impact,o.slice*set.wind,r);
        Layer(a,impact,source,sourceRate,set.rate[i],set.highpass,set.layerGain,set.dry);
        int fade=(int)Math.Floor(.03*SR);for(int k=0;k<fade;k++)a[a.Length-1-k]*=k/(float)fade;
        return Scale(a,.85f);
    }
    // Hachazo A "Swing pesado": the combo's whoosh, slower and lower, without a hit.
    static float[] HeavySwing(float[] swing,int swingRate)
    {
        var o=new Wind{lead=.17,tail=.3,f0=350,peak=1500,f1=420,q=1.4,air=1.5,slice=.45};
        return Swung(o,ForwardSwingImpact,900,.65,swing,swingRate,.95,260,1,.15,null);
    }
    // Desgarre A "Rompe escudo": a low whoosh, a heavy hit with a sub, a splintering crack and debris.
    static float[] ShieldBreak(float[] swing,int swingRate)
    {
        var o=new Wind{lead=.22,tail=.4,f0=220,peak=1000,f1=260,q=1.1,air=1.8,slice=.5};
        return Swung(o,ArmorRendImpact,930,.6,swing,swingRate,.82,180,.8,.1,(hit,impact,r)=>
        {
            Thump(hit,impact,120,45,25,9,.9);Thump(hit,impact,55,35,8,5,.5);
            Crack(hit,impact,.6,r);Debris(hit,impact+.02,.35,60,.35,r);
        });
    }
    // Basic A "Filo ligero": almost all wind, a short "fsh" over a little of the swing. The left axe sweeps 8 % higher
    // over a 4 % faster layer, with its own seed.
    static float[] LightBasic(float[] swing,int swingRate,bool left)
    {
        var o=new Wind{lead=.12,tail=.14,f0=600,peak=2100*(left?1.08:1),f1=750,q=1.8,air=1,slice=.35};
        return Swung(o,BasicImpact,left?961:960,1,swing,swingRate,1.3*(left?1.04:1),450,.5,0,null,.25);
    }
    static float[] Swung(Wind o,double impact,int seed,double wind,float[] swing,int swingRate,double rate,double highpass,double gain,double dry,Action<float[],double,Rng> strike,double pad=.55)
    {
        var r=new Rng(seed);var a=Buffer(impact+o.tail+pad);
        Blade(a,impact,o,o.peak,o.air,r);
        Scale(a,(float)wind);Slice(a,impact,o.slice*wind,r);
        Layer(a,impact,swing,swingRate,rate,highpass,gain,dry);
        if(strike!=null){var hit=new float[a.Length];strike(hit,impact,r);for(int i=0;i<a.Length;i++)a[i]+=hit[i];}
        int fade=(int)Math.Floor(.03*SR);for(int k=0;k<fade;k++)a[a.Length-1-k]*=k/(float)fade;
        return Scale(a,.85f);
    }
    // Seconds from the active phase's start (when the sound plays) to each hit of its FuriousComboAction, as tuned in the Inspector.
    static double[] ActionHits(AbilityDefinition ability)
    {
        var action=Array.Find(ability.actions,a=>a is FuriousComboAction) as FuriousComboAction;
        if(action==null||action.hits==null||action.hits.Length==0)throw new InvalidOperationException(AssetDatabase.GetAssetPath(ability)+" has no hits");
        return Array.ConvertAll(action.hits,h=>(double)h.at*ability.active);
    }
    static float[] Whirlwind(float[] swing,int swingRate,double[] hits)
    {
        var r=new Rng(1010);var a=Buffer(1.05);
        Whirl(a,0,.62,380,1600,.07,1.3,1.6,.35,hits,r);
        Scale(a,.75f);foreach(var h in hits)Slice(a,h,.35*.75,r);
        for(int i=0;i<hits.Length;i++)Layer(a,hits[i],swing,swingRate,1.2*(i>0?1.04:1),380,.55*.75,0);
        int fade=(int)Math.Floor(.03*SR);for(int k=0;k<fade;k++)a[a.Length-1-k]*=k/(float)fade;
        return Scale(a,.85f);
    }
    // Release "Viento suave": only air, low and round.
    static float[] ThrowReleaseWind()
    {
        var r=new Rng(1310);var a=Buffer(.5);var o=new Wind{lead=.14,tail=.22,f0=250,peak=1100,f1=300,q=1};
        Blade(a,.14,o,o.peak,1.6,r);
        return Faded(a,.85f);
    }
    // Flight "Hacha giratoria": a whum each quarter second (4 turns per second), every other one 4 % higher. Rendered 0.1 s
    // longer and the overhang crossfaded into the start, so the loop has no seam.
    static float[] ThrowWhirl()
    {
        var r=new Rng(1201);var b=Buffer(1.1);var o=new Wind{lead=.1,tail=.1,f0=300,peak=900,f1=320,q=1.2};
        for(int k=0;k<5;k++)Blade(b,.125+k*.25,o,o.peak*(k%2==1?1.04:1),1,r);
        int n=SR,x=(int)Math.Floor(.1*SR);var a=new float[n];Array.Copy(b,a,n);
        for(int i=0;i<x;i++)a[i]=b[i]*(i/(float)x)+b[n+i]*(1-i/(float)x);
        return Scale(a,.7f);
    }
    // Impact "Corte limpio": the last turn carrying through, a loud edge hiss and a bright crack; no low hit.
    static float[] ThrowCut()
    {
        var r=new Rng(1420);var a=Buffer(.4);
        Blade(a,.06,new Wind{lead=.06,tail=.09,f0=300,peak=1100,f1=500,q=1.4},1100,1,r);
        Slice(a,.06,1.1,r);Band(a,.06,.06,3400,1.5,80,.6,r);
        return Faded(a,.85f);
    }
    static float[] Faded(float[] a,float level){int f=(int)Math.Floor(.02*SR);for(int k=0;k<f;k++)a[a.Length-1-k]*=k/(float)f;return Scale(a,level);}
    // Noise through a fixed resonant band with its own decay.
    static void Band(float[] a,double at,double dur,double f,double q,double decay,double amp,Rng r)
    {
        int s=(int)Math.Floor(at*SR),n=(int)Math.Floor(dur*SR);double w=2*Math.PI*f/SR,alpha=Math.Sin(w)/(2*q),cos=Math.Cos(w),a0=1+alpha,x1=0,x2=0,y1=0,y2=0;
        for(int i=0;i<n&&s+i<a.Length;i++)
        {
            double t=i/(double)SR,x=r.Next()*2-1,y=(alpha*x-alpha*x2+2*cos*y1-(1-alpha)*y2)/a0;x2=x1;x1=x;y2=y1;y1=y;
            a[s+i]+=(float)(y*Math.Exp(-t*decay)*amp*Math.Min(1,i/20.0));
        }
    }
    // Hachazo doble A "Salto y doble tajo": a takeoff, the air rising through the fall, and both axes cutting 25 ms apart on
    // landing over a medium hit and some dirt.
    static float[] LeapSlam(float[] swing,int swingRate,double land)
    {
        var r=new Rng(1100);var a=Buffer(land+.75);
        var takeoff=new Wind{lead=.03,tail=.12,f0=200,peak=650,f1=240,q=1};
        var fall=new Wind{lead=.4,tail=.08,f0=250,peak=1300,f1=400,q=1};
        var cut=new Wind{lead=.12,tail=.2,f0=450,peak=1700,f1=550,q=1.4};
        const double gap=.025;
        Blade(a,.03,takeoff,takeoff.peak,3.5,r);Blade(a,land,fall,fall.peak,2.8,r);
        Blade(a,land-gap,cut,cut.peak,1.4,r);Blade(a,land,cut,cut.peak*1.08,1.4,r);
        Scale(a,.65f);Slice(a,land-gap,.4*.65,r);Slice(a,land,.45*.65,r);
        Layer(a,land-gap,swing,swingRate,1.1,330,.54,.05);Layer(a,land,swing,swingRate,1.1*1.04,330,.54,.05);
        var hit=new float[a.Length];Thump(hit,land,130,55,30,14,.7);Debris(hit,land+.02,.25,55,.28,r);
        for(int i=0;i<a.Length;i++)a[i]+=hit[i];
        int fade=(int)Math.Floor(.03*SR);for(int k=0;k<fade;k++)a[a.Length-1-k]*=k/(float)fade;
        return Scale(a,.85f);
    }
    // Wind that keeps turning: a resonant band whose pitch and level rise as each axe passes (a bump on every hit).
    static void Whirl(float[] a,double from,double to,double f0,double peak,double width,double q,double air,double bed,double[] hits,Rng r)
    {
        int s0=(int)Math.Floor(from*SR),s1=Math.Min(a.Length,(int)Math.Floor((to+.18)*SR));double x1=0,x2=0,y1=0,y2=0;
        for(int i=s0;i<s1;i++)
        {
            double t=i/(double)SR,bump=0;foreach(var h in hits){double u=(t-h)/width;bump+=Math.Exp(-u*u);}bump=Math.Min(1,bump);
            double swell=Math.Min(1,(t-from)/.08),fade=t>to?Math.Exp(-(t-to)*18):1,env=swell*fade*(bed+(1-bed)*bump);
            double f=f0+(peak-f0)*bump,w=2*Math.PI*f/SR,alpha=Math.Sin(w)/(2*q),cos=Math.Cos(w),a0=1+alpha;
            double x=r.Next()*2-1,y=(alpha*x-alpha*x2+2*cos*y1-(1-alpha)*y2)/a0;x2=x1;x1=x;y2=y1;y1=y;
            a[i]+=(float)(y*env*air);
        }
    }
    // A splintering crack: a burst of bright noise with a few hard clicks.
    static void Crack(float[] a,double at,double amp,Rng r)
    {
        int s=(int)Math.Floor(at*SR),n=(int)Math.Floor(.09*SR);double lp=0;
        for(int i=0;i<n&&s+i<a.Length;i++){double x=r.Next()*2-1;lp+=.5*(x-lp);a[s+i]+=(float)((x-lp)*Math.Exp(-70.0*i/SR)*amp);}
        for(int c=0;c<5;c++)
        {
            int p=s+(int)Math.Floor(r.Next()*.03*SR);double g=amp*(.5+.5*r.Next());
            for(int j=0;j<60&&p+j<a.Length;j++)a[p+j]+=(float)((r.Next()*2-1)*Math.Exp(-j/12.0)*g);
        }
    }
    // Pieces landing after the break, thinning out.
    static void Debris(float[] a,double at,double dur,double rate,double amp,Rng r)
    {
        double t=at;
        while(true)
        {
            t+=-Math.Log(1-r.Next())/Math.Max(1,rate*Math.Exp(-(t-at)*6));if(t>=at+dur)break;
            int s=(int)Math.Floor(t*SR),n=(int)Math.Floor(SR*(.002+.006*r.Next()));double g=amp*(.35+.65*r.Next())*Math.Exp(-(t-at)*4),prev=0;
            for(int j=0;j<n&&s+j<a.Length;j++){double x=r.Next()*2-1;a[s+j]+=(float)((x-prev)*Math.Exp(-j/(double)n*5)*g);prev=x;}
        }
    }
    // Noise through a resonant bandpass that sweeps up toward the impact and falls after it, like a blade passing by.
    static void Blade(float[] a,double impact,Wind o,double peak,double air,Rng r)
    {
        int s0=Math.Max(0,(int)Math.Floor((impact-o.lead)*SR)),i0=(int)Math.Floor(impact*SR),s1=Math.Min(a.Length,(int)Math.Floor((impact+o.tail)*SR));
        double x1=0,x2=0,y1=0,y2=0;
        for(int i=s0;i<s1;i++)
        {
            double env,f;
            if(i<i0){double u=(i-s0)/(double)Math.Max(1,i0-s0);env=Math.Pow(u,2.4);f=o.f0+(peak-o.f0)*Math.Pow(u,1.6);}
            else{double u=(i-i0)/(double)SR/o.tail;env=Math.Exp(-4.5*u);f=o.f1+(peak-o.f1)*Math.Exp(-3*u);}
            double w=2*Math.PI*f/SR,alpha=Math.Sin(w)/(2*o.q),cos=Math.Cos(w),a0=1+alpha;
            double x=r.Next()*2-1,y=(alpha*x-alpha*x2+2*cos*y1-(1-alpha)*y2)/a0;x2=x1;x1=x;y2=y1;y1=y;
            a[i]+=(float)(y*env*air);
        }
    }
    // The short bright hiss of the cut.
    static void Slice(float[] a,double at,double amp,Rng r)
    {
        int s0=(int)Math.Floor(at*SR),n=(int)Math.Floor(.12*SR);double l1=0,l2=0;
        for(int i=0;i<n&&s0+i<a.Length;i++){double x=r.Next()*2-1;l1+=.9*(x-l1);l2+=.35*(x-l2);a[s0+i]+=(float)((l1-l2)*Math.Exp(-60.0*i/SR)*amp);}
    }
    // The existing axe sound, sped up, with its low end removed (twice through a 12 dB highpass, true Q .707),
    // placed so its whoosh peak lands on the impact; dry adds back a little of the untouched sound.
    static void Layer(float[] a,double impact,float[] source,int sourceRate,double rate,double highpass,double gain,double dry)
    {
        int n=(int)Math.Floor(source.Length*(double)SR/sourceRate/rate);var resampled=new float[n];
        for(int i=0;i<n;i++)
        {
            double p=i*rate*sourceRate/SR,t=p-Math.Floor(p);int k=(int)p;
            resampled[i]=(float)((k<source.Length?source[k]:0)*(1-t)+(k+1<source.Length?source[k+1]:0)*t);
        }
        var high=Highpass(Highpass(resampled,highpass),highpass);
        int at=0;double best=0,env=0;
        for(int i=0;i<n;i++){env+=.004*(Math.Abs(high[i])-env);if(env>best){best=env;at=i;}}
        float highPeak=Peak(high),rawPeak=Peak(resampled);int offset=(int)Math.Floor(impact*SR)-at;
        for(int i=0;i<n;i++){int j=i+offset;if(j<0||j>=a.Length)continue;a[j]+=(float)(gain*(high[i]/highPeak+dry*resampled[i]/rawPeak));}
    }
    static float[] Highpass(float[] a,double frequency)
    {
        double w=2*Math.PI*frequency/SR,alpha=Math.Sin(w)/(2*.707),cos=Math.Cos(w),a0=1+alpha;
        double b0=(1+cos)/2,b1=-(1+cos),b2=(1+cos)/2,a1=-2*cos,a2=1-alpha,x1=0,x2=0,y1=0,y2=0;var o=new float[a.Length];
        for(int i=0;i<a.Length;i++){double x=a[i],y=(b0*x+b1*x1+b2*x2-a1*y1-a2*y2)/a0;x2=x1;x1=x;y2=y1;y1=y;o[i]=(float)y;}
        return o;
    }
    // An imported clip as mono samples (channels averaged).
    static float[] Mono(string path,out int rate)
    {
        var clip=Clip(path);
        if(clip.loadState!=AudioDataLoadState.Loaded)clip.LoadAudioData();
        int channels=clip.channels;var data=new float[clip.samples*channels];
        if(!clip.GetData(data,0))throw new InvalidOperationException("Cannot read "+path);
        var mono=new float[clip.samples];
        for(int i=0;i<mono.Length;i++)for(int c=0;c<channels;c++)mono[i]+=data[i*channels+c]/channels;
        rate=clip.frequency;return mono;
    }
    static float[] Scale(float[] a,float to){float m=Peak(a);if(m>0)for(int i=0;i<a.Length;i++)a[i]*=to/m;return a;}

    // ---- Building blocks, as in the samples. ----
    static float[] Buffer(double seconds)=>new float[(int)Math.Floor(SR*seconds)];
    // Sine with an exponential pitch drop: the body of every hit.
    static void Thump(float[] a,double at,double f0,double f1,double k,double decay,double amp)
    {
        double ph=0;int s=(int)Math.Floor(at*SR),len=Math.Min(a.Length-s,(int)Math.Floor(SR*6/decay));
        for(int i=0;i<len;i++){double t=i/(double)SR,f=f1+(f0-f1)*Math.Exp(-t*k);ph+=2*Math.PI*f/SR;a[s+i]+=(float)(Math.Sin(ph)*Math.Exp(-t*decay)*amp*Math.Min(1,i/40.0));}
    }
    // Noise through a one-pole lowpass minus a slower one: a band of hiss.
    static void Noise(float[] a,double at,double dur,double lpA,double hpA,Func<double,double> env,double amp,Rng r)
    {
        double lp=0,lp2=0;int s=(int)Math.Floor(at*SR),len=Math.Min(a.Length-s,(int)Math.Floor(SR*dur));
        for(int i=0;i<len;i++){double t=i/(double)SR,x=r.Next()*2-1;lp+=lpA*(x-lp);lp2+=hpA*(lp-lp2);a[s+i]+=(float)((lp-lp2)*env(t)*amp);}
    }
    static void Crackles(float[] a,double from,double to,Func<double,double> rate,double amp,Rng r)
    {
        double t=from;
        while(true)
        {
            t+=-Math.Log(1-r.Next())/Math.Max(.1,rate(t-from));if(t>=to)break;
            int s=(int)Math.Floor(t*SR),len=(int)Math.Floor(SR*(.002+.006*r.Next()));double g=amp*(.35+.65*r.Next()),prev=0;
            for(int j=0;j<len&&s+j<a.Length;j++){double x=r.Next()*2-1;a[s+j]+=(float)((x-prev)*Math.Exp(-j/(double)len*5)*g);prev=x;}
        }
    }
    static float Peak(float[] a){float m=0;foreach(var v in a)m=Math.Max(m,Math.Abs(v));return m;}
    static float[] Finish(float[] a,float peak)
    {
        float m=Peak(a);if(m>0)for(int i=0;i<a.Length;i++)a[i]*=peak/m;
        int n=Math.Min(400,a.Length);for(int i=0;i<n;i++)a[a.Length-1-i]*=i/(float)n;return a;
    }
    // mulberry32, bit for bit like the browser samples.
    sealed class Rng
    {
        uint state;public Rng(int seed){state=(uint)seed;}
        public double Next()
        {
            unchecked
            {
                state+=0x6D2B79F5;uint t=(state^(state>>15))*(1u|state);
                t=(t+(t^(t>>7))*(61u|t))^t;return (t^(t>>14))/4294967296.0;
            }
        }
    }

    // 16-bit mono PCM, like the other generated sounds of the project.
    static void Write(string path,float[] samples,bool force)
    {
        if(File.Exists(path)&&!force)return;
        WritePcm(path,samples,1,SR);
    }
    static void WritePcm(string path,float[] samples,int channels,int rate)
    {
        using(var file=new BinaryWriter(File.Create(path)))
        {
            file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+samples.Length*2);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            file.Write(16);file.Write((short)1);file.Write((short)channels);file.Write(rate);file.Write(rate*channels*2);file.Write((short)(channels*2));file.Write((short)16);
            file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(samples.Length*2);
            foreach(var s in samples)file.Write((short)(Mathf.Clamp(s,-1,1)*32767));
        }
    }
    static float[] Read(string path)
    {
        var bytes=File.ReadAllBytes(path);int count=(bytes.Length-44)/2;var samples=new float[count];
        for(int i=0;i<count;i++)samples[i]=BitConverter.ToInt16(bytes,44+i*2)/32767f;return samples;
    }
}
