$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v184/SwShTrainerCardAlbumForm.cs" "$src/SwShTrainerCardAlbumForm.cs" -Force
Copy-Item "naturaldex-build/v184/SwShTrainerCardGalleryForm.cs" "$src/SwShTrainerCardGalleryForm.cs" -Force
Copy-Item "naturaldex-build/v184/SwShLeagueCardGameViewForm.cs" "$src/SwShLeagueCardGameViewForm.cs" -Force

Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.8.3", "NDX Tools v1.8.4")
    $c = $c.Replace("NDX-Tools/1.8.3", "NDX-Tools/1.8.4")
    $c = $c.Replace("NDX Tools 1.8.3", "NDX Tools 1.8.4")
    $c = $c.Replace("NDX Switch Tools v1.8.3", "NDX Switch Tools v1.8.4")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.8.4</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.8.4.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.8.4.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.8.4</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$album = Get-Content "$src/SwShTrainerCardAlbumForm.cs" -Raw
$gallery = Get-Content "$src/SwShTrainerCardGalleryForm.cs" -Raw
$viewer = Get-Content "$src/SwShLeagueCardGameViewForm.cs" -Raw

if ($album -notmatch 'VER MI TARJETA COMO EN EL JUEGO') { throw "v1.8.4 verification: own-card game view button missing." }
if ($album -notmatch 'VER SELECCIONADA COMO EN EL JUEGO') { throw "v1.8.4 verification: selected-card game view button missing." }
if ($gallery -notmatch 'VER COMO EN EL JUEGO') { throw "v1.8.4 verification: gallery game view button missing." }
if ($viewer -notmatch 'VOLTEAR TARJETA') { throw "v1.8.4 verification: flip card UI missing." }
if ($viewer -notmatch 'EXPORTAR PREVIEW PNG') { throw "v1.8.4 verification: PNG export missing." }
if ($viewer -notmatch 'PKHeX.Drawing.PokeSprite.SpriteUtil') { throw "v1.8.4 verification: PKHeX sprite bridge missing." }
if ($viewer -notmatch 'DrawFront') { throw "v1.8.4 verification: front renderer missing." }
if ($viewer -notmatch 'DrawBack') { throw "v1.8.4 verification: back renderer missing." }
if ($viewer -notmatch 'Skin =') { throw "v1.8.4 verification: appearance ID parsing missing." }
if ($viewer -notmatch 'Hair =') { throw "v1.8.4 verification: hair ID parsing missing." }
if ($viewer -notmatch 'BottomOrDress') { throw "v1.8.4 verification: outfit parsing missing." }

Write-Host "NDX Tools v1.8.4 Game Style League Card viewer applied."
