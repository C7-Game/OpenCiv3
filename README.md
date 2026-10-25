# OpenCiv3

**OpenCiv3** (formerly known by the codename "C7") is an open-source, cross-platform, mod-oriented, modernized reimagining of _Civilization III_ by the fan community. Built with the Godot Engine and C#, it has capabilities inspired by the best of the 4X genre and lessons learned from modding Civ3. Our vision is to make Civ3 as it could have been, rebuilt for today's modders and players: removing arbitary limits, fixing broken features, expanding mod capabilities, and supporting modern graphics and platforms. A game that can go beyond C3C but retain all of its gameplay and content.

***OpenCiv3 is under active development and currently in a pre-alpha state.*** It is a rudimentary playable game but lacking many mechanics and late-game content, and errors are likely. Keep up with our development for the latest updates and opportunities to contribute!

- [OpenCiv3.org](https://www.openciv3.org)
- [CivFanatics subforum](https://forums.civfanatics.com/forums/civ3-future-development.604/)
- [Discord](https://discord.gg/uwxUuWhM89)

> [!NOTE]
> OpenCiv3 Copyright (c) OpenCiv3 contributors
>
> OpenCiv3 is not affiliated with or endorsed by civfanatics.com, Firaxis Games,  BreakAway Games,
> Hasbro Interactive, Infogrames Interactive, Atari Interactive, or Take-Two Interactive Software.
> CIV and CIVILIZATION are registered trademarks of Take-Two Interactive Software, Inc.
> All trademarks are property of their respective owners.


## Status

The latest stable version with installation instructions and a full list of new features for each version is always available on the [Releases page](https://github.com/C7-Game/OpenCiv3/releases). Additional information on the project can be found on the [project homepage.](https://www.openciv3.org/)

### System Requirements

- 64-bit Windows, Linux, or Mac OS. Other platforms may be supported in future releases.
- Minimum hardware requirements have not yet been identified. Refer to [Godot's system requirements](https://docs.godotengine.org/en/stable/about/system_requirements.html#exported-godot-project) as a baseline. Please let us know if OpenCiv3 does not perform well on your system, or how it does on a particularly old system.
- Currently the OpenCiv3 installation is about 200-300 Mb but this is expected to increase significantly as we add media.
- Recommended: A locally installed copy (the game itself does NOT have to run) of _Civilization III Conquests_ or _Complete_ edition. Standalone mode is available with placeholder graphics for those who do not have a copy.
  - _Civilization III Complete_ is available for a pittance from [Steam](https://store.steampowered.com/app/3910/Sid_Meiers_Civilization_III_Complete/) or [GOG](https://www.gog.com/en/game/sid_meiers_civilization_iii_complete)

## Contributing

Are you interested in becoming a contributor? OpenCiv3 is a public open source project licensed under [The MIT License](LICENSE),
meaning anyone is allowed to contribute or make their own modified versions. See [Contributing](https://github.com/C7-Game/OpenCiv3/wiki/Contributing) on our wiki for an explanation of our policies and guidance on
getting started.

OpenCiv3 is built on the open source Godot Engine available from www.godotengine.org and written in C# and Lua.
Its source is maintained with git, and the official repository is currently hosted at https://github.com/C7-Game/OpenCiv3.git.
Additional information about the project can be found at www.openciv3.org.

## What are those subfolders?

- Blast - An Apache-2.0 library for decompressing PKWare DCL, the compression used for Civ3 BIQ and SAV files. Copied from [jamestefler/Blast/Blast](https://github.com/jamestelfer/Blast/tree/3f8c7919c0444c75121f7371c812ec5c2bb9905b/Blast), used by QueryCiv3
- C7 - The core game, which runs on the Godot engine.
- C7Engine - The mechanics of the game, including AI logic.
- C7GameData - Stores native game data, which will be saved to disc when the save feature is merged.
- ConvertCiv3Media - A library dedicated to reading images and animations from Civ3, used by C7 at the time being.
- EngineTests - Tests for logic in the engine.
- QueryCiv3 - A data reader for Civ3 BIQ and SAV files that fetches data based on offsets from labeled section headers, used by C7
