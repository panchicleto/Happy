$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

# Replace the preliminary v1.1 Wonder Card model with the complete v1.2 catalog model.
Copy-Item "naturaldex-build/v120/WonderCardCatalogV120.cs" "$src/WonderCardCatalog.cs" -Force
Copy-Item "naturaldex-build/v120/WonderCardInjectionService.cs" "$src/WonderCardInjectionService.cs" -Force
Copy-Item "naturaldex-build/v120/WonderCardSelectionForm.cs" "$src/WonderCardSelectionForm.cs" -Force
Copy-Item "naturaldex-build/v120/NaturalDexForm.Events.cs" "$src/NaturalDexForm.Events.cs" -Force

$formPath = "$src/NaturalDexForm.cs"
$form = Get-Content $formPath -Raw

# NaturalDexForm must be partial so the event selector remains isolated from the Living Dex UI source.
$form = $form.Replace("public sealed class NaturalDexForm", "public sealed partial class NaturalDexForm")
if ($form -notmatch "public sealed partial class NaturalDexForm") {
    throw "v1.2 patch failed: could not make NaturalDexForm partial."
}

# Keep the existing button subscription intact, but move the old implementation aside.
# The partial class supplies a new GenerateEventsClick with the same EventHandler signature.
$form = [regex]::Replace($form, '(?m)(\bprivate\s+async\s+void\s+)GenerateEventsClick(\s*\()', '$1LegacyGenerateEventsClick$2', 1)
if ($form -notmatch 'LegacyGenerateEventsClick') {
    throw "v1.2 patch failed: legacy GenerateEventsClick declaration not found."
}

$form = $form.Replace('Text = "GENERAR EVENTOS"', 'Text = "EVENTOS / WONDER CARDS"')
$form = $form.Replace("NaturalDex v1.1.0 — Living Dex + Wonder Cards", "NaturalDex v1.2.0 — Event Injector + Living Dex")
$form = $form.Replace("NaturalDex v0.9.3", "NaturalDex v1.2.0")
$form = $form.Replace("NaturalDex v0.9.4", "NaturalDex v1.2.0")
Set-Content $formPath $form -Encoding UTF8

# Keep version text consistent in source-generated reports / headers.
Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NaturalDex v1.1.0", "NaturalDex v1.2.0")
    $c = $c.Replace("NaturalDex v0.9.3", "NaturalDex v1.2.0")
    $c = $c.Replace("NaturalDex v0.9.4", "NaturalDex v1.2.0")
    $c = $c.Replace("NaturalDex/0.7.0", "NaturalDex/1.2.0")
    $c = $c.Replace("NaturalDex 0.7.0", "NaturalDex 1.2.0")
    Set-Content $_.FullName $c -Encoding UTF8
}

# Align assembly/file/product versions where the SDK project permits it.
$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
if ($proj -notmatch '<Version>1\.2\.0</Version>') {
    $versionBlock = @"
    <Version>1.2.0</Version>
    <FileVersion>1.2.0.0</FileVersion>
    <AssemblyVersion>1.2.0.0</AssemblyVersion>
    <InformationalVersion>1.2.0</InformationalVersion>
"@
    $proj = [regex]::Replace($proj, '<PropertyGroup>', "<PropertyGroup>`r`n$versionBlock", 1)
}
Set-Content $projPath $proj -Encoding UTF8

# Hard assertions so a green Action cannot silently build the old event workflow.
$verify = Get-Content $formPath -Raw
if ($verify -notmatch 'LegacyGenerateEventsClick') { throw "v1.2 verification failed: legacy handler was not renamed." }

$required = @(
    "$src/WonderCardCatalog.cs",
    "$src/WonderCardInjectionService.cs",
    "$src/WonderCardSelectionForm.cs",
    "$src/NaturalDexForm.Events.cs"
)
foreach ($file in $required) {
    if (!(Test-Path $file)) { throw "v1.2 verification failed: missing $file" }
}

Write-Host "NaturalDex v1.2.0 event selector/injector source patch applied."

# v1.2.0 build trigger: compile from the complete selector/injector patch set.
