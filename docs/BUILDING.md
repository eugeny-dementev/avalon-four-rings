# Build and package

Use Windows, PowerShell 7, and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
The tested SDK version is 10.0.403. The two mod assemblies target .NET Standard 2.1.
Static checks run on .NET 10.

Clone the repository. Supply your Mono game installation, including BepInEx 5.
No game libraries are downloaded or redistributed by this project.
The first build may download Microsoft's .NET Standard reference pack from NuGet.

```powershell
./scripts/build.ps1 -GameDirectory 'D:\Games\Tainted Grail'
```

Use `-DotnetPath 'D:\Tools\dotnet\dotnet.exe'` when the SDK is not on PATH.
The build warns about untested fingerprints, compiles the plugin, and runs the static patch checks.
Those checks also compile the patcher and confirm the plugin excludes the runtime harness.

Create the normal release archive and checksums:

```powershell
./scripts/package.ps1 -GameDirectory 'D:\Games\Tainted Grail'
```

Output goes to ignored `artifacts/`. Use `-OutputDirectory` for a different destination.
The script refuses to overwrite release output and validates every archive entry against its source file.
It always builds without the runtime harness.

## Release procedure

1. Run the build and package commands from a clean checkout.
2. Install the archive into a Mono installation with a disposable save profile.
3. Check startup, four-slot placement, equip actions, and save loading.
4. Run `./scripts/check-repository.ps1` after staging the intended public files.
5. Commit the source and documentation. Tag the tested commit as `v1.0.2`.
6. Publish the ZIP and `SHA256SUMS.txt` on the corresponding GitHub Release.
7. Upload the identical ZIP to Nexus Mods.

GitHub automatically provides source archives for the tag.
Public CI checks repository hygiene and local documentation links.
It does not compile the mod or run game-dependent tests because those require proprietary local libraries.

See [runtime harness instructions](../tests/RuntimeHarness/README.md) for optional disposable-profile tests.
