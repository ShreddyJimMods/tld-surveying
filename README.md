# Skilled Cartographer

When the Faithful Cartographer update landed, I was hoping for more. Cartography was still pretty bare, and it had no interaction with the skill system. **Skilled Cartographer** finally fixes that. It adds **Cartography** as a proper skill, leveled by surveying new ground, and the further you take it, the better you get at finding your way around Great Bear.

It also serves as a slight overhaul to mapping and navigation, adding an additional compass element to the HUD and new UI elements to the map.

It never explores for you. Nothing is revealed that you haven't surveyed or walked yourself.

## What you get

- **Cartography as a new skill**, five levels, with its own level-up banner.
- **Quicker surveys as you level**, and a chance to keep your charcoal after mapping.
- **Trail notes**: the ground you've covered since your last survey gets sketched in too - in a much smaller radius. This represents not having amnesia about where you walked 30 seconds ago.
- **More advantage from high ground**, and surveys that stretch into dawn and dusk as your skills get sharper.
- **Navigate by the sun and stars!** Upon reaching Level 3, a compass will appear if the weather is clear enough. Further skill levels allow you to navigate in harsher weather.
- **A rough "you are here" circle** on the map that widens the further you wander from your last survey. The circle gets smaller at higher skill levels, but never exact.
- **Familiar Ground**: A lasting reward for fully surveying a region. You move faster, carry a bit more and are a little less likely to lose your footing in regions you know well.

Every perk can be tuned or switched off on the Skilled Cartographer page of Mod Settings, and most numbers can be individually configured.

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
