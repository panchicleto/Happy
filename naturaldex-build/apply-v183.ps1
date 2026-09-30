$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v183/SwShTrainerCardAlbumForm.cs" "$src/SwShTrainerCardAlbumForm.cs" -Force
Copy-Item "naturaldex-build/v183/SwShTrainerCardGalleryForm.cs" "$src/SwShTrainerCardGalleryForm.cs" -Force

Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.8.2", "NDX Tools v1.8.3")
    $c = $c.Replace("NDX-Tools/1.8.2", "NDX-Tools/1.8.3")
    $c = $c.Replace("NDX Tools 1.8.2", "NDX Tools 1.8.3")
    $c = $c.Replace("NDX Switch Tools v1.8.2", "NDX Switch Tools v1.8.3")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.8.3</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.8.3.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.8.3.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.8.3</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$album = Get-Content "$src/SwShTrainerCardAlbumForm.cs" -Raw
$gallery = Get-Content "$src/SwShTrainerCardGalleryForm.cs" -Raw

if ($album -notmatch 'VISTA GALERÍA / SOPORTE') { throw "v1.8.3 verification: gallery button missing." }
if ($gallery -notmatch 'Galería de League Cards') { throw "v1.8.3 verification: gallery UI missing." }
if ($gallery -notmatch 'Compatibles') { throw "v1.8.3 verification: support filter missing." }
if ($gallery -notmatch 'Con advertencias') { throw "v1.8.3 verification: warning filter missing." }
if ($gallery -notmatch 'DETALLE / SOPORTE') { throw "v1.8.3 verification: detail support panel missing." }
if ($gallery -notmatch 'Snapshot pokeldn @ 0x924') { throw "v1.8.3 verification: pokeldn support report missing." }
if ($gallery -notmatch 'Preservar bytes; no editar todavía') { throw "v1.8.3 verification: preserved visual fields disclosure missing." }

Write-Host "NDX Tools v1.8.3 visual League Card gallery applied."
