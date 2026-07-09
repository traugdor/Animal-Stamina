using Vintagestory.API.Common;

namespace StaminaMod.Config
{
    /// <summary>
    /// Global tuning knobs for the stamina system. Individual entity types can still
    /// override a subset of these via the "staminaMod" attributes block added by
    /// this mod's entity patches (see EntityBehaviorStamina.Initialize).
    /// </summary>
    public class StaminaModConfig
    {
        public static StaminaModConfig Instance = new StaminaModConfig();

        /// <summary>Stamina pool for a creature of ReferenceWeightKg. Bigger/smaller creatures scale from this.</summary>
        public float BaseMaxStamina = 100f;

        /// <summary>The entity weight (kg, see EntityProperties.Weight) that BaseMaxStamina is calibrated for.</summary>
        public float ReferenceWeightKg = 80f;

        /// <summary>How strongly weight influences max stamina. 0 = no effect, 1 = linear. We use a dampened curve.</summary>
        public float WeightScalingExponent = 0.4f;

        /// <summary>Lower bound for the weight-based max stamina multiplier (tiny critters still get a usable pool).</summary>
        public float MinWeightMultiplier = 0.55f;

        /// <summary>Upper bound for the weight-based max stamina multiplier (giants don't get absurdly huge pools).</summary>
        public float MaxWeightMultiplier = 2.25f;

        /// <summary>Below this movement-speed value (same units as AI task "movespeed"), no stamina is drained - covers wandering/foraging.</summary>
        public float TrotSpeedThreshold = 0.02f;

        /// <summary>At/above this movement-speed value, stamina drains at the full rate - covers chasing/fleeing sprints.</summary>
        public float SprintSpeedThreshold = 0.045f;

        /// <summary>Stamina points drained per second while sustaining a full sprint.</summary>
        public float DrainPerSecondAtSprint = 9f;

        /// <summary>Extra drain multiplier applied while swimming (predators shouldn't be able to swim forever either).</summary>
        public float SwimDrainMultiplier = 1.5f;

        /// <summary>Stamina points regenerated per second while resting (not exerting).</summary>
        public float RegenPerSecond = 4f;

        /// <summary>Seconds of no exertion required before regen kicks back in.</summary>
        public float RegenDelaySeconds = 2.5f;

        /// <summary>Below this fraction of max stamina, movement speed starts being penalized.</summary>
        public float ExhaustionFraction = 0.4f;

        /// <summary>Movement speed multiplier applied when stamina is fully depleted (interpolated up to 1.0 at ExhaustionFraction).</summary>
        public float MinSpeedMultiplier = 0.45f;

        /// <summary>Below this fraction of max stamina, a chasing predator gives up and disengages rather than continuing to seek.</summary>
        public float GiveUpChaseFraction = 0.18f;

        /// <summary>If true, an exhausted creature will still re-engage a target that recently attacked it (self-defense), even below GiveUpChaseFraction.</summary>
        public bool AllowRetaliationWhileExhausted = true;

        /// <summary>Melee damage multiplier applied to any AI-controlled creature with a stamina pool, compensating for the new speed/chase limits.</summary>
        public float MeleeDamageMultiplier = 1.2f;

        /// <summary>Predator-tagged creatures move at PredatorHasteMultiplier speed while their stamina fraction is at/above this threshold.</summary>
        public float PredatorHasteStaminaThreshold = 0.8f;

        /// <summary>Flat speed multiplier predators get while fresh (stamina fraction >= PredatorHasteStaminaThreshold). Above vanilla speed.</summary>
        public float PredatorHasteMultiplier = 1.15f;

        /// <summary>Flat speed multiplier applied for PanicDurationSeconds when panic mode triggers (overrides the normal fatigue throttle - adrenaline over exhaustion).</summary>
        public float PanicSpeedMultiplier = 1.45f;

        /// <summary>How long a panic-mode speed burst lasts once triggered.</summary>
        public float PanicDurationSeconds = 8f;

        /// <summary>Whether players get a stamina pool at all.</summary>
        public bool EnablePlayerStamina = true;

        /// <summary>Player max stamina. Default is roughly 4x a black bear's stamina pool under the weight-scaling formula above.</summary>
        public float PlayerMaxStamina = 630f;

        /// <summary>Sleeping for at least this many in-game hours fully restores player stamina, on top of normal passive regen.</summary>
        public float SleepHoursForFullRecharge = 3f;

        public static StaminaModConfig LoadOrCreate(ICoreAPI api)
        {
            StaminaModConfig config;
            try
            {
                config = api.LoadModConfig<StaminaModConfig>("StaminaMod.json");
                if (config == null)
                {
                    config = new StaminaModConfig();
                }
            }
            catch (System.Exception e)
            {
                api.Logger.Error("[staminamod] Failed to load config, using defaults: " + e);
                config = new StaminaModConfig();
            }
            api.StoreModConfig(config, "StaminaMod.json");
            Instance = config;
            return config;
        }
    }
}
