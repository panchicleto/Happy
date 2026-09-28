$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v130/WonderCardCatalogV130.cs" "$src/WonderCardCatalog.cs" -Force
Copy-Item "naturaldex-build/v130/NaturalDexForm.Events.cs" "$src/NaturalDexForm.Events.cs" -Force

$formPath = "$src/NaturalDexForm.cs"
$form = Get-Content $formPath -Raw

# Mount the existing Living Dex page and the new Event Injector page inside a TabControl.
if ($form -notmatch 'InitializeEventsTab\(\);') {
    $needle = '((Control)this).Controls.Add((Control)(object)val);'
    if ($form.Contains($needle)) {
        $indent = ([string][char]9) + ([string][char]9)
        $form = $form.Replace($needle, $needle + [Environment]::NewLine + $indent + 'InitializeEventsTab();')
    }
    else {
        # Fallback for a cleaner/non-decompiled source layout.
        $pattern = '(?m)^(\s*)Controls\.Add\(([^;]+)\);\s*$'
        $matches = [regex]::Matches($form, $pattern)
        if ($matches.Count -eq 0) {
            throw "v1.3 patch failed: could not locate final form Controls.Add call."
        }
        $m = $matches[$matches.Count - 1]
        $replacement = $m.Value + [Environment]::NewLine + $m.Groups[1].Value + 'InitializeEventsTab();'
        $form = $form.Remove($m.Index, $m.Length).Insert($m.Index, $replacement)
    }
}

Set-Content $formPath $form -Encoding UTF8

# Keep all user-visible version strings consistent.
Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NaturalDex v1.2.1", "NaturalDex v1.3.0")
    $c = $c.Replace("NaturalDex v1.2.0", "NaturalDex v1.3.0")
    $c = $c.Replace("NaturalDex/1.2.1", "NaturalDex/1.3.0")
    $c = $c.Replace("NaturalDex/1.2.0", "NaturalDex/1.3.0")
    $c = $c.Replace("NaturalDex 1.2.1", "NaturalDex 1.3.0")
    $c = $c.Replace("NaturalDex 1.2.0", "NaturalDex 1.3.0")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.3.0</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.3.0.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.3.0.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.3.0</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

# Hard assertions: a green build must actually contain the new tab and local file loader.
$verifyForm = Get-Content $formPath -Raw
$verifyEvents = Get-Content "$src/NaturalDexForm.Events.cs" -Raw
$verifyCatalog = Get-Content "$src/WonderCardCatalog.cs" -Raw

if ($verifyForm -notmatch 'InitializeEventsTab\(\);') {
    throw "v1.3 verification failed: Events tab was not mounted."
}
if ($verifyEvents -notmatch 'CARGAR WONDER CARD LOCAL') {
    throw "v1.3 verification failed: local Wonder Card button missing."
}
if ($verifyEvents -notmatch 'LoadWonderCardFilesClick') {
    throw "v1.3 verification failed: local file handler missing."
}
if ($verifyCatalog -notmatch 'LoadFiles\(IEnumerable<string> paths') {
    throw "v1.3 verification failed: local Wonder Card catalog loader missing."
}
if ($verifyEvents -notmatch 'AuditCommitted') {
    throw "v1.3 verification failed: post-commit legality audit missing."
}

Write-Host "NaturalDex v1.3.0 tabs + local Wonder Card import patch applied."
