using HarmonyLib;
using Vintagestory.API.Common;

namespace StaminaMod.Patches
{
    /// <summary>
    /// Every AI movement entry point (wander, seek, flee, get-out-of-water, stay-close-to, ...)
    /// ultimately funnels through PathTraverserBase.WalkTowards, which stashes the requested
    /// speed in the protected "movingSpeed" field consumed every tick by the traverser
    /// (ground movement AND swimming both read this same field). Scaling the speed here,
    /// once, covers every AI task and every traverser implementation without touching any
    /// individual task's code.
    /// </summary>
    [HarmonyPatch(typeof(PathTraverserBase), "WalkTowards")]
    public static class PathTraverserSpeedPatch
    {
        public static void Prefix(EntityAgent ___entity, ref float movingSpeed)
        {
            EntityBehaviorStamina stamina = ___entity?.GetBehavior<EntityBehaviorStamina>();
            if (stamina != null)
            {
                movingSpeed *= stamina.SpeedFactor;
            }
        }
    }
}
