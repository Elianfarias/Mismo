using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Mismo.Gameplay.Player.Editor
{
    public static class SoulEaterTreeChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly List<string> results=new List<string>();
        static readonly List<Vector3> fallen=new List<Vector3>();
        static readonly List<WorldDestroyedPropRecord> destroyedProps=new List<WorldDestroyedPropRecord>();
        static void Check(bool ok,string label){if(!ok)throw new Exception(label);results.Add("PASS "+label);Debug.Log("TREE_CHECK "+label);}
        static void Set(SoulEaterPhaseOneController b,string key,object value)=>typeof(SoulEaterPhaseOneController).GetField(key,Private).SetValue(b,value);
        sealed class FailedSave:IProfileRepository
        {
            public ProfileReadResult Read(Func<string,bool> validate,out string payload){payload=null;return ProfileReadResult.Missing;}
            public void Write(string payload)=>throw new IOException("Expected felled-tree save failure");
        }
        static WorldDestructible Prop(Vector3 p,WorldAssetKind kind)
        {
            var root=new GameObject("Tree contact validation: "+kind);root.transform.position=p;
            var trunk=GameObject.CreatePrimitive(PrimitiveType.Cube);trunk.transform.SetParent(root.transform,false);trunk.transform.localPosition=Vector3.up*5;trunk.transform.localScale=new Vector3(2,10,2);
            WorldDestructible.Attach(root,kind);return root.GetComponent<WorldDestructible>();
        }
        static void Warp(DragonArcCoordinator arc,Vector3 p)=>arc.GetComponent<Movement.PlayerMotor>().ResetPosition(p+Vector3.up*.25f);
        static Vector3 Lane(ExplorationChunks world,DragonArcCoordinator arc,int scenario)
        {
            for(int column=0;column<7;column++)for(int i=0;i<24;i++)
            {
                var p=arc.Layout.Arena+new Vector3(-22+column*22,0,30+i*6);p.y=world.SurfaceHeight(p);
                if(!world.CanDragonImpact(p,2)||!world.CanDragonImpact(p+Vector3.forward*50,2))continue;
                var blocked=Physics.OverlapBox(p+new Vector3(0,7,25),new Vector3(8,5,25)).Any(c=>c.GetComponentInParent<Health>()==null);
                if(!blocked&&Mathf.Abs(world.SurfaceHeight(p+Vector3.forward*50)-p.y)<.5f)return p;
            }
            throw new Exception("No clear test lane");
        }
        public static IEnumerator Play(DragonArcCoordinator arc,SoulEaterPhaseOneController b,Action<string> capture)
        {
            results.Clear();fallen.Clear();destroyedProps.Clear();var world=Object.FindFirstObjectByType<ExplorationChunks>();var originalTarget=arc.transform.position;
            bool enabled=b.enabled,charge=b.Settings.enablePursuitCharge;var created=new List<GameObject>();b.enabled=true;arc.GetComponent<Invulnerability>().StartWindow(300);
            try
            {
                for(int scenario=0;scenario<3;scenario++)
                {
                    b.ResetEncounter();var start=Lane(world,arc,scenario);b.transform.SetPositionAndRotation(start,Quaternion.identity);
                    var tree=Prop(start+Vector3.forward*(scenario==0?13:22+scenario*3),WorldAssetKind.Tree);created.Add(tree.gameObject);
                    var flank=Prop(start+new Vector3(11,0,13),WorldAssetKind.Tree);created.Add(flank.gameObject);
                    var extras=new List<WorldDestructible>();
                    foreach(var kind in new[]{WorldAssetKind.Rock,WorldAssetKind.Grass,WorldAssetKind.Bush,WorldAssetKind.Flower,WorldAssetKind.Fence,WorldAssetKind.Landmark,WorldAssetKind.Ruin})
                    {
                        var prop=Prop(tree.transform.position+Vector3.right*(.5f+extras.Count*.1f),kind);created.Add(prop.gameObject);extras.Add(prop);
                        if(kind!=WorldAssetKind.Rock)foreach(var collider in prop.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
                    }
                    var treePoint=tree.transform.position;float height=world.SurfaceHeight(treePoint);int impacts=WorldSession.Current.dragonImpacts.Count;
                    var terrain=Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Select(c=>c.sharedMesh).Where(m=>m!=null).ToArray();
                    if(scenario==0)
                    {
                        var repository=typeof(WorldSession).GetField("repository",BindingFlags.Static|BindingFlags.NonPublic);object previous=repository.GetValue(null);string before=JsonUtility.ToJson(WorldSession.Current);
                        try{repository.SetValue(null,new FailedSave());Check(!world.TryDragonDestroyProps(new[]{tree,extras[0],extras[1]},start)&&tree.gameObject.activeSelf&&extras.All(p=>p.gameObject.activeSelf)&&before==JsonUtility.ToJson(WorldSession.Current),"Save failure leaves trees, rocks, grass and world data intact");}
                        finally{repository.SetValue(null,previous);}
                        var protectedTree=Prop(arc.Layout.Altar+Vector3.right,WorldAssetKind.Tree);created.Add(protectedTree.gameObject);
                        Check(!world.TryDragonFellTrees(new[]{protectedTree},start)&&protectedTree.gameObject.activeSelf,"Protected altar trees cannot be destroyed by pursuit");
                        var legacy=WorldSession.Current.Copy();legacy.dragonFelledTrees=null;Check(legacy.IsValid(),"Existing worlds without felled-tree data remain valid");
                        var corrupt=WorldSession.Current.Copy();corrupt.dragonFelledTrees.Add(new WorldFelledTreeRecord(new Vector3(float.NaN,0,0)));Check(!corrupt.IsValid(),"Non-finite tree coordinates are rejected");
                        var rock=Prop(start+new Vector3(-11,0,13),WorldAssetKind.Rock);created.Add(rock.gameObject);Check(b.Battlefield.CanDestroyProp(rock),"Rocks can be cleared by contact");
                        var house=new GameObject("Protected building");created.Add(house);WorldDestructible.Attach(house,WorldAssetKind.House);
                        Check(house.GetComponent<WorldDestructible>()==null&&house.activeSelf,"Generated buildings are never registered as breakable decoration");
                        var protectedRock=Prop(arc.Layout.Altar+Vector3.right*2,WorldAssetKind.Rock);created.Add(protectedRock.gameObject);
                        Check(!world.TryDragonDestroyProps(new[]{protectedRock},start)&&protectedRock.gameObject.activeSelf,"Mission areas protect rocks and decoration too");
                        legacy.dragonDestroyedProps=null;Check(legacy.IsValid(),"Legacy saves without prop destruction data remain valid");
                        corrupt=WorldSession.Current.Copy();corrupt.dragonDestroyedProps.Add(new WorldDestroyedPropRecord(start,WorldAssetKind.House));
                        Check(!corrupt.IsValid(),"Save validation rejects destruction records for buildings");
                    }
                    b.Settings.enablePursuitCharge=scenario>0;Warp(arc,start+Vector3.forward*45);b.BeginEncounter(arc.transform);
                    Set(b,"nextBite",float.MaxValue);Set(b,"nextBreath",float.MaxValue);Set(b,"nextTail",float.MaxValue);
                    if(scenario==2){Set(b,"phaseTwo",true);Set(b,"nextAerial",float.MaxValue);Set(b,"nextRepeatCharge",float.MaxValue);}
                    bool charging=false;float lateral=0;int contact=-1;
                    for(int frame=0;frame<1200;frame++)
                    {
                        if(scenario==0)Warp(arc,start+new Vector3(Mathf.Sin(frame*.035f)*3,0,45));
                        Physics.SyncTransforms();b.Tick(1f/60);charging|=b.State==SoulEaterState.Charging;
                        lateral=Mathf.Max(lateral,Mathf.Abs(b.transform.position.x-start.x));
                        if(frame==20)Check(tree.gameObject.activeSelf,"Planning does not fell a distant tree: scenario "+scenario);
                        if(!tree.gameObject.activeSelf&&contact<0)contact=frame;
                        if(contact>=0&&b.transform.position.z>treePoint.z+3)break;
                    }
                    Check(contact>=0&&b.transform.position.z>treePoint.z+3,"Boss crosses the fallen tree: "+(scenario==0?"walking, moving target":"charge phase "+scenario)+"; contact="+contact+"; position="+b.transform.position+"; tree="+treePoint+"; state="+b.State);
                    Check(lateral<5&&flank.gameObject.activeSelf,"Pursuit stays direct and preserves trees outside its path: scenario "+scenario);
                    Check(extras.All(p=>!p.gameObject.activeSelf),"Contact clears rocks and no-collider vegetation/decoration: scenario "+scenario);
                    foreach(var prop in extras)
                    {
                        var record=new WorldDestroyedPropRecord(prop.transform.position,prop.Kind);destroyedProps.Add(record);
                        Check(WorldDestroyedPropRecord.Destroyed(prop.transform.position,prop.Kind),"Persistent removal: "+prop.Kind+" scenario "+scenario);
                    }
                    if(scenario>0)Check(charging,"Charge starts through tree cover: phase "+scenario);
                    Check(world.SurfaceHeight(treePoint)==height&&WorldSession.Current.dragonImpacts.Count==impacts&&terrain.All(m=>m!=null),"Felling does not dig a crater or replace terrain meshes: scenario "+scenario);
                    Check(WorldFelledTreeRecord.Destroyed(treePoint),"Tree contact persists before removing the collider: scenario "+scenario);fallen.Add(treePoint);
                    if(scenario==0)
                    {
                        var camera=UnityEngine.Camera.main;var orbit=Object.FindFirstObjectByType<Camera.ThirdPersonCamera>();bool orbitOn=orbit!=null&&orbit.enabled;if(orbit!=null)orbit.enabled=false;
                        var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
                        camera.transform.position=b.transform.position+new Vector3(20,12,-15);camera.transform.LookAt(b.transform.position+Vector3.up*4);
                        b.enabled=false;yield return new WaitForSeconds(.5f);b.enabled=true;capture("arboles-derribados-persecucion");camera.transform.SetPositionAndRotation(oldPosition,oldRotation);if(orbit!=null)orbit.enabled=orbitOn;
                    }
                }
                int count=WorldSession.Current.dragonFelledTrees.Count;Check(WorldSession.Continue()&&WorldSession.Current.dragonFelledTrees.Count==count&&fallen.All(WorldFelledTreeRecord.Destroyed),"Continue loads every felled tree from the actual world file");
                foreach(var p in fallen)
                {
                    var tree=Prop(p,WorldAssetKind.Tree);created.Add(tree.gameObject);Check(!tree.gameObject.activeSelf,"Restreaming suppresses the felled tree");
                    var rock=Prop(p,WorldAssetKind.Rock);created.Add(rock.gameObject);Check(rock.gameObject.activeSelf,"A saved tree position does not remove a rock");
                }
                Check(destroyedProps.All(r=>WorldDestroyedPropRecord.Destroyed(new Vector3(r.x,0,r.z),r.kind)),"Continue preserves every kind of destroyed decoration");
                foreach(var record in destroyedProps)
                {
                    var prop=Prop(new Vector3(record.x,world.SurfaceHeight(new Vector3(record.x,0,record.z)),record.z),record.kind);created.Add(prop.gameObject);
                    Check(!prop.gameObject.activeSelf,"Restreaming suppresses destroyed "+record.kind);
                }
                var propsSnapshot=WorldSession.Current;var propsCopy=propsSnapshot.Copy();propsCopy.dragonDestroyedProps.Clear();
                Check(propsSnapshot.dragonDestroyedProps.Count>=destroyedProps.Count,"World copies own their prop destruction list");
                var snapshot=WorldSession.Current;var copy=snapshot.Copy();copy.dragonFelledTrees.Clear();Check(snapshot.dragonFelledTrees.Count==count,"World copies own their tree destruction list");
            }
            finally
            {
                foreach(var go in created)if(go!=null)Object.DestroyImmediate(go);
                b.Settings.enablePursuitCharge=charge;b.ResetEncounter();Warp(arc,originalTarget);b.BeginEncounter(arc.transform);b.enabled=enabled;
                Directory.CreateDirectory("output/soul-tree-clearing");File.WriteAllLines("output/soul-tree-clearing/checks.txt",results);
            }
        }
        public static void VerifyReload()
        {
            Check(destroyedProps.Count==21&&destroyedProps.All(r=>WorldDestroyedPropRecord.Destroyed(new Vector3(r.x,0,r.z),r.kind)),"Scene reload preserves rocks, vegetation and decoration");
            Check(fallen.Count==3&&fallen.All(WorldFelledTreeRecord.Destroyed),"Scene reload retains all pursuit tree removals");
            Check(!WorldDestructible.Loaded.Any(p=>p.IsTree&&WorldFelledTreeRecord.Destroyed(p.transform.position)),"Reloaded world contains no active felled trees");
            File.WriteAllLines("output/soul-tree-clearing/checks.txt",results);
        }
    }
}
