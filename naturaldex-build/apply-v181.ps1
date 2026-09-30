$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v181/SwShTrainerCardAlbumForm.cs" "$src/SwShTrainerCardAlbumForm.cs" -Force

$tabPath = "$src/NDXSwitchTools.Tab.cs"
$tab = Get-Content $tabPath -Raw
if ($tab -notmatch 'OpenSwShTrainerCardAlbumTool') {
    $needle = '            AddToolButton(flow, "Poké Camp", OpenSwShPokeCampTool);'
    if (-not $tab.Contains($needle)) { throw "v1.8.1: SWSH button insertion point not found." }
    $insert = $needle + [Environment]::NewLine + '            AddToolButton(flow, "SWSH — Álbum de Tarjetas de Entrenador", OpenSwShTrainerCardAlbumTool);'
    $tab = $tab.Replace($needle, $insert)
    Set-Content $tabPath $tab -Encoding UTF8
}

Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.8.0", "NDX Tools v1.8.1")
    $c = $c.Replace("NDX-Tools/1.8.0", "NDX-Tools/1.8.1")
    $c = $c.Replace("NDX Tools 1.8.0", "NDX Tools 1.8.1")
    $c = $c.Replace("NDX Switch Tools v1.8.0", "NDX Switch Tools v1.8.1")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.8.1</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.8.1.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.8.1.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.8.1</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$cards = Get-Content "$src/SwShTrainerCardAlbumForm.cs" -Raw
$tab = Get-Content "$src/NDXSwitchTools.Tab.cs" -Raw

if ($cards -notmatch 'KFriendLeagueCards = 0x28E707F5') { throw "v1.8.1 verification: friend League Card block missing." }
if ($cards -notmatch 'AlbumSlots = 300') { throw "v1.8.1 verification: expected 300-card album missing." }
if ($cards -notmatch 'CardSize = 0x1D0') { throw "v1.8.1 verification: TrainerCard8 raw size missing." }
if ($cards -notmatch 'EXPORTAR MI TARJETA') { throw "v1.8.1 verification: own-card export UI missing." }
if ($cards -notmatch 'IMPORTAR CARPETA') { throw "v1.8.1 verification: bulk import UI missing." }
if ($cards -notmatch 'lc8album') { throw "v1.8.1 verification: whole album backup missing." }
if ($tab -notmatch 'Álbum de Tarjetas de Entrenador') { throw "v1.8.1 verification: Switch Tools button missing." }

Write-Host "NDX Tools v1.8.1 trainer-card album support applied."
