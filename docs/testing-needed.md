# Testing needed

Use the smallest test that exercises the affected behavior. Do not turn desktop, fake-service, source inspection, or one-device evidence into a broader claim.

## Required local checks

Supply the [development prerequisites and staged runtimes](development.md#prerequisites) first. The JVM command uses Gradle 8.14.3 on `PATH`.

```powershell
dotnet build src\STS2Mobile\STS2Mobile.csproj -c Release
.\scripts\test-local-gameplay-save-safety.ps1
dotnet run --project tests\STS2Mobile.GameIdentityTests\STS2Mobile.GameIdentityTests.csproj -c Release
gradle -p android testMonoReleaseUnitTest --rerun-tasks
```

Run `scripts/test-launcher-ui-preview.ps1` with the explicit importer, base-game PCK, and managed-runtime paths in [Focused development commands](steam-version-selection-tooling.md#launcher-navigation-and-mod-loading) when launcher or mod behavior changes.

The local suites cover:

- Local path containment, atomic saves, deterministic synchronization decisions, Pull/Push interruption behavior, and shared automatic/manual synchronization.
- Persisted mod/version selection and focused launcher interaction.
- Authoritative PCK/DLL identity and generation-bound PCK caching.
- Transactional branch N → N+1 updates, interruptions, stale-evidence rejection, and branch isolation.
- Runtime-pack candidate validation, atomic promotion/rollback, and active-cache gating.
- Selected-branch recovery preserving saves, credentials, and sibling branches.
- Attempt-bound launcher handoff under reordered, duplicated, background/resume, lock/unlock, timeout, and late-callback events.

The save suite uses one deterministic `FakeSaveRemote`; it does not prove Steam transport. Desktop mod fixtures do not prove Android activation or an in-game effect.

## Device results

Follow the [Android validation runbook](runbook-android-validation.md) for device checks. Record exact APK, commit, package/version/signer/hash, device, Android/API, ABI, selected branch, renderer, test actions, and failures for every device result.
