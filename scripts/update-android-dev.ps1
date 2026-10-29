param(
    [string]$DeviceSerial = "",
    [switch]$BuildOnly,
    [string]$DevHome = "$(Join-Path $env:LOCALAPPDATA 'StS2Launcher\dev')",
    [string]$AndroidHome = "$(Join-Path $env:USERPROFILE '.w40k-android-toolchain\android-sdk')",
    [string]$JavaHome = "$(Join-Path $env:USERPROFILE '.w40k-android-toolchain\jdk-17')",
    [string]$GradlePath = "$(Join-Path $env:USERPROFILE '.w40k-android-toolchain\gradle-8.14.3\bin\gradle.bat')"
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $PSScriptRoot 'android-signing-utils.ps1')
$package = 'com.sts2launcher.overhaul.fork.devlocal'
$adb = Join-Path $AndroidHome 'platform-tools\adb.exe'
$keytool = Join-Path $JavaHome 'bin\keytool.exe'
$keystore = Join-Path $DevHome 'dev.keystore'
$statePath = Join-Path $DevHome 'state.json'
$previousApk = Join-Path $DevHome 'last.apk'
$installedCode = 0

if (-not $BuildOnly) {
    $devices = @(& $adb devices | Where-Object { $_ -match '^\S+\s+device$' } | ForEach-Object { ($_ -split '\s+')[0] })
    if ($LASTEXITCODE -ne 0) { throw 'Could not list Android devices.' }
    if (-not $DeviceSerial) {
        if ($devices.Count -ne 1) { throw 'Connect exactly one authorized phone or pass -DeviceSerial.' }
        $DeviceSerial = $devices[0]
    }
    if ($devices -notcontains $DeviceSerial) { throw "Device $DeviceSerial is not connected and authorized." }
    $packageInfo = & $adb -s $DeviceSerial shell dumpsys package $package
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the installed dev app.' }
    $versionMatch = [regex]::Match(($packageInfo -join "`n"), 'versionCode=(\d+)')
    if ($versionMatch.Success) { $installedCode = [int]$versionMatch.Groups[1].Value }
}

$state = if (Test-Path -LiteralPath $statePath) { Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json } else { $null }
New-Item -ItemType Directory -Force -Path $DevHome | Out-Null
if (-not (Test-Path -LiteralPath $keystore)) {
    if ($state -or $installedCode -gt 0) { throw "The dev signing key is missing. Restore $keystore; refusing to replace it." }
    & $keytool -genkeypair -noprompt -keystore $keystore -storepass android -keypass android -alias sts2dev -keyalg RSA -keysize 2048 -validity 10000 -dname 'CN=StS2 Launcher Local Dev' | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the persistent dev signing key.' }
}
$signer = Get-KeystoreSignerSha256 -KeystorePath $keystore -KeystorePassword android -KeyAlias sts2dev -KeytoolPath $keytool
if ($state -and $state.signerSha256 -ne $signer) { throw 'The dev signing key changed. Restore the original key before updating.' }
$previousCode = if ($state) { [int]$state.versionCode } else { 0 }
$versionCode = [Math]::Max($previousCode, $installedCode) + 1
$versionName = "dev.$versionCode"

& (Join-Path $PSScriptRoot 'build-android-local.ps1') -VersionName $versionName -VersionCode $versionCode -PackageName $package -AppLabel 'StS2 Launcher Dev' -DevBuild -KeepGradleDaemon -KeystorePath $keystore -KeystoreAlias sts2dev -KeystorePassword android -AndroidHome $AndroidHome -JavaHome $JavaHome -GradlePath $GradlePath
$apk = Join-Path $root "artifacts\android\StS2Launcher-v$versionName-arm64-v8a.apk"
$verification = @{ ApkPath = $apk; ExpectedPackageName = $package; ExpectedSignerSha256 = $signer; ExpectedMinVersionCode = $versionCode }
if (Test-Path -LiteralPath $previousApk) { $verification.PreviousApkPath = $previousApk }
& (Join-Path $PSScriptRoot 'verify-android-update-compat.ps1') @verification

Copy-Item -LiteralPath $apk -Destination $previousApk -Force
@{ versionCode = $versionCode; signerSha256 = $signer; packageName = $package } | ConvertTo-Json | Set-Content -LiteralPath "$statePath.tmp" -Encoding UTF8
Move-Item -LiteralPath "$statePath.tmp" -Destination $statePath -Force

if (-not $BuildOnly) {
    & $adb -s $DeviceSerial install -r $apk
    if ($LASTEXITCODE -ne 0) { throw 'Dev install failed. The existing app data has not been cleared.' }
    & $adb -s $DeviceSerial shell am start -n "$package/com.game.sts2launcher.LauncherActivity"
    if ($LASTEXITCODE -ne 0) { throw 'Dev APK installed, but launching it failed.' }
}
Write-Host "Dev APK: $apk"
Write-Host "Persistent signing key: $keystore"
