# Surveying

A mod for **The Long Dark** that adds a **Surveying** skill. Mapping the world with charcoal earns skill points, and each level makes surveying easier and helps you find your way. It rewards mapping and exploring without making them unnecessary: the mod never reveals places you have not surveyed or walked.

> **Beta (0.9).** Most features have been played and work, but some are still being tuned and a few are untested. Back up your save before trying it, and please report problems with your MelonLoader log.

## What it adds

**The skill**
- A Surveying entry in the journal's Skills page, with five levels like the game's own skills and a level-up banner.
- Points come from surveying new ground. Overlapping earlier surveys earns proportionally less.
- On an existing save you get a one-time credit for locations you had already surveyed, the first time you open the journal's survey list.

**Survey benefits**
- Faster surveys at level 2 and again at level 4.
- From level 2, a chance a survey does not use up the charcoal.
- From level 3, high ground shows more of the map, and you can survey for a while longer around dawn and dusk at a reduced radius.

**Trail notes**
- Ground you walk is noted, and the next survey also maps small circles along your trail. The trail gets longer as you level up.
- From level 4, notes are also taken in bad weather and at night, at a smaller radius.

**Finding your way**
- From level 3, a compass strip on the HUD when the sky can be read. North is taken from the sun, so it is not always map-up.
- A small north pointer in the corner of the region map.
- From level 4, a rough "you are somewhere in here" circle on the map. It is never centred on you, and it grows the farther you walk from your last survey.

**Familiar Ground** (regions where every named location is surveyed)
- Inside that region only: faster movement outdoors, extra carry capacity, lower sprain risk and a trail line on the map.
- Every fully surveyed region also adds a little survey radius everywhere.

Everything can be switched off or tuned on the **Surveying** page of Mod Settings.

## Requirements

- The Long Dark 2.60 (Steam, PC)
- [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.2
- [ModSettings](https://github.com/zeobviouslyfakeacc/ModSettings)
- ModData (stores your skill points in the save)

## Install

1. Install MelonLoader into the game folder if you have not already, and start the game once.
2. Install ModSettings and ModData.
3. Download `Surveying.dll` from the [Releases](https://github.com/ShreddyJimMods/tld-surveying/releases) page.
4. Put it in `TheLongDark/Mods/`.

## Known issues and notes

- The Mod Settings page has three **TESTING** switches (level override, show the level-up banner, treat every region as fully surveyed). Leave them off for normal play. They will be removed in 1.0.
- Far Territory regions are judged by a heuristic for Familiar Ground, which has not been fully verified.
- Works alongside speed mods such as SonicMode: the Familiar Ground speed bonus multiplies on top.
- Detailed logging is off by default. Set `DetailedLog = true` under `[Surveying]` in `UserData/MelonPreferences.cfg` when reporting a problem.

## Building from source

Needs the .NET SDK (6.0 or newer) and a copy of the game with MelonLoader, ModSettings and ModData installed and run once.

```
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\TheLongDark"
```

Instead of `-p:GameDir=...` you can create a `GameDir.props` file next to the project (it is git-ignored):

```xml
<Project>
  <PropertyGroup>
    <GameDir>/path/to/TheLongDark</GameDir>
  </PropertyGroup>
</Project>
```

The DLL is written to `bin/Release/net6.0/Surveying.dll`.

## License

MIT. See [LICENSE](LICENSE).
