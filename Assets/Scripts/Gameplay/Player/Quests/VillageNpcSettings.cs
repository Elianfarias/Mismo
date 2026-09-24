using System;
using UnityEngine;
namespace Mismo.Gameplay.Player.Quests
{
    [Serializable] public sealed class VillageResident
    {
        public GameObject prefab;
        [Tooltip("X/Z: posición local del pueblo antes de escalarlo. Y: desplazamiento en metros sobre el terreno.")]
        public Vector3 localPosition;
        public float yaw;
        [Tooltip("Paradas locales: puesto de trabajo, plaza y paseo. Se proyectan sobre el terreno y NavMesh.")]
        public Vector3[] route=Array.Empty<Vector3>();
    }
    [CreateAssetMenu(menuName="Mismo/Quests/Village residents")]
    public sealed class VillageNpcSettings:ScriptableObject
    {
        public bool enabled=true;
        public bool startingVillageOnly=true;
        public VillageResident[] residents=Array.Empty<VillageResident>();
        [Tooltip("Escala de los habitantes, independiente del tamaño del pueblo. Los prefabs base miden 1,75 unidades.")]
        [Min(.1f)] public float residentScale=1.3f;
        [Min(.5f)] public float interactionRange=3;
        public bool requireLineOfSight=true;
        [Min(3)] public float markerDistance=35;
        [Min(0)] public float markerHeight=2.15f;
        [Range(18,64)] public int markerSize=36;
        public Color availableColor=new Color(1,.79f,.27f);
        public Color readyColor=new Color(.48f,1,.66f);
        public bool showActiveMarker=true;
        public bool showNames=true;
    }
}
