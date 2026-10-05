using System.Collections.Generic;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    public sealed class WorldDestructible : MonoBehaviour
    {
        public static readonly HashSet<WorldDestructible> Loaded=new HashSet<WorldDestructible>();
        public WorldAssetKind Kind {get;private set;}
        public bool IsTree=>Kind==WorldAssetKind.Tree||Kind==WorldAssetKind.Deadwood;
        static readonly Dictionary<Vector2Int,HashSet<WorldDestructible>> cells=new Dictionary<Vector2Int,HashSet<WorldDestructible>>();
        Vector2Int cell;
        static Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/16),Mathf.FloorToInt(p.z/16));
        public static bool Allowed(WorldAssetKind kind)=>kind==WorldAssetKind.Tree||kind==WorldAssetKind.Deadwood||kind==WorldAssetKind.Rock||kind==WorldAssetKind.Grass||kind==WorldAssetKind.Bush||kind==WorldAssetKind.Flower||kind==WorldAssetKind.Fence||kind==WorldAssetKind.Landmark||kind==WorldAssetKind.Ruin;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){Loaded.Clear();cells.Clear();}
        void OnEnable()
        {
            Loaded.Add(this);cell=Cell(transform.position);
            if(!cells.TryGetValue(cell,out var set)){set=new HashSet<WorldDestructible>();cells.Add(cell,set);}set.Add(this);
        }
        void OnDisable(){Loaded.Remove(this);if(cells.TryGetValue(cell,out var set)){set.Remove(this);if(set.Count==0)cells.Remove(cell);}}
        public static void Query(Vector3 center,float radius,List<WorldDestructible> results)
        {
            results.Clear();var min=Cell(center-Vector3.one*radius);var max=Cell(center+Vector3.one*radius);
            for(int z=min.y;z<=max.y;z++)for(int x=min.x;x<=max.x;x++)
                if(cells.TryGetValue(new Vector2Int(x,z),out var set))foreach(var prop in set)if(prop!=null)results.Add(prop);
        }
        public static GameObject Attach(GameObject root,WorldAssetKind kind)
        {
            if(!Allowed(kind))return root;
            var prop=root.GetComponent<WorldDestructible>()??root.AddComponent<WorldDestructible>();prop.Kind=kind;
            if(WorldImpactRecord.Destroyed(root.transform.position)||prop.IsTree&&WorldFelledTreeRecord.Destroyed(root.transform.position)||WorldDestroyedPropRecord.Destroyed(root.transform.position,kind))prop.Break(root.transform.position,false);
            return root;
        }
        public void Break(Vector3 origin,bool animate)
        {
            if(!gameObject.activeSelf)return;
            if(animate)
            {
                if(Kind==WorldAssetKind.Tree || Kind==WorldAssetKind.Deadwood)
                {
                    // Copy only visible mesh parts; no gathering scripts, colliders or loot are duplicated.
                    var falling=new GameObject("Dragon-felled tree");falling.transform.position=transform.position;
                    foreach(var source in GetComponentsInChildren<MeshFilter>())
                    {
                        var renderer=source.GetComponent<MeshRenderer>();if(renderer==null || !renderer.enabled || !source.gameObject.activeInHierarchy)continue;
                        var part=new GameObject("Falling wood");part.transform.SetParent(falling.transform);
                        part.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);part.transform.localScale=source.transform.lossyScale;
                        part.AddComponent<MeshFilter>().sharedMesh=source.sharedMesh;part.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                    }
                    falling.AddComponent<DragonFallingTree>().Initialize(origin);
                }
                // Ground cover is cheap to crush: do not create a particle system/material for every blade.
                if(IsTree||Kind==WorldAssetKind.Rock)
                    WorldImpactDebris.Emit(transform.position+Vector3.up*.6f,Kind==WorldAssetKind.Rock?new Color(.42f,.4f,.34f):new Color(.33f,.25f,.13f),Kind==WorldAssetKind.Rock?24:16);
            }
            // A stored footprint suppresses this prop again after streaming or continuing a save.
            gameObject.SetActive(false);
        }
    }
    public sealed class DragonFallingTree : MonoBehaviour
    {
        float age;Vector3 axis;
        public void Initialize(Vector3 origin)
        {var away=Vector3.ProjectOnPlane(transform.position-origin,Vector3.up).normalized;axis=Vector3.Cross(Vector3.up,away.sqrMagnitude>.01f?away:Vector3.right);}
        void Update(){age+=Time.deltaTime;transform.rotation=Quaternion.AngleAxis(Mathf.SmoothStep(0,85,age/1.2f),axis);if(age>2)Destroy(gameObject);}
    }
    public sealed class WorldImpactDebris : MonoBehaviour
    {
        Material material;Mesh cube;
        public static void Emit(Vector3 p,Color color,int count)
        {
            var go=new GameObject("Dragon impact fragments");go.transform.position=p;
            var owned=go.AddComponent<WorldImpactDebris>();
            var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.loop=false;main.playOnAwake=false;main.maxParticles=96;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=1.5f;
            var emission=particles.emission;emission.enabled=false;var shape=particles.shape;shape.enabled=false;
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);owned.cube=primitive.GetComponent<MeshFilter>().sharedMesh;primitive.SetActive(false);Destroy(primitive);
            var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=owned.cube;
            owned.material=RuntimeParticleMaterial.Create("Dragon debris",Color.white);renderer.sharedMaterial=owned.material;
            for(int i=0;i<count;i++)particles.Emit(new ParticleSystem.EmitParams{position=p+Random.insideUnitSphere*.8f,velocity=Random.insideUnitSphere*5+Vector3.up*6,startLifetime=Random.Range(.7f,1.6f),startSize=Random.Range(.12f,.4f),startColor=color},1);
            Destroy(go,2.5f);
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
