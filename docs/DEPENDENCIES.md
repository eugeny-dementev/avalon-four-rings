# Dependencies and attribution

The release distributes only the original Four Rings assemblies and documentation.
External dependencies retain their own licenses. Users supply the game and mod loader.

| Dependency | Purpose | Distribution |
| --- | --- | --- |
| [Tainted Grail: The Fall of Avalon](https://store.steampowered.com/app/1466060/Tainted_Grail_The_Fall_of_Avalon/) | Native equipment, UI, save system, Unity assemblies | Referenced from the local installation only |
| [BepInEx](https://github.com/BepInEx/BepInEx) | Mono plugin loading and preloader patching | Install separately |
| [Harmony](https://github.com/pardeike/Harmony) | Runtime UI initialization hook | Referenced from BepInEx/core |
| [Mono.Cecil](https://github.com/jbevain/cecil) | In-memory slot definitions and static checks | Referenced from BepInEx/core |
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Local compilation and static checks | Development only |
| [Better UI](https://www.nexusmods.com/taintedgrailthefallofavalon/mods/111) | Optional UI enhancements, shown in screenshots | Install separately |

Four Rings clones existing controls at runtime. It does not include extracted game artwork, assets, or assemblies.
The MIT license covers this project's original code. It does not relicense the game, screenshots' depicted assets, or external dependencies.
