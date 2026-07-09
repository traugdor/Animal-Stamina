using HarmonyLib;
using Newtonsoft.Json.Linq;
using StaminaMod.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace StaminaMod
{
    public class StaminaModSystem : ModSystem
    {
        private const string HarmonyId = "staminamod";

        private Harmony harmony;

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            StaminaModConfig.LoadOrCreate(api);
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
                harmony.PatchAll(typeof(StaminaModSystem).Assembly);
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            // Wildlife gets the behavior via JSON patches on their entity type files (see
            // assets/staminamod/patches). Players aren't defined that way, so we attach it here
            // instead, once their entity is fully loaded and ready to play.
            api.Event.PlayerNowPlaying += OnPlayerNowPlaying;

            api.ChatCommands.Create("staminamod")
                .RequiresPrivilege(Privilege.controlserver)
                .WithDescription("Inspect the stamina of the entity you're looking at, or your own if you aren't looking at anything with a stamina pool")
                .HandleWith(args =>
                {
                    Entity entity = args.Caller.Player?.CurrentEntitySelection?.Entity
                        ?? args.Caller.Entity;
                    if (entity == null)
                    {
                        return TextCommandResult.Error("No entity to inspect.");
                    }

                    EntityBehaviorStamina stamina = entity.GetBehavior<EntityBehaviorStamina>();
                    if (stamina == null)
                    {
                        return TextCommandResult.Error($"{entity.Code} does not have a stamina behavior.");
                    }

                    return TextCommandResult.Success(
                        $"{entity.Code}: stamina {stamina.Stamina:0.#}/{stamina.MaxStamina:0.#} " +
                        $"({stamina.StaminaFraction * 100:0}%), speedFactor {stamina.SpeedFactor:0.00}, " +
                        $"exhausted: {stamina.IsExhausted}, panicking: {stamina.IsPanicking}");
                });
        }

        private void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (!StaminaModConfig.Instance.EnablePlayerStamina)
            {
                return;
            }

            Entity entity = player.Entity;
            if (entity == null || entity.GetBehavior<EntityBehaviorStamina>() != null)
            {
                return;
            }

            EntityBehaviorStamina behavior = new EntityBehaviorStamina(entity);
            entity.AddBehavior(behavior);
            behavior.Initialize(entity.Properties, new JsonObject(new JObject()));
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
