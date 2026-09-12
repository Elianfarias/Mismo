using System;
using System.Collections.Generic;

namespace Mismo.Gameplay.Player.Equipment.Inventory
{
    public static class MaterialCatalog
    {
        public static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 120;
        public const string Herb = "MAT-01";
        public const string Wood = "MAT-02";
        public const string Stone = "MAT-03";
        public const string MonsterComponent = "MAT-04";
    }

    [Serializable]
    public sealed class MaterialStack
    {
        public string id;
        public int quantity;
        public MaterialStack Copy() => (MaterialStack)MemberwiseClone();
    }
}
