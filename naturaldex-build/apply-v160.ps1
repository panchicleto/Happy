$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

$files = @(
    "NDXCompatibility.cs",
    "SwShRaidAdvancedForm.cs",
    "SwShPokeCampToolForm.cs"
)
foreach ($f in $files) {
    Copy-Item "naturaldex-build/v160/$f" "$src/$f" -Force
}

$eventsPath = "$src/NaturalDexForm.Events.cs"
$events = Get-Content $eventsPath -Raw
if ($events -notmatch 'NDXCompatibility.WarnIfNeeded') {
    $needle = '        InitializeV150Tabs();'
    if (-not $events.Contains($needle)) { throw "v1.6.0: InitializeV150Tabs call not found." }
    $events = $events.Replace($needle, $needle + [Environment]::NewLine + '        NDXCompatibility.WarnIfNeeded(this);')
    Set-Content $eventsPath $events -Encoding UTF8
}

Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.5.0", "NDX Tools v1.6.0")
    $c = $c.Replace("NDX-Tools/1.5.0", "NDX-Tools/1.6.0")
    $c = $c.Replace("NDX Tools 1.5.0", "NDX Tools 1.6.0")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.6.0</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.6.0.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.6.0.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.6.0</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$compat = Get-Content "$src/NDXCompatibility.cs" -Raw
$advanced = Get-Content "$src/SwShRaidAdvancedForm.cs" -Raw
$camp = Get-Content "$src/SwShPokeCampToolForm.cs" -Raw
$tab = Get-Content "$src/NDXSwitchTools.Tab.cs" -Raw
$raid = Get-Content "$src/SwShRaidToolForm.cs" -Raw

if ($compat -notmatch '26, 7, 7, 0') { throw "v1.6 verification: exact Core target missing." }
if ($advanced -notmatch 'XoroMachineSkip') { throw "v1.6 verification: raid seed reversal missing." }
if ($advanced -notmatch 'GENERAR FRAMES') { throw "v1.6 verification: frame analyzer missing." }
if ($camp -notmatch 'Golden cookware') { throw "v1.6 verification: Poke Camp editor missing." }
if ($tab -notmatch 'SyncCurry151') { throw "v1.6 verification: Curry consistency sync missing." }
if ($raid -notmatch 'FRAMES / SEED FINDER') { throw "v1.6 verification: advanced raid button missing." }

Write-Host "NDX Tools v1.6.0 applied for PKHeX.Core 26.7.7.0."
