# Stamina Mod

A [Vintage Story](https://www.vintagestory.at/) mod that gives wild animals *and* players a
stamina pool.

## Why

Vanilla predators (wolves, bears, hyenas...) will chase a target in a dead-straight line for as
long as their AI decides to, with no cost to them - it's not unusual to be chased for thousands of
blocks by an animal that never tires. The same goes in reverse: prey will flee forever, and
everything moves unrealistically fast while swimming. The player already manages this kind of
trade-off through hunger drain while sprinting; animals had no equivalent limiter.

This mod adds one, then compensates for the swing in difficulty in both directions.

## What it does

**Core stamina system**

- Every patched wild animal (and, optionally, players) gets a stamina pool
  (`EntityBehaviorStamina`), sized loosely by the creature's weight - a moose gets more stamina to
  burn than a hare.
- Sprinting, fleeing, and swimming drain stamina. Wandering/foraging at normal pace does not.
- As stamina drops below a threshold, movement speed is throttled - it doesn't stop, it just can't
  keep sprinting indefinitely.
- Once stamina drops critically low, a chasing predator gives up and disengages instead of
  continuing to pursue (unless it was recently attacked and is defending itself, which is still
  allowed even while exhausted).
- Stamina regenerates automatically while not exerting, after a short delay.

**Compensation and extras**

- **Predators hit harder.** Any AI-controlled attacker with a stamina pool deals bonus melee
  damage, to offset the fact that it can no longer just run the target down forever.
- **Predator haste.** Predator-tagged animals (wolf, bear, hyena, fox, raccoon) move *faster* than
  vanilla while fresh - full speed bonus above 80% stamina, dropping back to normal at/below that
  threshold, then throttling down like everything else as they tire further.
- **Deer panic mode.** An injured deer gets a one-time adrenaline speed burst that overrides the
  normal fatigue throttle for a few seconds. It can only trigger while injured, and won't be
  available again until the deer heals back to full - no infinite panic-sprinting.
- **Player stamina.** Players get a stamina pool too (about 4x a black bear's, by default),
  drained by sprinting and throttled the same way animals are. It recharges passively at rest like
  everything else, or can be fully restored by sleeping at least 3 in-game hours. Below ~8%
  stamina, sprint is forcibly cut off - you can't will yourself into another burst on an empty
  tank.
- **Exhausted prey stop panicking too.** A critically gassed animal won't start a fresh voluntary
  flee (mirrors the predator give-up behavior). An already-in-progress escape is left to finish on
  its own - it's already slowed by the fatigue throttle - and the emergency "cornered" panic-flee
  response is untouched regardless of stamina, since that's a last-resort reflex, not a choice.
- **Stamina bar.** Players get a small statbar stacked 10px above the vanilla health bar, styled
  and positioned to match the vanilla HUD exactly (same width, same alignment math as the
  health/oxygen/food bars) rather than looking bolted-on. Only appears if player stamina is
  actually active for you.
- **Player stamina can be turned off.** `EnablePlayerStamina: false` in the config disables the
  player side of the mod entirely (no drain, no throttle, no HUD bar) while leaving wildlife
  stamina untouched.
- **Plays nice with Vigor.** If the [Vigor](https://mods.vintagestory.at/show/mod/26142) mod (a
  dedicated player stamina/exertion mod) is also installed and enabled, this mod automatically
  skips its own player stamina system to avoid two stamina pools/HUD bars fighting each other -
  wildlife stamina is unaffected. Set `DisablePlayerStaminaIfVigorPresent: false` in the config if
  you'd rather run both anyway.

No vanilla files are overwritten - the mod attaches a new behavior to existing wild animal entity
types via non-destructive JSON patches (and to players at runtime, since they aren't defined by an
entity type file the same way), and uses [Harmony](https://harmony.pardeike.net/) patches to hook
into the existing AI/pathing/combat code at four points: the shared path-traversal speed
calculation, the predator "seek entity" and prey "flee entity" tasks' give-up conditions, and the
melee attack damage calculation. See the doc comments in `Patches/` for exactly which methods are
hooked and why.

## Configuration

On first run the mod writes `ModConfig/StaminaMod.json` with tunable defaults (drain/regen rates,
speed thresholds, exhaustion/give-up fractions, predator haste, panic burst, player pool size,
sleep recharge threshold, etc). Individual entity types can also override most of these via a
`"staminamod"` behavior attributes block if you want to hand-tune a specific creature - see
`assets/staminamod/patches/wildlife-stamina.json` for how deer opt into panic mode this way.

A `/staminamod` server command (requires `controlserver` privilege) reports the stamina state of
whatever entity you're looking at, or your own if you aren't looking at anything with a stamina
pool - handy for checking your own player stamina. Subcommands `set <value>`, `full`, and `empty`
let you force a specific stamina value on the same target, for testing or admin convenience.

## Building

Requires the .NET SDK matching the game's target framework and a `VINTAGE_STORY` environment
variable pointing at your Vintage Story install (this is how the official Mod Maker template
resolves game DLL references).

```
dotnet build "Stamina Mod/Stamina Mod.csproj"
```

or use the Cake build script for a distributable zip:

```
./build.ps1   # or build.sh
```

## Credits

Concept and direction: Joel Trauger. Implementation (game code archaeology, design, C#, Harmony
patches, JSON patches): Claude (Anthropic), model Sonnet 5, working autonomously end-to-end
against a decompiled copy of the game and Harmony/API research.
