$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v191/HomeCollectionsTab.cs" "$src/HomeCollectionsTab.cs" -Force
Copy-Item "naturaldex-build/v191/HomeBoxManagerForm.cs" "$src/HomeBoxManagerForm.cs" -Force

Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.9.0", "NDX Tools v1.9.1")
    $c = $c.Replace("NDX-Tools/1.9.0", "NDX-Tools/1.9.1")
    $c = $c.Replace("NDX Tools 1.9.0", "NDX Tools 1.9.1")
    $c = $c.Replace("NDX Switch Tools v1.9.0", "NDX Switch Tools v1.9.1")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.9.1</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.9.1.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.9.1.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.9.1</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$homeTab = Get-Content "$src/HomeCollectionsTab.cs" -Raw
$boxes = Get-Content "$src/HomeBoxManagerForm.cs" -Raw
$homeCatalog = Get-Content "$src/HomeCollectionCatalog.cs" -Raw
$events = Get-Content "$src/NaturalDexForm.Events.cs" -Raw
$viewer = Get-Content "$src/SwShLeagueCardGameViewForm.cs" -Raw
$history = Get-Content "$src/WonderCardHistoryManagerForm.cs" -Raw

if ($homeTab -notmatch 'ADMINISTRAR CAJAS') { throw "v1.9.1: box manager button missing." }
if ($homeTab -notmatch 'OpenHomeBoxManager') { throw "v1.9.1: box manager action not wired." }

if ($boxes -notmatch 'IBoxDetailNameRead') { throw "v1.9.1: real box-name reader missing." }
if ($boxes -notmatch 'VACIAR SELECCIONADAS') { throw "v1.9.1: selected-box clear action missing." }
if ($boxes -notmatch 'VACIAR TODAS LAS CAJAS') { throw "v1.9.1: all-box clear action missing." }
if ($boxes -notmatch 'GetBoxSlotAtIndex') { throw "v1.9.1: box occupancy scan missing." }
if ($boxes -notmatch 'SetBoxSlotAtIndex') { throw "v1.9.1: box clear writer missing." }
if ($boxes -notmatch 'SaveBackupManager.Create') { throw "v1.9.1: automatic backup missing." }
if ($boxes -notmatch 'El nombre de la Caja') { throw "v1.9.1: box-name preservation audit missing." }
if ($boxes -notmatch 'auditoría binaria') { throw "v1.9.1: binary commit audit missing." }

# Regression guards.
if ($events -notmatch 'InitializeHomeCollectionsTab\(\)') { throw "v1.9.1 regression: HOME Collections tab lost." }
if ($homeCatalog -notmatch 'Ultimate HOME Collection') { throw "v1.9.1 regression: HOME catalog lost." }
if ($homeTab -notmatch 'EXPORTAR MANIFEST JSON') { throw "v1.9.1 regression: HOME manifest export lost." }
if ($events -notmatch 'ADMINISTRAR / BORRAR HISTORIAL DEL SAVE') { throw "v1.9.1 regression: Wonder Card history cleaner lost." }
if ($history -notmatch 'BORRAR TODO EL HISTORIAL') { throw "v1.9.1 regression: history manager lost." }
if ($viewer -notmatch 'VOLTEAR TARJETA') { throw "v1.9.1 regression: Game Style League Card viewer lost." }

Write-Host "NDX Tools v1.9.1 named box manager applied."
