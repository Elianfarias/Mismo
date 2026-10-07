using UnityEngine;

namespace Mismo.Gameplay.Player.Presentation
{
    /// <summary>Separate rigid meshes rotate about the shared hinge axis; no Animator or runtime mesh copies.</summary>
    [DisallowMultipleComponent]
    public sealed class HunterTrapVisual : MonoBehaviour
    {
        public Transform frontJaw, backJaw, pressurePlate;
        [Range(0, 90)] public float closingAngle = 78;
        public Vector3 plateRestPosition = new Vector3(0, .055f, 0);
        public float Closure { get; private set; }
        public void SetClosure(float value)
        {
            Closure = Mathf.Clamp01(value);
            if (frontJaw != null) frontJaw.localRotation = Quaternion.Euler(-closingAngle * Closure, 0, 0);
            if (backJaw != null) backJaw.localRotation = Quaternion.Euler(closingAngle * Closure, 0, 0);
            if (pressurePlate != null) pressurePlate.localPosition = plateRestPosition + Vector3.down * (.0125f * Closure);
        }
        void OnEnable() => SetClosure(0);
    }
}
