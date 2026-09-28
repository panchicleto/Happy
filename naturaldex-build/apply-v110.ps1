$ErrorActionPreference="Stop"
$fp="NaturalDexSource/NaturalDex.Plugin/NaturalDexForm.cs"
$c=Get-Content $fp -Raw
$c=$c.Replace("NaturalDex v0.9.3 PKHeXth — Strict Validation","NaturalDex v1.1.0 — Living Dex + Wonder Cards")
$c=$c.Replace("NaturalDex v0.9.4 PKHeXth — Audit + Sister Fallback","NaturalDex v1.1.0 — Living Dex + Wonder Cards")
# Simplify labels without removing proven generator logic.
$c=$c.Replace("Orden Nacional","Nacional (001 → último)")
$c=$c.Replace("Dex regional del juego","Juego base (sin DLC)")
$c=$c.Replace("Regional / juego","Juego base (sin DLC)")
# Existing expanded/regional path remains engine fallback for game + DLC while catalog tables are added next.
Set-Content $fp $c -Encoding UTF8

# Add a clean Wonder Card catalog model that compiles independently of UI iteration.
@'
using System;
using PKHeX.Core;
namespace NaturalDex.Plugin;
internal enum WonderRewardKind { Unknown, Pokemon, Item, BP, Clothing }
internal sealed record WonderCardEntry(string FilePath, DataMysteryGift Gift, WonderRewardKind Kind, bool Compatible, bool IsShiny, DateOnly? Start, DateOnly? End)
{
    public int CardID => Gift.CardID;
    public string Display => $"{CardID:0000} — {Gift.CardTitle}";
}
internal static class WonderCardClassifier
{
    public static WonderRewardKind GetKind(DataMysteryGift gift) => gift switch
    {
        WC8 w => w.CardType switch
        {
            WC8.GiftType.Pokemon => WonderRewardKind.Pokemon,
            WC8.GiftType.Item => WonderRewardKind.Item,
            WC8.GiftType.BP => WonderRewardKind.BP,
            WC8.GiftType.Clothing => WonderRewardKind.Clothing,
            _ => WonderRewardKind.Unknown,
        },
        _ when gift.IsEntity => WonderRewardKind.Pokemon,
        _ => WonderRewardKind.Unknown,
    };
}
'@ | Set-Content "NaturalDexSource/NaturalDex.Plugin/WonderCardCatalog.cs" -Encoding UTF8

@'
using System;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;
namespace NaturalDex.Plugin;
internal enum LivingDexOrder { National, BaseGame, BasePlusDLC }
internal static class LivingDexOrderEngine
{
    public static IReadOnlyList<ushort> Build(SaveFile sav, LivingDexOrder order)
    {
        int max = sav.MaxSpeciesID;
        if (order == LivingDexOrder.National)
            return Enumerable.Range(1, max).Select(z => (ushort)z).ToArray();

        if (sav is SAV8SWSH)
            return BuildSWSH(max, order == LivingDexOrder.BasePlusDLC);
        if (sav is SAV9SV)
            return BuildSV(max, order == LivingDexOrder.BasePlusDLC);

        // Games without a split DLC dex: use species present in the game's personal table.
        var table = sav.Personal;
        var list = new List<ushort>();
        for (ushort species=1; species<=max; species++)
            if (table.IsPresentInGame(species, 0))
                list.Add(species);
        return list;
    }

    private static IReadOnlyList<ushort> BuildSWSH(int max, bool dlc)
    {
        var t=PersonalTable.SWSH;
        var baseDex=new List<(ushort s,ushort i)>();
        var armor=new List<(ushort s,ushort i)>();
        var crown=new List<(ushort s,ushort i)>();
        for(ushort s=1;s<=max;s++)
        {
            var p=t[s];
            if(p.PokeDexIndex!=0) baseDex.Add((s,p.PokeDexIndex));
            if(dlc && p.ArmorDexIndex!=0) armor.Add((s,p.ArmorDexIndex));
            if(dlc && p.CrownDexIndex!=0) crown.Add((s,p.CrownDexIndex));
        }
        return Merge(baseDex,armor,crown);
    }

    private static IReadOnlyList<ushort> BuildSV(int max, bool dlc)
    {
        var t=PersonalTable.SV;
        var paldea=new List<(ushort s,ushort i)>();
        var kita=new List<(ushort s,ushort i)>();
        var blue=new List<(ushort s,ushort i)>();
        for(ushort s=1;s<=max;s++)
        {
            var p=t[s];
            if(p.DexPaldea!=0) paldea.Add((s,p.DexPaldea));
            if(dlc && p.DexKitakami!=0) kita.Add((s,p.DexKitakami));
            if(dlc && p.DexBlueberry!=0) blue.Add((s,p.DexBlueberry));
        }
        return Merge(paldea,kita,blue);
    }

    private static IReadOnlyList<ushort> Merge(params List<(ushort s,ushort i)>[] groups)
    {
        var seen=new HashSet<ushort>();
        var result=new List<ushort>();
        foreach(var group in groups)
            foreach(var x in group.OrderBy(z=>z.i).ThenBy(z=>z.s))
                if(seen.Add(x.s)) result.Add(x.s);
        return result;
    }
}
'@ | Set-Content "NaturalDexSource/NaturalDex.Plugin/LivingDexOrderEngine.cs" -Encoding UTF8

# Wire generator through three-mode planner.
$dp="NaturalDexSource/NaturalDex.Plugin/DexPlanner.cs"
$d=Get-Content $dp -Raw
$d=$d.Replace('if (layout != DexLayoutMode.BaseGameRegional)', 'if ((int)layout == 0)')
$d=$d.Replace('return GetBaseGamePlan(sav);', 'return GetOrderedPlan(sav, (int)layout == 2);')
$insert=@'
	private static List<DexPlanEntry> GetOrderedPlan(SaveFile sav, bool includeDLC)
	{
		var mode = includeDLC ? LivingDexOrder.BasePlusDLC : LivingDexOrder.BaseGame;
		var species = LivingDexOrderEngine.Build(sav, mode);
		var list = new List<DexPlanEntry>(species.Count);
		for (int i = 0; i < species.Count; i++)
			list.Add(new DexPlanEntry(species[i], i + 1));
		return list;
	}

'@
$needle='	private static List<DexPlanEntry> GetNationalPlan'
$d=$d.Insert($d.IndexOf($needle),$insert)
Set-Content $dp $d -Encoding UTF8

# Expose all three order modes in UI.
$fp="NaturalDexSource/NaturalDex.Plugin/NaturalDexForm.cs"
$c=Get-Content $fp -Raw
$c=$c.Replace('_dexLayout.Items.AddRange(new object[2] { "Dex nacional (orden nacional)", "Dex base del juego (sin DLC, orden regional)" });', '_dexLayout.Items.AddRange(new object[3] { "Nacional (001 a ultimo disponible)", "Juego base (sin DLC)", "Juego base + DLC (Base - DLC1 - DLC2)" });')
$c=$c.Replace('DexLayoutMode dexLayout = ((((ListControl)_dexLayout).SelectedIndex == 1) ? DexLayoutMode.BaseGameRegional : DexLayoutMode.National);', 'DexLayoutMode dexLayout = (DexLayoutMode)Math.Clamp(((ListControl)_dexLayout).SelectedIndex, 0, 2);')
$c=$c.Replace('string value = ((batch.Options.DexLayout == DexLayoutMode.BaseGameRegional) ? "Base game regional dex (no DLC)" : "National order");', 'string value = ((int)batch.Options.DexLayout) switch { 1 => "Juego base (sin DLC)", 2 => "Juego base + DLC", _ => "Nacional" };')
Set-Content $fp $c -Encoding UTF8
$ef="NaturalDexSource/NaturalDex.Plugin/DexLayoutMode.cs"
$e=Get-Content $ef -Raw
if ($e -notmatch 'BasePlusDLC') {
  $e=$e.Replace('BaseGameRegional', "BaseGameRegional," + [Environment]::NewLine + [char]9 + "BasePlusDLC")
  Set-Content $ef $e -Encoding UTF8
}

# Legendary/static/gift fallback: retry constrained failures through ALM Automatic.
$gp="NaturalDexSource/NaturalDex.Plugin/NaturalDexGenerator.cs"
$g=Get-Content $gp -Raw
$needle="if (pkm == null)"
$replacement=@'
if (pkm == null && options.EncounterSource != EncounterSourcePreference.Automatic)
				pkm = TryGenerateOne(entry.Species, entry.Form, options with { EncounterSource = EncounterSourcePreference.Automatic, EncounterFallback = EncounterFallbackMode.AllowAnyLegal }, trainer, game, false, out _);
			if (pkm == null)
'@
$g=$g.Replace($needle,$replacement)
Set-Content $gp $g -Encoding UTF8

# Shiny event cascade: current-save event first, then historical event, then normal legal shiny.
$gp="NaturalDexSource/NaturalDex.Plugin/NaturalDexGenerator.cs"
$g=Get-Content $gp -Raw
$old=@'
			OfficialEventEntry[] array = _eventCatalog.ShinyCandidates(species).ToArray();
			NaturalRandom.Shuffle(_rng, array.AsSpan());
'@
$new=@'
			OfficialEventEntry[] allEvents = _eventCatalog.ShinyCandidates(species).ToArray();
			OfficialEventEntry[] currentEvents = allEvents.Where(z => z.Generation == _sav.Generation).ToArray();
			OfficialEventEntry[] historicEvents = allEvents.Where(z => z.Generation < _sav.Generation).OrderByDescending(z => z.Generation).ToArray();
			NaturalRandom.Shuffle(_rng, currentEvents.AsSpan());
			NaturalRandom.Shuffle(_rng, historicEvents.AsSpan());
			OfficialEventEntry[] array = currentEvents.Concat(historicEvents).ToArray();
'@
$g=$g.Replace($old,$new)
# Historical cards must use a verified event date without being constrained by the current save's user date range.
$g=$g.Replace('if (!EventDateResolver.TryGetDate(officialEventEntry.Gift, _options.DateFrom, _options.DateTo, _options.RandomizeDates, _rng, out date, out string reason2))', 'if (!(officialEventEntry.Generation == _sav.Generation ? EventDateResolver.TryGetDate(officialEventEntry.Gift, _options.DateFrom, _options.DateTo, _options.RandomizeDates, _rng, out date, out string reason2) : EventDateResolver.TryGetAnyVerifiedDate(officialEventEntry.Gift, out date, out reason2)))')
Set-Content $gp $g -Encoding UTF8

# Restore positional gaps: every planned dex entry consumes exactly one physical box slot.
$fp="NaturalDexSource/NaturalDex.Plugin/NaturalDexForm.cs"
$c=Get-Content $fp -Raw
$old=@'
		foreach (PKM pokemon in _batch.Pokemon)
		{
			if (pokemon == null)
			{
				continue;
			}
			int num2 = FindNextOpenBoxSlot(saveFile, num);
			if (num2 < 0)
			{
				break;
			}
			list.Add((num2, pokemon));
			num = num2 + 1;
		}
'@
$new=@'
		foreach (PKM pokemon in _batch.Pokemon)
		{
			// Preserve Living Dex alignment: a failed species intentionally leaves one empty slot.
			if (pokemon == null)
			{
				num++;
				continue;
			}
			int num2 = FindNextOpenBoxSlot(saveFile, num);
			if (num2 < 0)
				break;
			list.Add((num2, pokemon));
			num = num2 + 1;
		}
'@
$c=$c.Replace($old,$new)
Set-Content $fp $c -Encoding UTF8

# v1.1.0 legendary/event fix: direct official shiny gift conversion + truly positional box gaps.
$gp="NaturalDexSource/NaturalDex.Plugin/NaturalDexGenerator.cs"
$g=Get-Content $gp -Raw

# Track the receipt only when the exact card is receivable by the currently opened save.
if ($g -notmatch '_pendingEventReceipt') {
  $fieldNeedle='	private AdventureHistoryPlanner? _history;'
  $g=$g.Replace($fieldNeedle, $fieldNeedle + [Environment]::NewLine + [Environment]::NewLine + '	private EventReceiptPlan? _pendingEventReceipt;')
}

# Every dex plan entry must have a corresponding batch entry, even skips.
$g=$g.Replace('				progress?.Report(new GenerationProgress(completed, count, $"#{species} skipped"));' + [Environment]::NewLine + '				continue;',
'				generationBatch.Pokemon.Add(null);' + [Environment]::NewLine + '				progress?.Report(new GenerationProgress(completed, count, $"#{species} skipped"));' + [Environment]::NewLine + '				continue;')
$g=$g.Replace('				progress?.Report(new GenerationProgress(completed, count, speciesNameGeneration + ": already present"));' + [Environment]::NewLine + '				continue;',
'				generationBatch.Pokemon.Add(null);' + [Environment]::NewLine + '				progress?.Report(new GenerationProgress(completed, count, speciesNameGeneration + ": already present"));' + [Environment]::NewLine + '				continue;')

# Reset per-species receipt before generation.
$g=$g.Replace('			PKM val = null;' + [Environment]::NewLine + '			string text = "No legal result.";',
'			PKM val = null;' + [Environment]::NewLine + '			_pendingEventReceipt = null;' + [Environment]::NewLine + '			string text = "No legal result.";')

# Commit current-save Wonder Record only after that Pokemon actually succeeded.
$g=$g.Replace('			generationBatch.Pokemon.Add(val);' + [Environment]::NewLine + '			EncounterSourcePreference source =',
'			generationBatch.Pokemon.Add(val);' + [Environment]::NewLine + '			if (_pendingEventReceipt is not null)' + [Environment]::NewLine + '				generationBatch.EventReceipts.Add(_pendingEventReceipt);' + [Environment]::NewLine + '			EncounterSourcePreference source =')

# Insert direct official-shiny resolver before the older ALM event path.
$marker='		DateOnly date;' + [Environment]::NewLine + '		if (shiny && _options.PreferOfficialShinyEvents && _eventCatalog != null)'
if ($g.Contains($marker) -and $g -notmatch 'TryGenerateDirectOfficialShiny') {
  $replacement='		DateOnly date;' + [Environment]::NewLine +
'		if (shiny && _options.PreferOfficialShinyEvents && _eventCatalog != null)' + [Environment]::NewLine +
'		{' + [Environment]::NewLine +
'			PKM? official = TryGenerateDirectOfficialShiny(species, out string officialReason);' + [Environment]::NewLine +
'			if (official is not null)' + [Environment]::NewLine +
'				return official;' + [Environment]::NewLine +
'			text = officialReason;' + [Environment]::NewLine +
'		}' + [Environment]::NewLine +
'		if (shiny && _options.PreferOfficialShinyEvents && _eventCatalog != null)'
  $g=$g.Replace($marker,$replacement)
}

# Direct Wonder Card -> PKM -> destination format -> legality. Fixed shiny events only.
$helper=@'
	private PKM? TryGenerateDirectOfficialShiny(ushort species, out string reason)
	{
		reason = "No hay un evento shiny oficial fijo utilizable para esta especie.";
		if (_eventCatalog is null)
			return null;

		var candidates = _eventCatalog.ForSpecies(species)
			.Where(z => z.IsFixedShiny && z.Gift is not null)
			.OrderByDescending(z => z.Generation == _sav.Generation)
			.ThenByDescending(z => z.Generation)
			.ThenBy(z => z.CardID)
			.ToArray();

		foreach (var entry in candidates)
		{
			var gift = entry.Gift!;
			if (!EventDateResolver.TryGetAnyVerifiedDate(gift, out DateOnly eventDate, out string dateReason))
			{
				reason = dateReason;
				continue;
			}

			try
			{
				PKM source = gift.ConvertToPKM(_sav, EncounterCriteria.Unrestricted, eventDate);
				if (source.Species != species || !source.IsShiny)
				{
					reason = "La Wonder Card no produjo la especie shiny esperada.";
					continue;
				}

				Type destType = _sav.BlankPKM.GetType();
				PKM? converted = source.GetType() == destType
					? source
					: EntityConverter.ConvertToType(source, destType, out _);
				if (converted is null)
				{
					reason = "PKHeX no pudo transferir el evento al formato del save.";
					continue;
				}

				_sav.AdaptToSaveFile(converted);
				var la = new LegalityAnalysis(converted, (StorageSlotType)0);
				if (!la.Valid)
				{
					reason = "El evento oficial convertido no pasa LegalityAnalysis en el save actual.";
					continue;
				}

				if (_options.StrictValidation && !StrictPokemonValidator.Validate(_sav, converted, false, out string strictReason))
				{
					reason = "Validación estricta del evento: " + strictReason;
					continue;
				}

				// Only the current save may receive a Wonder Record for a card it could actually redeem.
				if (entry.Generation == _sav.Generation && gift.IsCardCompatible(_sav, out _))
					_pendingEventReceipt = new EventReceiptPlan(gift, eventDate, entry.RelativePath);
				else
					_pendingEventReceipt = null;

				reason = entry.Generation == _sav.Generation
					? $"Evento shiny oficial actual: Card {entry.CardID:0000}."
					: $"Evento shiny oficial histórico Gen {entry.Generation}; transferido sin Wonder Card.";
				return converted;
			}
			catch (Exception ex)
			{
				reason = "Conversión del evento oficial: " + ex.GetBaseException().Message;
			}
		}

		return null;
	}

'@
$needle='	private PKM? TryGenerateForEncounter'
if ($g -notmatch 'private PKM\? TryGenerateDirectOfficialShiny') {
  $idx=$g.IndexOf($needle)
  if ($idx -ge 0) { $g=$g.Insert($idx,$helper) }
}
Set-Content $gp $g -Encoding UTF8

# Positional import: reserve exactly one physical slot per dex entry.
$fp="NaturalDexSource/NaturalDex.Plugin/NaturalDexForm.cs"
$c=Get-Content $fp -Raw
$pattern='(?s)\tprivate static bool TryBuildInsertionPlan\(SaveFile sav, GenerationBatch batch, int startBox, out List<\(int Slot, PKM Pokemon\)> plan, out string reason\)\s*\{.*?\n\t\}\n\n\tprivate static bool AuditInsertedSlots'
$replacement=@'
	private static bool TryBuildInsertionPlan(SaveFile sav, GenerationBatch batch, int startBox, out List<(int Slot, PKM Pokemon)> plan, out string reason)
	{
		plan = new List<(int Slot, PKM Pokemon)>();
		int firstSlot = (startBox - 1) * sav.BoxSlotCount;
		int totalSlots = sav.BoxCount * sav.BoxSlotCount;
		int requiredSpan = batch.Pokemon.Count;
		if (requiredSpan <= 0)
		{
			reason = "El lote no contiene posiciones para importar.";
			return false;
		}
		if (firstSlot + requiredSpan > totalSlots)
		{
			reason = $"La Living Dex necesita un tramo de {requiredSpan} slots desde la Caja {startBox}, pero no cabe en las cajas restantes.";
			return false;
		}

		for (int i = 0; i < requiredSpan; i++)
		{
			int slot = firstSlot + i;
			PKM? item = batch.Pokemon[i];
			PKM existing = sav.GetBoxSlotAtIndex(slot);
			if (existing.Species != 0)
			{
				int box = (slot / sav.BoxSlotCount) + 1;
				int boxSlot = (slot % sav.BoxSlotCount) + 1;
				reason = $"Para conservar el orden exacto, el tramo debe estar vacío. Caja {box}, slot {boxSlot} ya contiene #{existing.Species}.";
				plan.Clear();
				return false;
			}
			if (item is not null)
				plan.Add((slot, item));
			// null intentionally consumes this physical slot and leaves it empty.
		}

		reason = string.Empty;
		return plan.Count > 0;
	}

	private static bool AuditInsertedSlots
'@
$c=[regex]::Replace($c,$pattern,$replacement)
Set-Content $fp $c -Encoding UTF8

# Hard assertions so a green build cannot silently omit the fixes.
$verifyGen=Get-Content "NaturalDexSource/NaturalDex.Plugin/NaturalDexGenerator.cs" -Raw
if ($verifyGen -notmatch 'TryGenerateDirectOfficialShiny') { throw "v1.1.0 verification failed: direct shiny event resolver missing." }
if ($verifyGen -notmatch 'generationBatch\.EventReceipts\.Add\(_pendingEventReceipt\)') { throw "v1.1.0 verification failed: event receipt hook missing." }
$verifyForm=Get-Content "NaturalDexSource/NaturalDex.Plugin/NaturalDexForm.cs" -Raw
if ($verifyForm -notmatch 'int requiredSpan = batch\.Pokemon\.Count;') { throw "v1.1.0 verification failed: positional gap planner missing." }
if ($verifyForm -notmatch 'null intentionally consumes this physical slot') { throw "v1.1.0 verification failed: null gap behavior missing." }
