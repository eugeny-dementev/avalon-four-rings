# Verification report

Tested on 7 November 2026 with the supported Mono 1.30.012 assembly and BepInEx 5.4.23.6.
Runtime tests used disposable profiles. The original player profile was restored after testing.

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
- Other resolutions, Better UI settings, Vortex installation, other builds, and IL2CPP remain unverified or unsupported.
- Existing MiniMap startup exceptions occurred with and without Four Rings. Completed checks produced no new Four Rings exceptions.

The release plugin excludes the runtime harness. Source for the harness is provided for reproducibility, not normal play.
See [harness instructions](../tests/RuntimeHarness/README.md) before using it.
