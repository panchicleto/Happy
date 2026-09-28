$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

$files = @(
    "NDXSwitchTools.Tab.cs",
    "SwShRaidToolForm.cs",
    "SwShAdventureToolForm.cs",
    "SVOverworldToolForm.cs",
    "ZAStashToolForm.cs",
    "PLANPCToolForm.cs"
)
foreach ($f in $files) {
    Copy-Item "naturaldex-build/v150/$f" "$src/$f" -Force
}

$eventsPath = "$src/NaturalDexForm.Events.cs"
$events = Get-Content $eventsPath -Raw
if ($events -notmatch 'InitializeV150Tabs()') {
    $needle = '        InitializeNdxSuiteTabs();'
    if (-not $events.Contains($needle)) { throw "v1.5.0: InitializeNdxSuiteTabs call not found." }
    $events = $events.Replace($needle, $needle + [Environment]::NewLine + '        InitializeV150Tabs();')
    Set-Content $eventsPath $events -Encoding UTF8
}

# Promote all v1.4 branding to v1.5 after v1.4 has been applied.
Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.4.0", "NDX Tools v1.5.0")
    $c = $c.Replace("NDX-Tools/1.4.0", "NDX-Tools/1.5.0")
    $c = $c.Replace("NDX Tools 1.4.0", "NDX Tools 1.5.0")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.5.0</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.5.0.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.5.0.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.5.0</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

# Build-time verification that every promised module is present and wired.
$tab = Get-Content "$src/NDXSwitchTools.Tab.cs" -Raw
$raid = Get-Content "$src/SwShRaidToolForm.cs" -Raw
$adv = Get-Content "$src/SwShAdventureToolForm.cs" -Raw
$sv = Get-Content "$src/SVOverworldToolForm.cs" -Raw
$za = Get-Content "$src/ZAStashToolForm.cs" -Raw
$pla = Get-Content "$src/PLANPCToolForm.cs" -Raw
$events = Get-Content "$src/NaturalDexForm.Events.cs" -Raw

if ($tab -notmatch 'SWSH Raid Viewer / Editor') { throw "v1.5 verification: Raid module missing." }
if ($adv -notmatch 'MaxLair') { throw "v1.5 verification: Dynamax module missing." }
if ($tab -notmatch 'KCurryDex') { throw "v1.5 verification: Curry module missing." }
if ($sv -notmatch 'KOverworld') { throw "v1.5 verification: SV Overworld module missing." }
if ($za -notmatch 'KStoredShinyEntity') { throw "v1.5 verification: ZA Shiny Stash module missing." }
if ($pla -notmatch 'PA8' -or $pla -notmatch 'SIZE_STORED') { throw "v1.5 verification: PLA NPC scanner missing." }
if ($events -notmatch 'InitializeV150Tabs()') { throw "v1.5 verification: Switch Tools tab not wired." }

Write-Host "NDX Tools v1.5.0 modules applied successfully."
