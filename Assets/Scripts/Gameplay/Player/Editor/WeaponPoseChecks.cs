using System;
using Mismo.Gameplay.Player.Equipment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class WeaponPoseChecks
    {
        private static void Require(bool ok,string message) { if(!ok)throw new Exception(message); Debug.Log("POSE_CHECK "+message); }
        public static void RunBatch()
        {
            var originalScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool dirty=originalScene.isDirty;
            var stage=ScriptableObject.CreateInstance<WeaponPoseStage>();
            try
            {
                StageUtility.GoToStage(stage,true);
                Require(stage.scene.IsValid(),"Preview stage has an isolated scene");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerVoxelSwordE.prefab");
                if(prefab==null)prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
                var character=stage.Clone(prefab);
                Require(character.scene==stage.scene,"Character clone stays inside preview scene");
                foreach(var script in character.GetComponentsInChildren<MonoBehaviour>(true)) Require(!script.enabled,"Preview gameplay script disabled: "+script.GetType().Name);
                var rig=new GameObject("TestRig"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig,stage.scene);
                var animator=rig.AddComponent<Animator>(); rig.transform.SetParent(character.transform,false);
                var bone=new GameObject("CustomGrip");bone.transform.SetParent(rig.transform,false);bone.transform.localScale=Vector3.one*100;
                var pose=new WeaponAttachmentPose {anchor=WeaponAnchor.BonePath,bonePath="CustomGrip",offset=new Vector3(.2f,.1f,-.3f),rotation=new Vector3(15,25,35),scale=.7f};
                Require(pose.Resolve(character.transform,animator)==bone.transform,"Generic anchor resolves an authored path");
                var visual=new GameObject("Test weapon");visual.transform.SetParent(character.transform,false);
                for(int yaw=0;yaw<360;yaw+=45)
                {
                    character.transform.rotation=Quaternion.Euler(0,yaw,0);
                    pose.Apply(visual.transform,bone.transform);
                    Require(Vector3.Distance(Quaternion.Inverse(bone.transform.rotation)*(visual.transform.position-bone.transform.position),pose.offset)<.0001f,"Grip offset remains stable while turning "+yaw);
                    Require(Quaternion.Angle(visual.transform.rotation,bone.transform.rotation*Quaternion.Euler(pose.rotation))<.01f,"Weapon follows bone rotation "+yaw);
                    Require(Vector3.Distance(visual.transform.lossyScale,Vector3.one*.7f)<.001f,"Imported bone scale does not enlarge weapon "+yaw);
                }
                pose.bonePath="Missing"; Require(pose.Resolve(character.transform,animator)==null,"Missing bones are not silently replaced");
                pose.anchor=WeaponAnchor.Character; Require(pose.Resolve(character.transform,animator)==character.transform,"Holster can use character anchor");
                Require(originalScene.isDirty==dirty,"Preview did not dirty gameplay scene");
            }
            finally { StageUtility.GoToMainStage(); }
            Require(StageUtility.GetCurrentStage()==StageUtility.GetMainStage(),"Preview closes back to main stage");
            Debug.Log("WEAPON_POSE_PASS");
        }
    }
}
