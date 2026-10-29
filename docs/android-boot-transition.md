# Android boot transition and loading screens

## Current cold-start path

The launch/loading path is split across two deliberately separate surfaces:

- Android cold-start transition: `GodotAppSplashTheme` displays the official Godot Engine mark while Android and Godot initialize. Once the managed launcher has rendered two process frames and one post-draw frame, native Android transitions to the StS2 Launcher identity and then reveals the launcher.
- Godot post-launch warmup/status: `ShaderWarmupScreen` displays shader warmup progress, and `LauncherStartupStatus` displays game-startup phase text after the launcher closes.

The cold-start transition is silent and uses vector assets. It borrows the structure of a deliberate two-stage boot confirmation, but it does not reproduce PlayStation branding, timing, audio, or assets.

## Native transition policy

- The full transition runs only for the first ordinary launcher start in a process.
- Pending Start Game, Safe Start, return-to-launcher restarts, and deliberate game restarts skip it.
- The Godot mark remains visible for real initialization time; the exact four-second sequence starts only after the launcher-first-frame bridge fires.
- Reduced-motion devices receive a direct 200 ms fade with no icon hold.
- The overlay blocks touch only while visible and is removed from the view hierarchy on completion.
- A 30-second watchdog removes the overlay if launcher construction or readiness signalling fails.
- Repeated splash-exit callbacks are ignored after completion or timeout, and IME suppression is posted twice during overlay attachment to cover Samsung window-focus timing.
- `last_startup_timeline.txt` records installed, skipped, launcher-ready, completed, reduced-motion, and timeout phases.
- The generated bootstrap project disables Godot's separate boot image. Downloaded game PCKs and shader-warmup behavior are unchanged.

## Visual sequence

`AndroidBootSequence` owns one contiguous 4,000 ms clock. Every visual channel is derived from these phase boundaries:

| Phase | Timeline | Presentation |
| --- | ---: | --- |
| `registration` | 0-450 ms | The Godot mark fades and scales from 100% to 84% while offset cyan and orange identity layers establish the next mark. |
| `separation` | 450-700 ms | Both marks clear for a deliberate 250 ms near-black separation. |
| `identity-reveal` | 700-1,500 ms | Cyan and orange layers converge, the identity scales from 82% through 103.5%, and the resolved full-colour mark appears. |
| `final-settle` | 1,500-1,850 ms | The resolved mark compresses through 99.2%, returns once to 100%, and then reveals the live `StS2 LAUNCHER` wordmark. |
| `confirmation-hold` | 1,850-3,050 ms | The completed identity remains stable for 1,200 ms. |
| `identity-pulse` | 3,050-3,300 ms | One 1.4% pulse gives the confirmed identity a restrained tactile response. |
| `launcher-handoff` | 3,300-4,000 ms | The overlay fades out over the already-rendered launcher and is then removed from the hierarchy. |

This timing starts only after native overlay attachment and the managed first-frame readiness signal are both present. Runtime initialization before readiness is not forced into a fixed duration.

## Presentation ownership

`AndroidBootTransitionController` owns readiness, animation, watchdog and lifecycle cleanup. `AndroidBootSequence` samples visual state, and `AndroidBootIdentityView` renders it. The transition is silent.

## Branding restrictions

The permitted identity is the attributed official Godot Engine mark followed by project-owned StS2 Launcher artwork and the live `StS2 LAUNCHER` wordmark. The launcher name identifies the tool and must not imply ownership of Slay the Spire 2 or an official mobile port. The transition may use the general idea of a deliberate two-stage boot confirmation, but contributors must not:

- use Sony, PlayStation, PS1, or Sony Computer Entertainment logos, wordmarks, symbols, artwork, or trade dress;
- copy or derive the PlayStation startup sound, add a sound-alike recording, or market a future cue as PlayStation audio;
- reproduce the console sequence's exact timing, easing, geometry, composition, or animation assets; or
- describe the result as PlayStation branding, an official PlayStation-style boot screen, or an affiliation or endorsement.

The Godot mark remains subject to its Creative Commons Attribution 4.0 terms and Godot press-kit guidance. Its use identifies the runtime and does not imply Godot endorsement of StS2 Launcher.

## Shader and game boundary

- Shader warmup remains a separate Godot progress screen shown later when Start Game requires real first-run work. Its progress, watchdog, recovery behavior, and potentially 45-90 second runtime are not hidden by the native boot identity.
- The transition changes only the launcher/bootstrap presentation. Disabling the bootstrap project's duplicate Godot boot image does not alter a downloaded game PCK.
- No downloaded Slay the Spire 2 asset, assembly, PCK, shader cache, save, mod, or other core-game file is patched for this feature.
- Normal launch and Safe Start retain their existing startup, renderer, shader-warmup, and recovery routes after the native transition policy skips the ceremony.
