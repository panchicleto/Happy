$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

# Keep the v1.8.5 history manager implementation, but DO NOT replace NaturalDexForm.Events.cs.
Copy-Item "naturaldex-build/v185/WonderCardHistoryManagerForm.cs" "$src/WonderCardHistoryManagerForm.cs" -Force

$eventsPath = "$src/NaturalDexForm.Events.cs"
$events = Get-Content $eventsPath -Raw

# 1) Add the history-manager button field without replacing the accumulated Events file.
if ($events -notmatch 'ADMINISTRAR / BORRAR HISTORIAL DEL SAVE') {
    $needle = @'
    private readonly Button _eventsLoadFile = new()
    {
        Text = "CARGAR WONDER CARD LOCAL...",
        AutoSize = true,
    };
'@
    if (-not $events.Contains($needle)) { throw "v1.8.6: _eventsLoadFile field insertion point not found." }

    $insert = $needle + @'

    private readonly Button _eventsManageHistory = new()
    {
        Text = "ADMINISTRAR / BORRAR HISTORIAL DEL SAVE...",
        AutoSize = true,
    };
'@
    $events = $events.Replace($needle, $insert)
}

# 2) Wire click handler.
if ($events -notmatch '_eventsManageHistory\.Click \+= ManageWonderCardHistoryClick') {
    $needle = '        _eventsLoadFile.Click += LoadWonderCardFilesClick;'
    if (-not $events.Contains($needle)) { throw "v1.8.6: event click insertion point not found." }
    $events = $events.Replace(
        $needle,
        $needle + [Environment]::NewLine + '        _eventsManageHistory.Click += ManageWonderCardHistoryClick;')
}

# 3) Add button to existing local Wonder Card section.
if ($events -notmatch 'localFlow\.Controls\.Add\(_eventsManageHistory\)') {
    $needle = '        localFlow.Controls.Add(_eventsLoadFile);'
    if (-not $events.Contains($needle)) { throw "v1.8.6: local Wonder Card flow insertion point not found." }
    $events = $events.Replace(
        $needle,
        $needle + [Environment]::NewLine + '        localFlow.Controls.Add(_eventsManageHistory);')
}

# 4) Disable it while another Event operation is busy.
if ($events -notmatch '_eventsManageHistory\.Enabled = !busy') {
    $needle = '        _eventsLoadFile.Enabled = !busy;'
    if (-not $events.Contains($needle)) { throw "v1.8.6: SetEventBusy insertion point not found." }
    $events = $events.Replace(
        $needle,
        $needle + [Environment]::NewLine + '        _eventsManageHistory.Enabled = !busy;')
}

Set-Content $eventsPath $events -Encoding UTF8

# Promote all visible/runtime branding from the v1.8.4 baseline to v1.8.6.
Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.8.4", "NDX Tools v1.8.6")
    $c = $c.Replace("NDX-Tools/1.8.4", "NDX-Tools/1.8.6")
    $c = $c.Replace("NDX Tools 1.8.4", "NDX Tools 1.8.6")
    $c = $c.Replace("NDX Switch Tools v1.8.4", "NDX Switch Tools v1.8.6")
    $c = $c.Replace("NDX Tools v1.8.5", "NDX Tools v1.8.6")
    $c = $c.Replace("NDX-Tools/1.8.5", "NDX-Tools/1.8.6")
    $c = $c.Replace("NDX Tools 1.8.5", "NDX Tools 1.8.6")
    $c = $c.Replace("NDX Switch Tools v1.8.5", "NDX Switch Tools v1.8.6")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.8.6</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.8.6.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.8.6.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.8.6</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

# Regression guards: all previously accumulated wiring must remain.
$events = Get-Content $eventsPath -Raw
$mgr = Get-Content "$src/WonderCardHistoryManagerForm.cs" -Raw
$tab = Get-Content "$src/NDXSwitchTools.Tab.cs" -Raw
$gallery = Get-Content "$src/SwShTrainerCardGalleryForm.cs" -Raw
$viewer = Get-Content "$src/SwShLeagueCardGameViewForm.cs" -Raw

if ($events -notmatch 'InitializeNdxSuiteTabs\(\)') { throw "v1.8.6 regression: NDX suite tabs were lost." }
if ($events -notmatch 'InitializeV150Tabs\(\)') { throw "v1.8.6 regression: Switch Tools tab wiring was lost." }
if ($events -notmatch 'NDXCompatibility\.WarnIfNeeded') { throw "v1.8.6 regression: compatibility warning wiring was lost." }
if ($events -notmatch 'ADMINISTRAR / BORRAR HISTORIAL DEL SAVE') { throw "v1.8.6 verification: history button missing." }
if ($events -notmatch 'ManageWonderCardHistoryClick') { throw "v1.8.6 verification: history handler missing." }
if ($mgr -notmatch 'BORRAR TODO EL HISTORIAL') { throw "v1.8.6 verification: history manager implementation missing." }

if ($tab -notmatch 'SWSH — Álbum de Tarjetas de Entrenador') { throw "v1.8.6 regression: SWSH Trainer Card tool was lost." }
if ($gallery -notmatch 'VER COMO EN EL JUEGO') { throw "v1.8.6 regression: League Card gallery game-view action was lost." }
if ($viewer -notmatch 'VOLTEAR TARJETA') { throw "v1.8.6 regression: Game Style viewer was lost." }

if ($mgr -notmatch '0x112D5141') { throw "v1.8.6 verification: SWSH Mystery Gift clear support missing." }
if ($mgr -notmatch '0x99E1625E') { throw "v1.8.6 verification: PLA/SV Mystery Gift clear support missing." }
if ($mgr -notmatch 'MysteryBlock8b') { throw "v1.8.6 verification: BDSP history clear support missing." }
if ($mgr -notmatch 'SAV9ZA') { throw "v1.8.6 verification: Z-A safety gate missing." }

Write-Host "NDX Tools v1.8.6 additive Wonder Card history patch applied without removing previous features."
