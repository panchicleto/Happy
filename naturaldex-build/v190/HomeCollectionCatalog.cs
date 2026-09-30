using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal enum HomeCollectionKind
{
    ShinyOrigiDex,
    GameOriginDex,
    GenerationDex,
    StarterDex,
    ShinyStarterDex,
    FossilDex,
    ShinyFossilDex,
    PseudoLegendDex,
    ShinyPseudoLegendDex,
    BabyDex,
    ShinyBabyDex,
    LegendaryMythicalDex,
    RegionalFormDex,
    CompleteFormDex,
    VivillonDex,
    AlcremieDex,
    GenderPairDex,
    ShinyGenderPairDex,
    BallDex,
    ApriballMatrix,
    RareBallMatrix,
    EventDex,
    MythicalEventDex,
    AlphaDex,
    MarkDex,
    RibbonDex,
    GOOriginDex,
    LanguageDex,
    OTOriginDex,
    EvolutionArchive,
    UltimateHomeCollection,
}

internal enum HomeCollectionSupport
{
    Generator,
    Tracker,
    Hybrid,
}

internal sealed record HomeCollectionPreset(
    HomeCollectionKind Kind,
    string Name,
    string Category,
    HomeCollectionSupport Support,
    string Description,
    DexLayoutMode? GeneratorLayout = null,
    bool GeneratorShiny = false,
    bool ForceCurrentGameOrigin = false);

internal sealed record HomeCollectionEntry(
    int Index,
    string Group,
    ushort Species,
    sbyte Form = -1,
    sbyte Gender = -1,
    bool? Shiny = null,
    int? Ball = null,
    GameVersion? Origin = null,
    int? Language = null,
    bool? Alpha = null,
    bool RequireMark = false,
    bool RequireRibbon = false,
    bool RequireEvent = false,
    int? EventCardID = null,
    string Requirement = "",
    string Notes = "");

internal sealed record HomeCollectionStatus(
    HomeCollectionEntry Entry,
    bool Owned,
    bool SupportedInCurrentGame,
    int? BoxSlot,
    string MatchedBy);

internal sealed class HomeCollectionPlan
{
    public required HomeCollectionPreset Preset { get; init; }
    public required IReadOnlyList<HomeCollectionEntry> Entries { get; init; }
    public required IReadOnlyList<HomeCollectionStatus> Status { get; init; }
    public required string Notes { get; init; }

    public int Total => Entries.Count;
    public int Owned => Status.Count(z => z.Owned);
    public int Missing => Total - Owned;
    public int Supported => Status.Count(z => z.SupportedInCurrentGame);
    public int External => Total - Supported;
}

internal static class HomeCollectionCatalog
{
    private static readonly HomeCollectionPreset[] Presets =
    {
        new(HomeCollectionKind.ShinyOrigiDex, "Shiny OrigiDex", "Dex principales", HomeCollectionSupport.Generator,
            "Especies que debutaron en la región del juego abierto, todas shiny cuando exista una obtención legal.",
            DexLayoutMode.OrigiDexCurrentRegion, true),

        new(HomeCollectionKind.GameOriginDex, "Game Origin Dex", "Dex principales", HomeCollectionSupport.Generator,
            "Una especie de cada Pokémon disponible en el juego abierto, buscando que el origen sea ese juego y no la versión hermana.",
            DexLayoutMode.BasePlusDLC, false, true),

        new(HomeCollectionKind.GenerationDex, "Generation Dex", "Dex principales", HomeCollectionSupport.Generator,
            "Todas las especies que debutaron en la generación del save actual y que pueden existir en ese juego.",
            (DexLayoutMode)4),

        new(HomeCollectionKind.StarterDex, "Starter Collection", "Temáticas", HomeCollectionSupport.Generator,
            "Todos los starters y sus líneas evolutivas que sean compatibles con el juego abierto.",
            (DexLayoutMode)5),

        new(HomeCollectionKind.ShinyStarterDex, "Shiny Starter Collection", "Temáticas", HomeCollectionSupport.Generator,
            "La colección de starters y evoluciones en shiny legal cuando sea posible.",
            (DexLayoutMode)5, true),

        new(HomeCollectionKind.FossilDex, "Fossil Collection", "Temáticas", HomeCollectionSupport.Generator,
            "Todos los Pokémon fósil compatibles con el juego abierto.",
            (DexLayoutMode)6),

        new(HomeCollectionKind.ShinyFossilDex, "Shiny Fossil Collection", "Temáticas", HomeCollectionSupport.Generator,
            "Pokémon fósil shiny cuando exista una obtención legal.",
            (DexLayoutMode)6, true),

        new(HomeCollectionKind.PseudoLegendDex, "Pseudo-Legendary Collection", "Temáticas", HomeCollectionSupport.Generator,
            "Todas las familias pseudo-legendarias compatibles.",
            (DexLayoutMode)7),

        new(HomeCollectionKind.ShinyPseudoLegendDex, "Shiny Pseudo-Legendary Collection", "Temáticas", HomeCollectionSupport.Generator,
            "Familias pseudo-legendarias shiny.",
            (DexLayoutMode)7, true),

        new(HomeCollectionKind.BabyDex, "Baby Pokémon Collection", "Temáticas", HomeCollectionSupport.Generator,
            "Todos los Baby Pokémon compatibles.",
            (DexLayoutMode)8),

        new(HomeCollectionKind.ShinyBabyDex, "Shiny Baby Pokémon Collection", "Temáticas", HomeCollectionSupport.Generator,
            "Baby Pokémon shiny cuando sean legales.",
            (DexLayoutMode)8, true),

        new(HomeCollectionKind.LegendaryMythicalDex, "Legendary + Mythical Collection", "Temáticas", HomeCollectionSupport.Generator,
            "Legendarios y míticos compatibles con el juego abierto. Los eventos oficiales se priorizan cuando correspondan.",
            (DexLayoutMode)9),

        new(HomeCollectionKind.RegionalFormDex, "Regional FormDex", "Formas", HomeCollectionSupport.Tracker,
            "Todas las formas regionales Alola, Galar, Hisui y Paldea, sin contar especies nuevas como Sirfetch'd."),

        new(HomeCollectionKind.CompleteFormDex, "Complete FormDex", "Formas", HomeCollectionSupport.Tracker,
            "Formas persistentes y coleccionables en HOME: Unown, Rotom, Deerling, Furfrou, Oricorio, etc."),

        new(HomeCollectionKind.VivillonDex, "Vivillon Pattern Dex", "Formas", HomeCollectionSupport.Tracker,
            "Los 20 patrones de Vivillon."),

        new(HomeCollectionKind.AlcremieDex, "Alcremie Dex", "Formas", HomeCollectionSupport.Tracker,
            "Las 63 combinaciones de forma de Alcremie."),

        new(HomeCollectionKind.GenderPairDex, "Gender Pair Dex", "Variantes", HomeCollectionSupport.Tracker,
            "Un macho y una hembra de cada especie con ambos géneros posibles en el juego actual."),

        new(HomeCollectionKind.ShinyGenderPairDex, "Shiny Gender Pair Dex", "Variantes", HomeCollectionSupport.Tracker,
            "La misma colección de pares de género, pero shiny."),

        new(HomeCollectionKind.BallDex, "BallDex — Ball elegida", "Poké Balls", HomeCollectionSupport.Hybrid,
            "Una copia de cada especie compatible dentro de la Ball elegida. Cada combinación faltante debe validarse antes de generarse."),

        new(HomeCollectionKind.ApriballMatrix, "Apriball Matrix", "Poké Balls", HomeCollectionSupport.Tracker,
            "Matriz por especie con Fast, Level, Lure, Heavy, Love, Friend, Moon y Dream Ball."),

        new(HomeCollectionKind.RareBallMatrix, "Safari / Sport / Beast Matrix", "Poké Balls", HomeCollectionSupport.Tracker,
            "Matriz de Safari, Sport y Beast Ball."),

        new(HomeCollectionKind.EventDex, "EventDex", "Eventos", HomeCollectionSupport.Tracker,
            "Eventos Pokémon oficiales compatibles con el juego actual según EventsGallery."),

        new(HomeCollectionKind.MythicalEventDex, "Mythical EventDex", "Eventos", HomeCollectionSupport.Tracker,
            "Solo los eventos oficiales de Pokémon míticos compatibles con el juego actual."),

        new(HomeCollectionKind.AlphaDex, "AlphaDex", "Atributos", HomeCollectionSupport.Tracker,
            "Un Alpha por especie compatible. Se reconoce mediante el flag Alpha real del formato."),

        new(HomeCollectionKind.MarkDex, "Mark Dex", "Atributos", HomeCollectionSupport.Tracker,
            "Un ejemplar con al menos una Mark por especie."),

        new(HomeCollectionKind.RibbonDex, "Ribbon Collection", "Atributos", HomeCollectionSupport.Tracker,
            "Un ejemplar con al menos un Ribbon por especie; sirve como base para un Ribbon Master helper."),

        new(HomeCollectionKind.GOOriginDex, "GO Origin Dex", "Origen", HomeCollectionSupport.Tracker,
            "Una especie por Pokémon con GameVersion.GO como origen. No fabrica tracker de HOME."),

        new(HomeCollectionKind.LanguageDex, "Language Dex", "Origen", HomeCollectionSupport.Tracker,
            "Una copia por idioma oficial y especie compatible."),

        new(HomeCollectionKind.OTOriginDex, "OT Collection", "Origen", HomeCollectionSupport.Tracker,
            "Una especie por Pokémon cuyo OT sea el entrenador del save actual."),

        new(HomeCollectionKind.EvolutionArchive, "Evolution Family Archive", "Organización", HomeCollectionSupport.Tracker,
            "Archivo por especie pensado para ordenar después por familias evolutivas dentro de HOME."),

        new(HomeCollectionKind.UltimateHomeCollection, "Ultimate HOME Collection", "Organización", HomeCollectionSupport.Tracker,
            "Vista maestra: Living/OrigiDex + formas regionales + formas especiales + atributos de colección."),
    };

    private static readonly ushort[] Starters =
    {
        1,2,3,4,5,6,7,8,9,25,133,
        152,153,154,155,156,157,158,159,160,
        252,253,254,255,256,257,258,259,260,
        387,388,389,390,391,392,393,394,395,
        495,496,497,498,499,500,501,502,503,
        650,651,652,653,654,655,656,657,658,
        722,723,724,725,726,727,728,729,730,
        810,811,812,813,814,815,816,817,818,
        906,907,908,909,910,911,912,913,914,
    };

    private static readonly ushort[] Fossils =
    {
        138,139,140,141,142,
        345,346,347,348,
        408,409,410,411,
        564,565,566,567,
        696,697,698,699,
        880,881,882,883,
    };

    private static readonly ushort[] Pseudos =
    {
        147,148,149,246,247,248,371,372,373,374,375,376,
        443,444,445,633,634,635,704,705,706,782,783,784,
        885,886,887,996,997,998,
    };

    private static readonly ushort[] Babies =
    {
        172,173,174,175,236,238,239,240,298,360,406,433,438,439,440,446,447,458,848,
    };

    private static readonly ushort[] Mythicals =
    {
        151,251,385,386,489,490,491,492,493,494,647,648,649,719,720,721,
        801,802,807,808,809,893,1025,
    };

    private static readonly ushort[] Legendaries =
    {
        144,145,146,150,243,244,245,249,250,377,378,379,380,381,382,383,384,
        480,481,482,483,484,485,486,487,488,
        638,639,640,641,642,643,644,645,646,
        716,717,718,772,773,785,786,787,788,789,790,791,792,800,
        888,889,890,891,892,894,895,896,897,898,
        905,1001,1002,1003,1004,1005,1006,1007,1008,1009,1010,
        1014,1015,1016,1017,1020,1021,1022,1023,1024,
    };

    private static readonly (ushort Species, byte Form, string Label)[] RegionalForms =
    {
        (19,1,"Alola"),(20,1,"Alola"),(26,1,"Alola"),(27,1,"Alola"),(28,1,"Alola"),
        (37,1,"Alola"),(38,1,"Alola"),(50,1,"Alola"),(51,1,"Alola"),(52,1,"Alola"),
        (53,1,"Alola"),(74,1,"Alola"),(75,1,"Alola"),(76,1,"Alola"),(88,1,"Alola"),
        (89,1,"Alola"),(103,1,"Alola"),(105,1,"Alola"),

        (52,2,"Galar"),(77,1,"Galar"),(78,1,"Galar"),(79,1,"Galar"),(80,1,"Galar"),
        (83,1,"Galar"),(110,1,"Galar"),(122,1,"Galar"),(144,1,"Galar"),(145,1,"Galar"),
        (146,1,"Galar"),(199,1,"Galar"),(222,1,"Galar"),(263,1,"Galar"),(264,1,"Galar"),
        (554,1,"Galar"),(555,1,"Galar"),(562,1,"Galar"),(618,1,"Galar"),

        (58,1,"Hisui"),(59,1,"Hisui"),(100,1,"Hisui"),(101,1,"Hisui"),(157,1,"Hisui"),
        (211,1,"Hisui"),(215,1,"Hisui"),(503,1,"Hisui"),(549,1,"Hisui"),(570,1,"Hisui"),
        (571,1,"Hisui"),(628,1,"Hisui"),(705,1,"Hisui"),(706,1,"Hisui"),(713,1,"Hisui"),
        (724,1,"Hisui"),

        (128,1,"Paldea Combat"),(128,2,"Paldea Blaze"),(128,3,"Paldea Aqua"),(194,1,"Paldea"),
    };

    private static readonly (ushort Species, byte From, byte To, string Group)[] StableFormRanges =
    {
        (201,0,27,"Unown"),
        (386,0,3,"Deoxys"),
        (412,0,2,"Burmy"),
        (413,0,2,"Wormadam"),
        (422,0,1,"Shellos"),
        (423,0,1,"Gastrodon"),
        (479,0,5,"Rotom"),
        (550,0,2,"Basculin"),
        (585,0,3,"Deerling"),
        (586,0,3,"Sawsbuck"),
        (641,0,1,"Tornadus"),
        (642,0,1,"Thundurus"),
        (645,0,1,"Landorus"),
        (647,0,1,"Keldeo"),
        (666,0,19,"Vivillon"),
        (669,0,4,"Flabébé"),
        (670,0,4,"Floette"),
        (671,0,4,"Florges"),
        (676,0,9,"Furfrou"),
        (710,0,3,"Pumpkaboo"),
        (711,0,3,"Gourgeist"),
        (720,0,1,"Hoopa"),
        (741,0,3,"Oricorio"),
        (745,0,2,"Lycanroc"),
        (773,0,17,"Silvally"),
        (774,7,13,"Minior Core"),
        (801,0,1,"Magearna"),
        (849,0,1,"Toxtricity"),
        (854,0,1,"Sinistea"),
        (855,0,1,"Polteageist"),
        (869,0,62,"Alcremie"),
        (892,0,1,"Urshifu"),
        (893,0,1,"Zarude"),
        (916,0,1,"Oinkologne"),
        (925,0,1,"Maushold"),
        (931,0,3,"Squawkabilly"),
        (978,0,2,"Tatsugiri"),
        (982,0,1,"Dudunsparce"),
        (999,0,1,"Gimmighoul"),
        (1012,0,1,"Poltchageist"),
        (1013,0,1,"Sinistcha"),
    };

    public static IReadOnlyList<HomeCollectionPreset> GetPresets() => Presets;

    public static HomeCollectionPreset GetPreset(HomeCollectionKind kind) => Presets.First(z => z.Kind == kind);

    public static HomeCollectionPlan Build(
        SaveFile sav,
        HomeCollectionKind kind,
        Ball? selectedBall = null,
        EventGalleryCatalog? events = null)
    {
        HomeCollectionPreset preset = GetPreset(kind);
        IReadOnlyList<HomeCollectionEntry> entries = kind switch
        {
            HomeCollectionKind.ShinyOrigiDex => BuildOrigiDex(sav, true),
            HomeCollectionKind.GameOriginDex => BuildGameOrigin(sav),
            HomeCollectionKind.GenerationDex => BuildGeneration(sav),
            HomeCollectionKind.StarterDex => BuildSpeciesList(Starters, "Starter", false),
            HomeCollectionKind.ShinyStarterDex => BuildSpeciesList(Starters, "Starter", true),
            HomeCollectionKind.FossilDex => BuildSpeciesList(Fossils, "Fossil", false),
            HomeCollectionKind.ShinyFossilDex => BuildSpeciesList(Fossils, "Fossil", true),
            HomeCollectionKind.PseudoLegendDex => BuildSpeciesList(Pseudos, "Pseudo", false),
            HomeCollectionKind.ShinyPseudoLegendDex => BuildSpeciesList(Pseudos, "Pseudo", true),
            HomeCollectionKind.BabyDex => BuildSpeciesList(Babies, "Baby", false),
            HomeCollectionKind.ShinyBabyDex => BuildSpeciesList(Babies, "Baby", true),
            HomeCollectionKind.LegendaryMythicalDex => BuildLegendaryMythical(),
            HomeCollectionKind.RegionalFormDex => BuildRegionalForms(),
            HomeCollectionKind.CompleteFormDex => BuildStableForms(),
            HomeCollectionKind.VivillonDex => BuildFormRange(666, 0, 19, "Vivillon"),
            HomeCollectionKind.AlcremieDex => BuildFormRange(869, 0, 62, "Alcremie"),
            HomeCollectionKind.GenderPairDex => BuildGenderPairs(sav, false),
            HomeCollectionKind.ShinyGenderPairDex => BuildGenderPairs(sav, true),
            HomeCollectionKind.BallDex => BuildBallDex(sav, selectedBall),
            HomeCollectionKind.ApriballMatrix => BuildBallMatrix(sav, "Fast","Level","Lure","Heavy","Love","Friend","Moon","Dream"),
            HomeCollectionKind.RareBallMatrix => BuildBallMatrix(sav, "Safari","Sport","Beast"),
            HomeCollectionKind.EventDex => BuildEvents(sav, events, mythicalOnly: false),
            HomeCollectionKind.MythicalEventDex => BuildEvents(sav, events, mythicalOnly: true),
            HomeCollectionKind.AlphaDex => BuildAlphaDex(sav),
            HomeCollectionKind.MarkDex => BuildAttributeDex(sav, mark: true),
            HomeCollectionKind.RibbonDex => BuildAttributeDex(sav, ribbon: true),
            HomeCollectionKind.GOOriginDex => BuildOriginDex(sav, GameVersion.GO, "Pokémon GO"),
            HomeCollectionKind.LanguageDex => BuildLanguageDex(sav),
            HomeCollectionKind.OTOriginDex => BuildOTDex(sav),
            HomeCollectionKind.EvolutionArchive => BuildEvolutionArchive(sav),
            HomeCollectionKind.UltimateHomeCollection => BuildUltimate(sav),
            _ => Array.Empty<HomeCollectionEntry>(),
        };

        IReadOnlyList<HomeCollectionStatus> status = Scan(sav, entries);
        string notes = preset.Description;
        if (kind is HomeCollectionKind.EventDex or HomeCollectionKind.MythicalEventDex && events is null)
            notes += " EventsGallery no está cargado; actualiza/abre el catálogo de eventos para poblar esta colección.";

        return new HomeCollectionPlan
        {
            Preset = preset,
            Entries = entries,
            Status = status,
            Notes = notes,
        };
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildOrigiDex(SaveFile sav, bool shiny)
    {
        if (!OrigiDexCatalog.TryGetDefinition(sav.Version, out var d))
            return Array.Empty<HomeCollectionEntry>();

        var list = new List<HomeCollectionEntry>();
        int index = 1;
        for (ushort species = d.FirstSpecies; species <= d.LastSpecies; species++)
            list.Add(new HomeCollectionEntry(index++, d.Region, species, Shiny: shiny, Requirement: shiny ? "Shiny" : "Normal"));
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildGameOrigin(SaveFile sav)
    {
        var list = new List<HomeCollectionEntry>();
        int index = 1;
        for (ushort species = 1; species <= sav.MaxSpeciesID; species++)
        {
            if (!sav.Personal.IsSpeciesInGame(species))
                continue;
            list.Add(new HomeCollectionEntry(index++, sav.Version.ToString(), species,
                Origin: sav.Version, Requirement: $"Origen {sav.Version}"));
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildGeneration(SaveFile sav)
    {
        var (first, last) = GetGenerationRange(sav.Generation);
        var list = new List<HomeCollectionEntry>();
        int index = 1;
        for (ushort species = first; species <= last; species++)
            list.Add(new HomeCollectionEntry(index++, $"Gen {sav.Generation}", species, Requirement: $"Debut Gen {sav.Generation}"));
        return list;
    }

    private static (ushort First, ushort Last) GetGenerationRange(byte generation) => generation switch
    {
        1 => (1,151),
        2 => (152,251),
        3 => (252,386),
        4 => (387,493),
        5 => (494,649),
        6 => (650,721),
        7 => (722,809),
        8 => (810,905),
        _ => (906,1025),
    };

    private static IReadOnlyList<HomeCollectionEntry> BuildSpeciesList(IEnumerable<ushort> species, string group, bool shiny)
    {
        int i = 1;
        return species.Distinct().Select(s => new HomeCollectionEntry(i++, group, s, Shiny: shiny,
            Requirement: shiny ? $"{group} Shiny" : group)).ToArray();
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildLegendaryMythical()
    {
        int i = 1;
        var mythical = Mythicals.ToHashSet();
        return Legendaries.Concat(Mythicals).Distinct().OrderBy(z => z)
            .Select(s => new HomeCollectionEntry(i++, mythical.Contains(s) ? "Mythical" : "Legendary", s,
                Requirement: mythical.Contains(s) ? "Mítico" : "Legendario"))
            .ToArray();
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildRegionalForms()
    {
        int i = 1;
        return RegionalForms.Select(z => new HomeCollectionEntry(i++, z.Label, z.Species, Form: (sbyte)z.Form,
            Requirement: $"{z.Label} · Form {z.Form}")).ToArray();
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildStableForms()
    {
        var list = new List<HomeCollectionEntry>();
        int i = 1;
        foreach (var z in StableFormRanges)
        {
            for (byte form = z.From; form <= z.To; form++)
                list.Add(new HomeCollectionEntry(i++, z.Group, z.Species, Form: (sbyte)form,
                    Requirement: $"{z.Group} · Form {form}"));
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildFormRange(ushort species, byte from, byte to, string group)
    {
        var list = new List<HomeCollectionEntry>();
        int i = 1;
        for (byte form = from; form <= to; form++)
            list.Add(new HomeCollectionEntry(i++, group, species, Form: (sbyte)form, Requirement: $"{group} · Form {form}"));
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildGenderPairs(SaveFile sav, bool shiny)
    {
        var list = new List<HomeCollectionEntry>();
        int i = 1;
        for (ushort species = 1; species <= sav.MaxSpeciesID; species++)
        {
            if (!sav.Personal.IsSpeciesInGame(species))
                continue;

            int ratio = sav.Personal[(int)species].Gender;
            if (ratio <= 0 || ratio >= 254)
                continue;

            list.Add(new HomeCollectionEntry(i++, "Gender Pair", species, Gender: 0, Shiny: shiny,
                Requirement: shiny ? "Macho Shiny" : "Macho"));
            list.Add(new HomeCollectionEntry(i++, "Gender Pair", species, Gender: 1, Shiny: shiny,
                Requirement: shiny ? "Hembra Shiny" : "Hembra"));
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildBallDex(SaveFile sav, Ball? selected)
    {
        if (!selected.HasValue)
            return Array.Empty<HomeCollectionEntry>();

        int ball = (int)selected.Value;
        var list = new List<HomeCollectionEntry>();
        int i = 1;
        for (ushort species = 1; species <= sav.MaxSpeciesID; species++)
        {
            if (!sav.Personal.IsSpeciesInGame(species))
                continue;
            list.Add(new HomeCollectionEntry(i++, selected.Value.ToString(), species, Ball: ball,
                Requirement: selected.Value + " Ball",
                Notes: "La combinación faltante debe pasar LegalityAnalysis antes de generarse."));
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildBallMatrix(SaveFile sav, params string[] ballNames)
    {
        var balls = new List<Ball>();
        foreach (string name in ballNames)
            if (Enum.TryParse<Ball>(name, true, out var b))
                balls.Add(b);

        var list = new List<HomeCollectionEntry>();
        int i = 1;
        foreach (Ball ball in balls)
        {
            for (ushort species = 1; species <= sav.MaxSpeciesID; species++)
            {
                if (!sav.Personal.IsSpeciesInGame(species))
                    continue;
                list.Add(new HomeCollectionEntry(i++, ball.ToString(), species, Ball: (int)ball,
                    Requirement: ball + " Ball",
                    Notes: "Tracker: no asume que todas las combinaciones sean legales."));
            }
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildEvents(SaveFile sav, EventGalleryCatalog? events, bool mythicalOnly)
    {
        if (events is null)
            return Array.Empty<HomeCollectionEntry>();

        var list = new List<HomeCollectionEntry>();
        int i = 1;
        foreach (var ev in events.Entries)
        {
            if (ev.Gift is null || ev.Species == 0)
                continue;
            if (mythicalOnly && !Mythicals.Contains(ev.Species))
                continue;

            try
            {
                if (!ev.Gift.IsCardCompatible(sav, out _))
                    continue;
            }
            catch
            {
                continue;
            }

            list.Add(new HomeCollectionEntry(i++, $"Card {ev.CardID:0000}", ev.Species,
                Shiny: ev.IsFixedShiny ? true : null,
                RequireEvent: true,
                EventCardID: ev.CardID,
                Requirement: $"Evento oficial · Card {ev.CardID:0000}",
                Notes: ev.DisplayName));
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildAlphaDex(SaveFile sav)
    {
        if (sav is not SAV8LA && sav is not SAV9ZA)
            return Array.Empty<HomeCollectionEntry>();

        var list = new List<HomeCollectionEntry>();
        int i = 1;
        for (ushort species = 1; species <= sav.MaxSpeciesID; species++)
        {
            if (sav.Personal.IsSpeciesInGame(species))
                list.Add(new HomeCollectionEntry(i++, "Alpha", species, Alpha: true, Requirement: "Alpha"));
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildAttributeDex(SaveFile sav, bool mark = false, bool ribbon = false)
    {
        var list = new List<HomeCollectionEntry>();
        int i = 1;
        for (ushort species = 1; species <= sav.MaxSpeciesID; species++)
        {
            if (!sav.Personal.IsSpeciesInGame(species))
                continue;
            list.Add(new HomeCollectionEntry(i++, mark ? "Mark" : "Ribbon", species,
                RequireMark: mark, RequireRibbon: ribbon,
                Requirement: mark ? "≥1 Mark" : "≥1 Ribbon"));
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildOriginDex(SaveFile sav, GameVersion version, string label)
    {
        var list = new List<HomeCollectionEntry>();
        int i = 1;
        for (ushort species = 1; species <= sav.MaxSpeciesID; species++)
        {
            if (sav.Personal.IsSpeciesInGame(species))
                list.Add(new HomeCollectionEntry(i++, label, species, Origin: version, Requirement: $"Origen {label}"));
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildLanguageDex(SaveFile sav)
    {
        int[] languages = Enum.GetValues<LanguageID>()
            .Select(z => (int)z)
            .Where(z => z is >= 1 and <= 10 && z != 6)
            .Distinct()
            .OrderBy(z => z)
            .ToArray();

        var list = new List<HomeCollectionEntry>();
        int i = 1;
        foreach (int lang in languages)
        {
            string label = ((LanguageID)lang).ToString();
            for (ushort species = 1; species <= sav.MaxSpeciesID; species++)
            {
                if (!sav.Personal.IsSpeciesInGame(species))
                    continue;
                list.Add(new HomeCollectionEntry(i++, label, species, Language: lang,
                    Requirement: $"Idioma {label}"));
            }
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildOTDex(SaveFile sav)
    {
        var list = new List<HomeCollectionEntry>();
        int i = 1;
        for (ushort species = 1; species <= sav.MaxSpeciesID; species++)
        {
            if (sav.Personal.IsSpeciesInGame(species))
                list.Add(new HomeCollectionEntry(i++, "OT", species, Requirement: $"OT {sav.OT}",
                    Notes: "Debe coincidir con el OT del save abierto."));
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildEvolutionArchive(SaveFile sav)
    {
        var list = new List<HomeCollectionEntry>();
        int i = 1;
        for (ushort species = 1; species <= sav.MaxSpeciesID; species++)
        {
            if (sav.Personal.IsSpeciesInGame(species))
                list.Add(new HomeCollectionEntry(i++, "Evolution Archive", species,
                    Requirement: "Una copia por especie",
                    Notes: "Pensado para ordenar en HOME por familia evolutiva."));
        }
        return list;
    }

    private static IReadOnlyList<HomeCollectionEntry> BuildUltimate(SaveFile sav)
    {
        var list = new List<HomeCollectionEntry>();
        int i = 1;

        foreach (ushort species in Enumerable.Range(1, sav.MaxSpeciesID).Select(z => (ushort)z))
        {
            if (!sav.Personal.IsSpeciesInGame(species))
                continue;
            list.Add(new HomeCollectionEntry(i++, "Living", species, Requirement: "Living Dex"));
            list.Add(new HomeCollectionEntry(i++, "Shiny", species, Shiny: true, Requirement: "Shiny Dex"));
        }

        foreach (var e in BuildRegionalForms())
            list.Add(e with { Index = i++, Group = "Regional Forms" });

        foreach (var e in BuildStableForms())
            list.Add(e with { Index = i++, Group = "FormDex" });

        return list;
    }

    public static IReadOnlyList<HomeCollectionStatus> Scan(SaveFile sav, IReadOnlyList<HomeCollectionEntry> entries)
    {
        var boxes = BuildBoxIndex(sav);
        var result = new List<HomeCollectionStatus>(entries.Count);

        foreach (HomeCollectionEntry entry in entries)
        {
            BoxInfo? match = boxes.FirstOrDefault(z => Matches(z, entry, sav));
            bool owned = match is not null;
            bool supported = IsSupportedInCurrentGame(sav, entry);

            result.Add(new HomeCollectionStatus(
                entry,
                owned,
                supported,
                match?.Slot,
                owned ? match!.Describe() : supported ? "Falta" : "Requiere otro juego/formato"));
        }
        return result;
    }

    private static List<BoxInfo> BuildBoxIndex(SaveFile sav)
    {
        var list = new List<BoxInfo>();
        int total = sav.BoxCount * sav.BoxSlotCount;
        for (int slot = 0; slot < total; slot++)
        {
            PKM pk = sav.GetBoxSlotAtIndex(slot);
            if (pk.Species == 0)
                continue;

            int marks = GetIntProperty(pk, "MarkCount");
            int ribbons = GetIntProperty(pk, "RibbonCount");
            bool alpha = pk is IAlphaReadOnly a && a.IsAlpha;
            int eventCard = 0;
            bool isEvent = false;
            try
            {
                if (new LegalityAnalysis(pk, StorageSlotType.Box).EncounterMatch is MysteryGift gift)
                {
                    isEvent = true;
                    eventCard = gift.CardID;
                }
            }
            catch
            {
                // Collection tracking must never block because one stored Pokémon is malformed.
            }

            list.Add(new BoxInfo(slot, pk, marks, ribbons, alpha, isEvent, eventCard));
        }
        return list;
    }

    private static bool Matches(BoxInfo z, HomeCollectionEntry e, SaveFile sav)
    {
        PKM pk = z.Pokemon;
        if (pk.Species != e.Species)
            return false;
        if (e.Form >= 0 && pk.Form != (byte)e.Form)
            return false;
        if (e.Gender >= 0 && pk.Gender != (byte)e.Gender)
            return false;
        if (e.Shiny.HasValue && pk.IsShiny != e.Shiny.Value)
            return false;
        if (e.Ball.HasValue && pk.Ball != e.Ball.Value)
            return false;
        if (e.Origin.HasValue && pk.Version != e.Origin.Value)
            return false;
        if (e.Language.HasValue && pk.Language != e.Language.Value)
            return false;
        if (e.Alpha == true && !z.Alpha)
            return false;
        if (e.RequireMark && z.MarkCount <= 0)
            return false;
        if (e.RequireRibbon && z.RibbonCount <= 0)
            return false;
        if (e.RequireEvent && !z.IsEvent)
            return false;
        if (e.EventCardID.HasValue && z.EventCardID != e.EventCardID.Value)
            return false;
        if (e.Group == "OT" && !string.Equals(pk.OriginalTrainerName, sav.OT, StringComparison.Ordinal))
            return false;
        return true;
    }

    private static bool IsSupportedInCurrentGame(SaveFile sav, HomeCollectionEntry e)
    {
        if (e.Species == 0 || e.Species > sav.MaxSpeciesID || !sav.Personal.IsSpeciesInGame(e.Species))
            return false;

        if (e.Form >= 0)
        {
            try
            {
                int count = sav.Personal[(int)e.Species].FormCount;
                if (e.Form >= count)
                    return false;
            }
            catch
            {
                return false;
            }
        }

        if (e.Alpha == true && sav is not SAV8LA && sav is not SAV9ZA)
            return false;

        if (e.Origin.HasValue && e.Origin.Value == GameVersion.GO)
            return false; // Trackable after transfer, not generated as GO origin by NDX.

        return true;
    }

    private static int GetIntProperty(PKM pk, string property)
    {
        try
        {
            PropertyInfo? p = pk.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (p?.GetValue(pk) is int value)
                return value;
        }
        catch { }
        return 0;
    }

    private sealed record BoxInfo(
        int Slot,
        PKM Pokemon,
        int MarkCount,
        int RibbonCount,
        bool Alpha,
        bool IsEvent,
        int EventCardID)
    {
        public string Describe()
        {
            int box = (Slot / 30) + 1;
            int pos = (Slot % 30) + 1;
            return $"Caja {box}, slot {pos}";
        }
    }

    public static IReadOnlyList<ushort> GetGeneratorSpecies(DexLayoutMode layout, SaveFile sav)
    {
        IEnumerable<ushort> species = (int)layout switch
        {
            4 => BuildGeneration(sav).Select(z => z.Species),
            5 => Starters,
            6 => Fossils,
            7 => Pseudos,
            8 => Babies,
            9 => Legendaries.Concat(Mythicals),
            _ => Array.Empty<ushort>(),
        };

        return species.Distinct()
            .Where(z => z <= sav.MaxSpeciesID && sav.Personal.IsSpeciesInGame(z))
            .ToArray();
    }

    public static bool IsMythical(ushort species) => Mythicals.Contains(species);
}
