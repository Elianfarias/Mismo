using UnityEngine;

namespace Mismo.Gameplay.Enemies
{
    /// <summary>Shared humanoid combat responses, independent of the Animator rig type.</summary>
    public abstract class HumanoidEnemyController : EnemyController
    {
        protected override bool UsesAdditiveHitReaction => true;
        protected override bool AllowsHeavyRecoveryInterrupt => true;
        protected override float BreakRecoveryDuration => 1.5f;
        public override float NameplateHeight => 2.05f * transform.lossyScale.y;
    }
}
