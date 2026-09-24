using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mismo.Gameplay.Player.World.Structures
{
    public enum StructureStyle { Cave, Ruin, Sanctuary, Temple }
    public enum StructurePlacement { CompatibleSites, IceEscarpment }
    public enum StructureRoomRole { Entrance, Chamber, Branch, Final }
    [Serializable] public sealed class StructureRoom
    {
        [HideInInspector] public string id=Guid.NewGuid().ToString("N");
        [HideInInspector] public List<StructureContentEntry> contents=new List<StructureContentEntry>();
        public string name;
        public StructureRoomRole role;
        public Vector2 center;
        [Range(3,9)] public float radius=4;
        [Range(4,10)] public float height=6;
    }
    [Serializable] public sealed class StructureConnection
    {
        public int from,to;
        [Range(3.5f,8)] public float width=5;
    }
    [CreateAssetMenu(menuName="Mismo/World/Structure definition")]
    public sealed class StructureDefinition : ScriptableObject
    {
        public string displayName="Gruta del manantial";
        public StructureStyle style;
        public StructurePlacement placement;
        public WorldBiome[] worldBiomes=Array.Empty<WorldBiome>();
        public bool worldStoneSurface=true;
        public GroveWorldStyle stoneStyle;
        public Material waterMaterial;
        [Range(27,32)] public float moatRadius=31;
        public GameObject[] exteriorDetails=Array.Empty<GameObject>();
        public int layoutSeed=7319,decorationSeed=101;
        [Range(3,6)] public int mainRooms=4;
        [Range(0,2)] public int branches=1;
        [Range(.3f,.8f)] public float voxelSize=.5f;
        [Range(12,24)] public float mountainHeight=17;
        [Range(0,1)] public float decorationDensity=.65f;
        [Range(.01f,1)] public float interiorAmbient=.14f;
        [Range(0,4)] public float lightIntensity=1.4f;
        public Color lightColor=new Color(.23f,.9f,.85f);
        [Min(0)] public int finalExperience=100;
        public StructureRequirement finalRequirement;
        public Material shellMaterial;
        public GameObject entranceArch,finalLandmark,rewardVisual;
        public GameObject[] floorDetails=Array.Empty<GameObject>(),ceilingDetails=Array.Empty<GameObject>(),wallDetails=Array.Empty<GameObject>();
        public List<StructureRoom> rooms=new List<StructureRoom>();
        public List<StructureConnection> connections=new List<StructureConnection>();
        public float Radius
        {
            get{float r=0;foreach(var room in rooms)if(room!=null)r=Mathf.Max(r,room.center.magnitude+room.radius);return Mathf.Max(Mathf.Ceil(r+5),style==StructureStyle.Temple?moatRadius:0);}
        }
        public static bool IsMoat(Vector2 p,float radius)=>radius>0&&p.magnitude<radius-.7f&&(Mathf.Abs(p.x)>16||Mathf.Abs(p.y)>20);
        public static float MoatDepth(Vector2 p,float radius)=>IsMoat(p,radius)?-1.25f:0;
        public float FoundationOffset(Vector2 p)=>MoatDepth(p,style==StructureStyle.Temple?moatRadius:0);
        public int EntranceIndex=>rooms.FindIndex(r=>r!=null&&r.role==StructureRoomRole.Entrance);
        public int FinalIndex=>rooms.FindIndex(r=>r!=null&&r.role==StructureRoomRole.Final);
        public Vector3 Entrance=>new Vector3(rooms[EntranceIndex].center.x,0,-Radius-1);
        public void GenerateLayout()
        {
            // Match rooms by role and ordinal so moving/regenerating preserves consumed content IDs.
            var previous=new List<StructureRoom>(rooms);
            int wanted=Mathf.Clamp(mainRooms,3,6),wantedBranches=Mathf.Clamp(branches,0,2);
            int chamber=0,branch=0;
            foreach(var room in previous)if(room!=null)
            {
                bool removed=room.role==StructureRoomRole.Chamber&&chamber++>=wanted-2||room.role==StructureRoomRole.Branch&&branch++>=wantedBranches;
                if(removed&&room.contents!=null&&room.contents.Count>0)throw new InvalidOperationException("Hay contenido en una sala que se eliminaria. Moverlo o quitarlo antes de reducir las salas.");
            }
            var random=new System.Random(layoutSeed);rooms.Clear();connections.Clear();
            int count=Mathf.Clamp(mainRooms,3,6);
            for(int i=0;i<count;i++)
            {
                float z=Mathf.Lerp(style==StructureStyle.Temple?-11:-15,style==StructureStyle.Temple?10:13,i/(float)(count-1));
                float x=i==0||i==count-1?0:(float)(random.NextDouble()-.5)*(style==StructureStyle.Temple?7:11);
                rooms.Add(new StructureRoom{name=i==0?"Entrada":i==count-1?"Sala final":"Camara "+i,role=i==0?StructureRoomRole.Entrance:i==count-1?StructureRoomRole.Final:StructureRoomRole.Chamber,center=new Vector2(x,z),radius=i==count-1?6:4,height=i==count-1?7:6});
                if(i>0)connections.Add(new StructureConnection{from=i-1,to=i,width=5});
            }
            for(int i=0;i<Mathf.Clamp(branches,0,2);i++)
            {
                int parent=1+i%(count-2);var center=rooms[parent].center+new Vector2((i%2==0?-1:1)*10,1);
                rooms.Add(new StructureRoom{name="Ramal "+(i+1),role=StructureRoomRole.Branch,center=center,radius=3.5f,height=5});
                connections.Add(new StructureConnection{from=parent,to=rooms.Count-1,width=4.5f});
            }
            foreach(StructureRoomRole role in Enum.GetValues(typeof(StructureRoomRole)))
            {
                var oldRooms=previous.FindAll(r=>r!=null&&r.role==role);var newRooms=rooms.FindAll(r=>r.role==role);
                for(int i=0;i<Mathf.Min(oldRooms.Count,newRooms.Count);i++)
                {newRooms[i].id=oldRooms[i].id;newRooms[i].contents=oldRooms[i].contents??new List<StructureContentEntry>();if(newRooms[i].contents.Count>0){newRooms[i].radius=oldRooms[i].radius;newRooms[i].height=oldRooms[i].height;}}
            }
            EnsureContentIds();
        }
        public bool EnsureContentIds()
        {
            bool changed=false;
            foreach(var room in rooms)if(room!=null)
            {
                if(string.IsNullOrEmpty(room.id)){room.id=Guid.NewGuid().ToString("N");changed=true;}
                if(room.contents==null){room.contents=new List<StructureContentEntry>();changed=true;}
                foreach(var item in room.contents)if(item!=null&&string.IsNullOrEmpty(item.id)){item.id=Guid.NewGuid().ToString("N");changed=true;}
            }
            return changed;
        }
        public string ValidateLayout()
        {
            if(rooms==null||rooms.Count<2||rooms.Count>12)return "Se necesitan entre 2 y 12 salas.";
            if(rooms.Exists(r=>r==null))return "Hay una sala vacia.";
            if(rooms.FindAll(r=>r.role==StructureRoomRole.Entrance).Count!=1||rooms.FindAll(r=>r.role==StructureRoomRole.Final).Count!=1)return "Debe existir exactamente una entrada y una sala final.";
            foreach(var room in rooms)if(!Finite(room.center.x)||!Finite(room.center.y)||!Finite(room.radius)||!Finite(room.height)||room.radius<3||room.radius>9||room.height<4||room.height>10)return "Dimensiones de sala invalidas.";
            if(Radius>32)return "La estructura debe caber en un radio de 32 m para el streaming y el terreno.";
            if(style==StructureStyle.Temple&&(waterMaterial==null||moatRadius<27||moatRadius>32))return "Asignar material de agua y un estanque de 27 a 32 m.";
            if(!Finite(voxelSize)||!Finite(mountainHeight)||voxelSize<.3f||voxelSize>.8f||mountainHeight<12||mountainHeight>24||shellMaterial==null)return "Revisar resolucion, altura de montana y material.";
            if(connections==null)return "Faltan conexiones.";
            var reached=new HashSet<int>{EntranceIndex};
            foreach(var c in connections)if(c==null||c.from<0||c.to<0||c.from>=rooms.Count||c.to>=rooms.Count||c.from==c.to||!Finite(c.width)||c.width<3.5f||c.width>8)return "Conexion invalida.";
            bool changed=true;while(changed){changed=false;foreach(var c in connections){if(reached.Contains(c.from))changed|=reached.Add(c.to);if(reached.Contains(c.to))changed|=reached.Add(c.from);}}
            if(reached.Count!=rooms.Count)return "Hay salas sin acceso desde la entrada.";
            if(rooms[EntranceIndex].center.y>rooms[FinalIndex].center.y)return "La entrada debe estar al sur de la sala final.";
            var ids=new HashSet<string>();var roomIds=new HashSet<string>();
            foreach(var room in rooms)
            {
                if(!string.IsNullOrEmpty(room.id)&&!roomIds.Add(room.id))return "Hay salas con identidad duplicada. Crear las salas con Generar plano.";
                if(room.contents==null)continue;
                foreach(var item in room.contents)
                {
                    if(item==null)return room.name+": contenido vacio.";
                    string error=item.Validate();if(error!=null)return room.name+" / "+item.label+": "+error;
                    if(!ids.Add(item.id))return "Contenido con identidad duplicada. Usar Duplicar en el taller.";
                    float distance=style==StructureStyle.Temple||style==StructureStyle.Ruin?Mathf.Max(Mathf.Abs(item.offset.x),Mathf.Abs(item.offset.y)):item.offset.magnitude;
                    if(distance>room.radius-1)return room.name+": colocar el contenido al menos a 1 m de la pared.";
                    if(item.kind==StructureContentKind.Enemy&&item.prefab.GetComponent<UnityEngine.AI.NavMeshAgent>().height*item.scale>Ceiling(room.center+item.offset)-.3f)return room.name+": el enemigo es demasiado alto para el techo.";
                }
            }
            return null;
        }
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        public static float SegmentDistance(Vector2 point,Vector2 a,Vector2 b)
        {var d=b-a;float t=d.sqrMagnitude<.001f?0:Mathf.Clamp01(Vector2.Dot(point-a,d)/d.sqrMagnitude);return Vector2.Distance(point,a+d*t);}
        public float Ceiling(Vector2 p)
        {
            float top=-1;
            foreach(var r in rooms)
            {
                float distance=style==StructureStyle.Ruin||style==StructureStyle.Temple?Mathf.Max(Mathf.Abs(p.x-r.center.x),Mathf.Abs(p.y-r.center.y)):Vector2.Distance(p,r.center);
                float q=distance/r.radius;if(q<1)top=Mathf.Max(top,3.6f+(r.height-3.6f)*Mathf.Sqrt(1-q*q));
            }
            foreach(var c in connections)
            {float q=SegmentDistance(p,rooms[c.from].center,rooms[c.to].center)/(c.width*.5f);if(q<1)top=Mathf.Max(top,4.2f+1.3f*Mathf.Sqrt(1-q*q));}
            var entry=rooms[EntranceIndex];float access=SegmentDistance(p,new Vector2(entry.center.x,-Radius-2),entry.center)/2.6f;
            if(access<1)top=Mathf.Max(top,4.2f+1.3f*Mathf.Sqrt(1-access*access));
            return top;
        }
    }
}
