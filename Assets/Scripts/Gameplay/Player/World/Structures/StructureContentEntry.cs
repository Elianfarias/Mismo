using System;
using Mismo.Gameplay.Combat;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Equipment.Inventory;
using UnityEngine;
using UnityEngine.AI;

namespace Mismo.Gameplay.Player.World.Structures
{
    public enum StructureContentKind { Enemy, Chest, Resource, Decoration }
    public enum StructureRequirement { None, RoomCleared, StructureCleared }

    [Serializable] public sealed class StructureContentEntry
    {
        public string id=Guid.NewGuid().ToString("N");
        public string label="Contenido";
        public StructureContentKind kind;
        public Vector2 offset;
        public float yaw;
        [Range(.25f,3)] public float scale=1;
        public GameObject prefab;
        [Min(0)] public int enemyLevel;
        public bool requiredForClear=true;
        public ResourceNodeDefinition resource;
        public bool resourceOneTime=true;
        public MaterialLootTable loot;
        public WeaponDefinition weapon;
        [Range(1,5)] public int weaponTier=1;
        [Min(0)] public int experience;
        public StructureRequirement requirement;
        public StructureContentEntry Copy()=> (StructureContentEntry)MemberwiseClone();
        public StructureContentEntry Duplicate()
        {var copy=Copy();copy.id=Guid.NewGuid().ToString("N");return copy;}
        public string Validate()
        {
            if(string.IsNullOrWhiteSpace(id))return "Falta identidad del contenido; abrir el taller para asignarla.";
            if(!Enum.IsDefined(typeof(StructureContentKind),kind)||!Enum.IsDefined(typeof(StructureRequirement),requirement))return "Tipo de contenido invalido.";
            if(float.IsNaN(offset.x)||float.IsInfinity(offset.x)||float.IsNaN(offset.y)||float.IsInfinity(offset.y)||float.IsNaN(yaw)||float.IsInfinity(yaw)||float.IsNaN(scale)||scale<.25f||scale>3)return "Posicion o escala de contenido invalida.";
            if(experience<0||experience>1000000||enemyLevel<0||enemyLevel>1000||weaponTier<1||weaponTier>5)return "Nivel o recompensa fuera de rango.";
            if(kind==StructureContentKind.Enemy)
            {
                if(prefab==null||prefab.GetComponent<Health>()==null||prefab.GetComponent<DamageReceiver>()==null||prefab.GetComponent<NavMeshAgent>()==null)return "Asignar un prefab enemigo completo con vida, combate y NavMeshAgent.";
                if(requirement!=StructureRequirement.None)return "Los enemigos no pueden depender de limpiar la sala.";
            }
            if(kind==StructureContentKind.Resource&&(resource==null||resource.availablePrefab==null||resource.rewards==null))return "Asignar una mena/recurso con modelo y tabla de materiales.";
            if((kind==StructureContentKind.Chest||kind==StructureContentKind.Decoration)&&prefab==null)return "Asignar el modelo del objeto.";
            if(kind==StructureContentKind.Chest&&loot==null&&weapon==null&&experience==0)return "El cofre necesita materiales, un arma o experiencia.";
            return null;
        }
    }
}
