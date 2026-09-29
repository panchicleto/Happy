using System;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed record SVReconstructionResult(
    bool Success,
    PK9? Pokemon,
    string Encounter,
    string Message,
    bool IdentityPreserved);

internal static class SVOverworldReconstructor
{
    internal static SVReconstructionResult Reconstruct(SAV9SV sav, PK9 raw)
    {
        if (raw.Species == 0)
            return Fail("Entrada vacía.");

        var original = raw.Clone();
        if (IsLegal(sav, original, out var originalLA))
            return new(true, original, originalLA.EncounterMatch.GetType().Name, "La entrada ya es legal.", true);

        var probe = raw.Clone();
        PrepareProbe(sav, probe);

        var encounters = EncounterMovesetGenerator
            .GenerateEncounters(probe, sav, ReadOnlyMemory<ushort>.Empty, sav.Version)
            .Where(z => z.Generation == 9 && z.Species == raw.Species)
            .Where(z => IsFormCandidate(raw, z))
            .ToArray();

        if (encounters.Length == 0)
            return Fail("PKHeX no encontró encuentros Gen 9 candidatos para esta especie/forma.");

        byte preferred = GetPreferredMetLevel(raw);

        // Pass 1: preserve the original spawn identity (PID/EC/IVs/marks/size/Tera).
        foreach (var enc in OrderCandidates(encounters, preferred))
        {
            foreach (byte level in GetLevelCandidates(enc, preferred))
            {
                PK9 candidate = raw.Clone();
                ApplyNativeProvenance(sav, candidate, enc, level);

                if (IsLegal(sav, candidate, out var la))
                {
                    return new(
                        true,
                        candidate,
                        la.EncounterMatch.GetType().Name,
                        $"Reconstrucción legal exacta: {la.EncounterMatch.Name}; ubicación {candidate.MetLocation}, nivel {candidate.MetLevel}.",
                        true);
                }
            }
        }

        // Pass 2: use PKHeX's encounter converter with visible traits constrained.
        // This is only used if the archived raw identity cannot form a legal PK9 because
        // an encounter-specific RNG correlation is missing from the archived structure.
        foreach (var enc in OrderCandidates(encounters, preferred))
        {
            if (enc is not IEncounterConvertible convertible)
                continue;

            foreach (byte level in GetLevelCandidates(enc, preferred))
            {
                var criteria = BuildCriteria(raw, level);
                PKM generated;
                try { generated = convertible.ConvertToPKM(sav, criteria); }
                catch { continue; }

                if (generated is not PK9 candidate)
                    continue;

                PreserveNonCorrelationMetadata(raw, candidate);
                candidate.CurrentLevel = Math.Max(candidate.MetLevel, raw.CurrentLevel);
                candidate.ResetPartyStats();
                candidate.RefreshChecksum();

                if (!IsLegal(sav, candidate, out var la))
                    continue;

                return new(
                    true,
                    candidate,
                    la.EncounterMatch.GetType().Name,
                    "Se encontró una reconstrucción legal, pero PKHeX tuvo que regenerar PID/EC por una correlación específica del encuentro. " +
                    $"Encuentro: {la.EncounterMatch.Name}; ubicación {candidate.MetLocation}, nivel {candidate.MetLevel}.",
                    false);
            }
        }

        return Fail(
            $"Se probaron {encounters.Length} encuentros candidatos y variaciones de nivel, pero ninguno produjo un PK9 legal. " +
            "NDX conservará la entrada raw para extracción manual.");
    }

    private static IEnumerable<IEncounterable> OrderCandidates(IEncounterable[] encounters, byte preferred)
        => encounters
            .OrderBy(z => preferred < z.LevelMin ? z.LevelMin - preferred : preferred > z.LevelMax ? preferred - z.LevelMax : 0)
            .ThenBy(z => z is EncounterSlot9 ? 0 : z is EncounterTera9 ? 1 : 2)
            .ThenBy(z => z.LevelMin);

    private static IEnumerable<byte> GetLevelCandidates(IEncounterable enc, byte preferred)
    {
        var seen = new HashSet<byte>();

        byte clipped = (byte)Math.Clamp(preferred, enc.LevelMin, enc.LevelMax);
        if (seen.Add(clipped))
            yield return clipped;

        for (int delta = 1; delta <= 10; delta++)
        {
            int low = preferred - delta;
            int high = preferred + delta;
            if (low >= enc.LevelMin && low <= enc.LevelMax && seen.Add((byte)low))
                yield return (byte)low;
            if (high >= enc.LevelMin && high <= enc.LevelMax && seen.Add((byte)high))
                yield return (byte)high;
        }

        if (seen.Add(enc.LevelMin))
            yield return enc.LevelMin;
        if (seen.Add(enc.LevelMax))
            yield return enc.LevelMax;
    }

    private static bool IsFormCandidate(PK9 raw, IEncounterable enc)
    {
        if (enc.Form == raw.Form)
            return true;
        if (enc is IEncounterFormRandom { IsRandomUnspecificForm: true })
            return true;
        return FormInfo.IsFormChangeable(raw.Species, enc.Form, raw.Form, EntityContext.Gen9, EntityContext.Gen9);
    }

    private static byte GetPreferredMetLevel(PK9 raw)
    {
        if (raw.MetLevel is > 0 and <= 100)
            return raw.MetLevel;
        int current = raw.CurrentLevel;
        return (byte)Math.Clamp(current, 1, 100);
    }

    private static void PrepareProbe(SAV9SV sav, PK9 pk)
    {
        pk.Version = sav.Version;
        pk.ID32 = sav.ID32;
        pk.OriginalTrainerName = sav.OT;
        pk.OriginalTrainerGender = sav.Gender;
        pk.Language = sav.Language;
        pk.CurrentHandler = 0;
        pk.HandlingTrainerTrash.Clear();
        pk.HandlingTrainerGender = 0;
        pk.HandlingTrainerLanguage = 0;
        if (pk.MetLevel == 0)
            pk.MetLevel = GetPreferredMetLevel(pk);
        if (pk.MetDate is null)
            pk.MetDate = EncounterDate.GetDateSwitch();
        pk.RefreshChecksum();
    }

    private static void ApplyNativeProvenance(SAV9SV sav, PK9 pk, IEncounterable enc, byte metLevel)
    {
        pk.Version = enc.Version is GameVersion.SV ? sav.Version : enc.Version;
        if (pk.Version is not (GameVersion.SL or GameVersion.VL))
            pk.Version = sav.Version;

        pk.ID32 = sav.ID32;
        pk.OriginalTrainerName = sav.OT;
        pk.OriginalTrainerGender = sav.Gender;
        pk.Language = sav.Language;
        pk.CurrentHandler = 0;
        pk.HandlingTrainerTrash.Clear();
        pk.HandlingTrainerGender = 0;
        pk.HandlingTrainerLanguage = 0;
        pk.HandlingTrainerFriendship = 0;
        pk.MetLocation = enc.Location;
        pk.MetLevel = metLevel;
        pk.ObedienceLevel = metLevel;
        pk.EggLocation = 0;
        pk.FatefulEncounter = false;

        if (pk.MetDate is null)
            pk.MetDate = EncounterDate.GetDateSwitch();

        if (pk.Ball == 0)
            pk.Ball = (byte)(enc.FixedBall is Ball.None ? Ball.Poke : enc.FixedBall);

        var pi = PersonalTable.SV.GetFormEntry(pk.Species, pk.Form);
        pk.OriginalTrainerFriendship = pi.BaseFriendship;

        if (!pk.IsNicknamed || string.IsNullOrWhiteSpace(pk.Nickname))
        {
            pk.IsNicknamed = false;
            pk.Nickname = SpeciesName.GetSpeciesNameGeneration(pk.Species, pk.Language, 9);
        }

        pk.FixMemories();
        pk.ResetPartyStats();
        pk.RefreshChecksum();
    }

    private static EncounterCriteria BuildCriteria(PK9 raw, byte level)
    {
        AbilityPermission ability = raw.AbilityNumber switch
        {
            1 => AbilityPermission.OnlyFirst,
            2 => AbilityPermission.OnlySecond,
            4 => AbilityPermission.OnlyHidden,
            _ => AbilityPermission.Any12H,
        };

        return new EncounterCriteria
        {
            Gender = raw.Gender switch
            {
                0 => Gender.Male,
                1 => Gender.Female,
                2 => Gender.Genderless,
                _ => Gender.Random,
            },
            Ability = ability,
            Nature = raw.Nature,
            Shiny = raw.IsShiny ? Shiny.Always : Shiny.Never,
            IV_HP = (sbyte)raw.IV_HP,
            IV_ATK = (sbyte)raw.IV_ATK,
            IV_DEF = (sbyte)raw.IV_DEF,
            IV_SPE = (sbyte)raw.IV_SPE,
            IV_SPA = (sbyte)raw.IV_SPA,
            IV_SPD = (sbyte)raw.IV_SPD,
            LevelMin = level,
            LevelMax = level,
        };
    }

    private static void PreserveNonCorrelationMetadata(PK9 raw, PK9 generated)
    {
        generated.Scale = raw.Scale;
        generated.HeightScalar = raw.HeightScalar;
        generated.WeightScalar = raw.WeightScalar;
        generated.TeraTypeOverride = raw.TeraTypeOverride;

        // Archived overworld data can contain encounter marks. Copy the ribbon/mark
        // flags and let LegalityAnalysis decide whether the candidate location/weather
        // is compatible. This never forces an illegal mark through.
        for (int i = 0; i < 128; i++)
            generated.SetRibbon(i, raw.GetRibbon(i));

        generated.AffixedRibbon = raw.AffixedRibbon;
        generated.RefreshChecksum();
    }

    private static bool IsLegal(SAV9SV sav, PK9 pk, out LegalityAnalysis la)
    {
        try
        {
            la = new LegalityAnalysis(pk, sav.Personal, StorageSlotType.None);
            return la.Valid;
        }
        catch
        {
            la = new LegalityAnalysis(pk);
            return false;
        }
    }

    private static SVReconstructionResult Fail(string message) => new(false, null, string.Empty, message, true);
}
