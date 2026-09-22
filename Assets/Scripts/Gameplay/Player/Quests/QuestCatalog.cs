using System;
using UnityEngine;
using Mismo.Core;
namespace Mismo.Gameplay.Player.Quests
{
    [CreateAssetMenu(menuName="Mismo/Quests/Catalog and settings")]
    public sealed class QuestCatalog:ScriptableObject
    {
        public QuestDefinition[] quests=Array.Empty<QuestDefinition>();
        [Tooltip("Permite aceptar y entregar pedidos mediante los diálogos del diario mientras no hay NPCs en el mundo.")]
        public bool journalContacts=false;
        public VillageNpcSettings villageNpcs;
        public UnityEngine.InputSystem.Key journalKey=UnityEngine.InputSystem.Key.J;
        public UnityEngine.InputSystem.Key interactKey=UnityEngine.InputSystem.Key.T;
        [Tooltip("Desplazamiento del aviso de interacción desde el centro inferior de la pantalla. Y deja espacio para cofre, botín y habilidades.")]
        public Vector2 interactionPromptOffset=new Vector2(0,145);
        public bool showTracker=true;
        [Tooltip("Margen desde la esquina superior izquierda de la pantalla, en unidades de referencia 1280 x 800. Independiente del lienzo centrado del diario.")] public Vector2 trackerOffset=new Vector2(12,80);
        [Range(260,550)] public float trackerWidth=350;
        [Range(.2f,1)] public float panelOpacity=1;
        public Texture2D journalIcon;
        public Texture2D radialSurface,radialOutline;
        public Texture2D[] radialSelected=Array.Empty<Texture2D>();
        public static QuestCatalog Load()=>ProjectAssets.Load<QuestCatalog>("QuestCatalog");
        public QuestDefinition Find(string id)=>Array.Find(quests,q=>q!=null&&q.id==id);
        public bool Contains(QuestDefinition quest)=>quest!=null&&Array.IndexOf(quests,quest)>=0;
    }
}
