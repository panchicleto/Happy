$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v140/NDXSaveAuditor.cs" "$src/NDXSaveAuditor.cs" -Force
Copy-Item "naturaldex-build/v140/NDXSuiteTabs.cs" "$src/NDXSuiteTabs.cs" -Force

$eventsPath = "$src/NaturalDexForm.Events.cs"
$events = Get-Content $eventsPath -Raw
if ($events -notmatch 'InitializeNdxSuiteTabs()') {
    $needle = '        _mainTabs = tabs;'
    if (-not $events.Contains($needle)) { throw "v1.4.0: _mainTabs assignment not found." }
    $events = $events.Replace($needle, $needle + [Environment]::NewLine + '        InitializeNdxSuiteTabs();')
    Set-Content $eventsPath $events -Encoding UTF8
}

# Keep visible/runtime version strings consistent.
Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NaturalDex v1.3.1", "NDX Tools v1.4.0")
    $c = $c.Replace("NaturalDex/1.3.1", "NDX-Tools/1.4.0")
    $c = $c.Replace("NaturalDex 1.3.1", "NDX Tools 1.4.0")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.4.0</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.4.0.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.4.0.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.4.0</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$auditor = Get-Content "$src/NDXSaveAuditor.cs" -Raw
$suite = Get-Content "$src/NDXSuiteTabs.cs" -Raw
$events = Get-Content "$src/NaturalDexForm.Events.cs" -Raw

if ($auditor -notmatch 'LegalityAnalysis') { throw "v1.4.0 verification failed: auditor legality pass missing." }
if ($auditor -notmatch 'DuplicateIdentities') { throw "v1.4.0 verification failed: duplicate identity audit missing." }
if ($suite -notmatch 'Save Auditor') { throw "v1.4.0 verification failed: Save Auditor tab missing." }
if ($suite -notmatch 'Mystery Gifts / Wonder Cards') { throw "v1.4.0 verification failed: Wonder Cards navigation missing." }
if ($events -notmatch 'InitializeNdxSuiteTabs()') { throw "v1.4.0 verification failed: suite tabs not wired." }

Write-Host "NDX Tools v1.4.0 applied on top of NaturalDex v1.3.1."
