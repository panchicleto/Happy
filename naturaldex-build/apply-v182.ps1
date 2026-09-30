$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v182/SwShTrainerCardAlbumForm.cs" "$src/SwShTrainerCardAlbumForm.cs" -Force
Copy-Item "naturaldex-build/v182/SwShPokeLdnCardBridge.cs" "$src/SwShPokeLdnCardBridge.cs" -Force

Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.8.1", "NDX Tools v1.8.2")
    $c = $c.Replace("NDX-Tools/1.8.1", "NDX-Tools/1.8.2")
    $c = $c.Replace("NDX Tools 1.8.1", "NDX Tools 1.8.2")
    $c = $c.Replace("NDX Switch Tools v1.8.1", "NDX Switch Tools v1.8.2")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.8.2</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.8.2.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.8.2.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.8.2</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$album = Get-Content "$src/SwShTrainerCardAlbumForm.cs" -Raw
$bridge = Get-Content "$src/SwShPokeLdnCardBridge.cs" -Raw

if ($bridge -notmatch 'TrainerCardOffset = 0x924') { throw "v1.8.2 verification: pokeldn Trainer Card offset missing." }
if ($bridge -notmatch 'SnapshotLength = 3456') { throw "v1.8.2 verification: full pokeldn snapshot size missing." }
if ($bridge -notmatch 'ShortSnapshotLength = 2965') { throw "v1.8.2 verification: short pokeldn snapshot support missing." }
if ($bridge -notmatch 'ZLibStream') { throw "v1.8.2 verification: pokeldn short snapshot inflation missing." }
if ($bridge -notmatch 'BuildCardSetArguments') { throw "v1.8.2 verification: --card-set bridge missing." }
if ($bridge -notmatch 'pokeBase' -or $bridge -notmatch 'form_argument') { throw "v1.8.2 verification: six showcase Pokemon mapping missing." }
if ($album -notmatch 'POKELDN SNAPSHOT') { throw "v1.8.2 verification: snapshot UI missing." }
if ($album -notmatch 'EDITAR MI TARJETA') { throw "v1.8.2 verification: card editor UI missing." }
if ($album -notmatch 'MI TARJETA → SNAPSHOT POKELDN') { throw "v1.8.2 verification: snapshot injection UI missing." }

Write-Host "NDX Tools v1.8.2 pokeldn League Card integration applied."
