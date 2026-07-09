using Vintagestory.API.Common;

namespace AnimalStamina.Config
{
    /// <summary>
    /// Global tuning knobs for the stamina system. Individual entity types can still
    /// override a subset of these via the "animalStamina" attributes block added by
    /// this mod's entity patches (see EntityBehaviorStamina.Initialize).
    /// </summary>
    public class AnimalStaminaConfig
    {
        public static AnimalStaminaConfig Instance = new AnimalStaminaConfig();

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

        public static AnimalStaminaConfig LoadOrCreate(ICoreAPI api)
        {
            AnimalStaminaConfig config;
            try
            {
                config = api.LoadModConfig<AnimalStaminaConfig>("AnimalStamina.json");
                if (config == null)
                {
                    config = new AnimalStaminaConfig();
                }
            }
            catch (System.Exception e)
            {
                api.Logger.Error("[animalstamina] Failed to load config, using defaults: " + e);
                config = new AnimalStaminaConfig();
            }
            api.StoreModConfig(config, "AnimalStamina.json");
            Instance = config;
            return config;
        }
    }
}
