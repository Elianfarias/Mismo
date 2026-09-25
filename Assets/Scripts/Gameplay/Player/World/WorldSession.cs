using System;
using System.IO;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    // One active save, with an independent inventory file per world. New games never delete old profiles.
    public static class WorldSession
    {
        public const int CurrentVillageLayoutRevision=3;
        public static WorldSaveData Current {get;private set;}
        public static string LastError {get;private set;}
        static IProfileRepository repository;
        internal static string VerificationDirectory;
        public static string LegacyProfilePath=>Path.Combine(Application.persistentDataPath,"Profiles","single-player.mismo");
        public static string ProfilePath=>Current==null||Current.legacy?LegacyProfilePath:
            Path.Combine(VerificationDirectory??Path.Combine(Application.persistentDataPath,"Profiles"),"Worlds",Current.id+".mismo");
        static string SessionPath=>VerificationDirectory!=null?Path.Combine(VerificationDirectory,"active-world.mismo"):Path.Combine(Application.persistentDataPath,"Profiles","active-world.mismo");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){Current=null;repository=null;LastError=null;VerificationDirectory=null;}
        static bool Valid(string json)
        {
            try{return JsonUtility.FromJson<WorldSaveData>(json)?.IsValid()==true;}
            catch(ArgumentException){return false;}
        }
        static bool Read(out WorldSaveData data,out ProfileReadResult result)
        {
            data=null;result=ProfileReadResult.Invalid;LastError=null;
            try
            {
                repository=new ProtectedProfileRepository(SessionPath);
                result=repository.Read(Valid,out string json);
                if(result==ProfileReadResult.Invalid){LastError="No se pudo recuperar el mundo guardado.";return false;}
                data=json==null?null:JsonUtility.FromJson<WorldSaveData>(json);return true;
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is System.Security.Cryptography.CryptographicException)
            {LastError="No se pudo leer la partida: "+e.Message;return false;}
        }
        public static bool CanContinue()
        {
            if(!Read(out var data,out var result))return false;
            return data!=null||result==ProfileReadResult.Missing&&(File.Exists(LegacyProfilePath)||File.Exists(LegacyProfilePath+".bak"));
        }
        static ExplorationWorldSettings Template()=>Mismo.Core.ProjectAssets.Load<ExplorationWorldSettings>("ExplorationWorldSettings");
        static WorldSaveData CreateRecord(bool legacy,int previousSeed=0)
        {
            var template=Template();if(template==null)throw new InvalidOperationException("Falta la configuración del mundo.");
            var settings=UnityEngine.Object.Instantiate(template);
            string id=Guid.NewGuid().ToString("N");
            int seed=legacy?template.seed:WorldSaveData.FreshSeed(previousSeed);
            settings.seed=seed;settings.preserveAuthoredCenter=legacy;settings.streamingEnabled=true;
            settings.generationVersion=legacy?0:2;
            settings.introductionVersion=!legacy&&settings.content?.introduction!=null?1:0;
            Vector3 spawn=new Vector3(-50,4.25f,-70);float spawnYaw=0;
            if(!legacy)
            {
                var terrain=new ExplorationTerrain(settings);
                // The origin site is always a safe procedural village for new worlds.
                var site=terrain.Site(Vector2Int.zero);
                spawn=site.position+(settings.content!=null?settings.content.VillageArrivalOffset:new Vector3(0,0,-29));
                spawn.y=terrain.Height(spawn.x,spawn.z)+.3f;
                spawnYaw=settings.content!=null?settings.content.villageSpawnYaw:0;
                if(terrain.Introduction!=null){spawn=terrain.Introduction.Spawn;spawnYaw=terrain.Introduction.Facing.eulerAngles.y;}
            }
            else
            {
                var terrain=new ExplorationTerrain(settings);
                spawn=new Vector3(-50,4,-70)+(settings.content!=null?settings.content.VillageArrivalOffset:new Vector3(0,0,-29));
                spawn.y=terrain.Height(spawn.x,spawn.z)+.3f;
                spawnYaw=settings.content!=null?settings.content.villageSpawnYaw:0;
            }
            settings.content=null; // Runtime instance IDs are not portable save data.
            var record=new WorldSaveData{villageLayoutRevision=CurrentVillageLayoutRevision,id=legacy?"legacy":id,seed=seed,legacy=legacy,settingsJson=JsonUtility.ToJson(settings),
                x=spawn.x,y=spawn.y,z=spawn.z,yaw=spawnYaw,spawnX=spawn.x,spawnY=spawn.y,spawnZ=spawn.z};
            UnityEngine.Object.Destroy(settings);return record;
        }
        public static bool NewGame()
        {
            // Validate existing state before any replacement; corrupted files are preserved by the repository.
            if(!Read(out var previous,out _))return false;
            try
            {
                var next=CreateRecord(false,previous?.seed??0);
                return Commit(next);
            }
            catch(Exception e) when(e is InvalidOperationException||e is ArgumentException){LastError=e.Message;return false;}
        }
        public static bool Continue()
        {
            if(!Read(out var data,out var result))return false;
            if(data!=null)
            {
                Current=data;
                if(data.villageLayoutRevision>=CurrentVillageLayoutRevision)return true;
                var settings=Settings(Template());var next=data.Copy();
                MigrateVillageLayout(next,settings);UnityEngine.Object.Destroy(settings);
                return Commit(next);
            }
            if(result!=ProfileReadResult.Missing||!File.Exists(LegacyProfilePath)&&!File.Exists(LegacyProfilePath+".bak"))return false;
            try{return Commit(CreateRecord(true));}
            catch(Exception e) when(e is InvalidOperationException||e is ArgumentException){LastError=e.Message;return false;}
        }
        // Apply once when an older save meets the larger settlement layout.
        // Only relocate a saved player inside a surviving town; keep distant exploration intact.
        public static void MigrateVillageLayout(WorldSaveData data,ExplorationWorldSettings settings)
        {
            if(data.villageLayoutRevision>=CurrentVillageLayoutRevision)return;
            var field=new ExplorationTerrain(settings);var offset=settings.content!=null?settings.content.VillageArrivalOffset:new Vector3(0,0,-55);
            var start=settings.preserveAuthoredCenter?new Vector3(-50,4,-70):field.Site(Vector2Int.zero).position;
            var spawn=start+offset;spawn.y=field.Height(spawn.x,spawn.z)+.3f;
            data.spawnX=spawn.x;data.spawnY=spawn.y;data.spawnZ=spawn.z;
            Vector3? town=null;
            if(settings.preserveAuthoredCenter&&Mathf.Abs(data.x+50)<field.VillageHalfExtent+4&&Mathf.Abs(data.z+70)<field.VillageHalfExtent+4)town=start;
            int spacing=field.SiteSpacing,cx=Mathf.FloorToInt(data.x/spacing),cz=Mathf.FloorToInt(data.z/spacing);
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
            {
                var site=field.Site(new Vector2Int(cx+dx,cz+dz));
                if(site.kind==WorldSiteKind.Village&&field.IsExterior(site.position.x,site.position.z)&&Mathf.Abs(data.x-site.position.x)<field.VillageHalfExtent+4&&Mathf.Abs(data.z-site.position.z)<field.VillageHalfExtent+4)town=site.position;
            }
            if(town.HasValue)
            {var arrival=town.Value+offset;data.x=arrival.x;data.z=arrival.z;data.y=field.Height(arrival.x,arrival.z)+.3f;data.yaw=settings.content!=null?settings.content.villageSpawnYaw:0;}
            data.villageLayoutRevision=CurrentVillageLayoutRevision;
        }
        public static ExplorationWorldSettings Settings(ExplorationWorldSettings template)
        {
            if(Current==null)return template;
            var result=UnityEngine.Object.Instantiate(template);
            // Missing fields in old JSON must not inherit a future template's generator.
            result.generationVersion=0;
            result.introductionVersion=0;
            JsonUtility.FromJsonOverwrite(Current.settingsJson,result);
            result.content=template.content;result.seed=Current.seed;result.preserveAuthoredCenter=Current.legacy;
            return result;
        }
        public static bool Checkpoint(Vector3 position,float yaw)
        {
            if(Current==null)return false;var next=Current.Copy();next.x=position.x;next.y=position.y;next.z=position.z;next.yaw=yaw;
            if(DayNightCycle.Current!=null){next.hasTimeOfDay=true;next.timeOfDay=DayNightCycle.Current.hour;}
            return Commit(next);
        }
        public static bool SaveIntroduction(int stage,int lessons,Vector3? respawn=null,Vector3? position=null,float yaw=0)
        {
            if(Current==null)return false;
            var next=Current.Copy();next.introductionStage=stage;next.introductionLessons=lessons;
            if(respawn.HasValue){next.spawnX=respawn.Value.x;next.spawnY=respawn.Value.y;next.spawnZ=respawn.Value.z;}
            if(position.HasValue){next.x=position.Value.x;next.y=position.Value.y;next.z=position.Value.z;next.yaw=yaw;}
            return Commit(next);
        }
        public static bool SaveMapPins(System.Collections.Generic.List<Mismo.Gameplay.Player.Presentation.MapPin> pins)
        {
            if(Current==null)return false;
            var next=Current.Copy();next.mapPins=new System.Collections.Generic.List<Mismo.Gameplay.Player.Presentation.MapPin>(pins);
            return Commit(next);
        }
        public static bool SaveMapDiscovery(System.Collections.Generic.List<MapDiscoveryBlock> blocks)
        {
            if(Current==null)return false;
            var next=Current.Copy();next.mapDiscovery=blocks;return Commit(next);
        }
        public static void Respawn()
        {if(Current!=null)Checkpoint(new Vector3(Current.spawnX,Current.spawnY,Current.spawnZ),0);}
        static bool Commit(WorldSaveData next)
        {
            if(!next.IsValid())return false;
            try{repository.Write(JsonUtility.ToJson(next));Current=next;LastError=null;return true;}
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is System.Security.Cryptography.CryptographicException)
            {LastError="No se pudo guardar el mundo: "+e.Message;Debug.LogWarning(LastError);return false;}
        }
    }
}
