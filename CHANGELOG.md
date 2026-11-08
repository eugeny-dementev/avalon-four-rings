# Changelog

## 1.0.2 — 2026-11-08

- Fix the overlap between the fourth ring and the native horse armour control.
- Move horse armour left of the lower-left ring while preserving native visibility, lock state, and equipment actions.
- Update navigation links for the relocated horse control and the fourth ring.
- Supply the missing native horse favourite-panel reference so native unequip completes without an exception.
- Keep all slot indexes, permanent save identifiers, structural checks, and equipment effects unchanged.
- Add disposable-profile layout and horse armour interaction checks.

## 1.0.1 — 2026-11-07

- Allow other Mono builds to attempt initialization. Replace the exact fingerprint block with an untested-build warning.
- Keep structural checks, slot indexes, permanent save identifiers, and equipment behavior unchanged.
- Allow local builds and static checks to continue with other Mono assembly fingerprints.
- Add regression checks for changed fingerprints and incompatible equipment structures.
- Use a distinct archive timestamp so BepInEx refreshes cached plugin metadata when updating from 1.0.0.
- Clarify that Mono 1.30.012 is the tested build. Other Mono builds are allowed but unverified; IL2CPP remains unsupported.

## 1.0.0 — 2026-11-07

- Add two ring slots to the existing equipment diagram, for four active rings.
- Preserve native requirements, effects, equip actions, and save serialization.
- Add navigation links for the additional controls.
- Require the verified 1.30.012 Mono assembly fingerprint.
- Provide source, local build scripts, static checks, and disposable-profile test tools.

Unequip both additional slots and save before removing the mod.
