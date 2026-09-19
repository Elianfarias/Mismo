using System;
using System.Collections.Generic;
using System.Linq;
using Mismo.Gameplay.Enemies;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    /// <summary>Bakes generic source poses onto the current enemy skeleton without changing source clips.</summary>
    public static class EnemyAnimationRetargeter
    {
        const string SourceRig="Assets/Art/FBX/Characters/Voxel_Adventurer_Animated.fbx";
        const string TargetRig="Assets/Art/FBX/Goblins/Goblin_Concept_Animated.fbx";
        const string Folder="Assets/Art/Animations/GoblinConcept/Compatible";
        static bool queued,running;

        [InitializeOnLoadMethod]
        static void Initialize()=>Queue();
        public static void Queue()
        {
            if(queued||running||EditorApplication.isPlayingOrWillChangePlaymode)return;
            queued=true;EditorApplication.delayCall+=()=>
            {
                queued=false;
                if(EditorApplication.isCompiling||EditorApplication.isUpdating){Queue();return;}
                if(!EditorApplication.isPlayingOrWillChangePlaymode)RefreshAll();
            };
        }
        [MenuItem("Mismo/Enemigos/Actualizar clips compatibles")]
        public static void RefreshAll()
        {
            if(running)return;
            running=true;
            try
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(SourceRig);
                var target=AssetDatabase.LoadAssetAtPath<GameObject>(TargetRig);
                if(source==null||target==null)return;
                bool changed=false;
                foreach(string filter in new[]{"t:GoblinSettings","t:BossSettings"})
                foreach(string guid in AssetDatabase.FindAssets(filter,new[]{"Assets/Data", "Assets/Art/Prefabs"}))
                {
                    var asset=AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                    EnemyAttackAnimation[] bindings=asset is GoblinSettings g?new[]{g.slash?.animation,g.charge?.animation}
                        :asset is BossSettings b?new[]{b.frontSlash?.animation,b.overheadSmash?.animation,b.straightCharge?.animation}:Array.Empty<EnemyAttackAnimation>();
                    bool dirty=false;
                    foreach(var binding in bindings)if(binding!=null)dirty|=Prepare(binding,source,target);
                    if(dirty){EditorUtility.SetDirty(asset);changed=true;}
                }
                if(changed)AssetDatabase.SaveAssets();
            }
            finally{running=false;}
        }
        static bool Prepare(EnemyAttackAnimation binding,GameObject source,GameObject target)
        {
            AnimationClip adapted=null;
            if(binding.clip!=null)
            {
                var curves=AnimationUtility.GetCurveBindings(binding.clip).Where(b=>b.type==typeof(Transform)).ToArray();
                bool directlyCompatible=curves.All(b=>string.IsNullOrEmpty(b.path)||target.transform.Find(b.path)!=null);
                if(!directlyCompatible)
                {
                    if(curves.Length==0||!curves.All(b=>string.IsNullOrEmpty(b.path)||source.transform.Find(b.path)!=null))
                    {
                        Debug.LogWarning("El clip "+binding.clip.name+" no coincide con el rig enemigo ni con el rig de origen disponible. Revisá el esqueleto del clip.",binding.clip);
                    }
                    else
                    {
                        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Art/Animations/GoblinConcept","Compatible");
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(binding.clip,out string guid,out long fileId);
                        string path=Folder+"/"+guid+"_"+fileId.ToString().Replace("-","n")+".anim";
                        string signature="enemy-retarget-v3-grounded:"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(binding.clip))+":"+
                            AssetDatabase.GetAssetDependencyHash(SourceRig)+":"+AssetDatabase.GetAssetDependencyHash(TargetRig);
                        adapted=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if(adapted==null||AssetImporter.GetAtPath(path).userData!=signature)
                        {
                            var baked=Bake(binding.clip,source,target);
                            if(adapted==null){AssetDatabase.CreateAsset(baked,path);adapted=baked;}
                            else{EditorUtility.CopySerialized(baked,adapted);Object.DestroyImmediate(baked);EditorUtility.SetDirty(adapted);}
                            AssetDatabase.SaveAssets();
                            var importer=AssetImporter.GetAtPath(path);importer.userData=signature;importer.SaveAndReimport();
                        }
                    }
                }
            }
            bool changed=binding.compatibleClip!=adapted||binding.compatibleSource!=(adapted!=null?binding.clip:null);
            binding.compatibleClip=adapted;binding.compatibleSource=adapted!=null?binding.clip:null;
            return changed;
        }
        public static AnimationClip Bake(AnimationClip clip,GameObject sourcePrefab,GameObject targetPrefab)
        {
            var source=Object.Instantiate(sourcePrefab);var target=Object.Instantiate(targetPrefab);
            source.hideFlags=target.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                foreach(var animator in source.GetComponentsInChildren<Animator>())animator.enabled=false;
                foreach(var animator in target.GetComponentsInChildren<Animator>())animator.enabled=false;
                var sourceTransforms=source.GetComponentsInChildren<Transform>();
                var sourcePosition=sourceTransforms.Select(t=>t.localPosition).ToArray();
                var sourceRotation=sourceTransforms.Select(t=>t.localRotation).ToArray();
                var sourceScale=sourceTransforms.Select(t=>t.localScale).ToArray();
                var names=sourceTransforms.GroupBy(t=>t.name).Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.First());
                var skeleton=target.transform.Find("Goblin_Rig/Root");
                if(skeleton==null)throw new InvalidOperationException("No se encontró la raíz del rig enemigo.");
                var bones=skeleton.GetComponentsInChildren<Transform>();
                var contacts=FootContacts(target);
                float restFloor=ContactHeight(contacts);
                Vector3 rootPosition=skeleton.localPosition;
                var rootTracks=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                var rest= bones.ToDictionary(t=>t,t=>t.rotation);
                var sourceRest=names.ToDictionary(p=>p.Key,p=>p.Value.rotation);
                var tracks=bones.Where(t=>names.ContainsKey(t.name)).ToDictionary(t=>t,t=>new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()});
                var previous=new Dictionary<Transform,Quaternion>();
                var result=new AnimationClip{name=clip.name+" · Goblin",frameRate=60};
                int samples=Mathf.Max(1,Mathf.CeilToInt(clip.length*60));
                for(int frame=0;frame<=samples;frame++)
                {
                    float time=frame*clip.length/samples;
                    skeleton.localPosition=rootPosition;
                    for(int i=0;i<sourceTransforms.Length;i++)
                    {sourceTransforms[i].localPosition=sourcePosition[i];sourceTransforms[i].localRotation=sourceRotation[i];sourceTransforms[i].localScale=sourceScale[i];}
                    clip.SampleAnimation(source,time);
                    foreach(var bone in bones)
                    {
                        if(!tracks.TryGetValue(bone,out var curves))continue;
                        // World-space rotation deltas include intermediate bones absent from the simpler enemy rig.
                        bone.rotation=names[bone.name].rotation*Quaternion.Inverse(sourceRest[bone.name])*rest[bone];
                        Quaternion q=bone.localRotation;
                        if(previous.TryGetValue(bone,out var last)&&Quaternion.Dot(q,last)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                        previous[bone]=q;
                        curves[0].AddKey(time,q.x);curves[1].AddKey(time,q.y);curves[2].AddKey(time,q.z);curves[3].AddKey(time,q.w);
                    }
                    // These grounded melee attacks must keep the supporting sole at its rest height.
                    // Rotations alone shorten the projected legs and leave both feet above the floor.
                    if(contacts.Count>0)skeleton.position+=Vector3.up*(restFloor-ContactHeight(contacts));
                    Vector3 position=skeleton.localPosition;
                    rootTracks[0].AddKey(time,position.x);rootTracks[1].AddKey(time,position.y);rootTracks[2].AddKey(time,position.z);
                }
                string rootPath=AnimationUtility.CalculateTransformPath(skeleton,target.transform);
                for(int i=0;i<3;i++)AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve(rootPath,typeof(Transform),"m_LocalPosition."+"xyz"[i]),rootTracks[i]);
                foreach(var entry in tracks)
                {
                    string path=AnimationUtility.CalculateTransformPath(entry.Key,target.transform);
                    string[] properties={"m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
                    for(int i=0;i<4;i++)AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve(path,typeof(Transform),properties[i]),entry.Value[i]);
                }
                // Preserve enemy limb lengths, scale and gameplay-driven root displacement.
                result.EnsureQuaternionContinuity();
                return result;
            }
            finally{Object.DestroyImmediate(source);Object.DestroyImmediate(target);}
        }
        public static System.Collections.Generic.List<(Transform bone,Vector3 point)> FootContacts(GameObject model)
        {
            var result=new System.Collections.Generic.List<(Transform bone,Vector3 point)>();
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=skin.sharedMesh;if(mesh==null)continue;
                var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;var bones=skin.bones;
                for(int i=0;i<weights.Length;i++)
                {
                    int index=weights[i].boneIndex0;
                    if(weights[i].weight0>.999f&&index<bones.Length&&bones[index]!=null&&bones[index].name.StartsWith("Foot."))
                        result.Add((bones[index],bind[index].MultiplyPoint3x4(vertices[i])));
                }
            }
            return result;
        }
        public static float ContactHeight(System.Collections.Generic.List<(Transform bone,Vector3 point)> contacts)
            =>contacts.Count==0?0:contacts.Min(c=>c.bone.TransformPoint(c.point).y);
    }
    sealed class EnemyAnimationImportHook:AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] previous)
        {
            if (SessionState.GetBool("Mismo.Organization.Running", false)) return;
            if(imported.Concat(moved).Any(p=>!p.Contains("/Compatible/")&&
                (p.EndsWith(".anim")||p.EndsWith(".fbx")||p.StartsWith("Assets/Data/Enemies/")||p.StartsWith("Assets/Art/Prefabs/Enemies/"))))EnemyAnimationRetargeter.Queue();
        }
    }
    [CustomEditor(typeof(GoblinSettings))]
    sealed class GoblinSettingsAnimationEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if(DrawDefaultInspector())EnemyAnimationRetargeter.Queue();
            EditorGUILayout.HelpBox("Los clips del rig humanoide se adaptan automáticamente al rig del goblin. Se conserva el clip original; la copia compatible se incluye en la build.",MessageType.Info);
        }
    }
    [CustomEditor(typeof(BossSettings))]
    sealed class BossSettingsAnimationEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if(DrawDefaultInspector())EnemyAnimationRetargeter.Queue();
            EditorGUILayout.HelpBox("Los clips del rig humanoide se adaptan automáticamente al rig del jefe. Se conserva el clip original; la copia compatible se incluye en la build.",MessageType.Info);
        }
    }
    sealed class EnemyAnimationBuildPreparation:IPreprocessBuildWithReport
    {
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)=>EnemyAnimationRetargeter.RefreshAll();
    }
}
