using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace StaminaMod
{
    /// <summary>
    /// A single stamina statbar stacked 10px above the vanilla health bar, mirroring the exact
    /// screen-space anchoring vanilla's own HudStatbar uses for its health/oxygen/food bars (same
    /// dialog width, same CenterBottom alignment, same offset math) so it lines up perfectly
    /// without needing to touch or patch the vanilla HUD at all.
    ///
    /// Reads the "staminaModCurrent"/"staminaModMax" watched attributes directly off the local
    /// player's entity rather than going through EntityBehaviorStamina, since that behavior only
    /// ever gets attached server-side for players (see StaminaModSystem.OnPlayerNowPlaying) - the
    /// client-side Entity object never has its own instance of it. Watched attributes sync
    /// regardless. If those attributes never appear (player stamina disabled, or Vigor detected
    /// and this mod deferring to it), the bar simply never composes and stays invisible.
    ///
    /// Shares its screen slot with the vanilla oxygen bar, so the two negotiate who gets to show:
    /// see <see cref="UpdateOxygenConflict"/>.
    /// </summary>
    public class HudElementStamina : HudElement
    {
        private static readonly double[] StaminaBarColor = new double[4] { 0.85, 0.65, 0.13, 1.0 };

        private const float TrendEpsilon = 0.01f;

        private float lastStamina = -1f;
        private float lastMaxStamina = -1f;
        private GuiElementStatbar staminaBar;

        private float lastOxygenCurrent = float.NaN;
        private float lastStaminaForTrend = float.NaN;
        private bool hideForOxygen;

        public override double InputOrder => 1.0;

        public override string ToggleKeyCombinationCode => null;

        public override bool Focusable => false;

        public HudElementStamina(ICoreClientAPI capi) : base(capi)
        {
            capi.Event.RegisterGameTickListener(OnGameTick, 20);
        }

        private void OnGameTick(float dt)
        {
            IPlayer player = capi.World.Player;
            ITreeAttribute attrs = player?.Entity?.WatchedAttributes;
            if (attrs == null || !attrs.HasAttribute("staminaModMax"))
            {
                return;
            }

            if (staminaBar == null)
            {
                ComposeGuis();
            }

            float stamina = attrs.GetFloat("staminaModCurrent", 0f);
            float maxStamina = attrs.GetFloat("staminaModMax", 1f);

            UpdateOxygenConflict(player, stamina);

            if (stamina == lastStamina && maxStamina == lastMaxStamina)
            {
                return;
            }

            staminaBar.SetLineInterval(Math.Max(1f, maxStamina / 10f));
            staminaBar.SetValues(stamina, 0f, maxStamina);
            lastStamina = stamina;
            lastMaxStamina = maxStamina;
        }

        /// <summary>
        /// The stamina and oxygen bars can never usefully occupy the same spot at once, so this
        /// arbitrates between them every tick:
        ///  - oxygen full (or no oxygen tree at all, i.e. never been underwater) - no contest,
        ///    oxygen hides itself via HideWhenFull, stamina is free to show (subject to its own
        ///    HideWhenFull if it's full too).
        ///  - oxygen depleting (actively losing breath) - oxygen always wins, hide stamina.
        ///  - oxygen refilling (just surfaced, not yet full) - oxygen wins UNLESS stamina is
        ///    *also* actively draining right now, in which case stamina wins (you're mid-sprint
        ///    the moment you catch your breath - more useful to keep watching stamina).
        /// </summary>
        private void UpdateOxygenConflict(IPlayer player, float staminaCurrent)
        {
            ITreeAttribute oxygenTree = player?.Entity?.WatchedAttributes?.GetTreeAttribute("oxygen");
            float? oxygenCurrent = oxygenTree?.TryGetFloat("currentoxygen");
            float? oxygenMax = oxygenTree?.TryGetFloat("maxoxygen");

            bool oxygenFull = !oxygenCurrent.HasValue || !oxygenMax.HasValue
                || oxygenCurrent.Value >= oxygenMax.Value - TrendEpsilon;

            if (oxygenFull)
            {
                hideForOxygen = false;
            }
            else
            {
                bool oxygenDepleting = !float.IsNaN(lastOxygenCurrent) && oxygenCurrent.Value < lastOxygenCurrent - TrendEpsilon;
                bool staminaDepleting = !float.IsNaN(lastStaminaForTrend) && staminaCurrent < lastStaminaForTrend - TrendEpsilon;

                hideForOxygen = oxygenDepleting || !staminaDepleting;
            }

            lastOxygenCurrent = oxygenCurrent ?? float.NaN;
            lastStaminaForTrend = staminaCurrent;
        }

        public void ComposeGuis()
        {
            // Matches vanilla HudStatbar.ComposeGuis()'s outer bounds exactly (same width,
            // alignment and offset) so this dialog's internal coordinate space lines up with the
            // vanilla health/oxygen/food bars' dialog on screen.
            float width = 850f;
            ElementBounds dialogBounds = new ElementBounds
            {
                Alignment = EnumDialogArea.CenterBottom,
                BothSizing = ElementSizing.Fixed,
                fixedWidth = width,
                fixedHeight = 100.0
            }.WithFixedAlignmentOffset(0.0, 5.0);

            // Health bar sits at y-offset +5 (see vanilla HudStatbar); 10px of bar height plus a
            // 10px gap puts this bar's offset at -15, one full bar-height-plus-gap above it.
            ElementBounds staminaBarBounds = ElementStdBounds.Statbar(EnumDialogArea.LeftTop, width * 0.41)
                .WithFixedHeight(10.0)
                .WithFixedAlignmentOffset(0.0, -15.0);

            Composers["staminabar"] = capi.Gui.CreateCompo("staminamod-statbar", dialogBounds.FlatCopy().FixedGrow(0.0, 20.0))
                .BeginChildElements(dialogBounds)
                .AddStatbar(staminaBarBounds, StaminaBarColor, "staminastatbar")
                .EndChildElements()
                .Compose();

            staminaBar = Composers["staminabar"].GetStatbar("staminastatbar");
            // Same convention as vanilla's oxygen bar: don't clutter the HUD with a full bar.
            staminaBar.HideWhenFull = true;
            TryOpen();
        }

        public override bool TryClose()
        {
            return false;
        }

        public override bool ShouldReceiveKeyboardEvents()
        {
            return false;
        }

        public override void OnRenderGUI(float deltaTime)
        {
            if (capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Spectator && !hideForOxygen)
            {
                base.OnRenderGUI(deltaTime);
            }
        }

        protected override void OnFocusChanged(bool on)
        {
        }

        public override void OnMouseDown(MouseEvent args)
        {
        }
    }
}
