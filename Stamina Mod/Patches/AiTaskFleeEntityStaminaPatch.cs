using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace StaminaMod.Patches
{
    /// <summary>
    /// Mirrors the predator give-up patch for prey: a critically exhausted animal won't start a
    /// fresh voluntary flee (it's too gassed to panic-sprint again). We deliberately don't
    /// interrupt an already-in-progress flee the way we do for seeking predators - an animal
    /// mid-escape should get to finish that attempt (already self-limited by the speed throttle),
    /// rather than visibly freezing in place the moment its stamina crosses the threshold.
    /// The emergency "instaflee" panic response (triggered when cornered/badly hurt) is left
    /// untouched regardless of stamina - that's a last-resort reflex, not a voluntary sprint.
    /// </summary>
    [HarmonyPatch(typeof(AiTaskFleeEntity), "ShouldExecute")]
    public static class AiTaskFleeEntityShouldExecutePatch
    {
        public static bool Prefix(EntityAgent ___entity, bool ___instafleenow, ref bool __result)
        {
            if (___instafleenow)
            {
                return true;
            }

            EntityBehaviorStamina stamina = ___entity?.GetBehavior<EntityBehaviorStamina>();
            if (stamina == null || !stamina.IsExhausted)
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}
