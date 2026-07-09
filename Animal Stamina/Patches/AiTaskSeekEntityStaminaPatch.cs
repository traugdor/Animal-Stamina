using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace AnimalStamina.Patches
{
    /// <summary>
    /// Makes exhausted predators give up the chase instead of pursuing forever:
    ///  - ShouldExecute: an exhausted creature won't start a fresh chase (unless it was
    ///    recently attacked and retaliation-while-exhausted is allowed - a cornered animal
    ///    can still fight back even if too tired to run).
    ///  - ContinueExecute: if a creature becomes exhausted mid-chase, the task ends on its
    ///    next tick, which stops the path traverser and lets the AI fall through to idle/rest.
    /// </summary>
    [HarmonyPatch(typeof(AiTaskSeekEntity), "ShouldExecute")]
    public static class AiTaskSeekEntityShouldExecutePatch
    {
        public static bool Prefix(EntityAgent ___entity, long ___attackedByEntityMs, ref bool __result)
        {
            EntityBehaviorStamina stamina = ___entity?.GetBehavior<EntityBehaviorStamina>();
            if (stamina == null || !stamina.IsExhausted)
            {
                return true;
            }

            bool recentlyAttacked = ___entity.World.ElapsedMilliseconds - ___attackedByEntityMs < 30000;
            if (stamina.AllowRetaliationWhileExhausted && recentlyAttacked)
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(AiTaskSeekEntity), "ContinueExecute")]
    public static class AiTaskSeekEntityContinueExecutePatch
    {
        public static void Postfix(EntityAgent ___entity, ref bool __result)
        {
            if (!__result)
            {
                return;
            }

            EntityBehaviorStamina stamina = ___entity?.GetBehavior<EntityBehaviorStamina>();
            if (stamina != null && stamina.IsExhausted)
            {
                __result = false;
            }
        }
    }
}
