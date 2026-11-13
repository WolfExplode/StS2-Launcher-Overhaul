# Android validation runbook

How to validate a specific build. Record the results for the current release in [Android testing status](current-android-status.md).

## Local checks

1. Supply the [development prerequisites and staged runtimes](development.md#prerequisites), then record the source commit and working-tree state.
2. Run the [required local checks](testing-needed.md#required-local-checks).
3. Run `scripts/test-launcher-ui-preview.ps1` with the explicit local fixture
   paths shown in
   [Focused development commands](steam-version-selection-tooling.md#launcher-navigation-and-mod-loading).
4. If an APK is required, follow [Build an APK](development.md#build-an-apk) with explicit local tool and signing inputs, then inspect it with `scripts/verify-android-apk.ps1`.
5. Record exact commands, exit codes, and failures.

Fake remotes and desktop fixtures prove neither live Steam transport nor physical Android behavior.

## Device stability acceptance

Update the installed ARM64 app in place with `adb install -r`; never uninstall, clear data, downgrade, or use `-d`. Keep its saves, credentials and mod selection. Record the device model, Android version, APK version and hash, game branch, renderer and enabled mods before changing any setting.

Acceptance requires 20 successful Modded launches on one phone: ten cold, six warm, two background/resume and two lock/unlock. Each launch must reach the title screen, stay interactive for 60 seconds, enter gameplay and return. Also check Vanilla startup and that saves, credentials and mod selection are preserved.

If a launch freezes:

1. Keep settings fixed and alternate five cold launches each of the same Modded selection and Vanilla; expand to ten per mode if needed.
2. If only Modded reproduces, disable dependency-valid groups of mods to isolate a suspect, then verify it against the original selection.
3. During the stall, capture continuous logcat, the launcher's timeline and lifecycle reports, an Android bugreport and a Perfetto trace. Correlate preload fallback markers and post-mod Harmony patch chains with the blocked thread; heartbeat gaps or stale snapshots alone do not prove a cause.

Record a genuine process exit separately from a live-process freeze.

## Deferred-preload comparison

The opt-in deferred-preload experiment suppresses the deferred startup invocation that follows `OneTimeInitialization.ExecuteDeferred()`. `GodotApp.onCreate` reads the Android global setting `sts2_deferred_preload_experiment` once, so change it only between processes. Keep the same device, branch, renderer, mod selection and APK for both arms, and keep one continuous log running across both:

```powershell
adb -s <serial> logcat -c
adb -s <serial> logcat -b main -b system -b crash -v threadtime STS2Mobile:I AndroidRuntime:E libc:F DEBUG:F ActivityManager:I '*:S' > artifacts/android/deferred-preload-ab.log
```

Arm A (normal loading) and arm B (suppressed):

```powershell
adb -s <serial> shell am force-stop com.sts2launcher.overhaul.fork.local
adb -s <serial> shell settings delete global sts2_deferred_preload_experiment   # arm A
adb -s <serial> shell settings put global sts2_deferred_preload_experiment 1    # arm B
```

After each force-stop, open the launcher and press the same Play action. Both arms must log `ExecuteDeferred completed`; A logs `call=1 action=normal` and B logs `call=1 action=suppressed`. For each game process, the native `Deferred preload experiment arm:` and managed `[DeferredPreloadExperiment] installed arm=` lines must agree. Delete the global setting after arm B.

- A freezes and B reaches 60 seconds: deferred common/menu loading is implicated, not proved as the cause.
- B freezes at the same point despite the suppression marker: that call is ruled out as a necessary cause.
- Both healthy, or only B freezes: inconclusive.
