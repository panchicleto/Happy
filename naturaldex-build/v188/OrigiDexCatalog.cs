using System;
using System.Collections.Generic;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal readonly record struct OrigiDexDefinition(
    string Region,
    ushort FirstSpecies,
    ushort LastSpecies,
    string Notes);

internal static class OrigiDexCatalog
{
    public static bool TryGetDefinition(GameVersion version, out OrigiDexDefinition definition)
    {
        definition = version switch
        {
            // Kanto
            GameVersion.RD or GameVersion.GN or GameVersion.BU or GameVersion.YW or
            GameVersion.FR or GameVersion.LG or GameVersion.GP or GameVersion.GE
                => new("Kanto", 1, 151,
                    "Solo especies que debutaron originalmente en Kanto. Las formas regionales posteriores no se incluyen."),

            // Johto
            GameVersion.GD or GameVersion.SI or GameVersion.C or
            GameVersion.HG or GameVersion.SS
                => new("Johto", 152, 251,
                    "Solo especies nuevas introducidas originalmente en Johto."),

            // Hoenn
            GameVersion.R or GameVersion.S or GameVersion.E or
            GameVersion.OR or GameVersion.AS
                => new("Hoenn", 252, 386,
                    "Solo especies nuevas introducidas originalmente en Hoenn."),

            // Sinnoh
            GameVersion.D or GameVersion.P or GameVersion.Pt or
            GameVersion.BD or GameVersion.SP
                => new("Sinnoh", 387, 493,
                    "Solo especies nuevas introducidas originalmente en Sinnoh."),

            // Unova / Teselia
            GameVersion.B or GameVersion.W or GameVersion.B2 or GameVersion.W2
                => new("Teselia / Unova", 494, 649,
                    "Victini (#494) a Genesect (#649). Incluye legendarios y míticos; el generador usa eventos oficiales cuando corresponda."),

            // Kalos
            GameVersion.X or GameVersion.Y or GameVersion.ZA
                => new("Kalos", 650, 721,
                    "Solo especies con debut original en Kalos. Legends Z-A usa esta agrupación regional para la OrigiDex."),

            // Alola
            GameVersion.SN or GameVersion.MN or GameVersion.US or GameVersion.UM
                => new("Alola", 722, 807,
                    "Strict region mode: Rowlet (#722) a Zeraora (#807). Meltan/Melmetal se excluyen porque debutaron mediante Pokémon GO, no en Alola."),

            // Galar
            GameVersion.SW or GameVersion.SH
                => new("Galar", 810, 898,
                    "Grookey (#810) a Calyrex (#898), incluyendo especies nuevas de Isle of Armor y Crown Tundra."),

            // Hisui
            GameVersion.PLA
                => new("Hisui", 899, 905,
                    "Wyrdeer (#899) a Enamorus (#905). Las formas Hisui de especies antiguas no cuentan como especies nuevas."),

            // Paldea strict
            GameVersion.SL or GameVersion.VL
                => new("Paldea", 906, 1010,
                    "Strict region mode: Sprigatito (#906) a Iron Leaves (#1010). Kitakami, Blueberry/Terarium y Pecharunt se dejan fuera de la región principal."),

            _ => default,
        };

        return !string.IsNullOrWhiteSpace(definition.Region);
    }

    public static IReadOnlyList<ushort> BuildSpecies(SaveFile sav)
    {
        if (!TryGetDefinition(sav.Version, out var definition))
            return Array.Empty<ushort>();

        int max = Math.Min(sav.MaxSpeciesID, definition.LastSpecies);
        if (max < definition.FirstSpecies)
            return Array.Empty<ushort>();

        var result = new List<ushort>(max - definition.FirstSpecies + 1);
        for (int species = definition.FirstSpecies; species <= max; species++)
        {
            // The current native-region save should be able to represent the species.
            // This also correctly trims titles released before later species from the same
            // region existed (for example base Sun/Moon vs Ultra Sun/Ultra Moon additions).
            if (!sav.Personal.IsPresentInGame((ushort)species, 0))
                continue;

            result.Add((ushort)species);
        }

        return result;
    }

    public static string GetRegionName(SaveFile sav)
        => TryGetDefinition(sav.Version, out var definition) ? definition.Region : "No compatible";

    public static string GetNotes(SaveFile sav)
        => TryGetDefinition(sav.Version, out var definition) ? definition.Notes : "No existe una OrigiDex regional configurada para este save.";
}
