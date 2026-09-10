namespace Mismo.Gameplay.Player.Editor
{
    internal sealed class VoxelRegionGeometry : World.VoxelRegionGeometry { }
    internal sealed class VoxelRegionHeightfield : World.VoxelRegionHeightfield
    {
        public VoxelRegionHeightfield(VoxelRegionSettings settings) : base(settings.seed,settings.size,settings.relief,settings.stepHeight) { }
    }
}