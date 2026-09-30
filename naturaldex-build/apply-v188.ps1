$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v188/OrigiDexCatalog.cs" "$src/OrigiDexCatalog.cs" -Force

# Extend DexLayoutMode with OrigiDexCurrentRegion.
$enumPath = "$src/DexLayoutMode.cs"
$enum = Get-Content $enumPath -Raw
if ($enum -notmatch 'OrigiDexCurrentRegion') {
    if ($enum -notmatch 'BasePlusDLC') { throw "v1.8.8: BasePlusDLC enum value not found." }
    $enum = $enum.Replace(
        'BasePlusDLC',
        'BasePlusDLC,' + [Environment]::NewLine + [char]9 + 'OrigiDexCurrentRegion')
    Set-Content $enumPath $enum -Encoding UTF8
}

# Add OrigiDex to the existing Living Dex layout selector without changing DexMode.
$formPath = "$src/NaturalDexForm.cs"
$form = Get-Content $formPath -Raw

$oldItems = '_dexLayout.Items.AddRange(new object[3] { "Nacional (001 a ultimo disponible)", "Juego base (sin DLC)", "Juego base + DLC (Base - DLC1 - DLC2)" });'
$newItems = '_dexLayout.Items.AddRange(new object[4] { "Nacional (001 a ultimo disponible)", "Juego base (sin DLC)", "Juego base + DLC (Base - DLC1 - DLC2)", "OrigiDex — especies nuevas de la región del juego" });'
if ($form.Contains($oldItems)) {
    $form = $form.Replace($oldItems, $newItems)
}
elseif ($form -notmatch 'OrigiDex — especies nuevas de la región del juego') {
    throw "v1.8.8: Dex layout Items.AddRange insertion point not found."
}

$form = $form.Replace(
    'DexLayoutMode dexLayout = (DexLayoutMode)Math.Clamp(((ListControl)_dexLayout).SelectedIndex, 0, 2);',
    'DexLayoutMode dexLayout = (DexLayoutMode)Math.Clamp(((ListControl)_dexLayout).SelectedIndex, 0, 3);')

$form = $form.Replace(
    'string value = ((int)batch.Options.DexLayout) switch { 1 => "Juego base (sin DLC)", 2 => "Juego base + DLC", _ => "Nacional" };',
    'string value = ((int)batch.Options.DexLayout) switch { 1 => "Juego base (sin DLC)", 2 => "Juego base + DLC", 3 => "OrigiDex — región del juego", _ => "Nacional" };')

Set-Content $formPath $form -Encoding UTF8

# Route OrigiDex through the existing DexPlanner so all generation / legality logic is reused.
$plannerPath = "$src/DexPlanner.cs"
$planner = Get-Content $plannerPath -Raw

if ($planner -notmatch 'GetOrigiDexPlan') {
    $needle = 'if ((int)layout == 0)'
    $ix = $planner.IndexOf($needle)
    if ($ix -lt 0) { throw "v1.8.8: DexPlanner layout branch not found." }

    $insert = 'if ((int)layout == 3)' + [Environment]::NewLine +
              [char]9 + [char]9 + 'return GetOrigiDexPlan(sav);' + [Environment]::NewLine + [char]9 + [char]9

    $planner = $planner.Insert($ix, $insert)

    $methodNeedle = 'private static List<DexPlanEntry> GetNationalPlan'
    $methodIx = $planner.IndexOf($methodNeedle)
    if ($methodIx -lt 0) { throw "v1.8.8: GetNationalPlan insertion point not found." }

    $method = @'
private static List<DexPlanEntry> GetOrigiDexPlan(SaveFile sav)
	{
		var species = OrigiDexCatalog.BuildSpecies(sav);
		var list = new List<DexPlanEntry>(species.Count);
		for (int i = 0; i < species.Count; i++)
			list.Add(new DexPlanEntry(species[i], i + 1));
		return list;
	}

	
'@
    $planner = $planner.Insert($methodIx, $method)
    Set-Content $plannerPath $planner -Encoding UTF8
}

# Promote visible/runtime branding.
Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.8.7", "NDX Tools v1.8.8")
    $c = $c.Replace("NDX-Tools/1.8.7", "NDX-Tools/1.8.8")
    $c = $c.Replace("NDX Tools 1.8.7", "NDX Tools 1.8.8")
    $c = $c.Replace("NDX Switch Tools v1.8.7", "NDX Switch Tools v1.8.8")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.8.8</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.8.8.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.8.8.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.8.8</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

# Build-time feature and regression guards.
$form = Get-Content $formPath -Raw
$planner = Get-Content $plannerPath -Raw
$enum = Get-Content $enumPath -Raw
$origin = Get-Content "$src/OrigiDexCatalog.cs" -Raw
$events = Get-Content "$src/NaturalDexForm.Events.cs" -Raw
$viewer = Get-Content "$src/SwShLeagueCardGameViewForm.cs" -Raw
$history = Get-Content "$src/WonderCardHistoryManagerForm.cs" -Raw

if ($enum -notmatch 'OrigiDexCurrentRegion') { throw "v1.8.8 verification: OrigiDex layout enum missing." }
if ($form -notmatch 'OrigiDex — especies nuevas de la región del juego') { throw "v1.8.8 verification: OrigiDex UI option missing." }
if ($form -notmatch 'SelectedIndex, 0, 3') { throw "v1.8.8 verification: layout selector does not allow OrigiDex." }
if ($planner -notmatch 'GetOrigiDexPlan') { throw "v1.8.8 verification: OrigiDex planner missing." }
if ($planner -notmatch 'OrigiDexCatalog\.BuildSpecies') { throw "v1.8.8 verification: OrigiDex catalog not wired." }

if ($origin -notmatch 'GameVersion\.B or GameVersion\.W or GameVersion\.B2 or GameVersion\.W2') { throw "v1.8.8 verification: Unova mapping missing." }
if ($origin -notmatch 'new\("Teselia / Unova", 494, 649') { throw "v1.8.8 verification: Unova range missing." }
if ($origin -notmatch 'new\("Alola", 722, 807') { throw "v1.8.8 verification: strict Alola range missing." }
if ($origin -notmatch 'new\("Galar", 810, 898') { throw "v1.8.8 verification: Galar range missing." }
if ($origin -notmatch 'new\("Hisui", 899, 905') { throw "v1.8.8 verification: Hisui range missing." }
if ($origin -notmatch 'new\("Paldea", 906, 1010') { throw "v1.8.8 verification: strict Paldea range missing." }

# Previous functionality must remain.
if ($events -notmatch 'REGISTRAR SOLO EN HISTORIAL') { throw "v1.8.8 regression: Wonder Card history-only mode lost." }
if ($events -notmatch 'ADMINISTRAR / BORRAR HISTORIAL DEL SAVE') { throw "v1.8.8 regression: history cleaner lost." }
if ($history -notmatch 'BORRAR TODO EL HISTORIAL') { throw "v1.8.8 regression: history manager lost." }
if ($viewer -notmatch 'VOLTEAR TARJETA') { throw "v1.8.8 regression: Game Style League Card viewer lost." }

Write-Host "NDX Tools v1.8.8 OrigiDex current-region mode applied."
