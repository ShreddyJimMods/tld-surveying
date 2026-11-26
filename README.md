# Skilled Cartographer

When the Faithful Cartographer update landed, I was hoping for more. Cartography was still pretty bare, and it had nothing to do with the skill system. **Skilled Cartographer** fixes that. It adds **Cartography** as a proper skill, levelled by surveying new ground, and the further you take it, the better you get at finding your way around Great Bear.

It never explores for you. Nothing is revealed that you haven't surveyed or walked yourself.

## What you get

- **Cartography in the journal**, five levels, with its own level-up banner.
- **Quicker surveys**, and a chance to keep your charcoal.
- **Trail notes**: the ground you've covered since your last survey gets sketched in too.
- **More from high ground**, and surveys that stretch into dawn and dusk.
- **A sun compass** on the HUD when you can read the sky, plus a north marker on the map.
- **A rough "you are here" circle** on the map that widens the further you wander from your last survey.
- **Familiar Ground**: finish surveying a region and it pays you back while you're there. You move faster, carry a bit more and sprain less.

Every perk can be tuned or switched off on the Skilled Cartographer page of Mod Settings.

## Requirements

- The Long Dark 2.60
- [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.2
- [ModSettings](https://github.com/zeobviouslyfakeacc/ModSettings) and ModData

## Install

Drop `SkilledCartographer.dll` from the [latest release](https://github.com/ShreddyJimMods/tld-skilled-cartographer/releases) into `TheLongDark/Mods/`.

Coming from the old **Surveying** test build? Delete `Surveying.dll` first. Your progress and settings carry over.

## Beta

- Back up your save before trying it.
- Leave the three **TESTING** switches in Mod Settings off. They're for trying out higher levels and go away in 1.0.
- Found a bug? Set `DetailedLog = true` under `[Surveying]` in `UserData/MelonPreferences.cfg`, play until it happens, and open an issue with your `MelonLoader/Latest.log`.

## Building

.NET SDK 6+, with the game, MelonLoader, ModSettings and ModData installed:

```
dotnet build -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\TheLongDark"
```

Or put the path in a `GameDir.props` next to the project (git-ignored). Output: `bin/Release/net6.0/SkilledCartographer.dll`.

## License

MIT
