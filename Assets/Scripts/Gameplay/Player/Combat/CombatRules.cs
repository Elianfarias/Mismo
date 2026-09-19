using UnityEngine;
namespace Mismo.Gameplay.Combat
{
    [CreateAssetMenu(menuName="Mismo/Combat/Combat rules")]
    public sealed class CombatRules : ScriptableObject
    {
        static CombatRules cached;
        public static CombatRules Current => cached!=null?cached:(cached=Mismo.Core.ProjectAssets.Load<CombatRules>("CombatRules")??CreateInstance<CombatRules>());
        [Range(0,1)] public float frontalDamage=.85f;
        public float backDamage=1.2f, backPosture=1.5f, openingPosture=1.4f;
        [Range(0,.1f)] public float distanceBonus=.075f;
        public float optimalDistance=18;
        [Range(0,1)] public float closeRangeMultiplier=.8f;
        public float closeRange=2, effectiveRange=10;
        public float RangedMultiplier(float distance)
        {
            float close=Mathf.Lerp(closeRangeMultiplier,1,Mathf.InverseLerp(closeRange,Mathf.Max(closeRange+.01f,effectiveRange),distance));
            return close*(1+distanceBonus*Mathf.Clamp01(distance/Mathf.Max(1,optimalDistance)));
        }
        public float perfectParryWindow=.09f, perfectDodgeWindow=.085f;
        public float perfectParryPosture=35, parryPosture=12;
        public float perfectFocus=20, backFocus=8, openingFocus=6;
    }
}
