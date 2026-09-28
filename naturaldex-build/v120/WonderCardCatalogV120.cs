using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal enum WonderRewardKind
{
    Unknown,
    Pokemon,
    Item,
    Points,
    Clothing,
    Money,
    Underground,
}

internal sealed record WonderCardEntry(
    string FilePath,
    string RelativePath,
    DataMysteryGift Gift,
    WonderRewardKind Kind,
    bool CanInject,
    string Compatibility,
    bool IsShiny,
    DateOnly? VerifiedDate,
    string RewardSummary)
{
    public int CardID => Gift.CardID;
    public string Title
    {
        get
        {
            try { return Gift.CardTitle; }
            catch { return "Mystery Gift"; }
        }
    }

    public string Display => $"{CardID:0000} — {Title}";
}

internal static class WonderCardClassifier
{
    public static WonderRewardKind GetKind(DataMysteryGift gift) => gift switch
    {
        WC8 w => (byte)w.CardType switch
        {
            1 => WonderRewardKind.Pokemon,
            2 => WonderRewardKind.Item,
            3 => WonderRewardKind.Points,
            4 => WonderRewardKind.Clothing,
            5 => WonderRewardKind.Money,
            _ => WonderRewardKind.Unknown,
        },
        WB8 w => (byte)w.CardType switch
        {
            1 => WonderRewardKind.Pokemon,
            2 => WonderRewardKind.Item,
            3 => WonderRewardKind.Points,
            4 => WonderRewardKind.Clothing,
            5 => WonderRewardKind.Money,
            6 => WonderRewardKind.Underground,
            _ => WonderRewardKind.Unknown,
        },
        WA8 w => (byte)w.CardType switch
        {
            1 => WonderRewardKind.Pokemon,
            2 => WonderRewardKind.Item,
            3 => WonderRewardKind.Clothing,
            _ => WonderRewardKind.Unknown,
        },
        WC9 w => (byte)w.CardType switch
        {
            1 => WonderRewardKind.Pokemon,
            2 => WonderRewardKind.Item,
            3 => WonderRewardKind.Points,
            4 => WonderRewardKind.Clothing,
            _ => WonderRewardKind.Unknown,
        },
        _ when gift.IsEntity => WonderRewardKind.Pokemon,
        _ when gift.IsItem => WonderRewardKind.Item,
        _ => WonderRewardKind.Unknown,
    };

    public static string GetKindText(WonderRewardKind kind) => kind switch
    {
        WonderRewardKind.Pokemon => "Pokémon",
        WonderRewardKind.Item => "Objeto / ítem",
        WonderRewardKind.Points => "BP / LP",
        WonderRewardKind.Clothing => "Ropa / accesorio",
        WonderRewardKind.Money => "Dinero",
        WonderRewardKind.Underground => "Subsuelo",
        _ => "Otro",
    };
}

internal static class WonderCardCatalogLoader
{
    public static List<WonderCardEntry> Load(string root, SaveFile sav, CancellationToken token)
    {
        var list = new List<WonderCardEntry>();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return list;

        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var fi = new FileInfo(path);
                if (!MysteryGift.IsMysteryGift(fi.Length))
                    continue;

                var gift = MysteryGift.GetMysteryGift(File.ReadAllBytes(path), fi.Extension) as DataMysteryGift;
                if (gift is null || gift.IsEmpty || gift.CardID <= 0)
                    continue;

                var kind = WonderCardClassifier.GetKind(gift);
                bool shiny = gift.IsEntity && gift.IsShiny;
                DateOnly? date = TryResolveReceiptDate(gift, out var d) ? d : null;
                var (canInject, compatibility) = GetCompatibility(gift, kind, sav, date.HasValue);
                string relative;
                try { relative = Path.GetRelativePath(root, path); }
                catch { relative = Path.GetFileName(path); }

                list.Add(new WonderCardEntry(
                    path,
                    relative,
                    gift,
                    kind,
                    canInject,
                    compatibility,
                    shiny,
                    date,
                    DescribeReward(gift, kind)));
            }
            catch
            {
                // A malformed or unsupported card must never block the rest of the official catalog.
            }
        }

        return list
            .OrderByDescending(z => z.CanInject)
            .ThenBy(z => z.Gift.Generation)
            .ThenBy(z => z.CardID)
            .ThenBy(z => z.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryResolveReceiptDate(DataMysteryGift gift, out DateOnly date)
    {
        if (EventDateResolver.TryGetAnyVerifiedDate(gift, out date, out _))
            return true;

        // Some Switch gifts (notably many SWSH clothing WC8 cards) are deliberately
        // not date-restricted by PKHeX.Core. Requiring a distribution window here
        // incorrectly blocks gifts that PKHeX itself considers receivable without
        // a server-date restriction. Use a valid Switch receipt date for the Wonder Record.
        if (gift is IEncounterServerDate serverDate && !serverDate.IsDateRestricted)
        {
            date = EncounterDate.GetDateSwitch();
            return true;
        }

        date = default;
        return false;
    }

    private static (bool CanInject, string Reason) GetCompatibility(DataMysteryGift gift, WonderRewardKind kind, SaveFile sav, bool hasDate)
    {
        if (kind == WonderRewardKind.Unknown)
            return (false, "Tipo de regalo todavía no soportado.");

        if (kind == WonderRewardKind.Pokemon)
        {
            if (gift.Generation > sav.Generation)
                return (false, "El evento pertenece a una generación posterior al save.");
            if (gift.Species == 0 || gift.Species > sav.MaxSpeciesID)
                return (false, "La especie no existe en el formato de este save.");
            if (!sav.Personal.IsPresentInGame(gift.Species, gift.Form))
                return (false, "La especie o forma no puede almacenarse legalmente en este juego.");
            if (!hasDate)
                return (false, "No se pudo verificar una fecha oficial para el evento.");
            return (true, gift.Generation == sav.Generation
                ? "Compatible; se validará el Pokémon y el registro antes de aplicar."
                : $"Evento histórico Gen {gift.Generation}; se transferirá y validará, sin falsificar una Wonder Card actual.");
        }

        if (!hasDate)
            return (false, "No se pudo verificar una fecha oficial para registrar el evento.");

        if (!IsExactCardFormatForSave(gift, sav))
            return (false, "Los regalos no-Pokémon sólo se aplican al juego/formato exacto de su Wonder Card.");

        if (gift is WC8 wc8 && sav is SAV8SWSH swsh && !wc8.CanBeReceivedByVersion(swsh.Version))
            return (false, $"La Wonder Card no puede recibirse en {swsh.Version}.");

        if (gift is WC9 wc9 && sav is SAV9SV sv && !CanReceiveWC9ByVersion(wc9, sv.Version))
            return (false, $"La Wonder Card no puede recibirse en {sv.Version}.");

        if (gift is WB8 && kind == WonderRewardKind.Clothing)
            return (false, "Ropa BDSP: el layout de desbloqueo no está expuesto de forma segura en esta build.");
        if (gift is WB8 && kind == WonderRewardKind.Underground)
            return (false, "Objetos de Subsuelo BDSP: pendiente de adaptador específico seguro.");
        if (gift is WB8 && kind == WonderRewardKind.Points)
            return (false, "BP de BDSP: no hay un setter público verificado en esta versión de PKHeX.Core.");
        if (gift is WA8 && kind == WonderRewardKind.Clothing)
            return (false, "Ropa de PLA: los bloques de desbloqueo no están expuestos públicamente de forma estable.");

        return (true, "Compatible; recompensa e historial se comprobarán en un clon del save.");
    }

    private static bool IsExactCardFormatForSave(DataMysteryGift gift, SaveFile sav) => (gift, sav) switch
    {
        (WC8, SAV8SWSH) => true,
        (WB8, SAV8BS) => true,
        (WA8, SAV8LA) => true,
        (WC9, SAV9SV) => true,
        _ => false,
    };

    private static bool CanReceiveWC9ByVersion(WC9 wc9, GameVersion version) => wc9.RestrictVersion switch
    {
        0 or 3 => version is GameVersion.SL or GameVersion.VL,
        1 => version is GameVersion.SL,
        2 => version is GameVersion.VL,
        _ => false,
    };

    private static string DescribeReward(DataMysteryGift gift, WonderRewardKind kind)
    {
        try
        {
            if (kind == WonderRewardKind.Pokemon)
            {
                string name;
                try { name = GameInfo.Strings.Species[gift.Species]; }
                catch { name = $"#{gift.Species}"; }
                return shinySuffix(name, gift.IsShiny);
            }

            return gift switch
            {
                WC8 w => DescribeWC8(w, kind),
                WB8 w => DescribeWB8(w, kind),
                WA8 w => DescribeWA8(w, kind),
                WC9 w => DescribeWC9(w, kind),
                _ => gift.IsItem ? $"{ItemName(gift.ItemID)} x{gift.Quantity}" : WonderCardClassifier.GetKindText(kind),
            };
        }
        catch
        {
            return WonderCardClassifier.GetKindText(kind);
        }

        static string shinySuffix(string name, bool shiny) => shiny ? $"{name} (Shiny)" : name;
    }

    private static string DescribeWC8(WC8 w, WonderRewardKind kind)
    {
        if (kind == WonderRewardKind.Points)
            return $"{w.GetItem(0)} BP";
        if (kind == WonderRewardKind.Money)
            return $"₽{w.GetItem(0):N0}";
        if (kind == WonderRewardKind.Clothing)
        {
            var parts = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                int ofs = 0x20 + (8 * i);
                ushort region = BinaryPrimitives.ReadUInt16LittleEndian(w.Data[ofs..]);
                ushort index = BinaryPrimitives.ReadUInt16LittleEndian(w.Data[(ofs + 4)..]);
                if (region == 0 || region == ushort.MaxValue || index == ushort.MaxValue)
                    continue;
                parts.Add($"ropa {region:X2}:{index}");
            }
            return parts.Count == 0 ? "Ropa / accesorio" : string.Join(", ", parts);
        }
        return DescribeItems(i => w.GetItem(i), i => w.GetQuantity(i));
    }

    private static string DescribeWB8(WB8 w, WonderRewardKind kind)
    {
        if (kind == WonderRewardKind.Points)
            return $"{w.GetItem(0)} BP";
        if (kind == WonderRewardKind.Money)
            return $"₽{w.GetItem(0):N0}";
        if (kind == WonderRewardKind.Clothing)
            return $"Conjunto de ropa #{w.GetItem(0)}";
        if (kind == WonderRewardKind.Underground)
            return $"Objeto de Subsuelo #{w.GetItem(0)} x{w.GetQuantity(0)}";
        return DescribeItems(i => w.GetItem(i), i => w.GetQuantity(i), 0x10);
    }

    private static string DescribeWA8(WA8 w, WonderRewardKind kind)
    {
        if (kind == WonderRewardKind.Clothing)
        {
            var parts = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                int ofs = 0x18 + (8 * i);
                ushort category = BinaryPrimitives.ReadUInt16LittleEndian(w.Data[ofs..]);
                ushort index = BinaryPrimitives.ReadUInt16LittleEndian(w.Data[(ofs + 4)..]);
                if (index == ushort.MaxValue)
                    continue;
                parts.Add($"ropa {category:X2}:{index}");
            }
            return parts.Count == 0 ? "Ropa / accesorio" : string.Join(", ", parts);
        }
        return DescribeItems(i => w.GetItem(i), i => w.GetQuantity(i));
    }

    private static string DescribeWC9(WC9 w, WonderRewardKind kind)
    {
        if (kind == WonderRewardKind.Points)
        {
            uint lp = BinaryPrimitives.ReadUInt32LittleEndian(w.Data[0x18..]);
            return $"{lp:N0} LP";
        }
        if (kind == WonderRewardKind.Clothing)
        {
            var parts = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                int ofs = 0x18 + (8 * i);
                ushort category = BinaryPrimitives.ReadUInt16LittleEndian(w.Data[ofs..]);
                ushort index = BinaryPrimitives.ReadUInt16LittleEndian(w.Data[(ofs + 4)..]);
                if (index == ushort.MaxValue)
                    continue;
                parts.Add($"ropa {category:X2}:{index}");
            }
            return parts.Count == 0 ? "Ropa / accesorio" : string.Join(", ", parts);
        }
        return DescribeItems(i => w.GetItem(i), i => w.GetQuantity(i));
    }

    private static string DescribeItems(Func<int, int> getItem, Func<int, int> getQuantity, int strideMarker = 0)
    {
        var parts = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            int id;
            int qty;
            try { id = getItem(i); qty = getQuantity(i); }
            catch { break; }
            if (id <= 0 || id == ushort.MaxValue)
                continue;
            parts.Add($"{ItemName(id)} x{Math.Max(1, qty)}");
        }
        return parts.Count == 0 ? "Objetos" : string.Join(", ", parts);
    }

    private static string ItemName(int id)
    {
        var items = GameInfo.Strings.Item;
        try { return !string.IsNullOrWhiteSpace(items[id]) ? items[id] : $"Item #{id}"; }
        catch { return $"Item #{id}"; }
    }
}
