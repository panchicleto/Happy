$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

$files = @(
    "SVOverworldReconstructor.cs",
    "ZASpawnerMapLoader.cs"
)
foreach ($f in $files) {
    Copy-Item "naturaldex-build/v170/$f" "$src/$f" -Force
}

# Promote v1.6 branding to v1.7 after all earlier patches have been applied.
Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.6.0", "NDX Tools v1.7.0")
    $c = $c.Replace("NDX-Tools/1.6.0", "NDX-Tools/1.7.0")
    $c = $c.Replace("NDX Tools 1.6.0", "NDX Tools 1.7.0")
    $c = $c.Replace("NDX Switch Tools v1.6.0", "NDX Switch Tools v1.7.0")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.7.0</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.7.0.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.7.0.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.7.0</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$injector = Get-Content "$src/WonderCardInjectionService.cs" -Raw
$catalog = Get-Content "$src/WonderCardCatalog.cs" -Raw
$sv = Get-Content "$src/SVOverworldToolForm.cs" -Raw
$recon = Get-Content "$src/SVOverworldReconstructor.cs" -Raw
$pla = Get-Content "$src/PLANPCToolForm.cs" -Raw
$za = Get-Content "$src/ZAStashToolForm.cs" -Raw

if ($injector -notmatch 'ApplyUnderground') { throw "v1.7 verification: BDSP Underground event support missing." }
if ($injector -notmatch 'BattleTower\.BP') { throw "v1.7 verification: BDSP BP event support missing." }
if ($injector -notmatch 'AuditReceiptWhenExposed') { throw "v1.7 verification: post-commit receipt audit missing." }
if ($catalog -match 'Objetos de Subsuelo BDSP: pendiente') { throw "v1.7 verification: old BDSP Underground block remains." }
if ($recon -notmatch 'EncounterMovesetGenerator') { throw "v1.7 verification: SV encounter reconstruction missing." }
if ($sv -notmatch 'RECONSTRUIR \+ EXTRAER LEGAL') { throw "v1.7 verification: SV reconstruction UI missing." }
if ($pla -notmatch 'Probable NPC/Farm block') { throw "v1.7 verification: PLA block classifier missing." }
if ($za -notmatch 'CARGAR CSV DE SPAWNERS') { throw "v1.7 verification: ZA spawner resolver missing." }

Write-Host "NDX Tools v1.7.0 applied for PKHeX.Core 26.7.7.0."
