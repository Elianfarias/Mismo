using Mismo.Gameplay.Player.Quests;
using UnityEngine;

namespace Mismo.Gameplay.Player.World
{
    [CreateAssetMenu(menuName="Mismo/World/Dragon tutorial arc")]
    public sealed class DragonArcDefinition : ScriptableObject
    {
        public const int BossPhases = 2;
        public QuestDefinition audience, awakening;
        public GameObject lookout, king, flyingVisual, encounterPrefab, altar;
        public AnimationClip flight;
        [Tooltip("Escala del modelo durante el sobrevuelo del tutorial.")]
        [Min(.01f)] public float flyingVisualScale = .8f;
        [Header("Primer sobrevuelo · metros y segundos")]
        public Vector3 flybyStartOffset = new Vector3(-140,55,165);
        public Vector3 flybyDirection = new Vector3(1,.035f,.16f);
        [Min(1)] public float flybySpeed = 48;
        [Min(2)] public float flybyCinematicTime = 6.5f;
        [Min(.1f)] public float flybyCameraReturnTime = 1.1f;
        [Range(15,70)] public float flybyCameraFov = 34;
        [Tooltip("Continúa volando después de devolver el control. Solo se retira fuera de cámara y a esta distancia mínima del jugador.")]
        [Min(50)] public float flybyCleanupDistance = 260;
        [Tooltip("Pendiente del concept y modelo definitivos. Sin asignar: solo punto de interacción y marcador.")]
        public GameObject beacon;
        public Vector2[] beaconOffsets = { new Vector2(-160,80), new Vector2(155,-135), new Vector2(185,230) };
        public Vector2 arenaOffset = new Vector2(370,80);
        [Min(40)] public float arenaRadius = 46;
        public string[] beaconNames = { "Faro del bosque", "Faro de las ruinas", "Faro de la quebrada" };
        [TextArea(3,8)] public string warning = "Ese dragón es Soul Eater. Desde que empezó a sobrevolar la región, los monstruos se acercan cada vez más al pueblo. Ya no responde a nuestras ofrendas. Algo cambió en las tierras del interior. El rey está aquí, ayudándonos a proteger el camino. Hablá con él antes de seguir.";
        [TextArea(3,8)] public string audienceText = "Las ofrendas ya no bastan. Los antiguos encendían tres faros para llamar al dragón: el del bosque, el de las ruinas y el de la quebrada. Reactivalos en el orden que prefieras. Cuando los tres respondan, acercate al altar de la llamada. El ritual solo comenzará cuando vos lo decidas. Prepará tus armas antes de invocar a Soul Eater.";
        public bool Valid => audience != null && awakening != null && audience.Validate(out _) && awakening.Validate(out _) &&
            audience.objectives.Length == 2 && awakening.objectives.Length == 4 && beaconOffsets?.Length == 3 && beaconNames?.Length == 3;
    }
}
