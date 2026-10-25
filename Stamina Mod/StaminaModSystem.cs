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
        private ICoreServerAPI sapi;
        private HudElementStamina staminaHud;

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
            sapi = api;

            if (StaminaModConfig.Instance.DisablePlayerStaminaIfVigorPresent && api.ModLoader.IsModEnabled("vigor"))
            {
                api.Logger.Notification("[staminamod] Vigor detected - this mod's own player stamina is disabled (wildlife stamina is unaffected). Set DisablePlayerStaminaIfVigorPresent to false in the config to override.");
            }

            // Wildlife gets the behavior via JSON patches on their entity type files (see
            // assets/staminamod/patches). Players aren't defined that way, so we attach it here
            // instead, once their entity is fully loaded and ready to play.
            api.Event.PlayerNowPlaying += OnPlayerNowPlaying;

            api.ChatCommands.Create("staminamod")
                .RequiresPrivilege(Privilege.controlserver)
                .WithDescription("Inspect the stamina of the entity you're looking at, or your own if you aren't looking at anything with a stamina pool")
                .HandleWith(args =>
                {
                    (Entity entity, EntityBehaviorStamina stamina, TextCommandResult error) = ResolveTarget(args);
                    if (error != null)
                    {
                        return error;
                    }

                    return TextCommandResult.Success(
                        $"{entity.Code}: stamina {stamina.Stamina:0.#}/{stamina.MaxStamina:0.#} " +
                        $"({stamina.StaminaFraction * 100:0}%), speedFactor {stamina.SpeedFactor:0.00}, " +
                        $"exhausted: {stamina.IsExhausted}, panicking: {stamina.IsPanicking}");
                })
                .BeginSubCommand("set")
                .WithDescription("Set the stamina of the entity you're looking at (or yourself) to an exact value")
                .WithArgs(api.ChatCommands.Parsers.Float("value"))
                .HandleWith(args =>
                {
                    (Entity entity, EntityBehaviorStamina stamina, TextCommandResult error) = ResolveTarget(args);
                    if (error != null)
                    {
                        return error;
                    }

                    stamina.SetStamina((float)args.Parsers[0].GetValue());
                    return TextCommandResult.Success($"{entity.Code} stamina set to {stamina.Stamina:0.#}/{stamina.MaxStamina:0.#}");
                })
                .EndSubCommand()
                .BeginSubCommand("full")
                .WithDescription("Fully refill the stamina of the entity you're looking at (or yourself)")
                .HandleWith(args =>
                {
                    (Entity entity, EntityBehaviorStamina stamina, TextCommandResult error) = ResolveTarget(args);
                    if (error != null)
                    {
                        return error;
                    }

                    stamina.SetStamina(stamina.MaxStamina);
                    return TextCommandResult.Success($"{entity.Code} stamina fully refilled");
                })
                .EndSubCommand()
                .BeginSubCommand("empty")
                .WithDescription("Drain the stamina of the entity you're looking at (or yourself) to zero, for testing exhaustion/panic/give-up behavior")
                .HandleWith(args =>
                {
                    (Entity entity, EntityBehaviorStamina stamina, TextCommandResult error) = ResolveTarget(args);
                    if (error != null)
                    {
                        return error;
                    }

                    stamina.SetStamina(0f);
                    return TextCommandResult.Success($"{entity.Code} stamina drained to 0");
                })
                .EndSubCommand();
        }

        private static (Entity entity, EntityBehaviorStamina stamina, TextCommandResult error) ResolveTarget(TextCommandCallingArgs args)
        {
            Entity entity = args.Caller.Player?.CurrentEntitySelection?.Entity ?? args.Caller.Entity;
            if (entity == null)
            {
                return (null, null, TextCommandResult.Error("No entity to target."));
            }

            EntityBehaviorStamina stamina = entity.GetBehavior<EntityBehaviorStamina>();
            if (stamina == null)
            {
                return (entity, null, TextCommandResult.Error($"{entity.Code} does not have a stamina behavior."));
            }

            return (entity, stamina, null);
        }

        private void OnPlayerNowPlaying(IServerPlayer player)
        {
            StaminaModConfig cfg = StaminaModConfig.Instance;
            bool disabled = !cfg.EnablePlayerStamina
                || (cfg.DisablePlayerStaminaIfVigorPresent && sapi.ModLoader.IsModEnabled("vigor"));

            Entity entity = player.Entity;
            if (entity == null)
            {
                return;
            }

            if (disabled)
            {
                // Nothing else clears these if the behavior was previously attached, and the
                // client HUD's visibility keys off "staminaModMax" existing at all - leftover
                // data here would mean a bar that never disappears.
                ITreeAttribute attrs = entity.WatchedAttributes;
                if (attrs.HasAttribute("staminaModCurrent") || attrs.HasAttribute("staminaModMax"))
                {
                    attrs.RemoveAttribute("staminaModCurrent");
                    attrs.RemoveAttribute("staminaModMax");
                }
                return;
            }

            if (entity.GetBehavior<EntityBehaviorStamina>() != null)
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
            staminaHud = new HudElementStamina(api);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(HarmonyId);
            base.Dispose();
        }
    }
}
