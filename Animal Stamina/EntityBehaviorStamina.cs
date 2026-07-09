using System;
using AnimalStamina.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace AnimalStamina
{
    /// <summary>
    /// Tracks a per-entity stamina pool that drains while sprinting/fleeing/swimming and
    /// regenerates while the creature isn't exerting itself. Other systems (the path traverser
    /// speed patch, the seek-entity give-up patch) read <see cref="SpeedFactor"/> and
    /// <see cref="IsExhausted"/> to make animals slow down and break off chases once gassed.
    /// </summary>
    public class EntityBehaviorStamina : EntityBehavior
    {
        public const string BehaviorCode = "animalstamina";

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

        private float secondsSinceExertion;

        public float MaxStamina => maxStamina;

        public float Stamina
        {
            get => entity.WatchedAttributes.GetFloat("animalStamina", maxStamina);
            private set => entity.WatchedAttributes.SetFloat("animalStamina", GameMath.Clamp(value, 0f, maxStamina));
        }

        public float StaminaFraction => maxStamina > 0f ? Stamina / maxStamina : 1f;

        /// <summary>Multiplier applied to AI path-following speed. 1.0 = unaffected, floor is MinSpeedMultiplier.</summary>
        public float SpeedFactor { get; private set; } = 1f;

        /// <summary>True once stamina has dropped below the give-up-the-chase threshold.</summary>
        public bool IsExhausted { get; private set; }

        public bool AllowRetaliationWhileExhausted => allowRetaliationWhileExhausted;

        public EntityBehaviorStamina(Entity entity) : base(entity)
        {
        }

        public override string PropertyName() => BehaviorCode;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            AnimalStaminaConfig cfg = AnimalStaminaConfig.Instance;

            float weight = properties.Weight > 0 ? properties.Weight : cfg.ReferenceWeightKg;
            float weightMul = GameMath.Clamp(
                MathF.Pow(weight / cfg.ReferenceWeightKg, cfg.WeightScalingExponent),
                cfg.MinWeightMultiplier, cfg.MaxWeightMultiplier);

            maxStamina = attributes["maxStamina"].AsFloat(cfg.BaseMaxStamina * weightMul);
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

            secondsSinceExertion = regenDelaySeconds + 1f;

            if (!entity.WatchedAttributes.HasAttribute("animalStamina"))
            {
                entity.WatchedAttributes.SetFloat("animalStamina", maxStamina);
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

            // deltaTime can spike (lag, chunk loads); clamp so a single tick can't nuke or fully refill the pool.
            float dt = GameMath.Clamp(deltaTime, 0f, 0.5f);

            double motionX = entity.Pos.Motion.X;
            double motionZ = entity.Pos.Motion.Z;
            float horizontalSpeed = (float)Math.Sqrt(motionX * motionX + motionZ * motionZ);

            float exertion = GameMath.Clamp(
                (horizontalSpeed - trotSpeedThreshold) / Math.Max(0.0001f, sprintSpeedThreshold - trotSpeedThreshold),
                0f, 1f);

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

            RecomputeSpeedFactor();
        }

        private void RecomputeSpeedFactor()
        {
            float fraction = StaminaFraction;

            SpeedFactor = fraction >= exhaustionFraction
                ? 1f
                : GameMath.Lerp(minSpeedMultiplier, 1f, GameMath.Clamp(fraction / Math.Max(0.0001f, exhaustionFraction), 0f, 1f));

            IsExhausted = fraction <= giveUpChaseFraction;
        }
    }
}
