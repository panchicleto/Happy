$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v131/WonderCardCatalogV131.cs" "$src/WonderCardCatalog.cs" -Force
Copy-Item "naturaldex-build/v131/WonderCardInjectionService.cs" "$src/WonderCardInjectionService.cs" -Force
Copy-Item "naturaldex-build/v131/WonderCardSelectionForm.cs" "$src/WonderCardSelectionForm.cs" -Force

# Keep version strings consistent.
Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NaturalDex v1.3.0", "NaturalDex v1.3.1")
    $c = $c.Replace("NaturalDex/1.3.0", "NaturalDex/1.3.1")
    $c = $c.Replace("NaturalDex 1.3.0", "NaturalDex 1.3.1")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.3.1</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.3.1.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.3.1.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.3.1</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$catalog = Get-Content "$src/WonderCardCatalog.cs" -Raw
$selector = Get-Content "$src/WonderCardSelectionForm.cs" -Raw
$injector = Get-Content "$src/WonderCardInjectionService.cs" -Raw

if ($catalog -match 'EncounterDate\.GetDateSwitch\(\)') {
    throw "v1.3.1 verification failed: current-date fallback still present in catalog."
}
if ($catalog -notmatch '1605 SWSH - Clothing Set Tracksuit') {
    throw "v1.3.1 verification failed: official non-Pokemon date catalog missing."
}
if ($selector -notmatch '_dateOverrides') {
    throw "v1.3.1 verification failed: manual date override UI missing."
}
if ($selector -notmatch 'fecha requerida') {
    throw "v1.3.1 verification failed: missing-date guard missing."
}
if ($injector -notmatch 'IsReceiptDateValid') {
    throw "v1.3.1 verification failed: receipt-date audit missing."
}

Write-Host "NaturalDex v1.3.1 Wonder Card date fixes applied."
