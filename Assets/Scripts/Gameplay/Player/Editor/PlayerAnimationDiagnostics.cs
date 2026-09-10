using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using Mismo.Gameplay.Player.Presentation;
using Mismo.Gameplay.Player.Movement;

namespace Mismo.Gameplay.Player.Editor
{
    public static class PlayerAnimationDiagnostics
    {
        [MenuItem("Mismo/Character/Capture Animation Diagnostics")]
        static void Capture()
        {
            var driver=Object.FindFirstObjectByType<PlayerAnimationDriver>();
            if(driver==null || driver.Animator==null)return;
            var a=driver.Animator;
            var text=new StringBuilder();
            text.AppendLine("playing="+EditorApplication.isPlaying+" scene="+driver.gameObject.scene.path+" animator="+a.name+" enabled="+a.enabled+" active="+a.gameObject.activeInHierarchy);
            text.AppendLine("player="+driver.transform.position+" visual="+a.transform.position+" scale="+a.transform.lossyScale+" motion="+driver.Motion+" speed="+driver.GetComponent<PlayerMotor>().Speed);
            text.AppendLine("controller="+AssetDatabase.GetAssetPath(a.runtimeAnimatorController)+" human="+a.isHuman+" initialized="+a.isInitialized);
            foreach(var t in a.GetComponentsInChildren<Transform>().Where(t=>t.name=="Root"||t.name=="Hips"||t.name=="Foot.L"||t.name=="Foot.R"))
                text.AppendLine(t.name+" world="+t.position+" local="+t.localPosition+" scale="+t.lossyScale);
            if(EditorApplication.isPlaying)
            {
                var playback=typeof(PlayerAnimationDriver).GetField("playback",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(driver);
                text.AppendLine("playback="+(playback!=null));
                if(playback!=null){var c=(AnimatorControllerPlayable)typeof(WeaponActionPlayback).GetField("controller",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(playback);text.AppendLine("graph motion="+c.GetInteger("Motion")+" speed="+c.GetFloat("LocomotionSpeed")+" rate="+c.GetFloat("PlaybackRate")+" time="+c.GetCurrentAnimatorStateInfo(0).normalizedTime);}
                foreach(var hit in Physics.RaycastAll(driver.transform.position+Vector3.up*3,Vector3.down,8).Where(h=>h.transform.root!=driver.transform.root).OrderBy(h=>h.distance).Take(4))text.AppendLine("ground="+hit.collider.name+" y="+hit.point.y);
            }
            Directory.CreateDirectory(".validation");File.WriteAllText(".validation/live-player-animation.txt",text.ToString());
        }
    }
}
