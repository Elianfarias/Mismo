using System;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.Equipment.Inventory;
using Mismo.Gameplay.Player.World;
using Mismo.Gameplay.Player.World.Structures;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class StructureContentSetup
{
    public const string ChestPath="Assets/Art/Prefabs/World/Structures/Content/AncientChest.prefab";
    public const string LootPath=StructureWorkshopSetup.DefRoot+"Content/AncientChestLoot.asset";
    public const string OrePath=StructureWorkshopSetup.DefRoot+"Content/ArcaneVein.asset";
    public static StructureContentEntry DefaultEntry(StructureContentKind kind)
    {
        var e=new StructureContentEntry{kind=kind};
        switch(kind)
        {
            case StructureContentKind.Enemy:e.label="Guardian";e.prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Enemies/Goblin.prefab");break;
            case StructureContentKind.Chest:e.label="Cofre antiguo";e.yaw=180;e.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ChestPath);e.loot=AssetDatabase.LoadAssetAtPath<MaterialLootTable>(LootPath);e.experience=25;break;
            case StructureContentKind.Resource:e.label="Veta arcana";e.resource=AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(OrePath);break;
            default:e.label="Decoracion";e.prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/EnchantedGrove/Ruin_Pillar.prefab");break;
        }
        return e;
    }
    static void CreateChest()
    {
        if(AssetDatabase.LoadAssetAtPath<GameObject>(ChestPath)!=null)return;
        StructureBaker.Folder(Path.GetDirectoryName(ChestPath).Replace('\\','/'));
        string meshFolder=StructureBaker.MeshRoot+"Content/";StructureBaker.Folder(meshFolder);
        var root=new GameObject("AncientChest");var wood=new Color(.22f,.10f,.045f).linear;var gold=new Color(.55f,.38f,.13f).linear;var dark=new Color(.08f,.045f,.027f).linear;
        try
        {
            var body=new VoxelRegionGeometry();
            body.Box(new Vector3(0,.13f,0),new Vector3(1.55f,.26f,.95f),dark);
            foreach(float z in new[]{-.42f,.42f})for(int i=0;i<3;i++)body.Box(new Vector3(0,.28f+i*.14f,z),new Vector3(1.48f,.125f,.10f),i%2==0?wood:wood*1.22f);
            foreach(float x in new[]{-.7f,.7f})body.Box(new Vector3(x,.44f,0),new Vector3(.12f,.54f,.88f),wood);
            foreach(float x in new[]{-.53f,.53f})foreach(float z in new[]{-.49f,.49f})body.Box(new Vector3(x,.4f,z),new Vector3(.13f,.65f,.08f),gold);
            body.Box(new Vector3(0,.53f,.51f),new Vector3(.22f,.25f,.10f),gold);
            MeshPart(root,"Body",body.Mesh("AncientChest_Body"),meshFolder);
            var lidRoot=new GameObject("Lid");lidRoot.transform.SetParent(root.transform,false);lidRoot.transform.localPosition=new Vector3(0,.7f,-.45f);
            var lid=new VoxelRegionGeometry();
            for(int i=0;i<7;i++){float z=.075f+i*.13f;float y=.11f+Mathf.Sin((i+.5f)/7*Mathf.PI)*.13f;lid.Box(new Vector3(0,y,z),new Vector3(1.52f,.20f,.12f),i%2==0?wood:wood*1.22f);foreach(float x in new[]{-.53f,.53f})lid.Box(new Vector3(x,y+.105f,z),new Vector3(.13f,.045f,.13f),gold);}
            MeshPart(lidRoot,"Lid mesh",lid.Mesh("AncientChest_Lid"),meshFolder);
            // Visual colliders only: the slot owns interaction and persistent state.
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,.42f,0);collider.size=new Vector3(1.55f,.84f,1);
            PrefabUtility.SaveAsPrefabAsset(root,ChestPath);
        }
        finally{Object.DestroyImmediate(root);}
    }
    static void MeshPart(GameObject parent,string name,Mesh mesh,string folder)
    {
        var go=new GameObject(name);go.transform.SetParent(parent.transform,false);go.AddComponent<MeshFilter>().sharedMesh=StructureBaker.SaveMesh(mesh,folder+mesh.name+".asset");go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/TerrainSurface.mat");
    }
    static void CreateRewards()
    {
        StructureBaker.Folder(StructureWorkshopSetup.DefRoot+"Content");
        var original=AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>("Assets/Data/Gathering/Minerals/crystal_basalt_green.023.asset");
        if(AssetDatabase.LoadAssetAtPath<MaterialLootTable>(LootPath)==null)
        {var loot=Object.Instantiate(original.rewards);foreach(var e in loot.entries){e.minimum=2;e.maximum=4;e.probability=1;}AssetDatabase.CreateAsset(loot,LootPath);}
        if(AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(OrePath)==null)
        {var ore=Object.Instantiate(original);ore.id="structure-arcane-vein";ore.displayName="Veta de cristal arcano";ore.actionName="Extraer";ore.harvestSeconds=3;AssetDatabase.CreateAsset(ore,OrePath);}
        var vein=AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(OrePath);
        if(vein.availablePrefab==original.availablePrefab)
        {
            vein.availablePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/CaveKit/C03_Racimo_alto.prefab");
            vein.depletedPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/World/CaveKit/P03_Grupo_grava.prefab");
            EditorUtility.SetDirty(vein);
        }
    }
    static void SeedContent(StructureDefinition definition)
    {
        if(definition.rooms.Any(r=>r.contents!=null&&r.contents.Count>0))return;
        var chamber=definition.rooms.First(r=>r.role==StructureRoomRole.Chamber);
        var guard=DefaultEntry(StructureContentKind.Enemy);guard.offset=new Vector2(1.5f,0);guard.enemyLevel=2;chamber.contents.Add(guard);
        var final=definition.rooms[definition.FinalIndex];var sentinel=DefaultEntry(StructureContentKind.Enemy);sentinel.label="Guardian de la sala final";sentinel.enemyLevel=3;sentinel.offset=new Vector2(-2,0);final.contents.Add(sentinel);
        var branch=definition.rooms.FirstOrDefault(r=>r.role==StructureRoomRole.Branch)??final;
        var ore=DefaultEntry(StructureContentKind.Resource);ore.offset=new Vector2(-1.5f,1);branch.contents.Add(ore);
        var chest=DefaultEntry(StructureContentKind.Chest);chest.offset=new Vector2(2,1);chest.requirement=StructureRequirement.StructureCleared;final.contents.Add(chest);
        definition.finalRequirement=StructureRequirement.StructureCleared;
    }
    public static void RunBatch()
    {
        if(!Application.isBatchMode||!Directory.GetCurrentDirectory().Replace('\\','/').Contains("/.validation/"))throw new InvalidOperationException("Use isolated validation project");
        try
        {
            CreateChest();CreateRewards();
            foreach(var meshName in new[]{"AncientChest_Body","AncientChest_Lid"})
            {var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(StructureBaker.MeshRoot+"Content/"+meshName+".asset");var colors=mesh.colors;for(int i=0;i<colors.Length;i++)colors[i].a=0;mesh.colors=colors;EditorUtility.SetDirty(mesh);}
            foreach(var name in new[]{"Cave_Moss","Cave_Crystal","Temple_Grove","Ruin_Grove"})
            {
                var d=AssetDatabase.LoadAssetAtPath<StructureDefinition>(StructureWorkshopSetup.DefRoot+name+".asset");d.EnsureContentIds();
                if(name=="Cave_Crystal"||name=="Temple_Grove")SeedContent(d);
                if(name=="Cave_Crystal"||name=="Temple_Grove")foreach(var item in d.rooms.SelectMany(r=>r.contents))
                    if(item.kind==StructureContentKind.Chest&&item.label=="Cofre antiguo"&&item.yaw==0&&item.offset==new Vector2(2,1))item.yaw=180;
                EditorUtility.SetDirty(d);var prefab=StructureBaker.Bake(d);StructureWorkshopSetup.CreatePlayground(d,prefab);
            }
            AssetDatabase.SaveAssets();StructureWorkshopChecks.Run();
            try{ProjectOrganizationChecks.Run();File.WriteAllText(StructureWorkshopSetup.Output+"/organization.txt","PASS");}catch(Exception e){File.WriteAllText(StructureWorkshopSetup.Output+"/organization.txt",e.Message);}
            Debug.Log("STRUCTURE_CONTENT_SETUP_OK");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
