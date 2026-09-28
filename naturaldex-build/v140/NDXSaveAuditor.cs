using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed record NDXAuditResult(string Summary, int Pokemon, int Legal, int Illegal, int DuplicateIdentities, int FutureDates);

internal static class NDXSaveAuditor
{
    public static NDXAuditResult Run(SaveFile sav)
    {
        var sb = new StringBuilder();
        var identities = new Dictionary<(uint PID, uint EC), List<string>>();
        var encounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var illegal = new List<string>();
        var future = new List<string>();

        int total = 0;
        int legal = 0;
        int bad = 0;
        int shiny = 0;
        int events = 0;

        sb.AppendLine("NDX Tools v1.4.0 — Save Auditor");
        sb.AppendLine(new string('=', 72));
        sb.AppendLine($"Save: {sav.GetType().Name} | Juego: {sav.Version} | Gen {sav.Generation}");
        sb.AppendLine($"Trainer: {sav.OT} | TID {sav.DisplayTID} | SID {sav.DisplaySID}");
        sb.AppendLine($"Checksums: {(sav.ChecksumsValid ? "OK" : "INVALIDOS")}");
        sb.AppendLine($"Cajas: {sav.BoxCount} × {sav.BoxSlotCount} = {sav.SlotCount} slots");
        if (sav.HasPokeDex)
            sb.AppendLine($"Pokédex: vistos {sav.SeenCount} | capturados {sav.CaughtCount}");
        sb.AppendLine();

        for (int i = 0; i < sav.SlotCount; i++)
        {
            PKM pk = sav.GetBoxSlotAtIndex(i);
            if (pk.Species == 0)
                continue;

            int box = (i / sav.BoxSlotCount) + 1;
            int slot = (i % sav.BoxSlotCount) + 1;
            AuditOne(sav, pk, $"Caja {box:00} / Slot {slot:00}", identities, encounters, illegal, future,
                ref total, ref legal, ref bad, ref shiny, ref events);
        }

        if (sav.HasParty)
        {
            for (int i = 0; i < sav.PartyData.Count; i++)
            {
                PKM pk = sav.PartyData[i];
                if (pk.Species == 0)
                    continue;
                AuditOne(sav, pk, $"Equipo / Slot {i + 1}", identities, encounters, illegal, future,
                    ref total, ref legal, ref bad, ref shiny, ref events);
            }
        }

        var duplicates = identities.Where(z => z.Value.Count > 1).ToArray();

        sb.AppendLine("RESUMEN");
        sb.AppendLine($"Pokémon analizados: {total}");
        sb.AppendLine($"Legales: {legal}");
        sb.AppendLine($"Con problemas de legalidad: {bad}");
        sb.AppendLine($"Shiny: {shiny}");
        sb.AppendLine($"Encuentros Mystery Gift detectados: {events}");
        sb.AppendLine($"PID+EC repetidos: {duplicates.Length}");
        sb.AppendLine($"Fechas de encuentro futuras: {future.Count}");
        sb.AppendLine();

        if (encounters.Count != 0)
        {
            sb.AppendLine("ORÍGENES / ENCOUNTERS");
            foreach (var pair in encounters.OrderByDescending(z => z.Value).ThenBy(z => z.Key))
                sb.AppendLine($"  {pair.Value,4}  {pair.Key}");
            sb.AppendLine();
        }

        if (!sav.ChecksumsValid)
        {
            sb.AppendLine("ADVERTENCIA — CHECKSUM");
            sb.AppendLine("El save informa checksums inválidos antes de cualquier cambio de NDX.");
            sb.AppendLine();
        }

        if (illegal.Count != 0)
        {
            sb.AppendLine("POKÉMON QUE NO PASAN LEGALITYANALYSIS");
            foreach (string line in illegal.Take(100))
                sb.AppendLine("  " + line);
            if (illegal.Count > 100)
                sb.AppendLine($"  ... {illegal.Count - 100} adicionales.");
            sb.AppendLine();
        }

        if (duplicates.Length != 0)
        {
            sb.AppendLine("IDENTIDADES PID+EC REPETIDAS");
            sb.AppendLine("Esto no se marca automáticamente como ilegal; se reporta como coherencia/sospecha de clon.");
            foreach (var pair in duplicates.Take(50))
                sb.AppendLine($"  PID {pair.Key.PID:X8} / EC {pair.Key.EC:X8}: {string.Join(", ", pair.Value)}");
            if (duplicates.Length > 50)
                sb.AppendLine($"  ... {duplicates.Length - 50} grupos adicionales.");
            sb.AppendLine();
        }

        if (future.Count != 0)
        {
            sb.AppendLine("FECHAS FUTURAS");
            sb.AppendLine("Puede ser consecuencia del reloj de la consola; se reporta como advertencia, no como ilegalidad automática.");
            foreach (string line in future.Take(50))
                sb.AppendLine("  " + line);
            if (future.Count > 50)
                sb.AppendLine($"  ... {future.Count - 50} adicionales.");
            sb.AppendLine();
        }

        sb.AppendLine("COHERENCIA NDX");
        sb.AppendLine(bad == 0
            ? "✓ Todos los Pokémon analizados pasan LegalityAnalysis."
            : "⚠ Hay Pokémon que requieren revisión individual en PKHeX.");
        sb.AppendLine(duplicates.Length == 0
            ? "✓ No se detectaron PID+EC repetidos entre los Pokémon analizados."
            : "⚠ Se detectaron identidades repetidas; revisa si son copias intencionales.");
        sb.AppendLine(sav.ChecksumsValid
            ? "✓ El save reporta checksums válidos."
            : "⚠ El save reporta checksums inválidos.");

        return new NDXAuditResult(sb.ToString(), total, legal, bad, duplicates.Length, future.Count);
    }

    private static void AuditOne(
        SaveFile sav,
        PKM pk,
        string location,
        Dictionary<(uint PID, uint EC), List<string>> identities,
        Dictionary<string, int> encounters,
        List<string> illegal,
        List<string> future,
        ref int total,
        ref int legal,
        ref int bad,
        ref int shiny,
        ref int events)
    {
        total++;
        if (pk.IsShiny)
            shiny++;

        var key = (pk.PID, pk.EncryptionConstant);
        if (!identities.TryGetValue(key, out var list))
            identities[key] = list = new List<string>();
        list.Add($"{location} (#{pk.Species})");

        LegalityAnalysis la;
        try
        {
            la = new LegalityAnalysis(pk, sav.Personal, (StorageSlotType)0);
        }
        catch (Exception ex)
        {
            bad++;
            illegal.Add($"{location}: #{pk.Species} — excepción en legalidad: {ex.GetBaseException().Message}");
            return;
        }

        string encounter = la.EncounterMatch.GetType().Name;
        encounters.TryGetValue(encounter, out int count);
        encounters[encounter] = count + 1;

        if (la.EncounterMatch is MysteryGift)
            events++;

        if (la.Valid)
            legal++;
        else
        {
            bad++;
            illegal.Add($"{location}: #{pk.Species} F{pk.Form} | {encounter} | origen {pk.Version}");
        }

        if (pk.MetDate is DateOnly met && met > DateOnly.FromDateTime(DateTime.Today))
            future.Add($"{location}: #{pk.Species} — {met:yyyy-MM-dd}");
    }
}
