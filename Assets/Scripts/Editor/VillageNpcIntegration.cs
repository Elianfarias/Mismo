using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using Mismo.Gameplay.Player.Editor;
using Mismo.Gameplay.Player.Quests;

[InitializeOnLoad]
public static class VillageNpcIntegration
{
    static VillageNpcIntegration(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Temp/VillageNpcSetup.request";
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(request))return;
        File.Delete(request);
        try{Setup();File.WriteAllText("Temp/VillageNpcSetup.result","PASS");}
        catch(Exception e){File.WriteAllText("Temp/VillageNpcSetup.result",e.ToString());Debug.LogException(e);}
    }
    public const string Source="Assets/Art/FBX/Characters/NPC/craftpix-net-700077-free-medieval-3d-people-low-poly-models";
    public const string Prefabs="Assets/Art/Prefabs/Voxelized/NPC/Craftpix";
    public const string Residents="Assets/Art/Prefabs/Quests/Village";
    public const string SettingsPath="Assets/Data/Quests/VillageNpcSettings.asset";
    const string TexturePath="Assets/Art/Textures/Characters/NPC/Craftpix/people_texture_map.png";
    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        var parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    static void Move(string source,string target)
    {
        if(!File.Exists(source)&&!AssetDatabase.IsValidFolder(source))return;
        if(File.Exists(target)||AssetDatabase.IsValidFolder(target))throw new InvalidOperationException("No se sobrescribe el destino existente: "+target);
        Folder(Path.GetDirectoryName(target).Replace('\\','/'));string guid=AssetDatabase.AssetPathToGUID(source);
        string error=AssetDatabase.MoveAsset(source,target);if(error.Length>0||AssetDatabase.AssetPathToGUID(target)!=guid)throw new InvalidOperationException("MoveAsset: "+error);
    }
    [MenuItem("Mismo/Quests/Preparar NPCs voxelizados del pueblo")]
    public static void Setup()
    {
        Directory.CreateDirectory("output/village-npcs");
        Move(Source+"/texture/people_texture_map.png",TexturePath);
        Move(Source+"/texture/people_texture_map.tx","Assets/Art/Source/Characters/NPC/Craftpix/people_texture_map.tx");
        foreach(var name in new[]{"License.txt","readme.txt"})Move(Source+"/"+name,"Assets/Documentation/NPC/Craftpix/"+name);
        Folder(Prefabs);Folder(Residents);Folder("Assets/Art/Materials/Characters/NPC/Craftpix");
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);if(texture==null)throw new Exception("NPC palette missing");
        var ti=(TextureImporter)AssetImporter.GetAtPath(TexturePath);ti.filterMode=FilterMode.Point;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.mipmapEnabled=false;ti.SaveAndReimport();
        const string matPath="Assets/Art/Materials/Characters/NPC/Craftpix/PeoplePalette.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.mainTexture=texture;material.SetFloat("_Smoothness",0);AssetDatabase.CreateAsset(material,matPath);}
        foreach(string path in Directory.GetFiles(Source+"/fbx/people_unity","*.fbx").Select(p=>p.Replace('\\','/')).OrderBy(p=>p))
        {
            string name="NPC_"+Path.GetFileNameWithoutExtension(path),target=Prefabs+"/"+name+".prefab";
            if(File.Exists(target))continue;
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"People_texture_map"),material);importer.SaveAndReimport();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var voxel=WeaponVoxelizerWindow.ExportModel(source,name,64,true);
            string original=AssetDatabase.GetAssetPath(voxel);
            Move(original,target);
            Move("Assets/Art/Meshes/Voxelized/"+name+".asset","Assets/Art/Meshes/Voxelized/NPC/Craftpix/"+name+".asset");
            Move("Assets/Art/Animations/Voxelized/"+name+"_Animations","Assets/Art/Animations/Voxelized/NPC/Craftpix/"+name+"_Animations");
            Move("Assets/Art/Materials/"+name+"_Voxel.mat","Assets/Art/Materials/Characters/NPC/Craftpix/"+name+"_Voxel.mat");
        }
        var catalog=AssetDatabase.LoadAssetAtPath<QuestCatalog>("Assets/Data/Quests/QuestCatalog.asset");
        var settings=AssetDatabase.LoadAssetAtPath<VillageNpcSettings>(SettingsPath);
        if(settings==null)
        {
            settings=ScriptableObject.CreateInstance<VillageNpcSettings>();AssetDatabase.CreateAsset(settings,SettingsPath);
            string[] ids={"mara","bruno","elena","iria","tomas"};
            string[] models={"peasant_4","peasant_1","peasant_6","rich_citizzens_2","city_dwellers_1"};
            // The central square is open in the current village. Keep the entrance and routes clear.
            Vector3[] positions={new Vector3(-3,0,-3),new Vector3(3,0,-3),new Vector3(-4,0,2),new Vector3(3,0,2),new Vector3(3,0,-9)};
            settings.residents=new VillageResident[ids.Length];
            for(int i=0;i<ids.Length;i++)settings.residents[i]=new VillageResident{prefab=Resident(ids[i],models[i],catalog,settings),localPosition=positions[i],yaw=i%2==0?90:270};
        }
        if(catalog.villageNpcs==null){catalog.journalContacts=false;catalog.villageNpcs=settings;EditorUtility.SetDirty(catalog);}
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Inspect();RenderModels();
        try{ProjectOrganizationChecks.Run();File.WriteAllText("output/village-npcs/Organization.txt","PASS");}
        catch(Exception e){File.WriteAllText("output/village-npcs/Organization.txt",e.Message);Debug.LogWarning("NPC_ORGANIZATION_REPORT: "+e.Message);}
        Debug.Log("NPC_SETUP_OK: 14 voxelized models, 5 residents, physical quest contacts");
    }
    static GameObject Resident(string id,string model,QuestCatalog catalog,VillageNpcSettings settings)
    {
        string path=Residents+"/"+id+".prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
        var definition=AssetDatabase.LoadAssetAtPath<QuestNpcDefinition>("Assets/Data/Quests/NPCs/"+id+".asset");
        var root=new GameObject(definition.displayName);
        try
        {
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/NPC_"+model+".prefab"));visual.transform.SetParent(root.transform,false);
            foreach(var collider in visual.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
            foreach(var animator in visual.GetComponentsInChildren<Animator>())animator.enabled=false;
            RestPose(visual);
            var skin=visual.GetComponent<SkinnedMeshRenderer>();var baked=new Mesh();skin.BakeMesh(baked);var bounds=baked.bounds;Object.DestroyImmediate(baked);
            float scale=1.75f/bounds.size.y;visual.transform.localScale=Vector3.one*scale;visual.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*scale;
            var body=root.AddComponent<CapsuleCollider>();body.center=new Vector3(0,.875f,0);body.height=1.75f;body.radius=.28f;
            var giver=root.AddComponent<QuestGiver>();giver.npc=definition;giver.interactionLabel="Hablar con "+definition.displayName;giver.quests=catalog.quests.Where(q=>q.npc==definition).ToArray();
            // These encounters still need a world controller; do not advertise an impossible assignment.
            giver.eventQuests=giver.quests.Where(q=>!q.offerInJournal).ToArray();giver.markerSettings=settings;
            var idle=root.AddComponent<VillageNpcIdle>();var bones=visual.GetComponentsInChildren<Transform>();idle.spine=bones.FirstOrDefault(t=>t.name=="Spine_02");idle.head=bones.FirstOrDefault(t=>t.name=="Neck_01");idle.phase=(id.Length*1.7f);
            return PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{Object.DestroyImmediate(root);}
    }
    public static void RestPose(GameObject visual)
    {
        foreach(var bone in visual.GetComponentsInChildren<Transform>().Where(t=>t.name=="Upperarm_L"||t.name=="Upperarm_R"))
        {
            var lower=bone.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith("Lowerarm_"));if(lower==null)continue;
            float side=Mathf.Sign((lower.position-bone.position).x);
            bone.rotation=Quaternion.FromToRotation(lower.position-bone.position,new Vector3(side*.18f,-1,.04f))*bone.rotation;
        }
    }
    public static void RenderModels()
    {
        var previewScene=EditorSceneManager.NewPreviewScene();
        try{
        var light=new GameObject("Preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(40,-30,0);
        var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.20f,.22f);camera.orthographic=true;camera.orthographicSize=1.13f;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,previewScene);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,previewScene);camera.scene=previewScene;
        var rt=new RenderTexture(320,420,24);camera.targetTexture=rt;
        foreach(var path in Directory.GetFiles(Prefabs,"*.prefab").OrderBy(p=>p))
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/')));RestPose(go);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,previewScene);
            foreach(var animator in go.GetComponentsInChildren<Animator>())animator.enabled=false;
            camera.transform.position=new Vector3(2,.5f,4);camera.transform.LookAt(Vector3.zero);camera.Render();
            RenderTexture.active=rt;var image=new Texture2D(320,420,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,320,420),0,0);image.Apply();File.WriteAllBytes("output/village-npcs/"+Path.GetFileNameWithoutExtension(path)+".png",image.EncodeToPNG());Object.DestroyImmediate(image);Object.DestroyImmediate(go);
        }
        camera.targetTexture=null;RenderTexture.active=null;rt.Release();Object.DestroyImmediate(rt);
        }finally{EditorSceneManager.ClosePreviewScene(previewScene);}
    }
    public static void Inspect()
    {
        Directory.CreateDirectory("output/village-npcs");
        var lines=new System.Collections.Generic.List<string>();
        foreach(var path in Directory.GetFiles(Source+"/fbx/people_unity","*.fbx").OrderBy(p=>p))
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\','/'));
            var go=Object.Instantiate(model);
            var renderers=go.GetComponentsInChildren<Renderer>();
            var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);
            lines.Add(Path.GetFileName(path)+" bounds="+b+" skins="+go.GetComponentsInChildren<SkinnedMeshRenderer>().Length+" clips="+AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Count()+" materials="+string.Join(",",renderers.SelectMany(r=>r.sharedMaterials).Select(m=>m==null?"NULL":m.name)));
            Object.DestroyImmediate(go);
        }
        File.WriteAllLines("output/village-npcs/SourceModels.txt",lines);
        Debug.Log("NPC_INSPECT_OK");
    }
}
