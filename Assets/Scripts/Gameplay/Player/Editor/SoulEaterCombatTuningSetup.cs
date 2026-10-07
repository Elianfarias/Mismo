using System;
using System.Linq;
using Mismo.Gameplay.Player.Voxels;
using UnityEditor;
using UnityEngine;
using Mismo.Gameplay.Enemies;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterCombatTuningSetup
    {
        public const string MarkerPath="Assets/Art/Prefabs/DragonBosses/SoulEater_Telegraph.prefab";
        public const string MaterialPath="Assets/Art/Materials/DragonBosses/SoulEater/SoulTelegraph.mat";
        [MenuItem("Mismo/Bosses/Soul Eater/Configurar todas las habilidades")]
        public static void SelectSettings()
        {Selection.activeObject=AssetDatabase.LoadAssetAtPath<SoulEaterPhaseOneSettings>(SoulEaterPhaseOneSetup.DataPath);EditorGUIUtility.PingObject(Selection.activeObject);}
        [MenuItem("Mismo/Bosses/Soul Eater/Crear assets de telegrafía ausentes")]
        public static void Run()
        {
            var settings=AssetDatabase.LoadAssetAtPath<SoulEaterPhaseOneSettings>(SoulEaterPhaseOneSetup.DataPath);
            if(settings==null)throw new InvalidOperationException("Falta SoulEater_PhaseOne.asset");
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if(material==null)
            {
                material=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/CombatParticles.shader")){name="SoulTelegraph",color=Color.white};
                AssetDatabase.CreateAsset(material,MaterialPath);
            }
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(MarkerPath);
            if(prefab==null)
            {
                var go=new GameObject("SoulEater_Telegraph");
                try
                {
                    var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.loop=true;line.useWorldSpace=false;
                    line.widthMultiplier=.18f;line.widthCurve=AnimationCurve.Constant(0,1,1);line.positionCount=64;
                    line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
                    line.startColor=line.endColor=settings.trackingTelegraphColor;
                    for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;line.SetPosition(i,new Vector3(Mathf.Cos(a),.12f,Mathf.Sin(a))*settings.impactRadius);}
                    prefab=PrefabUtility.SaveAsPrefabAsset(go,MarkerPath);
                }
                finally{Object.DestroyImmediate(go);}
            }
            if(settings.telegraphPrefab==null)settings.telegraphPrefab=prefab.GetComponent<LineRenderer>();
            if(settings.breathMaterial==null)settings.breathMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/DragonBosses/SoulEater/SoulFlame.mat");
            EditorUtility.SetDirty(settings);CalibrateFeet(settings);AssetDatabase.SaveAssets();
            Debug.Log("SOUL_COMBAT_TUNING_ASSETS_OK");
        }
        static void CalibrateFeet(SoulEaterPhaseOneSettings settings)
        {
            var root=PrefabUtility.LoadPrefabContents(SoulEaterPhaseOneSetup.PrefabPath);
            var measurement=Object.Instantiate(root);
            try
            {
                var rig=measurement.GetComponentInChildren<VoxelRigInstance>();rig.animator.enabled=false;
                settings.idle.SampleAnimation(rig.animator.gameObject,0);
                var mesh=rig.surface.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;
                var skin=rig.surface.bones.Select((b,i)=>b.localToWorldMatrix*bind[i]).ToArray();float min=float.PositiveInfinity;
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];string name=rig.surface.bones[w.boneIndex0].name;
                    if(!name.StartsWith("Hand_")&&!name.StartsWith("HandTip_")&&!name.StartsWith("Feet_")&&!name.StartsWith("Toe_"))continue;
                    var v=vertices[i];var p=skin[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+skin[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+skin[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+skin[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                    min=Mathf.Min(min,p.y);
                }
                var visual=root.GetComponentInChildren<VoxelRigInstance>().transform;
                if(!float.IsPositiveInfinity(min)){visual.position+=Vector3.up*(measurement.transform.position.y+settings.soleClearance-min);PrefabUtility.RecordPrefabInstancePropertyModifications(visual);}
                PrefabUtility.SaveAsPrefabAsset(root,SoulEaterPhaseOneSetup.PrefabPath);
            }
            finally{Object.DestroyImmediate(measurement);PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void RepairWorldAndBuild(){Run();SoulEaterMouthRepair.Run();SoulEaterGeometryReview.Capture();SoulEaterMouthChecks.Run();DragonArcChecks.RunAndBuild();}
        public static void VerifyWorldAndBuild(){SoulEaterMouthChecks.Run();DragonArcChecks.RunAndBuild();}
        public static void RepairAndCheck(){Run();SoulEaterMouthRepair.Run();SoulEaterGeometryReview.Capture();SoulEaterMouthChecks.Run();SoulEaterPhaseOneChecks.Run();}
        public static void RunAndCheck(){Run();SoulEaterMouthChecks.Run();SoulEaterPhaseOneChecks.Run();}
    }
}
