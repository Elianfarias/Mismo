using System;
using Mismo.Gameplay.Player.Equipment;
using Mismo.Gameplay.Player.Presentation;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    [CreateAssetMenu(menuName="Mismo/World/Introduction")]
    public sealed class WorldIntroductionDefinition:ScriptableObject
    {
        public GameObject cave,grove,narrator;
        public Vector3 caveSpawn=new Vector3(0,.35f,36);
        public float caveRadius=50;
        public TutorialSequence arrival,sprint,story,village;
        public WorldIntroductionEncounter[] encounters=Array.Empty<WorldIntroductionEncounter>();
    }
    [Serializable]
    public sealed class WorldIntroductionEncounter
    {
        public string title;
        [TextArea] public string instruction;
        public Vector2 offset;
        public TutorialSequence lesson;
        public GameObject prefab;
        public WeaponDefinition requiredWeapon;
        public WeaponDefinition lessonWeapon;
        public int count=1;
    }
}
