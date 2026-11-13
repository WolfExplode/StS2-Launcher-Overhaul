# Android testing status

Updated October 26, 2026. This page separates local validation from physical-device and live-service results.

## Current release: v0.2.436

[Release notes](release-notes/v0.2.436.md), [ARM64 APK](https://github.com/SocialHummingbird/StS2-Launcher-Overhaul/releases/download/v0.2.436/StS2Launcher-v0.2.436-arm64-v8a.apk), [checksum](https://github.com/SocialHummingbird/StS2-Launcher-Overhaul/releases/download/v0.2.436/StS2Launcher-v0.2.436-arm64-v8a.apk.sha256).

This release replaces launcher-owned mod parsing/loading and activation
guesses with the selected game's original framework. It removes the restricted
BaseLib initializer, named patch exclusions, assembly publicizing and replacement
model construction. Android Mono and Harmony now have pinned source recipes.

| Check | Observed result |
| --- | --- |
| Managed / Android JVM | 220 / 56 passed. This includes committed-package recovery, writable package state, fresh result identity, cold reopening, read-only background migration and rejected stale UI completions. |
| Pre-merge lint | Managed analyzers and source whitespace/syntax checks pass. Android lint reports the same 15 errors and 39 warnings on this change and `main`; comparison finds no new diagnostics. The existing Gradle configuration does not fail on lint errors. |
| Original loader contracts | Both supported game initialization shapes pass in separate desktop processes, including original discovery, dependency ordering, multiple manifests, exact manual selection, early secondary scripts and original failure propagation. |
| Desktop representative packages | Five unmodified packages initialize; original model construction, serialization-cache initialization and repeated act/encounter access pass. |
| In-place phone update | Candidate `436008` installed over the existing package with the same signing certificate; no uninstall or application-data clearing. |
| Published APK | `0.2.436` / `436009`, built from `33abc177097fe55bd2c630756f55ecd5e7a60ef8`. Contents, crypto, signing, checksum and update compatibility pass. The final APK has not been installed on a phone; device evidence in this table concerns candidate `436008`. |
| Steam preparation on phone | Authenticated `mysubscriptions` query returns all four subscriptions. All four committed packages prepare successfully; their 17 payload files match the desktop packages. |
| Android host | Ordinary MD5, SHA-1, SHA-256 and background-stream SHA-256 pass on the Java-hosted phone process. Importer profile controls and BaseLib configuration render and respond through the unmodified packages' Godot scripts. |
| Runtime gate | The original ARM64 method-pointer preparation and protected-member access failures are reproduced. The corrected source pair passes the integrated ARM64 contract, including 92 named-assembly access checks, cached code, exception regions, assembly context, first invocation and required initialization; 64 worker collections/callbacks also pass. Candidate `436008` has no cached `UnlockState` failure or protected-field exception in the observed combat. x86_64 is built but not executed. |
| Android functional flow | Unmodified ARAM runs through actual combat, card awards and shared BaseLib hooks. QuickRestart restores the original enemy HP, energy and hand. Save and Quit followed by process termination and cold Continue preserves the modded card, rune and custom run configuration; their combat effects still execute after reload. |
| CookieCursor settings | Unmodified CookieCursor adds working settings controls, writes its sibling configuration and reloads the saved setting after process termination. Physical-mouse cursor appearance and multiplayer behaviour were not observed. |
| Isolation and status | Candidate `436008` Vanilla completion has zero sources, zero mods and native `IsRunningModded=false`. Disabled manual sources are absent from Modded discovery. Cold reopening shows matching Workshop/manual results; changing a companion config invalidates them, and a new launch establishes a matching result. |
| State preservation | All 403 game/save/settings files match the October 26 pre-test snapshot byte for byte. Newer user progress is preserved. Temporary packages, helper application and device backup directory are removed; profile 1 and connectivity are restored. All four Workshop choices remain enabled because the user's newer active run contains ARAM data. |

These results cover the shared loader/runtime path and the observed Android
functionality.
The original Whirlwind failure was traced to the shared protected-access contract;
the subsequent run offered Battle Trance, exercising the same card-award path.
The final ARM64 APK is published as the latest GitHub release. These checks
do not certify every Workshop package or replace the separate stability procedure
listed below. Evidence is retained under `artifacts/mod-framework-device/`.

## Still needs testing

- Install and test the published v0.2.436 APK (`436009`) on an ARM64 phone. The functional device evidence above comes from candidate `436008`.
- Complete the [20-launch stability procedure](runbook-android-validation.md#device-stability-acceptance) on the same APK, including warm launches, background/resume and lock/unlock. An intermittent title-screen freeze was captured on earlier builds; its resolution still needs this acceptance testing.
- Audio and graphics on more devices and drivers.
- Real Steam Cloud transfers and switching play between PC and Android.
- Additional Workshop packages, game versions and untested mod features, including physical-mouse cursor behaviour and multiplayer interactions.
- The in-app updater's full download-to-install cycle with a newer compatible APK.
- LAN multiplayer across devices.

Password-protected Steam branches are not supported. x86_64 emulator builds are for diagnostics, not the supported game target.

For help, see [Troubleshooting](android-troubleshooting.md). Current ownership is in [the reduction inventory](../REDUCTION_INVENTORY.md). Results for earlier releases are in their [GitHub releases](https://github.com/SocialHummingbird/StS2-Launcher-Overhaul/releases) and the Git history.
