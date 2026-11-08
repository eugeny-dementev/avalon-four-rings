# Verification report

Tested on 7–8 November 2026 with Mono 1.30.012 and BepInEx 5.4.23.6.
Runtime tests used disposable profiles. The original player profile was restored after testing.

Version 1.0.1 allows other Mono builds but does not verify their compatibility. IL2CPP remains unsupported.
The initial runtime matrix below describes the 1.0.0 baseline. Later sections record checks for each update.

## Completed checks

| Area | Result |
| --- | --- |
| Slot definitions | Original indexes 0–18 remain unchanged; new permanent fields use unique indexes 19 and 20. |
| Acceptance and collections | Both new slots accept rings only. All, Rings, and Accessories contain 21, 4, and 6 entries. |
| Repeated patching | A second application does not duplicate fields or instructions. |
| Assembly integrity | Patched assembly writes and reads successfully. Original TG.Main.dll remains unchanged. |
| Three-ring matrix | Each of three rings occupied every slot. All combinations of three occupied slots passed. |
| Four-ring setup | A temporary fourth ring used a distinct item identity. All four equipped simultaneously. |
| Bonuses | Placements matched native Ring1 across 63 stable stat values. Unequipping restored the baseline. |
| Available effects | Equip-triggered effects, passive registrations, and event listeners matched native Ring1. |
| Save persistence | Full process exit, restart, and reload preserved assignments, identities, counts, and stats. |
| Interface | Four controls, native chooser, tooltips, selection, and repeated inventory opening passed interactive inspection. |
| Storage and dropping | Native actions cleared the additional slot and removed its effects. |
| Selling eligibility | Equipped rings were excluded from native sellable inventory. Unequipping restored eligibility. |
| Synthetic controller events | Bidirectional navigation, confirm, cancel, and unequip passed. |
| Removal | A save cleaned with all additional slots empty loaded after both DLLs were removed. |

The test rings were Lesser Health Ring, Athlete's Pride, Lesser Mana Ring, and a temporary second Lesser Health Ring.

| Equipped rings | Maximum health | Maximum mana | Maximum stamina |
| --- | ---: | ---: | ---: |
| None | 53 | 36 | 43 |
| Three original rings | 58 | 46 | 48 |
| Four, including the temporary health ring | 63 | 46 | 48 |

## Configuration and limits

- Interface checks used 3840 × 2160 and Better UI 1.0.4, with extra loadout pages disabled.
- Avalon Mod Manager, MiniMap, Better Upscaling, Contact Shadows, Better Volumetric Fog, and Loot Stats remained enabled.
- Physical gamepad input remains unverified. Tests used a temporary harness to invoke native UI events.
- Actual merchant transactions remain unverified; native selling eligibility was checked directly.
- Combat-triggered ring effects remain unverified; available rings had equip-triggered stat effects.
- Requirement rejection for an unavailable-requirement ring remains unverified.
- Automatic empty-slot selection and replacement behavior were checked by static inspection.
- Other resolutions, Better UI settings, Vortex installation, and other Mono builds remain unverified. IL2CPP is unsupported.
- Existing MiniMap startup exceptions occurred with and without Four Rings. Completed checks produced no new Four Rings exceptions.

The release plugin excludes the runtime harness. Source for the harness is provided for reproducibility, not normal play.
See [harness instructions](../tests/RuntimeHarness/README.md) before using it.

## Release package check

The public source built from a clean checkout with zero warnings and errors.
Static checks passed, including the assertion that the normal plugin contains no harness type.
The separate, explicitly enabled harness build also compiled successfully.

The install ZIP contains exactly two mod DLLs, installation instructions, the MIT license, and the changelog.
Archive entries matched their build inputs by SHA-256.
The packaged DLLs loaded the archived disposable checkpoint with four occupied ring controls.
Opening an additional slot displayed the native ring chooser.
Returning displayed its native tooltip, including Athlete's Pride and its maximum stamina bonus.
Reopening the inventory retained exactly four ring controls.
No Four Rings exception appeared during this package check.

Three actual gameplay images document the package check.
The cover is a direct crop of the complete inventory capture.
Better UI was installed separately; its settings remained unchanged.
These images show the archived capture setup, separately from the controlled stat matrix above.

After capture, the game was closed and the current player profile was restored.
All 23 profile files matched the fresh backup by file name, length, and SHA-256.
No capture progress or temporary test items were merged into the restored profile.

## Version 1.0.1 checks

- The plugin compiled with zero warnings and errors. All static patch checks passed.
- A temporary copy with a different assembly identifier produced a different SHA-256 fingerprint.
- The real startup patch entry point accepted that fixture and registered both additional slots.
- Changed ring counts, changed rule counts, a missing Ring2 field, and partial registration were rejected.
- Each rejection left the input assembly unchanged.
- The normal plugin excluded the runtime harness. Both assembly metadata and plugin metadata identified version 1.0.1.
- The packaged DLLs loaded the archived four-ring checkpoint on Mono 1.30.012.
- All four occupied controls appeared. An additional slot opened the native chooser and displayed the Athlete's Pride tooltip.
- No Four Rings exception appeared. The original game DLL retained its tested hash.
- The game was closed, and all 23 original profile files were restored with matching hashes and sizes.

The changed-fingerprint fixture verifies removal of the version block; it is not another released game version.
Other Mono builds remain unverified. The complete runtime matrix was not repeated because equipment behavior did not change.

## Version 1.0.2 checks

Tests used a fresh disposable copy of the latest checkpoint, containing eight existing rings and available horse armour.
No additional item was created for these checks.

| Area | Result |
| --- | --- |
| Layout | Four ring controls, boots, and horse armour had separate on-screen bounds at 3840 × 2160. |
| Horse placement | Horse armour appeared left of the lower-left ring, aligned with the boots row. |
| Reopening | Repeated inventory opening retained one control per slot and exactly three alignment components. |
| Native interactions | All four rings and horse armour passed chooser, confirm, cancel, equip, and unequip checks. |
| Mouse inspection | Native ring tooltips displayed item statistics. Horse armour displayed its tooltip and equipped or unequipped from its relocated control. |
| Navigation | Bidirectional links reached every control. Native navigation skipped horse armour when its visibility condition was disabled temporarily. |
| Ring matrix | Three rings passed every slot and all three-ring combinations. Native bonuses returned to the baseline. |
| Effects | All eight rings matched native Ring1 registrations and event listeners across all four slots. Unequipping restored the baseline. |
| Selling eligibility | Native sellable inventory excluded equipped rings and included them after unequipping. |

Testing exposed a missing favourite-panel reference in the native horse control.
Version 1.0.2 supplies an inactive panel when that reference is absent, allowing native unequip to complete without an exception.
The final interaction checks passed after this correction.

Physical gamepad input, locked horse armour, other resolutions, other interface configurations, and other Mono builds remain unverified.
Combat-triggered effects and merchant transactions remain unverified.
Storage, dropping, and cleaned-save removal retain their 1.0.0 verification; this update does not change those operations.

### Packaged release and screenshots

The 1.0.2 source built from a clean checkout with zero warnings and errors.
All static checks passed, including the check that the release plugin excludes the harness.
Archive validation confirmed the five allowed entries and their SHA-256 hashes.

The disposable session saved four equipped rings and horse armour before a complete process exit.
The packaged DLLs then loaded that checkpoint without the test harness.
All five assignments remained visible. Maximum health, mana, and stamina remained 66, 52, and 51.
Repeated inventory opening retained four separate ring controls and the relocated horse control.
Native ring and horse tooltips displayed correctly. No exception appeared in the packaged session log.
The original game assembly retained its tested SHA-256 fingerprint.

Four new gameplay PNGs document this release: equipment crop, inventory overview, ring tooltip, and horse armour tooltip.
The overview and tooltip images were resized from the captured frames. The cover is a direct crop.
No image contains generated artwork or simulated UI.

The game was closed before restoring the original profile.
All 19 original profile files matched the backup by name, length, and SHA-256.
No test progress was merged. The installed release DLLs match the packaged files and exclude the harness.
