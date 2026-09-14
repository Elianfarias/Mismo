using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    [CreateAssetMenu(menuName="Mismo/Bestiary/Species")]
    public sealed class CreatureSpecies:ScriptableObject
    {
        public string id,displayName;
        [TextArea]public string description;
        public GameObject[] prefabs;
        public bool domesticable,mountable;
        [Range(0,1)]public float recognitionChance=.1f;
        public RiderPose riderPose;
        [Min(1)]public float ridingSpeed=8,followingSpeed=5.5f;
        [Min(1)]public float turnSpeed=180;
        [Range(.3f,1)]public float bookZoom=1;
        public static bool Visible(Transform creature,Transform observer)
        {
            var camera=UnityEngine.Camera.main;if(camera==null)return false;var point=creature.position+Vector3.up*.7f;var viewport=camera.WorldToViewportPoint(point);
            if(viewport.z<=0||viewport.z>40||viewport.x<0||viewport.x>1||viewport.y<0||viewport.y>1)return false;
            foreach(var hit in Physics.RaycastAll(camera.transform.position,(point-camera.transform.position).normalized,Vector3.Distance(point,camera.transform.position),~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(creature)&&!hit.transform.IsChildOf(observer))return false;
            return true;
        }
        public static CreatureSpecies Find(string id)=>System.Array.Find(Resources.LoadAll<CreatureSpecies>("Bestiary"),s=>s.id==id);
        public static CreatureSpecies For(GameObject actor)
        {
            string name=actor.name.Replace("(Clone)","").Trim();
            foreach(var species in Resources.LoadAll<CreatureSpecies>("Bestiary"))
                if(species.prefabs!=null)foreach(var prefab in species.prefabs)if(prefab!=null&&prefab.name==name)return species;
            return null;
        }
    }
}

