$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v190/HomeCollectionCatalog.cs" "$src/HomeCollectionCatalog.cs" -Force
Copy-Item "naturaldex-build/v190/HomeCollectionsTab.cs" "$src/HomeCollectionsTab.cs" -Force

$enumPath = "$src/DexLayoutMode.cs"
$enum = Get-Content $enumPath -Raw
if ($enum -notmatch 'GenerationCurrentGame') {
    $replacement = 'OrigiDexCurrentRegion,' + [Environment]::NewLine +
        [char]9 + 'GenerationCurrentGame,' + [Environment]::NewLine +
        [char]9 + 'StarterCollection,' + [Environment]::NewLine +
        [char]9 + 'FossilCollection,' + [Environment]::NewLine +
        [char]9 + 'PseudoLegendCollection,' + [Environment]::NewLine +
        [char]9 + 'BabyCollection,' + [Environment]::NewLine +
        [char]9 + 'LegendaryMythicalCollection'
    $enum = $enum.Replace('OrigiDexCurrentRegion', $replacement)
    Set-Content $enumPath $enum -Encoding UTF8
}

$plannerPath = "$src/DexPlanner.cs"
$planner = Get-Content $plannerPath -Raw
if ($planner -notmatch 'GetHomeCollectionPlan') {
    $pattern = 'if \(\(int\)layout == 3\)\s*return GetOrigiDexPlan\(sav\);'
    $replacement = 'if ((int)layout == 3)' + [Environment]::NewLine +
        [char]9 + [char]9 + 'return GetOrigiDexPlan(sav);' + [Environment]::NewLine +
        [char]9 + [char]9 + 'if ((int)layout is >= 4 and <= 9)' + [Environment]::NewLine +
        [char]9 + [char]9 + [char]9 + 'return GetHomeCollectionPlan(sav, layout);'
    $planner = [regex]::Replace($planner, $pattern, $replacement, 1)

    $methodNeedle = 'private static List<DexPlanEntry> GetNationalPlan'
    $ix = $planner.IndexOf($methodNeedle)
    if ($ix -lt 0) { throw "v1.9.0: GetNationalPlan insertion point missing." }

    $method = @'
private static List<DexPlanEntry> GetHomeCollectionPlan(SaveFile sav, DexLayoutMode layout)
	{
		var species = HomeCollectionCatalog.GetGeneratorSpecies(layout, sav);
		var list = new List<DexPlanEntry>(species.Count);
		for (int i = 0; i < species.Count; i++)
			list.Add(new DexPlanEntry(species[i], i + 1));
		return list;
	}

	
'@
    $planner = $planner.Insert($ix, $method)
    Set-Content $plannerPath $planner -Encoding UTF8
}

$formPath = "$src/NaturalDexForm.cs"
$form = Get-Content $formPath -Raw
$oldItems = '_dexLayout.Items.AddRange(new object[4] { "Nacional (001 a ultimo disponible)", "Juego base (sin DLC)", "Juego base + DLC (Base - DLC1 - DLC2)", "OrigiDex — especies nuevas de la región del juego" });'
$newItems = '_dexLayout.Items.AddRange(new object[10] { "Nacional (001 a ultimo disponible)", "Juego base (sin DLC)", "Juego base + DLC (Base - DLC1 - DLC2)", "OrigiDex — especies nuevas de la región del juego", "HOME — Generation Dex", "HOME — Starter Collection", "HOME — Fossil Collection", "HOME — Pseudo-Legendary Collection", "HOME — Baby Pokémon Collection", "HOME — Legendary + Mythical Collection" });'

if ($form.Contains($oldItems)) {
    $form = $form.Replace($oldItems, $newItems)
}
elseif ($form -notmatch 'HOME — Generation Dex') {
    throw "v1.9.0: Living Dex layout items insertion point missing."
}

$form = $form.Replace(
    'DexLayoutMode dexLayout = (DexLayoutMode)Math.Clamp(((ListControl)_dexLayout).SelectedIndex, 0, 3);',
    'DexLayoutMode dexLayout = (DexLayoutMode)Math.Clamp(((ListControl)_dexLayout).SelectedIndex, 0, 9);')

$form = $form.Replace(
    '_mysteryGiftHistory.Checked, _strictValidation.Checked, true, (int)_startBox.Value, (int)_retries.Value)',
    '_mysteryGiftHistory.Checked, _strictValidation.Checked, _sisterVersionFallback.Checked, (int)_startBox.Value, (int)_retries.Value)')

$form = $form.Replace(
    'string value = ((int)batch.Options.DexLayout) switch { 1 => "Juego base (sin DLC)", 2 => "Juego base + DLC", 3 => "OrigiDex — región del juego", _ => "Nacional" };',
    'string value = ((int)batch.Options.DexLayout) switch { 1 => "Juego base (sin DLC)", 2 => "Juego base + DLC", 3 => "OrigiDex — región del juego", 4 => "HOME Generation Dex", 5 => "HOME Starter Collection", 6 => "HOME Fossil Collection", 7 => "HOME Pseudo-Legendary Collection", 8 => "HOME Baby Collection", 9 => "HOME Legendary + Mythical", _ => "Nacional" };')

Set-Content $formPath $form -Encoding UTF8

$eventsPath = "$src/NaturalDexForm.Events.cs"
$events = Get-Content $eventsPath -Raw
if ($events -notmatch 'InitializeHomeCollectionsTab\(\);') {
    $needle = '        InitializeV150Tabs();'
    if (-not $events.Contains($needle)) { throw "v1.9.0: InitializeV150Tabs call missing." }
    $events = $events.Replace($needle, $needle + [Environment]::NewLine + '        InitializeHomeCollectionsTab();')
    Set-Content $eventsPath $events -Encoding UTF8
}

Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.8.8", "NDX Tools v1.9.0")
    $c = $c.Replace("NDX-Tools/1.8.8", "NDX-Tools/1.9.0")
    $c = $c.Replace("NDX Tools 1.8.8", "NDX Tools 1.9.0")
    $c = $c.Replace("NDX Switch Tools v1.8.8", "NDX Switch Tools v1.9.0")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.9.0</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.9.0.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.9.0.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.9.0</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$events = Get-Content $eventsPath -Raw
$form = Get-Content $formPath -Raw
$planner = Get-Content $plannerPath -Raw
$enum = Get-Content $enumPath -Raw
$home = Get-Content "$src/HomeCollectionCatalog.cs" -Raw
$homeTab = Get-Content "$src/HomeCollectionsTab.cs" -Raw
$selector = Get-Content "$src/WonderCardSelectionForm.cs" -Raw
$viewer = Get-Content "$src/SwShLeagueCardGameViewForm.cs" -Raw
$history = Get-Content "$src/WonderCardHistoryManagerForm.cs" -Raw

if ($events -notmatch 'InitializeHomeCollectionsTab\(\)') { throw "v1.9.0: HOME Collections tab is not mounted." }
if ($homeTab -notmatch 'HOME Collections') { throw "v1.9.0: HOME Collections UI missing." }
if ($homeTab -notmatch 'EXPORTAR MANIFEST CSV') { throw "v1.9.0: CSV manifest export missing." }
if ($homeTab -notmatch 'EXPORTAR MANIFEST JSON') { throw "v1.9.0: JSON manifest export missing." }
if ($homeTab -notmatch 'APLICAR PRESET Y GENERAR') { throw "v1.9.0: generator bridge missing." }

if ($enum -notmatch 'LegendaryMythicalCollection') { throw "v1.9.0: HOME generator layouts missing." }
if ($planner -notmatch 'HomeCollectionCatalog\.GetGeneratorSpecies') { throw "v1.9.0: HOME layouts not wired to planner." }
if ($form -notmatch 'HOME — Starter Collection') { throw "v1.9.0: HOME layouts missing from Living Dex selector." }
if ($form -notmatch 'SelectedIndex, 0, 9') { throw "v1.9.0: Living Dex does not allow HOME layout values." }
if ($form -notmatch '_sisterVersionFallback\.Checked') { throw "v1.9.0: sister-version checkbox is not honored." }

if ($home -notmatch 'Regional FormDex') { throw "v1.9.0: Regional FormDex missing." }
if ($home -notmatch 'Complete FormDex') { throw "v1.9.0: Complete FormDex missing." }
if ($home -notmatch 'Vivillon Pattern Dex') { throw "v1.9.0: Vivillon Dex missing." }
if ($home -notmatch 'Alcremie Dex') { throw "v1.9.0: Alcremie Dex missing." }
if ($home -notmatch 'Gender Pair Dex') { throw "v1.9.0: GenderDex missing." }
if ($home -notmatch 'Apriball Matrix') { throw "v1.9.0: Apriball tracker missing." }
if ($home -notmatch 'EventDex') { throw "v1.9.0: EventDex missing." }
if ($home -notmatch 'AlphaDex') { throw "v1.9.0: AlphaDex missing." }
if ($home -notmatch 'GO Origin Dex') { throw "v1.9.0: GO Origin Dex missing." }
if ($home -notmatch 'Language Dex') { throw "v1.9.0: Language Dex missing." }
if ($home -notmatch 'Ultimate HOME Collection') { throw "v1.9.0: Ultimate HOME collection missing." }

if ($selector -notmatch 'REGISTRAR SOLO EN HISTORIAL') { throw "v1.9.0 regression: Wonder Card history-only mode lost." }
if ($events -notmatch 'ADMINISTRAR / BORRAR HISTORIAL DEL SAVE') { throw "v1.9.0 regression: Wonder Card history cleaner lost." }
if ($history -notmatch 'BORRAR TODO EL HISTORIAL') { throw "v1.9.0 regression: history manager lost." }
if ($viewer -notmatch 'VOLTEAR TARJETA') { throw "v1.9.0 regression: Game Style League Card viewer lost." }
if ($form -notmatch 'OrigiDex — especies nuevas de la región del juego') { throw "v1.9.0 regression: OrigiDex lost." }

Write-Host "NDX Tools v1.9.0 HOME Collections applied with regression guards."
