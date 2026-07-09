# Animal Stamina

A [Vintage Story](https://www.vintagestory.at/) mod that gives wild animals a stamina pool.

## Why

Vanilla predators (wolves, bears, hyenas...) will chase a target in a dead-straight line for as
long as their AI decides to, with no cost to them - it's not unusual to be chased for thousands of
blocks by an animal that never tires. The same goes in reverse: prey will flee forever, and
everything moves unrealistically fast while swimming. The player already manages this kind of
trade-off through hunger drain while sprinting; animals had no equivalent limiter.

This mod adds one.

## What it does

- Every patched wild animal gets a stamina pool (`EntityBehaviorStamina`), sized loosely by the
  creature's weight (a moose gets more stamina to burn than a hare).
- Sprinting, fleeing, and swimming drain stamina. Wandering/foraging at normal pace does not.
- As stamina drops below a threshold, the animal's movement speed is throttled - it doesn't stop,
  it just can't keep sprinting indefinitely.
- Once stamina drops critically low, a chasing predator gives up and disengages instead of
  continuing to pursue (unless it was recently attacked and is defending itself, which is still
  allowed even while exhausted).
- Stamina regenerates while the animal isn't exerting itself, after a short delay.

No vanilla files are overwritten - the mod attaches a new behavior to existing wild animal entity
types via non-destructive JSON patches, and uses [Harmony](https://harmony.pardeike.net/) patches
to hook into the existing AI/pathing code at two points: the shared path-traversal speed
calculation, and the predator "seek entity" task's give-up conditions. See the doc comments in
`Patches/` for exactly which methods are hooked and why.

## Configuration

On first run the mod writes `ModConfig/AnimalStamina.json` with tunable defaults (drain/regen
rates, speed thresholds, exhaustion/give-up fractions, etc). Individual entity types can also
override any of these via an `"animalStamina"` attributes block if you want to hand-tune a
specific creature.

A `/animalstamina` server command (requires `controlserver` privilege) reports the stamina state
of whatever entity you're looking at, for debugging/tuning.

## Building

Requires the .NET SDK matching the game's target framework and a `VINTAGE_STORY` environment
variable pointing at your Vintage Story install (this is how the official Mod Maker template
resolves game DLL references).

```
dotnet build "Animal Stamina/Animal Stamina.csproj"
```

or use the Cake build script for a distributable zip:

```
./build.ps1   # or build.sh
```

## Credits

Concept and direction: Joel Trauger. Implementation (game code archaeology, design, C#, Harmony
patches, JSON patches): Claude (Anthropic), model Sonnet 5, working autonomously end-to-end
against a decompiled copy of the game and Harmony/API research.
