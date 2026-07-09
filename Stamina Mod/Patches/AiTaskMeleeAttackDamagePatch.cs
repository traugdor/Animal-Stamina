using HarmonyLib;
using StaminaMod.Config;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace StaminaMod.Patches
{
    /// <summary>
    /// Compensates AI-controlled creatures for the new stamina limits: any attacker with a
    /// stamina pool hits harder. attackTarget() reads the "damage" field directly rather than
    /// taking it as a parameter, so we temporarily scale the field for the duration of the call
    /// (Prefix/Postfix pair) instead of mutating it permanently - a permanent mutation would
    /// compound every single attack.
    /// </summary>
    [HarmonyPatch(typeof(AiTaskMeleeAttack), "attackTarget")]
    public static class AiTaskMeleeAttackDamagePatch
    {
        public static void Prefix(EntityAgent ___entity, ref float ___damage, ref float __state)
        {
            __state = ___damage;

            EntityBehaviorStamina stamina = ___entity?.GetBehavior<EntityBehaviorStamina>();
            if (stamina != null)
            {
                ___damage *= StaminaModConfig.Instance.MeleeDamageMultiplier;
            }
        }

        public static void Postfix(ref float ___damage, float __state)
        {
            ___damage = __state;
        }
    }
}
