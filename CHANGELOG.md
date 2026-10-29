# Changelog

## 2026-10-29 - Make confirmation dialogs adapt to the screen

- Confirmation actions use the buttons' own pressed signals. The dialog resizes its scrollable message and uses a responsive action grid when the viewport changes.
- The full launcher preview suite passed, including ImportVanillaSaves. Physical taps and rotation with a dialog open have not been separately verified.

## 2026-10-29 - Keep version summary readable after rotation

- The compact install section now uses a wrapping, two-line version summary in either orientation instead of selecting a text policy from the startup viewport.
- The full launcher preview suite passed with the ImportVanillaSaves fixture, and dev.10 was installed in place on the Galaxy S23. The phone screen was unavailable for the requested portrait and landscape visual comparison, so that check remains pending.

## 2026-10-29 - Apply Android display changes only when needed

- The launcher now compares keyboard height, viewport and window size, safe area and focus before updating insets or keyboard layout. Unchanged frames no longer rewrite the panel shift. A resize still recalculates layout, and changing focus while the keyboard is open can reveal the new field.
- The full launcher preview suite passed, including the original ImportVanillaSaves fixture. On the Galaxy S23, dev.9 retained full-width layout when rotated between portrait and landscape. Rotation with the keyboard open and the composition-refresh workaround still need a device check.

## 2026-10-29 - Run the launcher interaction checks against Godot

- Added the matching Godot source generator to the launcher library, so Godot dispatches its C# lifecycle and layout overrides. Without it, the touch scroll container never subscribed to new controls, leaving buttons and panels blocking drags. Styled controls now have the partial declarations required by the generator; the library continues to use the host's initialization entry point.
- The scroll fixture now actually overflows phone and tablet viewports. Its synthetic wheel input includes a release so it does not retain mouse focus and block subsequent taps.
- Added `-UiOnly` to the preview test command for machines without the original ImportVanillaSaves mod fixture. The default full suite still requires that fixture.
- Built the pinned Android Mono and Harmony runtimes from source. Phone and desktop UI interaction checks passed in Godot, including tablet scale cases, rotation, button locks and tap/drag scrolling. Full mod activation checks remain blocked by the missing fixture. Physical device validation is still pending.

## 2026-10-29 - Failure reporting, Workshop marker, theme overrides and manifest reading

- Recording a Steam sign-in, connection or timeout failure no longer blocks the launcher while the sync status file is written. Before, it waited on the write on the main thread, including on any running sync that held the file. Callers now await the write, so it still completes before the next step.
- Removed the Workshop consent marker file. It was written on Workshop update and deleted on removal, but nothing read it; the confirmation shown before each Workshop update is the consent. An existing `workshop_mods_accepted.txt` is left in place and is harmless.
- Container spacing and margins are set through typed helpers (`LauncherThemeOverrides`) that only accept the container types that read them, so spacing set on the wrong kind of container no longer compiles. Converting all 44 spacing settings found no other case of this. Three copies of the `"separation"` constant are removed.
- The runtime pack manifest is read only under the names the launcher writes. Before, each field was also accepted under up to three other spellings that no launcher in this repository's history wrote. A pack from an older launcher already fails the patch-set check, and only "usable" or "not usable" affects behaviour, so existing installs are treated the same. The never-written minimum launcher version, which diagnostics always reported as missing, is removed.
- Checked by compiling against stubbed game types; not run.

## 2026-10-29 - Launch locks

- While a launch is in progress, the game version can no longer be switched, checked or refreshed, and "Redownload selected version" is disabled. Before, these stayed usable while the game was starting, and a switch or redownload could repair or delete the files it was loading.
- Save sync and profile copy can no longer start while a launch is in progress. Before, Play cancelled a running sync and then left "Sync now" pressable while the game started from the local saves.
- An automatic sync blocked by a running save operation or launch no longer counts as done, so the next sign-in can still start it.
- The launch coordinator reports when a launch starts and ends; the view and the Saves page both draw from that.
- Checked by compiling against stubbed game types. The desktop launcher interaction test now covers the version lock during a launch, but was not run.

## 2026-10-29 - Versions page state

- The version selector, update button and version list tools on Home and the Versions page are drawn from one state: the selected version, the version list, the last update check, and whether a check, list refresh or download is running. Before, each section kept its own copy, and four separate calls turned the controls on and off.
- Version controls are locked while a download, repair, update check or list refresh runs. Before, the Versions page could switch version, check for updates or refresh the list during a download. Ending one operation could also unlock controls another still needed locked: for example, a download reset re-enabled "Refresh list" during a refresh.
- Choosing a version in a selector no longer changes the page until the switch is confirmed. Declining keeps the previous version shown.
- The download's running state comes from the launcher model's own guard, so it also covers automatic repair and stays locked until the operation actually ends.
- Checked by compiling against stubbed game types. The desktop launcher interaction test now covers the locking, but was not run.

## 2026-10-29 - Saves page state

- The Saves page is drawn in one place from the sync setting, the running operation, an unresolved conflict and the sync service's saved status. Before, the controller kept its own copy of the page and a "before this operation" copy to restore. Both are removed, along with three separate checks of the sync setting in the view.
- A sync conflict now stays on the page until a sync resolves it. Before, signing in or retrying the session redrew the page from the saved status and could show "Up to date" over an unresolved conflict.
- A failed sync now shows the failure the service recorded (offline, sign-in required or failed), instead of a second mapping in the controller that could disagree with the page shown after the next refresh.
- The sync toggle only reports the change; the controller saves the setting and redraws the page.
- The Redownload action on Home is in a vertical box, so its spacing setting takes effect. A one-column grid ignored it.
- Checked by compiling against stubbed game types. The desktop launcher interaction test was updated but not run.

## 2026-10-29 - Launcher rotation layout

- Rotating the phone no longer leaves the launcher panel shifted sideways and cut off. The keyboard shift wrote the panel's position every frame, which fixed its current width into its layout; if that ran while the panel still had the previous orientation's width, the panel kept that width and centred the content within it. The panel and launcher root now stay anchored to the screen, and the keyboard shift moves only the top and bottom edges.
- Button rows (save actions, mod actions, version controls, the Steam Guard code row and Help diagnostics) choose side-by-side or stacked from their current width on every resize, instead of once from the orientation the launcher started in.
- Checked by compiling against stubbed game types. The desktop preview's rotation check now applies the keyboard shift between resizes and checks every page fits after each rotation, but was not run.

## 2026-10-29 - Launcher touch scrolling

- A finger drag now scrolls launcher pages, dialogs and the startup recovery panel from anywhere, including on buttons, toggles, panels and the diagnostics log. Before, panels and buttons stopped the drag from reaching the scroll area, so only gaps between them scrolled. Starting a drag on a button no longer presses it. Text fields keep their own text-selection drag.
- The scrollbar is styled and scaled for the screen density instead of the few-pixel default.
- Checked by compiling against stubbed game types. The desktop launcher interaction test now covers tap and drag on a mod toggle, but was not run.

## 2026-10-29 - Repository maintenance

- Documentation now describes the current release only. Dated investigations, plans, audits, duplicate architecture pages and earlier release notes are removed; Git history and the GitHub releases keep them. The pending 20-launch stability procedure moved into the validation runbook.
- Removed the legacy `scripts/build.sh`, which bypassed the pinned runtime staging, plus a duplicate icon and an unused logo. Documented how the native Steam and Sentry stub libraries reach the APK.
- Removed the disabled event-layout and UI-scale dropdown patches, the UI-scale change event (which was never raised, so each combat room and main menu stayed subscribed), a diagnostics report that was never written, and other unreferenced members.

## 2026-10-29 - Launcher UI logic cleanup

- Home now moves between explicit stages (setup guide, sign-in, Steam Guard, download, ready, retry), and each stage sets every Home section and play action, replacing six copies of the same visibility steps.
- The update button now follows the last check result, so showing the play actions again no longer turns "Update selected Steam branch" back into "Check for updates" while the version still reports an update. Finishing a check restores the button label.
- Opening diagnostics now scrolls to the drawer instead of to the top of the page. Mods, version, and support tools are visible from startup, including when automatic repair runs first.
- View callbacks are wired by name instead of 24 positional arguments. Removed the never-built compact workflow strip, the always-hidden status detail cue, unused overloads and styles, and redundant hide calls in the download and branch-switch flows.
- Checked by compiling against stubbed game types. The desktop launcher interaction test was extended but not run, because it needs the game's assemblies.

## 2026-10-26 - Documentation corrections

- Correct mod loading results, first-time save copying, Safe Start synchronization and offline-launch requirements.
- Align build instructions and runtime/update documentation with the current implementation; separate dated investigations from current guidance.
- Simplify bug-report forms and repair release links. These are documentation changes; no APK or device-test results are added.

## 2026-10-26 - v0.2.436 original mod framework on Android

- Restore the game's discovery, initialization and model construction; remove named-mod workarounds, publicizing and activation guesses.
- Build the shared patching runtime from pinned sources and preserve complete Workshop generations with rollback on failed preparation.
- Bind displayed results to the actual game, runtime, package contents and selection.
- Publish the signed ARM64 APK with update compatibility against v0.2.435 and candidate 436008. See [release notes](docs/release-notes/v0.2.436.md) for candidate gameplay evidence and final-APK verification limits.

## 2026-10-02 - v0.2.435-preload-repair-local-r2 Android preload and Mono repairs

- Repaired the existing preload lookup to accept publicized Android members while preserving all field and method shape checks and loading budgets.
- Repaired the existing Mono bootstrap to leave its native engine host GC-safe after first delegate creation; subsequent callbacks retain their existing transitions.
- Consolidated the older fix PR's test scaffolding into the existing preload test file and extended the existing probe with an actual Android Mono GC regression.
- Published at the user's request after local checks and two full Modded/two Vanilla phone passes. Full device acceptance and intermittent freeze resolution remain pending; see [release notes](https://github.com/SocialHummingbird/StS2-Launcher-Overhaul/releases/tag/v0.2.435-preload-repair-local-r2).

## 2026-10-02 - v0.2.434-cleanup-local launch repairs and code cleanup

- Repaired startup task ownership, late-completion handling, preload fallback ordering and diagnostic sampling in the existing launch flow.
- Consolidated menu readiness, diagnostic marker reads, file hashing, authentication retries, mod-result validation and Android restart decisions into their existing owners.
- Removed superseded wrappers and the silent native sound subsystem; consolidated 301 source fragments into 63 responsibility files.
- Preserved launch contracts, stored formats, saves, credentials, mod selection and the existing package/signing channel.
- See [release notes](https://github.com/SocialHummingbird/StS2-Launcher-Overhaul/releases/tag/v0.2.434-cleanup-local) for local validation and pending physical-device acceptance.

## 2026-09-06 - v0.2.431 launcher and startup improvements

- Improved launcher layout and navigation with no horizontal scrolling.
- Added GitHub diagnostic bug reports and verified in-app APK updates.
- Unified startup task ownership, scene readiness, Android restart requests, and safe timeout handling.
- Preserved the previous v0.2.430 release FMOD/PCK preparation fixes by merging their source into main.
- See [release notes](https://github.com/SocialHummingbird/StS2-Launcher-Overhaul/releases/tag/v0.2.431) for validation and device-test limits.

## 2026-08-22 - Issue #38 runtime identity and ARM64 RC4

- Made the final installed PCK and source `sts2.dll` authoritative, removing stale marker/manifest fallbacks from selected-game identity.
- Made Steam branch updates transactional and fail-closed with per-branch `updating`/`ready` state.
- Added unique staging, complete validation, atomic promotion/rollback, and final-path revalidation for runtime packs and the Android active assembly cache.
- Limited redownload/native recovery to the selected branch so saves, credentials, Workshop content, and unrelated downloaded branches are preserved.
- Removed the bulk **Remove old versions** behavior from the current launcher.
- Bound main-menu readiness and launcher-overlay dismissal to one launch-attempt ID, foreground activity, and window focus so late callbacks cannot complete a later launch.
- Published [`v0.2.429-issue38-arm64-rc4`](https://github.com/SocialHummingbird/StS2-Launcher-Overhaul/releases/tag/v0.2.429-issue38-arm64-rc4) for `arm64-v8a`. The exact APK passed 10/10 Samsung `SM-F971B` / Android 17 launches: four cold, four warm, one background/resume, and one lock/unlock, with preservation checks passing.

## 2026-08-13 - Launcher simplification candidate prerelease

- Reduced the shared launcher shell to one useful status on Home and exceptional-state banners on secondary pages.
- Simplified Home, Saves, Versions, Mods, and Help around one decision and one dominant action per page.
- Kept save states, mod runtime results, version selection, and recovery actions explicit and truthful without repeating internal state as prose.
- Restored the existing Android main-menu preparation and startup-task lifetime guard with one focused behavioural test.
- Added an opt-in, same-APK deferred-preload experiment for a later one-device comparison; normal loading remains the default and no device result is claimed.
- Built `0.2.428-launcher-simplification-unverified` from the current source for offline inspection and prerelease handoff. No Android device was available for this build.

## 2026-08-13 - Mod-loading candidate prerelease

- Converged Workshop and manual mods into one validated, persisted launch plan and one loader path.
- Made missing or corrupt selection default to Vanilla and made Modded mode require an explicit selection.
- Added truthful persisted mod results and launcher states for installed, enabled, loaded last launch, partial, failed, stale, and not tested.
- Added early compiled-script registration for the representative importer, fail-closed activation evidence, current `ModManager` compatibility, and an Android-safe BaseLib path that omits the demonstrated hanging CustomPile compatibility patch.
- Reduced the retained mod tests to one representative fixture, fresh-process Vanilla/disabled/active checks, and one launcher interaction journey.
- Built and inspected the ARM64 `0.2.425-mod-chain-fix2-unverified` candidate. A limited device run showed BaseLib and ImportVanillaSaves loading and activating, the modded save namespace being used, and main-menu startup. The retained event then entered an intermittent live-process freeze after the main menu appeared; it does not prove a process crash. The importer control and relaunch result remain unverified, so this is an unverified prerelease rather than a phone-ready release.
- Reconnected the existing Android rendered-frame menu preparation and startup-task lifetime guard, added one behavioural lifetime test, and built the unpublished `0.2.426-main-menu-handoff-diagnostic` ARM64 artifact. This is diagnostic evidence only, not an Android stability claim.

## 2026-08-09 - Save-sync restoration and reduction (unreleased)

- Replaced the former coordinator/cache/recovery/evidence stack with one Steam Cloud transport, one manifest-based synchronization service, and one atomic four-field sync-state document.
- Added automatic Pull before the first settings/profile read and automatic verified Push after committed gameplay save changes, while keeping local writes authoritative and launch fail-open.
- Restored the Saves page actions `Sync now`, `Pull from Steam`, and `Push to Steam`; automatic and manual operations use the same service and conflict policy.
- Retained Steam authentication, owned-game and Workshop download, unconditional launch, the existing package identity, and the app-private save root.
- Removed recovery bundles, backup/export/snapshot/quarantine surfaces, evidence collectors, architecture matrices, static-audit families, and release-governance scaffolding.
- Redesigned the launcher around Home, Saves, Versions, Mods, and Help, with one focused navigation/action test.
- Built and structurally inspected the ARM64 `0.2.421-auto-sync-unverified` candidate. Offline tests pass, but no Android device was available and real Steam enumerate/download/upload/commit/read-back remains unproven.
- Kept the downloader's existing idle-timeout suspension without restoring the abandoned Cloud-specific timer workaround.

Older implementation history remains available in Git commits and published GitHub releases.
