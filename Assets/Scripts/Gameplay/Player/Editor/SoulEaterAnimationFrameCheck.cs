using System;
using System.Linq;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.Voxels;
using UnityEngine;
using UnityEngine.LowLevel;
namespace Mismo.Gameplay.Player.Editor
{
    // Run on both sides of Unity's animation pass; same-frame sampling missed the overwrite.
    public sealed class SoulEaterAnimationFrameCheck : IDisposable
    {
        public bool Complete {get;private set;}
        public string Failure {get;private set;}
        readonly VoxelRigInstance rig;
        readonly SoulEaterAnimation playback;
        readonly PlayerLoopSystem previousLoop;
        Quaternion[] sampled;int frame;bool disposed;
        public SoulEaterAnimationFrameCheck(VoxelRigInstance visual)
        {
            rig=visual;playback=new SoulEaterAnimation(rig.animator);
            previousLoop=PlayerLoop.GetCurrentPlayerLoop();var loop=previousLoop;
            // Copy the top-level list so the snapshot remains unchanged.
            loop.subSystemList=(PlayerLoopSystem[])loop.subSystemList.Clone();
            Append(ref loop,typeof(UnityEngine.PlayerLoop.Update),Sample);
            Append(ref loop,typeof(UnityEngine.PlayerLoop.PreLateUpdate),Observe);
            PlayerLoop.SetPlayerLoop(loop);
        }
        static void Append(ref PlayerLoopSystem loop,Type phase,PlayerLoopSystem.UpdateFunction action)
        {
            for(int i=0;i<loop.subSystemList.Length;i++)if(loop.subSystemList[i].type==phase)
            {
                var system=loop.subSystemList[i];system.subSystemList=system.subSystemList.Concat(new[]{new PlayerLoopSystem{type=typeof(SoulEaterAnimationFrameCheck),updateDelegate=action}}).ToArray();loop.subSystemList[i]=system;return;
            }
            throw new InvalidOperationException("Missing player-loop phase: "+phase);
        }
        void Sample()
        {
            if(Complete||Failure!=null)return;
            int index=frame/3;
            if(index>=rig.clips.Length){Complete=true;return;}
            playback.Sample(rig.clips[index],.15f+.3f*(frame%3),Time.deltaTime,true);
            sampled=rig.surface.bones.Select(b=>b.localRotation).ToArray();
        }
        void Observe()
        {
            if(Complete||Failure!=null||sampled==null)return;
            for(int i=0;i<sampled.Length;i++)if(Quaternion.Angle(sampled[i],rig.surface.bones[i].localRotation)>.1f)
            {Failure=rig.clips[frame/3].name+" / "+rig.surface.bones[i].name;break;}
            frame++;
        }
        public void Dispose(){if(disposed)return;disposed=true;PlayerLoop.SetPlayerLoop(previousLoop);playback.Dispose();}
    }
}
