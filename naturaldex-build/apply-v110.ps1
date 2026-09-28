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
