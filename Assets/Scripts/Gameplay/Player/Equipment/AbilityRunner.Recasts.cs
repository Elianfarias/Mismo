using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.Equipment
{
    // Recast chains (Riven-style): each press runs the next stage; costs are paid by the first press and the
    // cooldown starts when the chain closes. The state belongs to this actor, never to the shared ability asset.
    public sealed partial class AbilityRunner
    {
        sealed class RecastChain {public int next;public float window,cooldown;public long useId;public RecastProgress progress;}
        readonly Dictionary<AbilityDefinition,RecastChain> recasts=new Dictionary<AbilityDefinition,RecastChain>();
        readonly List<AbilityDefinition> closingRecasts=new List<AbilityDefinition>();

        // Stage the next press will run; 0 = no chain open.
        public int NextRecastStage(AbilityDefinition definition)=>definition!=null&&recasts.TryGetValue(definition,out var chain)?chain.next:0;
        // Fraction of the window left to press again; it stays full while a stage of the chain still plays.
        public float RecastWindow(AbilityDefinition definition)=>definition!=null&&recasts.TryGetValue(definition,out var chain)?Mathf.Clamp01(chain.window/Mathf.Max(.1f,definition.recastWindow)):0;

        // Called right after Current started its stage.
        void AdvanceRecast(AbilityDefinition definition,RecastChain chain,float cooldown)
        {
            if(chain==null)recasts[definition]=chain=new RecastChain{useId=Current.UseId,cooldown=cooldown,progress=Current.Progress};
            chain.next=Current.RecastStage+1;chain.window=definition.recastWindow;
            if(chain.next>=definition.RecastCount)CloseRecast(definition);
        }
        // Real time, counted only while none of the chain's stages runs: the window starts when a stage ends.
        void TickRecasts(float dt)
        {
            if(recasts.Count==0)return;
            foreach(var pair in recasts)if((Current==null||Current.Definition!=pair.Key)&&(pair.Value.window-=dt)<=0)closingRecasts.Add(pair.Key);
            foreach(var definition in closingRecasts)CloseRecast(definition);
            closingRecasts.Clear();
        }
        void CloseRecasts()
        {
            closingRecasts.AddRange(recasts.Keys);
            foreach(var definition in closingRecasts)CloseRecast(definition);
            closingRecasts.Clear();
        }
        void CloseRecast(AbilityDefinition definition)
        {
            if(!recasts.TryGetValue(definition,out var chain))return;
            recasts.Remove(definition);cooldownDurations[definition]=chain.cooldown;readyAt[definition]=Time.time+chain.cooldown;
        }
    }
}
