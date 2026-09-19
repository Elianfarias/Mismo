using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Enemies;
using Mismo.Gameplay.Player.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

namespace Mismo.Gameplay.Player.Editor
{
    public static class VoxelRegionGenerator
    {
        public const string DefaultScene="Assets/Scenes/VoxelRegion_7319.unity";
        private const string DataRoot="Assets/Data/World";
        private static readonly Color Wood=new Color(.32f,.19f,.10f),Stone=new Color(.52f,.57f,.53f);
        public static VoxelRegionSettings DefaultSettings()
        {
            Folder(DataRoot);
            const string file=DataRoot+"/VoxelRegionSettings.asset";
            var settings=AssetDatabase.LoadAssetAtPath<VoxelRegionSettings>(file);
            if(settings!=null)return settings;
            settings=ScriptableObject.CreateInstance<VoxelRegionSettings>();AssetDatabase.CreateAsset(settings,file);return settings;
        }
        public static void BuildBatch()
        {
            ValidateSeeds();
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(DefaultScene)!=null)
                throw new InvalidOperationException("La escena ya existe. Generar una variante desde la ventana para conservarla.");
            Generate(DefaultSettings());
        }
        public static void Generate(VoxelRegionSettings settings)
        {
            if(settings==null || EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(settings.size<256 || settings.size>384 || settings.stepHeight<=0 || settings.stepHeight>.25f)
                throw new InvalidOperationException("Tamaño permitido 256–384 m; escalón mayor que cero y hasta 0,25 m.");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var watch=Stopwatch.StartNew();
            var setup=EditorSceneManager.GetSceneManagerSetup();
            string name="VoxelRegion_"+settings.seed;
            string scenePath=AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/"+name+".unity");
            Folder("Assets/Art/Meshes/World/Generated");
            string folder=AssetDatabase.GenerateUniqueAssetPath("Assets/Art/Meshes/World/Generated/"+name);
            string dataFolder=DataRoot+"/Generated/"+Path.GetFileName(folder);Folder(dataFolder);
            string materialFolder="Assets/Art/Materials/World/Generated/"+Path.GetFileName(folder);Folder(materialFolder);
            Folder(folder);
            // Opening a scene unloads unreferenced editor assets; retain a transient copy.
            settings=Object.Instantiate(settings);
            settings.hideFlags=HideFlags.HideAndDontSave;
            settings.size=Mathf.Clamp(Mathf.RoundToInt(settings.size/32f)*32,256,384);
            try
            {
                if(!AssetDatabase.CopyAsset(BossPrototypeTool.ScenePath,scenePath))throw new InvalidOperationException("Falta BossArena.");
                var scene=EditorSceneManager.OpenScene(scenePath);
                foreach(var root in scene.GetRootGameObjects())
                    if(root.GetComponentInChildren<PlayerController>()==null && root.GetComponent<UnityEngine.Camera>()==null && root.GetComponent<Light>()==null)
                        Object.DestroyImmediate(root);
                var snapshot=Object.Instantiate(settings);snapshot.hideFlags=HideFlags.None;snapshot.name="Generation Settings";AssetDatabase.CreateAsset(snapshot,dataFolder+"/Settings.asset");
                var material=new Material(Shader.Find("Mismo/Voxel Landscape") ?? throw new InvalidOperationException("Falta shader Voxel Landscape"));
                AssetDatabase.CreateAsset(material,materialFolder+"/Landscape.mat");
                var rootRegion=new GameObject("Voxel Region · seed "+settings.seed+" · v"+VoxelRegionSettings.GeneratorVersion);
                Undo.RegisterCreatedObjectUndo(rootRegion,"Generate Voxel Region");
                Transform generated=Group("GeneratedGeometry",rootRegion.transform);
                Transform authored=Group("AuthoredGeometry",rootRegion.transform);
                // Both authoring branches feed one static navigation tree.
                Transform geometry=Group("RegionGeometry",rootRegion.transform);
                generated.SetParent(geometry);authored.SetParent(geometry);
                var field=new VoxelRegionHeightfield(settings);
                int half=settings.size/2;
                for(int z=-half;z<half;z+=32)
                    for(int x=-half;x<half;x+=32)
                    {
                        var mesh=Terrain(field,x,z,Mathf.Min(32,half-x),Mathf.Min(32,half-z),1);
                        SaveMesh(mesh,"Terrain_"+x+"_"+z,generated,material,folder,true);
                    }
                // Coarse scenery outside the playable square extends the horizon.
                var backdrop=Group("Distant landscape",rootRegion.transform);
                for(int z=-half-96;z<half+96;z+=32)
                    for(int x=-half-96;x<half+96;x+=32)
                        if(x<-half || z<-half || x>=half || z>=half)
                            SaveMesh(Terrain(field,x,z,32,32,4),"Horizon_"+x+"_"+z,backdrop,material,folder,false);
                Trees(settings,field,generated,material,folder);
                Village(authored,material,folder,settings);
                Secret(field,authored,rootRegion.transform,material,folder);
                Boss(field,authored,rootRegion.transform,material,folder);
                var enemies=Group("Authored Encounters",rootRegion.transform);
                Spawn("Goblin",At(field,-30,-30),enemies);
                Spawn("Goblin",At(field,9,5),enemies);
                Spawn("Goblin",At(field,15,7),enemies);
                Spawn("Goblin",At(field,66,8),enemies);
                Spawn("GoblinElite",At(field,-8,50),enemies);
                // Invisible safety limits are beyond the mountain rim, not visible dungeon walls.
                var boundary=Group("Safety boundary",rootRegion.transform);
                foreach(var spec in new[]{new Vector4(-half,0,1,settings.size+2),new Vector4(half,0,1,settings.size+2),new Vector4(0,-half,settings.size+2,1),new Vector4(0,half,settings.size+2,1)})
                {
                    var wall=Group("World limit",boundary).gameObject;wall.transform.position=new Vector3(spec.x,40,spec.y);
                    wall.AddComponent<BoxCollider>().size=new Vector3(spec.z,120,spec.w);
                }
                rootRegion.AddComponent<GoblinNavigation>().Configure(geometry);
                var player=Object.FindAnyObjectByType<PlayerController>();
                player.transform.position=At(field,-50,-70)+Vector3.up*.15f;
                if(player.GetComponent<RegionRespawn>()==null)player.gameObject.AddComponent<RegionRespawn>();
                Group("Village Spawn",rootRegion.transform).position=player.transform.position;
                var sun=Object.FindAnyObjectByType<Light>();
                if(sun!=null){sun.transform.rotation=Quaternion.Euler(48,-35,0);sun.color=new Color(1,.94f,.80f);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;}
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.57f,.69f,.83f);
                RenderSettings.ambientEquatorColor=new Color(.42f,.48f,.39f);
                RenderSettings.ambientGroundColor=new Color(.24f,.29f,.19f);
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.66f,.80f,.87f);
                RenderSettings.fogStartDistance=130;RenderSettings.fogEndDistance=370;
                ValidateNavigation(geometry,field);
                var build=EditorBuildSettings.scenes.ToList();
                if(!build.Any(entry=>entry.path==scenePath))build.Add(new EditorBuildSettingsScene(scenePath,true));
                EditorBuildSettings.scenes=build.ToArray();
                EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                Selection.activeGameObject=rootRegion;
                UnityEngine.Debug.Log("VOXEL_REGION_BUILD_OK scene="+scenePath+" seed="+settings.seed+" generationSeconds="+watch.Elapsed.TotalSeconds.ToString("F2"));
            }
            catch(Exception exception)
            {
                // Only this invocation's newly allocated assets are removed on failure.
                UnityEngine.Debug.LogException(exception);
                if(setup.Length>0)EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                AssetDatabase.DeleteAsset(scenePath);AssetDatabase.DeleteAsset(folder);throw;
            }
            finally { Object.DestroyImmediate(settings); }
        }

        private static Mesh Terrain(VoxelRegionHeightfield field,int startX,int startZ,int width,int depth,int cell)
        {
            var mesh=new VoxelRegionGeometry();
            for(int z=startZ;z<startZ+depth;z+=cell)
                for(int x=startX;x<startX+width;x+=cell)
                {
                    float y=field.Height(x+.5f,z+.5f),x1=x+cell,z1=z+cell;
                    mesh.Quad(new Vector3(x,y,z),new Vector3(x,y,z1),new Vector3(x1,y,z1),new Vector3(x1,y,z),field.Top(x,z,y));
                    Color side=y>24?Stone:new Color(.32f,.43f,.17f);
                    float low=field.Height(x+cell+.5f,z+.5f);
                    if(y>low)mesh.Quad(new Vector3(x1,low,z),new Vector3(x1,y,z),new Vector3(x1,y,z1),new Vector3(x1,low,z1),side);
                    low=field.Height(x-cell+.5f,z+.5f);
                    if(y>low)mesh.Quad(new Vector3(x,low,z1),new Vector3(x,y,z1),new Vector3(x,y,z),new Vector3(x,low,z),side);
                    low=field.Height(x+.5f,z+cell+.5f);
                    if(y>low)mesh.Quad(new Vector3(x1,low,z1),new Vector3(x1,y,z1),new Vector3(x,y,z1),new Vector3(x,low,z1),side);
                    low=field.Height(x+.5f,z-cell+.5f);
                    if(y>low)mesh.Quad(new Vector3(x,low,z),new Vector3(x,y,z),new Vector3(x1,y,z),new Vector3(x1,low,z),side);
                }
            return mesh.Mesh("Terrain");
        }
        private static void Trees(VoxelRegionSettings settings,VoxelRegionHeightfield field,Transform parent,Material material,string folder)
        {
            var trees=Group("Generated Forest",parent);var random=new System.Random(settings.seed);
            var variants=new Mesh[3];
            for(int variant=0;variant<3;variant++)
            {
                var mesh=new VoxelRegionGeometry();mesh.Box(new Vector3(0,3,0),new Vector3(.9f,6,.9f),Wood);
                // Exposed faces of a voxel ellipsoid create a block silhouette, not a giant cube crown.
                Func<int,int,int,bool> filled=(x,y,z)=>x*x/12f+(y-7)*(y-7)/7f+z*z/12f<1;
                for(int y=4;y<=10;y++)for(int z=-4;z<=4;z++)for(int x=-4;x<=4;x++)
                {
                    if(!filled(x,y,z))continue;
                    Color c=Color.Lerp(new Color(.18f,.38f+.035f*variant,.10f),new Color(.40f,.62f,.18f),(float)random.NextDouble());
                    Vector3 a=new Vector3(x,y,z),b=a+Vector3.one;
                    if(!filled(x,y+1,z))mesh.Quad(new Vector3(x,y+1,z),new Vector3(x,y+1,z+1),b,new Vector3(x+1,y+1,z),c);
                    if(!filled(x,y-1,z))mesh.Quad(a,new Vector3(x+1,y,z),new Vector3(x+1,y,z+1),new Vector3(x,y,z+1),c*.8f);
                    if(!filled(x+1,y,z))mesh.Quad(new Vector3(x+1,y,z),new Vector3(x+1,y+1,z),b,new Vector3(x+1,y,z+1),c);
                    if(!filled(x-1,y,z))mesh.Quad(new Vector3(x,y,z+1),new Vector3(x,y+1,z+1),new Vector3(x,y+1,z),a,c);
                    if(!filled(x,y,z+1))mesh.Quad(new Vector3(x+1,y,z+1),b,new Vector3(x,y+1,z+1),new Vector3(x,y,z+1),c);
                    if(!filled(x,y,z-1))mesh.Quad(a,new Vector3(x,y+1,z),new Vector3(x+1,y+1,z),new Vector3(x+1,y,z),c);
                }
                variants[variant]=mesh.Mesh("Tree "+variant);AssetDatabase.CreateAsset(variants[variant],folder+"/Tree_"+variant+".asset");
            }
            int half=settings.size/2-12;
            for(int z=-half;z<half;z+=9)for(int x=-half;x<half;x+=9)
            {
                float px=x+(float)random.NextDouble()*4,pz=z+(float)random.NextDouble()*4;
                if(random.NextDouble()>settings.vegetationDensity || field.Reserved(px,pz,4))continue;
                float y=field.Height(px,pz);
                if(y>28 || Mathf.Abs(y-field.Height(px+2,pz+2))>1.25f)continue;
                var tree=Group("Oak",trees).gameObject;tree.transform.position=At(field,px,pz);
                tree.transform.localScale=Vector3.one*Mathf.Lerp(.8f,1.35f,(float)random.NextDouble());
                tree.AddComponent<MeshFilter>().sharedMesh=variants[random.Next(3)];tree.AddComponent<MeshRenderer>().sharedMaterial=material;
                var collider=tree.AddComponent<BoxCollider>();collider.center=new Vector3(0,3,0);collider.size=new Vector3(.9f,6,.9f);
            }
            // Small plants have no collision and are combined into a single saved mesh.
            var plants=new VoxelRegionGeometry();
            for(int i=0;i<1800*settings.vegetationDensity;i++)
            {
                float x=random.Next(-half,half),z=random.Next(-half,half);
                if(field.Reserved(x,z,1)||field.Height(x,z)>25)continue;
                Color c=i%4==0?new Color(.95f,.77f,.26f):new Color(.39f,.59f,.20f);
                plants.Box(At(field,x,z)+Vector3.up*.2f,new Vector3(.18f,.4f,.18f),c);
            }
            SaveMesh(plants.Mesh("Meadow"),"Meadow",parent,material,folder,false);
        }
        private static void Village(Transform parent,Material material,string folder,VoxelRegionSettings settings)
        {
            var village=Group("Village",parent);
            TownVillageBuilder.Build(village,settings);
            var well=new VoxelRegionGeometry();
            well.Box(new Vector3(-50,4.5f,-76),new Vector3(3,1,3),Stone);
            well.Box(new Vector3(-50,5.02f,-76),new Vector3(2,.05f,2),new Color(.19f,.45f,.57f));
            SaveMesh(well.Mesh("Well"),"Village Well",village,material,folder,true);
        }
        private static void Secret(VoxelRegionHeightfield field,Transform geometry,Transform root,Material material,string folder)
        {
            var mesh=new VoxelRegionGeometry();
            mesh.Box(new Vector3(72,9.5f,8),new Vector3(5,1,7),Stone);
            mesh.Box(new Vector3(68.5f,9.25f,8),new Vector3(2,.5f,4),Stone);
            foreach(float z in new[]{5f,11f})mesh.Box(new Vector3(74,11.5f,z),new Vector3(1,5,1),Stone);
            mesh.Box(new Vector3(74,14,8),new Vector3(1,1,7),Stone);
            SaveMesh(mesh.Mesh("Ruins"),"Hidden Ruin",geometry,material,folder,true);
            var pickupMesh=new VoxelRegionGeometry();pickupMesh.Box(Vector3.zero,Vector3.one*.65f,new Color(1,.8f,.20f));
            var pickup=SaveMesh(pickupMesh.Mesh("Restoration"),"Secret Restoration",root,material,folder,false);
            pickup.transform.position=new Vector3(72,10.7f,8);pickup.AddComponent<BoxCollider>().isTrigger=true;
            var rb=pickup.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;
            pickup.AddComponent<SecretRewardPickup>();
        }
        private static void Boss(VoxelRegionHeightfield field,Transform geometry,Transform root,Material material,string folder)
        {
            var shrine=new VoxelRegionGeometry();
            shrine.Box(new Vector3(34,16,100),new Vector3(2,8,12),Stone);
            shrine.Box(new Vector3(46,16,100),new Vector3(2,8,12),Stone);
            shrine.Box(new Vector3(40,16,106),new Vector3(14,8,2),Stone);
            shrine.Box(new Vector3(40,20,100),new Vector3(14,1,14),Stone);
            foreach(float x in new[]{36f,44f})shrine.Box(new Vector3(x,16,94),new Vector3(2,8,2),Stone);
            SaveMesh(shrine.Mesh("Guardian Shrine"),"Guardian Shrine",geometry,material,folder,true);
            var gateMesh=new VoxelRegionGeometry();gateMesh.Box(Vector3.zero,new Vector3(6,8,1),new Color(.25f,.39f,.49f));
            var gate=SaveMesh(gateMesh.Mesh("Gate"),"Boss Passage Gate",root,material,folder,true);gate.transform.position=new Vector3(40,16,94);
            var rewardMesh=new VoxelRegionGeometry();rewardMesh.Box(Vector3.zero,new Vector3(1.5f,1.5f,1.5f),new Color(1,.73f,.12f));
            var reward=SaveMesh(rewardMesh.Mesh("Reward"),"Boss Reward Indicator",root,material,folder,false);reward.transform.position=new Vector3(40,13,101);reward.SetActive(false);
            var boss=Spawn("FirstBoss",At(field,40,80),root).GetComponent<BossController>();
            Group("Boss Encounter",root).gameObject.AddComponent<BossEncounter>().Configure(boss,gate,reward);
        }
        private static void ValidateNavigation(Transform geometry,VoxelRegionHeightfield field)
        {
            Physics.SyncTransforms();
            var sources=new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(geometry,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var bounds=new Bounds(Vector3.zero,Vector3.one);
            foreach(var c in geometry.GetComponentsInChildren<Collider>())bounds.Encapsulate(c.bounds);
            bounds.Expand(4);
            var data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,bounds,Vector3.zero,Quaternion.identity);
            if(data==null)throw new InvalidOperationException("No se pudo construir la navegación.");
            var instance=NavMesh.AddNavMeshData(data);
            try
            {
                if(!NavMesh.SamplePosition(At(field,-50,-70),out var start,2,NavMesh.AllAreas))throw new InvalidOperationException("Spawn fuera del NavMesh.");
                foreach(var site in VoxelRegionHeightfield.Sites)
                {
                    var path=new NavMeshPath();
                    if(!NavMesh.SamplePosition(At(field,site.x,site.z),out var end,2,NavMesh.AllAreas) || !NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path) || path.status!=NavMeshPathStatus.PathComplete)
                        throw new InvalidOperationException("Semilla inválida: ruta incompleta hasta "+site);
                }
            }
            finally{instance.Remove();Object.DestroyImmediate(data);}
        }
        public static void ValidateSeeds()
        {
            var settings=ScriptableObject.CreateInstance<VoxelRegionSettings>();
            try
            {
                foreach(int seed in new[]{7319,17,942})
                {
                    settings.seed=seed;var field=new VoxelRegionHeightfield(settings);
                    for(int z=-128;z<128;z+=3)for(int x=-128;x<128;x+=3)
                    {
                        float height=field.Height(x+.5f,z+.5f);
                        if(height!=field.Height(x+.5f,z+.5f) || float.IsNaN(height))throw new InvalidOperationException("Altura no determinista");
                    }
                    for(int link=0;link<VoxelRegionHeightfield.Links.GetLength(0);link++)
                    {
                        Vector3 a=VoxelRegionHeightfield.Sites[VoxelRegionHeightfield.Links[link,0]],b=VoxelRegionHeightfield.Sites[VoxelRegionHeightfield.Links[link,1]];
                        float previous=field.Height(a.x,a.z);
                        for(float t=0;t<=1;t+=.002f)
                        {
                            Vector3 point=Vector3.Lerp(a,b,t);float height=field.Height(point.x,point.z);
                            if(Mathf.Abs(height-previous)>.251f)throw new InvalidOperationException("Escalón excesivo en ruta");previous=height;
                        }
                    }
                    UnityEngine.Debug.Log("VOXEL_SEED_OK "+seed);
                }
            }
            finally{Object.DestroyImmediate(settings);}
        }
        public static void ValidateAdditionalSeeds()
        {
            foreach(int seed in new[]{17,942})
            {
                var settings=ScriptableObject.CreateInstance<VoxelRegionSettings>();
                settings.hideFlags=HideFlags.HideAndDontSave;settings.seed=seed;
                try{Generate(settings);}finally{Object.DestroyImmediate(settings);}
            }
        }
        private static Vector3 At(VoxelRegionHeightfield field,float x,float z)=>new Vector3(x,field.Height(Mathf.Floor(x)+.5f,Mathf.Floor(z)+.5f),z);
        private static Transform Group(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent);return go.transform;}
        private static GameObject SaveMesh(Mesh mesh,string name,Transform parent,Material material,string folder,bool collider)
        {
            AssetDatabase.CreateAsset(mesh,folder+"/"+name+".asset");
            var go=Group(name,parent).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            if(collider)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
        }
        private static GameObject Spawn(string name,Vector3 position,Transform parent)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/"+name+".prefab");
            if(prefab==null)throw new InvalidOperationException("Falta "+name);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.transform.position=position;return go;
        }
        private static void Folder(string path)
        {
            string current="Assets";
            foreach(string part in path.Split('/').Skip(1)){string next=current+"/"+part;if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,part);current=next;}
        }
    }
}
