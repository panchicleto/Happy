$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v180/WonderCardCatalogV180.cs" "$src/WonderCardCatalog.cs" -Force
Copy-Item "naturaldex-build/v180/WonderCardInjectionService.cs" "$src/WonderCardInjectionService.cs" -Force

# Promote v1.7 branding to v1.8.
Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.7.0", "NDX Tools v1.8.0")
    $c = $c.Replace("NDX-Tools/1.7.0", "NDX-Tools/1.8.0")
    $c = $c.Replace("NDX Tools 1.7.0", "NDX Tools 1.8.0")
    $c = $c.Replace("NDX Switch Tools v1.7.0", "NDX Switch Tools v1.8.0")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.8.0</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.8.0.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.8.0.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.8.0</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$injector = Get-Content "$src/WonderCardInjectionService.cs" -Raw
$catalog = Get-Content "$src/WonderCardCatalog.cs" -Raw

if ($injector -notmatch 'PlatinumStyleFlag = 1248') { throw "v1.8 verification: BDSP Platinum clothing writer missing." }
if ($injector -notmatch 'TryGetPLAFashionKey') { throw "v1.8 verification: PLA clothing adapter missing." }
if ($injector -notmatch 'block\.Data\[\(int\)index\] = 2') { throw "v1.8 verification: PLA ownership byte write missing." }
if ($catalog -notmatch 'WA9 w =>') { throw "v1.8 verification: WA9 classifier missing." }
if ($catalog -match 'Ropa BDSP: el layout de desbloqueo no está expuesto') { throw "v1.8 verification: old BDSP clothing block remains." }
if ($catalog -match 'Ropa de PLA: los bloques de desbloqueo no están expuestos') { throw "v1.8 verification: old PLA clothing block remains." }

Write-Host "NDX Tools v1.8.0 event hardening applied for PKHeX.Core 26.7.7.0."
