using UnityEngine;
namespace Mismo.Gameplay.Player.World
{
    [CreateAssetMenu(menuName="Mismo/Bestiary/Book")]
    public sealed class BestiaryBook:ScriptableObject
    {
        public string title="Bestiario";
        public CreatureSpecies[] pages;
        public Color paper=new Color(.88f,.82f,.65f),ink=new Color(.16f,.12f,.075f);
    }
}
