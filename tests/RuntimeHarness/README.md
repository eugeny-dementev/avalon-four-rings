# Disposable-profile runtime harness

This optional test component changes equipment, creates items, saves progress, and can drop items.
Never install it for normal play. Normal builds and release archives exclude it.

## Prepare

1. Exit the game completely.
2. Back up the complete current save profile, including preferences and synchronization metadata.
3. Verify the backup's file set, lengths, and SHA-256 hashes.
4. Replace the active profile with a disposable working copy.
5. Create a separate test directory outside the repository.
6. Create `test-session.active` there containing exactly `I CONFIRM THE ACTIVE GAME PROFILE IS DISPOSABLE`.

Build the harness explicitly:

```powershell
./scripts/build.ps1 -GameDirectory 'D:\Games\Tainted Grail' -IncludeHarness -TestDirectory 'D:\Temp\FourRingsTests'
```

Install the resulting patcher and plugin manually. The plugin now contains the harness.
Set `FOUR_RINGS_TEST_DIRECTORY` to the absolute test directory in the environment that launches the game.
The harness requires this environment variable and the confirmation file before it accepts commands.
These checks record operator intent; they cannot determine whether you actually copied your profile.

Wait until the native title screen is fully ready, then load the disposable checkpoint through the game menu.
Do not issue an early load command during title initialization.

## Commands

Write one command to `command.txt` in the test directory. Read `runtime-tests.log` before continuing.
The harness consumes the command file. Run one test at a time.

| Command | Action |
| --- | --- |
| `dump` | Record rings, slots, stats, and control positions |
| `layout` | Check ring, boots, and horse control bounds, spacing, and alignment component counts |
| `hide-horse` | Disable horse armour visibility in the disposable profile; reopen inventory afterward |
| `restore-horse` | Restore the previous horse armour visibility flag; reopen inventory afterward |
| `test` | Move the first three rings through all slots and three-ring combinations |
| `clone` | Add one distinct ring instance; requires exactly three rings |
| `four` | Equip the first four available ring instances into four slots |
| `effects` | Compare equip effects, registrations, and selling eligibility |
| `navigate` | Check native navigation links while equipment UI is open |
| `controller` | Exercise native selection, confirm, cancel, and unequip for all four rings and unlocked horse armour |
| `inventory` | Exercise storage transfer and dropping; reload afterward |
| `save` | Create a native manual test save |
| `verify` | Check four occupied slots and distinct inventory ring identities after restart |
| `clean` | Unequip all rings |
| `load` | Load the newest native save after title initialization is complete |
| `quit` | Request normal game exit |

## Restore

1. Exit the game completely.
2. Archive the disposable profile outside the live save directory.
3. Restore the complete fresh backup, replacing the disposable profile rather than merging files.
4. Verify the exact original file set, lengths, and hashes.
5. Remove the confirmation file and test environment variable.
6. Rebuild without `-IncludeHarness`, then install the normal plugin.

Physical controller testing remains separate from these synthetic event checks.
