# RimWorld Bushcraft

RimWorld Bushcraft adds land-based foraging through a dedicated work type and zone.

Colonists assigned to `Bushcraft` can work designated bushcraft zones to gather modest survival resources such as berries, raw fungus, insect meat, eggs, herbal medicine, and rare insect jelly. Output is affected by pawn skills, zone depletion, terrain, biome, season, temperature, weather, and time of day.

## Features

- Dedicated `Bushcraft` work type.
- Land-only `Bushcraft zone` designator in the Zone menu.
- Per-zone forage density that depletes on successful finds and recovers over time.
- Slower recovery when an area has been heavily depleted.
- Skill-weighted results:
  - `Plants` improves plant, fungus, and medicine finds.
  - `Animals` improves insect, egg, and animal-adjacent finds.
- Seasonal and temperature effects, with weaker cold-season yields.
- Weather and time-of-day effects, including better insect activity in some wet or night conditions.
- Minor bad outcomes on failed foraging, including scratches, bites, stings, food poisoning, mood penalties, and rare small manhunter incidents.
- Generic discovery of modded wild plant harvest products.
- Mod settings for broad tuning and per-item enable/odds/count controls.

## Modded Item Discovery

Bushcraft scans loaded plant defs for wild plants that have `plant.harvestedThingDef`. If the harvested item is suitable, it can be added automatically as a forage result.

The scanner is intentionally generic. It does not rely on specific mod package IDs or hardcoded def names. This keeps compatibility low effort for mods that define wild harvestable plants normally.

By default, the scanner skips manufactured outputs such as components or chemfuel. Modded special harvests can still appear when they are not manufactured, but they are weighted conservatively and can be disabled or tuned in the settings menu.

## Settings

The mod settings menu includes:

- Global discovered item odds.
- Global discovered item count.
- Bad failure odds.
- Manhunter odds.
- Searchable item list.
- Separate `Modded items` and `Vanilla items` sections.
- Per-item enable toggle.
- Per-item odds multiplier.
- Per-item count multiplier.

## Dev Tools

When RimWorld dev mode is enabled, selecting a Bushcraft zone shows a `Bushcraft debug` gizmo.

The debug window reports:

- Zone cell counts.
- Forageable cell counts.
- Current forage density.
- Recovery rate factor.
- Current biome, season, hour, temperature, and weather.
- Vanilla and discovered loot candidate counts.
- Discovered candidate preview.
- Terrain mix inside the zone.

## Balance Target

Bushcraft is intended to be supplemental survival work rather than a direct replacement for farming, hunting, or ranching.

In high-yield conditions, one skilled pawn working full time should be able to feed roughly three colonists. In average or poor conditions, output should be lower and less reliable.

## Development

Build the C# assembly with:

```powershell
dotnet build Source\RimWorldBushcraft\RimWorldBushcraft.csproj -c Debug
```

The compiled assembly is written to `Assemblies/RimWorldBushcraft.dll`.

The local implementation notes are kept in `.metadata/` and are intentionally ignored by Git.
