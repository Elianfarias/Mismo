namespace Mismo.Gameplay.Enemies
{
    /// <summary>Goblin decisions: authored actions, or the original slash/charge rhythm.</summary>
    public sealed class GoblinController : HumanoidEnemyController
    {
        protected override GoblinAttack ChooseAttack(float distance, bool pressure)
        {
            if (AttackSelection.HasActions(Settings))
                return AttackSelection.SelectWeighted(Settings, distance);
            return AttackSelection.SelectLegacyMelee(Settings, distance, pressure);
        }
    }
}
