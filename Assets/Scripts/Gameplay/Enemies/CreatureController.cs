namespace Mismo.Gameplay.Enemies
{
    /// <summary>Terrestrial creatures share execution without inheriting humanoid reactions.</summary>
    public sealed class CreatureController : EnemyController
    {
        protected override GoblinAttack ChooseAttack(float distance, bool pressure) =>
            AttackSelection.HasActions(Settings)
                ? AttackSelection.SelectWeighted(Settings, distance)
                : AttackSelection.SelectLegacyMelee(Settings, distance, pressure);
    }
}
