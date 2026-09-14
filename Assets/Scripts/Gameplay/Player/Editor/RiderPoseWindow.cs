using System.Linq;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Mismo.Gameplay.Player.Editor
{
    public sealed class RiderPoseWindow:EditorWindow
    {
        CreatureSpecies species;RiderPose pose;GameObject playerPrefab,rider,mount;WeaponPoseStage stage;UnityEditor.Editor inspector;Animator animator;Vector2 scroll;int boneIndex;
        [MenuItem("Mismo/Criaturas/Taller de poses de montura")]
        public static void Open()=>GetWindow<RiderPoseWindow>("Poses de montura");
        void OnEnable(){playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");SceneView.duringSceneGui+=HandlesGUI;}
        void OnDisable(){SceneView.duringSceneGui-=HandlesGUI;Close();}
        void Close(){if(stage!=null&&StageUtility.GetCurrentStage()==stage)StageUtility.GoToMainStage();if(inspector!=null)DestroyImmediate(inspector);stage=null;mount=rider=null;}
        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Seleccioná una especie y abrí la vista de prueba. Ajustá el asiento y la pose; se guardan en el asset, sin modificar la escena de juego.",MessageType.Info);
            var next=(CreatureSpecies)EditorGUILayout.ObjectField("Especie",species,typeof(CreatureSpecies),false);
            if(next!=species){Close();species=next;pose=species!=null?species.riderPose:null;}
            playerPrefab=(GameObject)EditorGUILayout.ObjectField("Personaje",playerPrefab,typeof(GameObject),false);
            if(species==null||pose==null){EditorGUILayout.HelpBox("Asigná un Rider Pose a la especie desde el Inspector.",MessageType.Info);EditorGUILayout.EndScrollView();return;}
            if(GUILayout.Button("Abrir vista de montura"))Build();
            if(inspector==null)inspector=UnityEditor.Editor.CreateEditor(pose);
            EditorGUI.BeginChangeCheck();inspector.OnInspectorGUI();if(EditorGUI.EndChangeCheck())Refresh();
            if(animator!=null)
            {
                var bones=animator.GetComponentsInChildren<Transform>();var paths=bones.Select(t=>AnimationUtility.CalculateTransformPath(t,animator.transform)).ToArray();
                boneIndex=EditorGUILayout.Popup("Hueso",Mathf.Clamp(boneIndex,0,bones.Length-1),paths);var bone=bones[boneIndex];
                EditorGUI.BeginChangeCheck();var angle=EditorGUILayout.Vector3Field("Rotación del hueso",bone.localEulerAngles);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(pose,"Ajustar pose del jinete");var entries=pose.bones.ToList();var existing=entries.Find(b=>b.path==paths[boneIndex]);
                    if(existing==null){existing=new RiderPose.Bone{path=paths[boneIndex]};entries.Add(existing);}existing.rotation=angle;pose.bones=entries.ToArray();EditorUtility.SetDirty(pose);Refresh();
                }
            }
            if(GUILayout.Button("Guardar pose")){EditorUtility.SetDirty(pose);AssetDatabase.SaveAssets();}
            EditorGUILayout.EndScrollView();
        }
        void Build()
        {
            Close();if(playerPrefab==null||species.prefabs==null||species.prefabs.Length==0)return;
            stage=CreateInstance<WeaponPoseStage>();stage.Header="Taller de monturas";StageUtility.GoToStage(stage,true);mount=stage.Clone(species.prefabs[0]);mount.transform.position=Vector3.zero;
            rider=stage.Clone(playerPrefab);rider.transform.SetParent(mount.transform,false);animator=rider.GetComponentInChildren<Animator>();
            foreach(var nav in mount.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>())nav.enabled=false;
            if(animator!=null)animator.enabled=false;Refresh();SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.up,Vector3.one*4),false);
        }
        void Refresh()
        {
            if(rider==null||pose==null)return;rider.transform.localPosition=pose.position;rider.transform.localRotation=Quaternion.Euler(pose.rotation);rider.transform.localScale=Vector3.one*pose.scale;
            if(animator!=null&&pose.seatedAnimation!=null)pose.seatedAnimation.SampleAnimation(animator.gameObject,pose.sampleTime*pose.seatedAnimation.length);pose.Apply(animator!=null?animator.transform:rider.transform);SceneView.RepaintAll();
        }
        void HandlesGUI(SceneView scene)
        {
            if(rider==null||stage==null||StageUtility.GetCurrentStage()!=stage)return;
            EditorGUI.BeginChangeCheck();var position=Handles.PositionHandle(rider.transform.position,rider.transform.rotation);var rotation=Handles.RotationHandle(rider.transform.rotation,position);
            if(EditorGUI.EndChangeCheck()){Undo.RecordObject(pose,"Mover asiento");pose.position=mount.transform.InverseTransformPoint(position);pose.rotation=(Quaternion.Inverse(mount.transform.rotation)*rotation).eulerAngles;EditorUtility.SetDirty(pose);Refresh();}
        }
    }
}
