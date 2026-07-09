using AnimalStamina.Config;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace AnimalStamina
{
    public class AnimalStaminaModSystem : ModSystem
    {
        private const string HarmonyId = "animalstamina";

        private Harmony harmony;

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            AnimalStaminaConfig.LoadOrCreate(api);
        }

        // Called on server and client - the behavior class needs to exist on both sides so
        // entities carrying it deserialize correctly, but the actual stamina simulation only
        // runs server-side (see EntityBehaviorStamina.OnGameTick).
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterEntityBehaviorClass(EntityBehaviorStamina.BehaviorCode, typeof(EntityBehaviorStamina));

            if (!Harmony.HasAnyPatches(HarmonyId))
            {
                harmony = new Harmony(HarmonyId);
                harmony.PatchAll(typeof(AnimalStaminaModSystem).Assembly);
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            api.ChatCommands.Create("animalstamina")
                .RequiresPrivilege(Privilege.controlserver)
                .WithDescription("Inspect the stamina of the entity you're looking at")
                .HandleWith(args =>
                {
                    Entity entity = args.Caller.Player?.CurrentEntitySelection?.Entity;
                    if (entity == null)
                    {
                        return TextCommandResult.Error("Look at an animal first.");
                    }

                    EntityBehaviorStamina stamina = entity.GetBehavior<EntityBehaviorStamina>();
                    if (stamina == null)
                    {
                        return TextCommandResult.Error($"{entity.Code} does not have a stamina behavior.");
                    }

                    return TextCommandResult.Success(
                        $"{entity.Code}: stamina {stamina.Stamina:0.#}/{stamina.MaxStamina:0.#} " +
                        $"({stamina.StaminaFraction * 100:0}%), speedFactor {stamina.SpeedFactor:0.00}, " +
                        $"exhausted: {stamina.IsExhausted}");
                });
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            base.Dispose();
        }
    }
}
