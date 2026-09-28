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
