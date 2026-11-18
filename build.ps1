param(
    [string]$GameDir = $env:FALLEN_ACES_DIR,
    [string]$UnityEditor = $env:UNITY_EDITOR
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($GameDir)) { throw "Set -GameDir or FALLEN_ACES_DIR to the Fallen Aces installation directory." }
if ([string]::IsNullOrWhiteSpace($UnityEditor)) { throw "Set -UnityEditor or UNITY_EDITOR to the Unity 2022.3.62f2 editor executable." }
if (!(Test-Path -LiteralPath $GameDir -PathType Container)) { throw "Game directory not found: $GameDir" }
if (!(Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw "Unity Editor not found: $UnityEditor" }
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
$UnityEditor = (Resolve-Path -LiteralPath $UnityEditor).Path
$managed = Join-Path $GameDir 'Fallen Aces_Data/Managed'
if (!(Test-Path -LiteralPath (Join-Path $managed 'FallenAces.CSharp.dll'))) { throw 'Fallen Aces Mono installation not found.' }
New-Item -ItemType Directory -Force 'lib' | Out-Null
$bepinexZip = Join-Path $PSScriptRoot 'lib/BepInEx_win_x64_5.4.23.5.zip'
if (!(Test-Path -LiteralPath $bepinexZip)) {
    Invoke-WebRequest 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip' -OutFile $bepinexZip
}
if ((Get-FileHash -LiteralPath $bepinexZip -Algorithm SHA256).Hash -ne '82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4') { throw 'BepInEx archive SHA-256 mismatch.' }
$dependencyDir = Join-Path ([IO.Path]::GetTempPath()) ('rtlc-fallenaces-dependencies-' + [Guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $bepinexZip -DestinationPath $dependencyDir
foreach ($name in 'BepInEx.dll', '0Harmony.dll') {
    $official = Join-Path $dependencyDir "BepInEx/core/$name"
    $reference = Join-Path $PSScriptRoot "lib/$name"
    if (!(Test-Path -LiteralPath $reference)) { Copy-Item -LiteralPath $official -Destination $reference }
    if ((Get-FileHash -LiteralPath $official -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $reference -Algorithm SHA256).Hash) {
        throw "Build reference differs from pinned BepInEx archive: $name"
    }
}
dotnet build 'FallenAces.RussianTranslate.csproj' -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
python 'tools/validate_translations.py' --game-dir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Translation validation failed.' }
$assemblyInventory = Join-Path ([IO.Path]::GetTempPath()) ('rtlc-fallenaces-assembly-text-' + [Guid]::NewGuid().ToString('N') + '.json')
dotnet run --project 'tools/AssemblyTextAudit/AssemblyTextAudit.csproj' -c Release -- $assemblyInventory (Join-Path $managed 'FallenAces.CSharp.dll') (Join-Path $managed 'Assembly-CSharp.dll') (Join-Path $managed 'PackageTools.dll')
if ($LASTEXITCODE -ne 0) { throw 'Complete CIL literal extraction failed.' }
python 'tools/validate_assembly_text.py' $assemblyInventory
if ($LASTEXITCODE -ne 0) { throw 'Game assembly text inventory requires review.' }
dotnet run --project 'tests/CatalogTests.csproj' -c Release "-p:GameDir=$GameDir" -- $assemblyInventory
if ($LASTEXITCODE -ne 0) { throw 'Catalog regression tests failed.' }
$bundleProject = Join-Path $PSScriptRoot 'tools/BundleProject'
$bundleInput = Join-Path $bundleProject 'Assets/RTLC'
if (!(Test-Path -LiteralPath $UnityEditor)) { throw "Unity Editor not found: $UnityEditor" }
New-Item -ItemType Directory -Force (Join-Path $bundleInput 'translations'), (Join-Path $bundleInput 'fonts') | Out-Null
foreach ($name in 'data.ru.json', 'subtitles.ru.json', 'ui.ru.json') {
    Copy-Item -LiteralPath (Join-Path 'translations' $name) -Destination (Join-Path $bundleInput 'translations') -Force
}
Copy-Item -LiteralPath (Join-Path 'Fonts' 'fonts.json') -Destination (Join-Path $bundleInput 'fonts') -Force
foreach ($name in 'Broadway Cyrillic Extended.ttf', 'brushscriptstd.ttf', 'JosefinSansExt-Regular.ttf', 'Troubleside.ttf') {
    Copy-Item -LiteralPath (Join-Path 'Fonts' $name) -Destination (Join-Path $bundleInput 'fonts') -Force
}
$bundlePath = Join-Path $bundleProject 'BundleOutput/rtlc.bundle'
if (Test-Path -LiteralPath $bundlePath) { Remove-Item -LiteralPath $bundlePath -Force }
$unityLog = Join-Path ([IO.Path]::GetTempPath()) ('rtlc-fallenaces-unity-' + [Guid]::NewGuid().ToString('N') + '.log')
$unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList '-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $bundleProject + '"'), '-executeMethod', 'BuildRTLC.Build', '-logFile', ('"' + $unityLog + '"') -WindowStyle Hidden -Wait -PassThru
if ($unityProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $bundlePath) -or !(Test-Path -LiteralPath $unityLog) -or !(Select-String -LiteralPath $unityLog -Pattern 'RTLC_BUNDLE_OK: 8 assets' -Quiet)) {
    if (Test-Path -LiteralPath $unityLog) { Get-Content -LiteralPath $unityLog -Tail 80 }
    throw 'AssetBundle build or Unity readback failed.'
}
python 'tools/verify_bundle.py' $bundlePath
if ($LASTEXITCODE -ne 0) { throw 'Bundled contents differ from source translations or fonts.' }
$stage = Join-Path ([IO.Path]::GetTempPath()) ('rtlc-fallenaces-package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $stage | Out-Null
Get-ChildItem -LiteralPath $dependencyDir -Force | Copy-Item -Destination $stage -Recurse
$plugin = Join-Path $stage 'BepInEx/plugins/RTLC.FallenAces.RussianTranslate'
New-Item -ItemType Directory -Force $plugin | Out-Null
Copy-Item 'bin/Release/netstandard2.1/RTLC.FallenAces.RussianTranslate.dll' $plugin
Copy-Item -LiteralPath $bundlePath -Destination $plugin
$licenseStage = Join-Path $stage 'ThirdPartyLicenses'
New-Item -ItemType Directory -Force $licenseStage | Out-Null
Copy-Item -LiteralPath 'THIRD_PARTY_NOTICES.txt' -Destination $licenseStage
foreach ($license in Get-ChildItem -LiteralPath 'LICENSES' -Filter '*.txt' -File) {
    Copy-Item -LiteralPath $license.FullName -Destination $licenseStage
}
New-Item -ItemType Directory -Force 'dist' | Out-Null
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath 'dist/FallenAces-RussianTranslate-0.1.0.zip' -Force
python 'tools/verify_package.py' 'dist/FallenAces-RussianTranslate-0.1.0.zip' $bepinexZip
if ($LASTEXITCODE -ne 0) { throw 'Release package verification failed.' }
Get-FileHash 'dist/FallenAces-RussianTranslate-0.1.0.zip' -Algorithm SHA256
