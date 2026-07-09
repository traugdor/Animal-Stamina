using System;
using System.Linq;
using StaminaMod.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace StaminaMod
{
    /// <summary>
    /// Tracks a per-entity stamina pool that drains while sprinting/fleeing/swimming (or, for
    /// players, sprinting) and regenerates while not exerting. Other systems (the path traverser
    /// speed patch, the seek-entity give-up patch, the melee damage patch) read
    /// <see cref="SpeedFactor"/> and <see cref="IsExhausted"/> to make animals slow down, break
    /// off chases once gassed, and hit harder to compensate. Also handles two species-agnostic
    /// extras: a flat "haste" speed bonus for fresh predators, and an injury-triggered one-shot
    /// panic speed burst (enabled per entity type via the "enablePanicMode" attribute).
    /// </summary>
    public class EntityBehaviorStamina : EntityBehavior
    {
        public const string BehaviorCode = "staminamod";

        private const string WalkspeedStatKey = "staminamod:tired";

        private float maxStamina;
        private float trotSpeedThreshold;
        private float sprintSpeedThreshold;
        private float drainPerSecondAtSprint;
        private float swimDrainMultiplier;
        private float regenPerSecond;
        private float regenDelaySeconds;
        private float exhaustionFraction;
        private float minSpeedMultiplier;
        private float giveUpChaseFraction;
        private bool allowRetaliationWhileExhausted;
        private float predatorHasteStaminaThreshold;
        private float predatorHasteMultiplier;
        private float panicSpeedMultiplier;
        private float panicDurationSeconds;
        private float sleepHoursForFullRecharge;
        private bool panicModeEnabled;

        private bool isPlayer;
        private bool isPredator;

        private float secondsSinceExertion;

        private bool panicActive;
        private long panicUntilMs;
        private float? lastHealthFraction;

        private bool wasSleeping;
        private double sleepStartTotalHours;

        public float MaxStamina => maxStamina;

        public float Stamina
        {
            get => entity.WatchedAttributes.GetFloat("staminaModCurrent", maxStamina);
            private set => entity.WatchedAttributes.SetFloat("staminaModCurrent", GameMath.Clamp(value, 0f, maxStamina));
        }

        public float StaminaFraction => maxStamina > 0f ? Stamina / maxStamina : 1f;

        /// <summary>Multiplier applied to AI path-following speed (animals) or the "walkspeed" stat (players). 1.0 = unaffected.</summary>
        public float SpeedFactor { get; private set; } = 1f;

        /// <summary>True once stamina has dropped below the give-up-the-chase threshold.</summary>
        public bool IsExhausted { get; private set; }

        /// <summary>True while a panic-mode speed burst is active (deer-style injury adrenaline).</summary>
        public bool IsPanicking => panicActive;

        public bool AllowRetaliationWhileExhausted => allowRetaliationWhileExhausted;

        public EntityBehaviorStamina(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => BehaviorCode;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            StaminaModConfig cfg = StaminaModConfig.Instance;

            isPlayer = entity is EntityPlayer;
            isPredator = !isPlayer && entity.Api?.EntityTagRegistry != null
                && entity.Api.EntityTagRegistry.SlowEnumerateTagNames(properties.Tags).Contains("predator");

            if (isPlayer)
            {
                maxStamina = attributes["maxStamina"].AsFloat(cfg.PlayerMaxStamina);
            }
            else
            {
                float weight = properties.Weight > 0 ? properties.Weight : cfg.ReferenceWeightKg;
                float weightMul = GameMath.Clamp(
                    MathF.Pow(weight / cfg.ReferenceWeightKg, cfg.WeightScalingExponent),
                    cfg.MinWeightMultiplier, cfg.MaxWeightMultiplier);
                maxStamina = attributes["maxStamina"].AsFloat(cfg.BaseMaxStamina * weightMul);
            }

            trotSpeedThreshold = attributes["trotSpeedThreshold"].AsFloat(cfg.TrotSpeedThreshold);
            sprintSpeedThreshold = attributes["sprintSpeedThreshold"].AsFloat(cfg.SprintSpeedThreshold);
            drainPerSecondAtSprint = attributes["drainPerSecondAtSprint"].AsFloat(cfg.DrainPerSecondAtSprint);
            swimDrainMultiplier = attributes["swimDrainMultiplier"].AsFloat(cfg.SwimDrainMultiplier);
            regenPerSecond = attributes["regenPerSecond"].AsFloat(cfg.RegenPerSecond);
            regenDelaySeconds = attributes["regenDelaySeconds"].AsFloat(cfg.RegenDelaySeconds);
            exhaustionFraction = attributes["exhaustionFraction"].AsFloat(cfg.ExhaustionFraction);
            minSpeedMultiplier = attributes["minSpeedMultiplier"].AsFloat(cfg.MinSpeedMultiplier);
            giveUpChaseFraction = attributes["giveUpChaseFraction"].AsFloat(cfg.GiveUpChaseFraction);
            allowRetaliationWhileExhausted = attributes["allowRetaliationWhileExhausted"].AsBool(cfg.AllowRetaliationWhileExhausted);
            predatorHasteStaminaThreshold = attributes["predatorHasteStaminaThreshold"].AsFloat(cfg.PredatorHasteStaminaThreshold);
            predatorHasteMultiplier = attributes["predatorHasteMultiplier"].AsFloat(cfg.PredatorHasteMultiplier);
            panicSpeedMultiplier = attributes["panicSpeedMultiplier"].AsFloat(cfg.PanicSpeedMultiplier);
            panicDurationSeconds = attributes["panicDurationSeconds"].AsFloat(cfg.PanicDurationSeconds);
            panicModeEnabled = attributes["enablePanicMode"].AsBool(false);
            sleepHoursForFullRecharge = attributes["sleepHoursForFullRecharge"].AsFloat(cfg.SleepHoursForFullRecharge);

            secondsSinceExertion = regenDelaySeconds + 1f;

            if (!entity.WatchedAttributes.HasAttribute("staminaModCurrent"))
            {
                entity.WatchedAttributes.SetFloat("staminaModCurrent", maxStamina);
            }
            if (panicModeEnabled && !entity.WatchedAttributes.HasAttribute("staminaModPanicAvailable"))
            {
                entity.WatchedAttributes.SetBool("staminaModPanicAvailable", true);
            }

            RecomputeSpeedFactor();
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if (entity.World.Side != EnumAppSide.Server || !entity.Alive)
            {
                return;
            }

            if (isPlayer && !StaminaModConfig.Instance.EnablePlayerStamina)
            {
                return;
            }

            // deltaTime can spike (lag, chunk loads); clamp so a single tick can't nuke or fully refill the pool.
            float dt = GameMath.Clamp(deltaTime, 0f, 0.5f);

            float exertion = isPlayer ? GetPlayerExertion() : GetAnimalExertion();

            if (exertion > 0f)
            {
                float drain = drainPerSecondAtSprint * exertion * (entity.Swimming ? swimDrainMultiplier : 1f) * dt;
                if (drain > 0f)
                {
                    Stamina -= drain;
                }
                secondsSinceExertion = 0f;
            }
            else
            {
                secondsSinceExertion += dt;
                if (secondsSinceExertion >= regenDelaySeconds)
                {
                    float stamina = Stamina;
                    if (stamina < maxStamina)
                    {
                        Stamina = stamina + regenPerSecond * dt;
                    }
                }
            }

            if (panicModeEnabled)
            {
                UpdatePanicMode();
            }

            if (isPlayer)
            {
                UpdateSleepRecharge();
            }

            RecomputeSpeedFactor();

            if (isPlayer)
            {
                ApplyPlayerWalkspeedStat();
            }
        }

        private float GetAnimalExertion()
        {
            double motionX = entity.Pos.Motion.X;
            double motionZ = entity.Pos.Motion.Z;
            float horizontalSpeed = (float)Math.Sqrt(motionX * motionX + motionZ * motionZ);

            return GameMath.Clamp(
                (horizontalSpeed - trotSpeedThreshold) / Math.Max(0.0001f, sprintSpeedThreshold - trotSpeedThreshold),
                0f, 1f);
        }

        private float GetPlayerExertion()
        {
            return (entity as EntityAgent)?.Controls?.Sprint == true ? 1f : 0f;
        }

        private void UpdatePanicMode()
        {
            EntityBehaviorHealth health = entity.GetBehavior<EntityBehaviorHealth>();
            if (health == null || health.MaxHealth <= 0f)
            {
                return;
            }

            float healthFraction = health.Health / health.MaxHealth;
            bool fullyHealed = healthFraction >= 0.999f;

            if (fullyHealed)
            {
                entity.WatchedAttributes.SetBool("staminaModPanicAvailable", true);
            }
            else if (lastHealthFraction.HasValue && healthFraction < lastHealthFraction.Value && !panicActive
                && entity.WatchedAttributes.GetBool("staminaModPanicAvailable", true))
            {
                panicActive = true;
                panicUntilMs = entity.World.ElapsedMilliseconds + (long)(panicDurationSeconds * 1000);
                entity.WatchedAttributes.SetBool("staminaModPanicAvailable", false);
            }

            if (panicActive && entity.World.ElapsedMilliseconds >= panicUntilMs)
            {
                panicActive = false;
            }

            lastHealthFraction = healthFraction;
        }

        private void UpdateSleepRecharge()
        {
            EntityBehaviorTiredness tiredness = entity.GetBehavior<EntityBehaviorTiredness>();
            bool sleepingNow = tiredness?.IsSleeping ?? false;

            if (sleepingNow && !wasSleeping)
            {
                sleepStartTotalHours = entity.World.Calendar.TotalHours;
            }
            else if (!sleepingNow && wasSleeping)
            {
                double hoursSlept = entity.World.Calendar.TotalHours - sleepStartTotalHours;
                if (hoursSlept >= sleepHoursForFullRecharge)
                {
                    Stamina = maxStamina;
                }
            }
            wasSleeping = sleepingNow;
        }

        private void ApplyPlayerWalkspeedStat()
        {
            float newValue = SpeedFactor - 1f;
            float? currentValue = entity.Stats.FirstOrDefault(kv => kv.Key == "walkspeed").Value
                ?.ValuesByKey.TryGetValue(WalkspeedStatKey)?.Value;
            if (!currentValue.HasValue || Math.Abs(currentValue.Value - newValue) >= 0.01f)
            {
                entity.Stats.Set("walkspeed", WalkspeedStatKey, newValue, persistent: false);
            }
        }

        private void RecomputeSpeedFactor()
        {
            float fraction = StaminaFraction;
            float baseFactor;

            if (isPredator && fraction >= predatorHasteStaminaThreshold)
            {
                baseFactor = predatorHasteMultiplier;
            }
            else if (fraction >= exhaustionFraction)
            {
                baseFactor = 1f;
            }
            else
            {
                baseFactor = GameMath.Lerp(minSpeedMultiplier, 1f, GameMath.Clamp(fraction / Math.Max(0.0001f, exhaustionFraction), 0f, 1f));
            }

            SpeedFactor = panicActive ? panicSpeedMultiplier : baseFactor;
            IsExhausted = fraction <= giveUpChaseFraction;
        }
    }
}
