using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mismo.Gameplay.Player.World.Structures;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// A graph is shared by every style; only the shell and dressing change.
public static class StructureBaker
{
    public const string PrefabRoot="Assets/Art/Prefabs/World/Structures/";
    public const string MeshRoot="Assets/Art/Meshes/Environment/Structures/";
    public static void Folder(string path)
    {path=path.TrimEnd('/');if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static float Noise(float x,float z,int seed)=>Mathf.PerlinNoise(x*.17f+(seed&255),z*.17f+31);
    public static Mesh CreateMesh(StructureDefinition d)
    {
        var error=d.ValidateLayout();if(error!=null)throw new InvalidOperationException(error);
        float step=d.voxelSize,radius=d.Radius;
        var g=new EnchantedGroveVoxels(step);int n=Mathf.CeilToInt(radius/step);
        for(int z=-n;z<=n;z++)for(int x=-n;x<=n;x++)
        {
            var p=new Vector2((x+.5f)*step,(z+.5f)*step);float r=p.magnitude/radius;
            float ceiling=d.Ceiling(p),top;
            if(d.style==StructureStyle.Cave)
            {
                if(r>1)continue;
                top=StructureExterior.RockHeight(d,p);
                // Maintain a rock roof over all interior rooms; only the south access opens to daylight.
                if(ceiling>0&&p.y>-radius+6)top=Mathf.Max(top,ceiling+2);
                if(top<.1f&&ceiling<0)continue;
            }
            else if(d.style==StructureStyle.Temple)
            {
                top=StructureExterior.TempleHeight(p);
                if(p.y>-19)
                {
                    float adjacent=ceiling;
                    // Rooms moved beyond the main facade still need a thick, closed outer wall.
                    if(top<=0)foreach(var offset in new[]{Vector2.left,Vector2.right,Vector2.up,Vector2.down})adjacent=Mathf.Max(adjacent,d.Ceiling(p+offset));
                    if(adjacent>0)top=Mathf.Max(top,adjacent+2);
                }
                if(top<=0)continue;
            }
            else
            {
                bool near=ceiling>0;
                foreach(var offset in new[]{new Vector2(1,0),new Vector2(-1,0),new Vector2(0,1),new Vector2(0,-1)})near|=d.Ceiling(p+offset)>0;
                if(!near)continue;
                top=d.style==StructureStyle.Sanctuary?.65f:2.7f+Noise(p.x,p.y,d.layoutSeed)*1.7f;
            }
            int maxY=Mathf.CeilToInt(top/step);
            for(int y=-1;y<maxY;y++)
            {
                float py=(y+.5f)*step;
                if(py>=0&&ceiling>0&&(py<ceiling||d.style==StructureStyle.Ruin||d.style==StructureStyle.Sanctuary))continue;
                byte color=(byte)(1+Mathf.Clamp((int)(Noise(p.x+py*.23f,p.y-py*.16f,d.layoutSeed)*3),0,2));
                if(py<0)color=4;
                g.Cells[new Vector3Int(x,y,z)]=color;
            }
        }
        if(d.style==StructureStyle.Temple)
        {
            StructureExterior.TempleDetails(g,d);
            // Cornices never close the generated walking route.
            foreach(var cell in g.Cells.Keys.ToArray())
            {var p=((Vector3)cell+Vector3.one*.5f)*step;float ceiling=d.Ceiling(new Vector2(p.x,p.z));if(p.y>=0&&p.y<ceiling)g.Cells.Remove(cell);}
        }
        if(d.placement==StructurePlacement.IceEscarpment)
        {
            foreach(float side in new[]{-1f,1f})
            {
                for(int i=0;i<3;i++)
                {float x=side*(4+i*1.7f),h=12+i*2;g.Box(V(x-.8f,0,-18+i),V(x+.8f,h,-14+i),3);g.Box(V(x-1.2f,h,-19+i),V(x+1.2f,h+1,-13+i),56);}
            }
            g.Box(V(-5,8,-18),V(5,9,-14),56);
        }
        var mesh=g.Mesh(d.name+"_Shell",8);StructureExterior.ColorStone(mesh,d);return mesh;
    }
    static GameObject Place(GameObject prefab,Transform parent,Vector3 position,float yaw,float scale,bool decoration=true)
    {
        if(prefab==null)return null;
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale*=scale;
        if(decoration)foreach(var c in go.GetComponentsInChildren<Collider>())c.enabled=false;
        return go;
    }
    static bool InCorridor(StructureDefinition d,Vector2 p,float margin)
    {
        foreach(var c in d.connections)if(StructureDefinition.SegmentDistance(p,d.rooms[c.from].center,d.rooms[c.to].center)<c.width*.5f+margin)return true;
        return StructureDefinition.SegmentDistance(p,new Vector2(d.Entrance.x,d.Entrance.z),d.rooms[d.EntranceIndex].center)<2.6f+margin;
    }
    static GameObject Pick(GameObject[] values,System.Random random)=>values!=null&&values.Length>0?values[random.Next(values.Length)]:null;
    public static GameObject Create(StructureDefinition d,Scene scene,Mesh mesh)
    {
        var root=new GameObject(d.name);SceneManager.MoveGameObjectToScene(root,scene);
        var shell=new GameObject("Shell");shell.transform.SetParent(root.transform,false);shell.AddComponent<MeshCollider>().sharedMesh=mesh;
        // Separate render bounds let URP choose local room lights and cull hidden rock sectors.
        foreach(var part in RenderSections(mesh))
        {var go=new GameObject(part.name);go.transform.SetParent(shell.transform,false);go.AddComponent<MeshFilter>().sharedMesh=part;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=d.shellMaterial;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.TwoSided;}
        var instance=root.AddComponent<StructureInstance>();instance.title=d.displayName;instance.style=d.style;instance.footprintRadius=d.Radius;instance.entrance=d.Entrance;instance.ambientMultiplier=d.interiorAmbient;
        instance.finalRequirement=d.finalRequirement;instance.finalRoomId=d.rooms[d.FinalIndex].id;
        var contentRoot=new GameObject("Room content").transform;contentRoot.SetParent(root.transform,false);
        foreach(var room in d.rooms)if(room.contents!=null)foreach(var item in room.contents)
        {
            var point=new GameObject(item.label).AddComponent<StructureContentPoint>();point.transform.SetParent(contentRoot,false);
            point.transform.localPosition=V(room.center.x+item.offset.x,.06f,room.center.y+item.offset.y);point.transform.localRotation=Quaternion.Euler(0,item.yaw,0);
            point.roomId=room.id;point.data=item.Copy();
        }
        instance.moatRadius=d.style==StructureStyle.Temple?d.moatRadius:0;
        var final=d.rooms[d.FinalIndex];instance.finalRoom=V(final.center.x,0,final.center.y);
        instance.rooms=d.rooms.Select(r=>new StructureInteriorRoom{center=V(r.center.x,0,r.center.y),radius=r.radius,height=r.height}).ToArray();
        var ends=new List<Vector3>();var radii=new List<float>();
        foreach(var c in d.connections){var a=d.rooms[c.from];var b=d.rooms[c.to];ends.Add(V(a.center.x,0,a.center.y));ends.Add(V(b.center.x,0,b.center.y));radii.Add(c.width*.5f);}
        ends.Add(d.Entrance);var entry=d.rooms[d.EntranceIndex];ends.Add(V(entry.center.x,0,entry.center.y));radii.Add(2.6f);instance.corridorEnds=ends.ToArray();instance.corridorRadii=radii.ToArray();
        var dressing=new GameObject("Decoration").transform;dressing.SetParent(root.transform,false);
        var random=new System.Random(d.decorationSeed);
        foreach(var room in d.rooms)
        {
            int count=Mathf.RoundToInt(12*d.decorationDensity);
            for(int i=0;i<count;i++)
            {
                float a=(i+(float)random.NextDouble()*.3f)*Mathf.PI*2/Mathf.Max(1,count);
                var p=room.center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(room.radius-.75f);
                if(!InCorridor(d,p,.7f))Place(Pick(d.floorDetails,random),dressing,V(p.x,.05f,p.y),(float)random.NextDouble()*360,.6f+(float)random.NextDouble()*.3f);
                if((d.style==StructureStyle.Cave||d.style==StructureStyle.Temple)&&i%3==0)
                {
                    var ceilingPoint=room.center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(room.radius-.5f);
                    float height=d.Ceiling(ceilingPoint);
                    if(height>4.4f)Place(Pick(d.ceilingDetails,random),dressing,V(ceilingPoint.x,height+.12f,ceilingPoint.y),a*Mathf.Rad2Deg,.5f);
                }
                if(i%4==0&&!InCorridor(d,p,.8f))Place(Pick(d.wallDetails,random),dressing,V(p.x,d.style==StructureStyle.Cave?3.3f:0,p.y),-a*Mathf.Rad2Deg+90,.7f);
            }
            // One bounded point light per room, rather than one per glowing prop.
            if(d.style==StructureStyle.Cave||d.style==StructureStyle.Temple)
            {
                var light=new GameObject("Room light "+room.name).AddComponent<Light>();light.transform.SetParent(root.transform,false);light.transform.localPosition=V(room.center.x,2.8f,room.center.y);light.type=LightType.Point;light.range=room.radius*2.2f;light.color=d.lightColor;light.intensity=d.lightIntensity*8;light.renderMode=LightRenderMode.ForcePixel;light.shadows=LightShadows.None;
            }
        }
        var arch=Place(d.entranceArch,dressing,V(entry.center.x,0,d.style==StructureStyle.Temple?-19:-d.Radius+7),0,d.style==StructureStyle.Temple?1.35f:.90f);
        if(d.style==StructureStyle.Cave&&d.worldStoneSurface&&arch!=null)
        foreach(var filter in arch.GetComponentsInChildren<MeshFilter>())
        {var surface=Object.Instantiate(filter.sharedMesh);surface.name=d.name+"_Entrance_"+filter.sharedMesh.name;StructureExterior.ColorStone(surface,d);filter.sharedMesh=SaveMesh(surface,MeshRoot+surface.name+".asset");filter.GetComponent<MeshRenderer>().sharedMaterial=d.shellMaterial;}
        Exterior(d,root.transform,dressing,random);
        var landmark=instance.finalRoom+V(0,0,2.4f);Place(d.finalLandmark,dressing,landmark,0,1.0f);
        var reward=new GameObject("Final room reward");reward.transform.SetParent(root.transform,false);reward.transform.localPosition=instance.finalRoom+V(0,1,0);
        var trigger=reward.AddComponent<SphereCollider>();trigger.radius=1.2f;trigger.isTrigger=true;reward.AddComponent<StructureReward>().experience=d.finalExperience;
        Place(d.rewardVisual,reward.transform,V(0,-1,0),0,.7f);
        var spawn=new GameObject("Entrance spawn").transform;spawn.SetParent(root.transform,false);spawn.localPosition=d.Entrance+V(0,.2f,-1);
        return root;
    }
    static void Exterior(StructureDefinition d,Transform root,Transform dressing,System.Random random)
    {
        if(d.style==StructureStyle.Temple)
        {
            var water=new GameObject("Temple moat");water.transform.SetParent(root,false);water.AddComponent<MeshFilter>().sharedMesh=SaveMesh(StructureExterior.Water(d),MeshRoot+d.name+"_Water.asset");water.AddComponent<MeshRenderer>().sharedMaterial=d.waterMaterial;
            var trees=d.exteriorDetails.Where(p=>p!=null&&(p.name.StartsWith("Tree_")||p.name.StartsWith("Shrub_"))).ToArray();
            for(int i=0;i<16;i++)
            {float a=i*Mathf.PI*2/16;var p=V(Mathf.Cos(a)*(d.Radius-.1f),0,Mathf.Sin(a)*(d.Radius-.1f));if(p.z<-20&&Mathf.Abs(p.x)<9)continue;Place(Pick(trees,random),dressing,p,a*Mathf.Rad2Deg,.9f+(float)random.NextDouble()*.35f);}
            var lilies=d.exteriorDetails.FirstOrDefault(p=>p!=null&&p.name=="Lily_Pads");var reeds=d.exteriorDetails.FirstOrDefault(p=>p!=null&&p.name=="Reeds");
            foreach(float sign in new[]{-1f,1f})for(int i=0;i<6;i++)
            {Place(lilies,dressing,V(sign*(19+i%2*2),-.32f,-12+i*4),i*42,1.2f);Place(reeds,dressing,V(sign*16.5f,-.3f,-15+i*5),i*30,1.2f);}
            var vines=d.wallDetails.FirstOrDefault(p=>p!=null&&p.name.Contains("Vines"));
            foreach(float side in new[]{-1f,1f})foreach(var ledge in new[]{V(11,13,-16.6f),V(8,20,-14.6f),V(6,27,-10.6f)})
            {var p=ledge;p.x*=side;Place(vines,dressing,p,0,1.5f);p.x+=side*2;Place(vines,dressing,p,0,.8f);}
        }
        else if(d.style==StructureStyle.Cave)
        {
            for(int i=0;i<12;i++)
            {float a=i*Mathf.PI*2/12;var p=V(Mathf.Cos(a)*(d.Radius-4),0,Mathf.Sin(a)*(d.Radius-4));if(p.z<-10&&Mathf.Abs(p.x)<5)continue;Place(Pick(d.exteriorDetails,random),dressing,p,a*Mathf.Rad2Deg,1.3f+(float)random.NextDouble()*.6f);}
        }
    }
    public static Mesh SaveMesh(Mesh mesh,string path)
    {
        Folder(Path.GetDirectoryName(path).Replace('\\','/'));var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
    }
    static IEnumerable<Mesh> RenderSections(Mesh source)
    {
        var vertices=source.vertices;var normals=source.normals;var uv=source.uv;var colors=source.colors;var triangles=source.triangles;
        var groups=new Dictionary<Vector3Int,List<int>>();
        for(int t=0;t<triangles.Length;t+=6)
        {
            var center=(vertices[triangles[t]]+vertices[triangles[t+1]]+vertices[triangles[t+2]])/3;
            var key=Vector3Int.FloorToInt(center/8);if(!groups.TryGetValue(key,out var list))groups[key]=list=new List<int>();
            for(int i=0;i<6;i++)list.Add(triangles[t+i]);
        }
        string path=AssetDatabase.GetAssetPath(source);var existing=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Where(m=>m!=source).ToDictionary(m=>m.name);
        foreach(var pair in groups)
        {
            string name="Sector_"+pair.Key.x+"_"+pair.Key.y+"_"+pair.Key.z;
            var indices=new Dictionary<int,int>();var v=new List<Vector3>();var n=new List<Vector3>();var u=new List<Vector2>();var c=new List<Color>();var tr=new List<int>();
            foreach(int index in pair.Value){if(!indices.TryGetValue(index,out int mapped)){mapped=v.Count;indices[index]=mapped;v.Add(vertices[index]);n.Add(normals[index]);u.Add(uv[index]);if(colors.Length==vertices.Length)c.Add(colors[index]);}tr.Add(mapped);}
            var part=new Mesh{name=name};part.SetVertices(v);part.SetNormals(n);part.SetUVs(0,u);if(c.Count==v.Count)part.SetColors(c);part.SetTriangles(tr,0);part.RecalculateBounds();
            if(existing.TryGetValue(name,out var saved)){EditorUtility.CopySerialized(part,saved);Object.DestroyImmediate(part);part=saved;EditorUtility.SetDirty(part);}else AssetDatabase.AddObjectToAsset(part,source);
            yield return part;
        }
    }
    public static GameObject Bake(StructureDefinition d)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Salir de Play para guardar la estructura.");
        if(d.EnsureContentIds())EditorUtility.SetDirty(d);
        string error=d.ValidateLayout();if(error!=null)throw new InvalidOperationException(error);
        Folder(MeshRoot);Folder(PrefabRoot);
        var mesh=CreateMesh(d);string meshPath=MeshRoot+d.name+"_Shell.asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(existing==null)AssetDatabase.CreateAsset(mesh,meshPath);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(existing);}
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();GameObject root=null;
        try{root=Create(d,scene,mesh);var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabRoot+d.name+".prefab");AssetDatabase.SaveAssets();return prefab;}
        finally{if(root!=null)Object.DestroyImmediate(root);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
    }
}
