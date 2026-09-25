using System;

namespace Mismo.Gameplay.Player.World
{
    [Serializable]
    public sealed class WorldSaveData
    {
        public int version=1;
        public System.Collections.Generic.List<Mismo.Gameplay.Player.Presentation.MapPin> mapPins = new System.Collections.Generic.List<Mismo.Gameplay.Player.Presentation.MapPin>();
        public System.Collections.Generic.List<MapDiscoveryBlock> mapDiscovery = new System.Collections.Generic.List<MapDiscoveryBlock>();
        public int villageLayoutRevision;
        public int introductionStage;
        public int introductionLessons;
        public string id;
        public int seed;
        public string settingsJson;
        public bool legacy;
        public bool hasTimeOfDay;
        public float timeOfDay;
        public float x,y,z,yaw,spawnX,spawnY,spawnZ;
        public WorldSaveData Copy()=>(WorldSaveData)MemberwiseClone();
        public static int FreshSeed(int previous)
        {
            int seed=(int)(BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(),0)&0x7fffffff);
            if(seed==0)seed=1;
            return seed==previous?(seed==int.MaxValue?1:seed+1):seed;
        }
        public bool IsValid()=>PinsValid()&&version==1&&(legacy?id=="legacy":Guid.TryParseExact(id,"N",out _))&&seed>0&&
            !string.IsNullOrEmpty(settingsJson)&&settingsJson.Length<64000&&
            Finite(x)&&Finite(y)&&Finite(z)&&Finite(yaw)&&Finite(spawnX)&&Finite(spawnY)&&Finite(spawnZ)&&(!hasTimeOfDay||Finite(timeOfDay)&&timeOfDay>=0&&timeOfDay<24);
        bool PinsValid()
        {
            if(mapPins==null)return true; // Existing worlds predate markers.
            if(mapPins.Count>500)return false;
            var ids=new System.Collections.Generic.HashSet<string>();
            foreach(var pin in mapPins)
                if(pin==null||string.IsNullOrWhiteSpace(pin.id)||!ids.Add(pin.id)||string.IsNullOrWhiteSpace(pin.typeId)||
                    string.IsNullOrWhiteSpace(pin.name)||pin.name.Length>48||!Finite(pin.x)||!Finite(pin.z))return false;
            return true;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&Math.Abs(value)<=1000000;
    }
}



