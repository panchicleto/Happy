using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed record WonderInjectionResult(
    bool Success,
    SaveFile? PreviewSave,
    IReadOnlyList<string> Messages,
    int PokemonAdded,
    int RewardsApplied,
    int ReceiptsWritten)
{
    public string Summary => string.Join(Environment.NewLine, Messages);
}

internal static class WonderCardInjectionService
{
    public static WonderInjectionResult Preflight(SaveFile source, IReadOnlyList<WonderCardEntry> selected, bool strictValidation)
    {
        if (selected.Count == 0)
            return Fail("No hay eventos seleccionados.");

        SaveFile preview = source.Clone();
        var messages = new List<string>();
        int pokemon = 0;
        int rewards = 0;
        int receipts = 0;

        foreach (var entry in selected)
        {
            if (!entry.CanInject)
                return Fail($"{entry.Display}: {entry.Compatibility}", messages);

            if (!ApplyOne(preview, entry, strictValidation, out string message, out bool addedPokemon, out bool appliedReward, out bool wroteReceipt))
                return Fail($"{entry.Display}: {message}", messages);

            messages.Add($"✓ {entry.Display} — {message}");
            if (addedPokemon) pokemon++;
            if (appliedReward) rewards++;
            if (wroteReceipt) receipts++;
        }

        messages.Insert(0, $"Preflight OK: {selected.Count} evento(s), {pokemon} Pokémon, {rewards} recompensa(s), {receipts} registro(s) de Mystery Gift.");
        return new WonderInjectionResult(true, preview, messages, pokemon, rewards, receipts);

        WonderInjectionResult Fail(string reason, List<string>? prior = null)
        {
            var m = prior is null ? new List<string>() : new List<string>(prior);
            m.Add("✗ " + reason);
            return new WonderInjectionResult(false, null, m, pokemon, rewards, receipts);
        }
    }

    public static bool AuditCommitted(SaveFile sav, IReadOnlyList<WonderCardEntry> selected, out string reason)
    {
        foreach (var entry in selected)
        {
            if (entry.Kind != WonderRewardKind.Pokemon)
                continue;

            bool foundLegal = false;
            for (int i = 0; i < sav.SlotCount; i++)
            {
                var pk = sav.GetBoxSlotAtIndex(i);
                if (pk.Species != entry.Gift.Species)
                    continue;
                var la = new LegalityAnalysis(pk, (StorageSlotType)0);
                if (la.Valid)
                {
                    foundLegal = true;
                    break;
                }
            }

            if (!foundLegal)
            {
                reason = $"No se encontró un #{entry.Gift.Species} legal después del commit.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool ApplyOne(
        SaveFile sav,
        WonderCardEntry entry,
        bool strictValidation,
        out string message,
        out bool addedPokemon,
        out bool appliedReward,
        out bool wroteReceipt)
    {
        message = string.Empty;
        addedPokemon = false;
        appliedReward = false;
        wroteReceipt = false;

        var gift = entry.Gift;
        if (entry.VerifiedDate is not DateOnly date)
        {
            message = "El evento no tiene una fecha oficial verificable.";
            return false;
        }

        bool exactCardGame = IsExactCardFormatForSave(gift, sav);
        if (exactCardGame)
        {
            if (!gift.IsCardCompatible(sav, out string compatibility))
            {
                message = "PKHeX rechazó la compatibilidad de la Wonder Card: " + compatibility;
                return false;
            }

            int receiptCount = EventReceiptWriter.Apply(
                sav,
                new[] { new EventReceiptPlan(gift, date, entry.RelativePath) },
                out string receiptMessage);

            if (receiptCount <= 0)
            {
                message = string.IsNullOrWhiteSpace(receiptMessage)
                    ? "No fue posible registrar la Wonder Card; puede estar ya recibida o el layout no es compatible."
                    : receiptMessage;
                return false;
            }

            wroteReceipt = true;
        }

        if (entry.Kind == WonderRewardKind.Pokemon)
        {
            if (!ApplyPokemon(sav, entry, date, strictValidation, out message))
                return false;
            addedPokemon = true;
            if (!exactCardGame)
                message += " Evento transferido; no se creó un registro falso de Wonder Card en el juego destino.";
            return true;
        }

        if (!exactCardGame)
        {
            message = "Los regalos no-Pokémon requieren el juego exacto de la Wonder Card.";
            return false;
        }

        bool ok = entry.Kind switch
        {
            WonderRewardKind.Item => ApplyItems(sav, gift, out message),
            WonderRewardKind.Points => ApplyPoints(sav, gift, out message),
            WonderRewardKind.Clothing => ApplyClothing(sav, gift, out message),
            WonderRewardKind.Money => ApplyMoney(sav, gift, out message),
            _ => Unsupported(entry.Kind, out message),
        };

        appliedReward = ok;
        return ok;
    }

    private static bool ApplyPokemon(SaveFile sav, WonderCardEntry entry, DateOnly date, bool strictValidation, out string message)
    {
        try
        {
            var gift = entry.Gift;
            ITrainerInfo trainer = gift.Generation < sav.Generation ? CreateHistoricalTrainer(sav, gift.Generation) : sav;
            PKM source = gift.ConvertToPKM(trainer, EncounterCriteria.Unrestricted, date);
            if (source.Species != gift.Species)
            {
                message = "La Wonder Card no produjo la especie esperada.";
                return false;
            }

            Type destinationType = sav.BlankPKM.GetType();
            PKM? converted = source.GetType() == destinationType
                ? source
                : EntityConverter.ConvertToType(source, destinationType, out _);

            if (converted is null)
            {
                message = "PKHeX no pudo convertir/transferir el Pokémon al formato del save.";
                return false;
            }

            if (gift.Generation == sav.Generation)
                sav.AdaptToSaveFile(converted);

            var legality = new LegalityAnalysis(converted, (StorageSlotType)0);
            if (!legality.Valid)
            {
                message = "LegalityAnalysis rechazó el Pokémon del evento.";
                return false;
            }

            if (strictValidation && gift.Generation == sav.Generation &&
                !StrictPokemonValidator.Validate(sav, converted, false, out string strictReason))
            {
                message = "Validación estricta: " + strictReason;
                return false;
            }

            int slot = FindFirstOpenBoxSlot(sav);
            if (slot < 0)
            {
                message = "No hay espacio libre en las cajas para el Pokémon del evento.";
                return false;
            }

            sav.SetBoxSlotAtIndex(converted, slot);
            var check = sav.GetBoxSlotAtIndex(slot);
            var postLegality = new LegalityAnalysis(check, (StorageSlotType)0);
            if (check.Species != converted.Species || !postLegality.Valid)
            {
                message = "La auditoría posterior a la escritura del Pokémon falló.";
                return false;
            }

            int box = (slot / sav.BoxSlotCount) + 1;
            int boxSlot = (slot % sav.BoxSlotCount) + 1;
            message = $"Pokémon legal añadido en Caja {box}, slot {boxSlot}.";
            return true;
        }
        catch (Exception ex)
        {
            message = "Error generando el Pokémon del evento: " + ex.GetBaseException().Message;
            return false;
        }
    }

    private static ITrainerInfo CreateHistoricalTrainer(SaveFile sav, byte generation)
    {
        GameVersion version = generation switch
        {
            7 => GameVersion.SN,
            6 => GameVersion.X,
            5 => GameVersion.B,
            4 => GameVersion.D,
            _ => GameVersion.R,
        };

        var tr = new MutableTrainerInfo(version)
        {
            OT = sav.OT,
            TID16 = sav.TID16,
            SID16 = sav.SID16,
            Gender = sav.Gender,
            Language = sav.Language,
        };

        if (generation is 6 or 7)
        {
            tr.ConsoleRegion = 1;
            tr.Country = 49;
            tr.Region = 7;
        }

        return tr;
    }

    private static int FindFirstOpenBoxSlot(SaveFile sav)
    {
        for (int i = 0; i < sav.SlotCount; i++)
            if (sav.GetBoxSlotAtIndex(i).Species == 0)
                return i;
        return -1;
    }

    private static bool ApplyItems(SaveFile sav, DataMysteryGift gift, out string message)
    {
        var rewards = GetItems(gift);
        if (rewards.Count == 0)
        {
            message = "La Wonder Card no contiene objetos reconocibles.";
            return false;
        }

        foreach (var (id, qty) in rewards)
        {
            if (!GiveItemExact(sav, id, qty, out message))
                return false;
        }

        message = $"{rewards.Count} tipo(s) de objeto aplicados y verificados.";
        return true;
    }

    private static List<(ushort Item, int Quantity)> GetItems(DataMysteryGift gift)
    {
        var result = new List<(ushort, int)>();
        for (int i = 0; i < 6; i++)
        {
            int id = 0;
            int qty = 0;
            try
            {
                switch (gift)
                {
                    case WC8 w:
                        id = w.GetItem(i);
                        qty = w.GetQuantity(i);
                        break;
                    case WB8 w:
                        id = w.GetItem(i);
                        qty = w.GetQuantity(i);
                        break;
                    case WA8 w:
                        id = w.GetItem(i);
                        qty = w.GetQuantity(i);
                        break;
                    case WC9 w:
                        id = w.GetItem(i);
                        qty = w.GetQuantity(i);
                        break;
                }
            }
            catch { break; }

            if (id <= 0 || id == ushort.MaxValue)
                continue;
            result.Add(((ushort)id, Math.Max(1, qty)));
        }
        return result;
    }

    private static bool GiveItemExact(SaveFile sav, ushort item, int quantity, out string message)
    {
        if (item == 0 || item > sav.MaxItemID)
        {
            message = $"Item #{item} no existe en este juego.";
            return false;
        }

        var inventory = sav.Inventory;
        var pouch = inventory.FirstOrDefault(p => p.CanContain(item));
        if (pouch is null)
        {
            message = $"Item #{item} no pertenece a una bolsa legal de este save.";
            return false;
        }

        int before = pouch.Items.FirstOrDefault(z => z.Index == item)?.Count ?? 0;
        int result = pouch.GiveItem(sav, item, quantity);
        if (result < 0)
        {
            message = $"No hay espacio para Item #{item}.";
            return false;
        }

        if (result - before != quantity)
        {
            message = $"Item #{item}: la cantidad del evento ({quantity}) excede la capacidad legal disponible.";
            return false;
        }

        sav.Inventory = inventory;
        int after = sav.Inventory
            .SelectMany(p => p.Items)
            .Where(z => z.Index == item)
            .Select(z => z.Count)
            .DefaultIfEmpty(0)
            .Max();

        if (after < before + quantity)
        {
            message = $"Item #{item}: la auditoría de inventario falló.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool ApplyPoints(SaveFile sav, DataMysteryGift gift, out string message)
    {
        if (sav is SAV8SWSH swsh && gift is WC8 wc8)
        {
            int amount = wc8.GetItem(0);
            if (amount <= 0 || swsh.Misc.BP > ushort.MaxValue - amount)
            {
                message = "La cantidad de BP no cabe legalmente.";
                return false;
            }
            int expected = swsh.Misc.BP + amount;
            swsh.Misc.BP = expected;
            if (swsh.Misc.BP != expected)
            {
                message = "La auditoría de BP falló.";
                return false;
            }
            message = $"{amount:N0} BP añadidos.";
            return true;
        }

        if (sav is SAV9SV sv && gift is WC9 wc9)
        {
            uint amount = BinaryPrimitives.ReadUInt32LittleEndian(wc9.Data[0x18..]);
            if (amount == 0 || amount > uint.MaxValue - sv.LeaguePoints)
            {
                message = "La cantidad de LP no cabe legalmente.";
                return false;
            }
            uint expected = sv.LeaguePoints + amount;
            sv.LeaguePoints = expected;
            if (sv.LeaguePoints != expected)
            {
                message = "La auditoría de LP falló.";
                return false;
            }
            message = $"{amount:N0} LP añadidos.";
            return true;
        }

        message = "Este tipo de puntos no tiene un setter seguro verificado para el save.";
        return false;
    }

    private static bool ApplyMoney(SaveFile sav, DataMysteryGift gift, out string message)
    {
        int amount = gift switch
        {
            WC8 w => w.GetItem(0),
            WB8 w => w.GetItem(0),
            _ => 0,
        };

        if (amount <= 0)
        {
            message = "Cantidad de dinero inválida.";
            return false;
        }

        ulong expected = (ulong)sav.Money + (uint)amount;
        if (expected > (ulong)sav.MaxMoney)
        {
            message = $"La recompensa excedería el máximo legal de dinero ({sav.MaxMoney:N0}).";
            return false;
        }

        sav.Money = (uint)expected;
        if (sav.Money != (uint)expected)
        {
            message = "La auditoría de dinero falló.";
            return false;
        }

        message = $"₽{amount:N0} añadidos.";
        return true;
    }

    private static bool ApplyClothing(SaveFile sav, DataMysteryGift gift, out string message)
    {
        if (sav is SAV8SWSH swsh && gift is WC8 wc8)
            return ApplyClothingSWSH(swsh, wc8, out message);
        if (sav is SAV9SV sv && gift is WC9 wc9)
            return ApplyClothingSV(sv, wc9, out message);

        message = "El desbloqueo de ropa de este juego no tiene un adaptador seguro en esta build.";
        return false;
    }

    private static bool ApplyClothingSWSH(SAV8SWSH sav, WC8 card, out string message)
    {
        int applied = 0;
        for (int i = 0; i < 6; i++)
        {
            int ofs = 0x20 + (8 * i);
            ushort region = BinaryPrimitives.ReadUInt16LittleEndian(card.Data[ofs..]);
            ushort index = BinaryPrimitives.ReadUInt16LittleEndian(card.Data[(ofs + 4)..]);
            if (index == ushort.MaxValue)
                continue;
            if (region is < FashionUnlock8.REGION_EYEWEAR or > FashionUnlock8.REGION_FOOTWEAR)
            {
                message = $"Categoría de ropa SWSH no válida: {region}.";
                return false;
            }

            bool[] owned = sav.Fashion.GetArrayOwnedFlag(region);
            bool[] isNew = sav.Fashion.GetArrayNewFlag(region);
            if (index >= owned.Length || index >= isNew.Length)
            {
                message = $"Índice de ropa SWSH fuera de rango: {region}:{index}.";
                return false;
            }

            owned[index] = true;
            isNew[index] = true;
            sav.Fashion.SetArrayOwnedFlag(region, owned);
            sav.Fashion.SetArrayNewFlag(region, isNew);

            if (!sav.Fashion.GetArrayOwnedFlag(region)[index])
            {
                message = $"No se pudo verificar el desbloqueo de ropa {region}:{index}.";
                return false;
            }
            applied++;
        }

        if (applied == 0)
        {
            message = "La Wonder Card de ropa no contiene prendas reconocibles.";
            return false;
        }

        message = $"{applied} prenda(s)/accesorio(s) SWSH desbloqueados y verificados.";
        return true;
    }

    private static bool ApplyClothingSV(SAV9SV sav, WC9 card, out string message)
    {
        int applied = 0;
        for (int i = 0; i < 6; i++)
        {
            int ofs = 0x18 + (8 * i);
            ushort category = BinaryPrimitives.ReadUInt16LittleEndian(card.Data[ofs..]);
            ushort item = BinaryPrimitives.ReadUInt16LittleEndian(card.Data[(ofs + 4)..]);
            if (item == ushort.MaxValue)
                continue;

            if (!TryGetSVFashionKey(category, item, out uint key))
            {
                message = $"Ropa SV no válida o fuera de su categoría: {category}:{item}.";
                return false;
            }

            var block = sav.Blocks.GetBlock(key);
            bool existed = ContainsFashionItem(block.Data, item);
            int added = PlayerFashionUnlock9.Add(sav.Blocks, key, new ushort[] { item });
            bool present = ContainsFashionItem(block.Data, item);
            if (!present || (!existed && added != 1))
            {
                message = $"No se pudo verificar el desbloqueo de ropa SV {category}:{item}.";
                return false;
            }
            applied++;
        }

        if (applied == 0)
        {
            message = "La Wonder Card de ropa no contiene prendas reconocibles.";
            return false;
        }

        message = $"{applied} prenda(s)/accesorio(s) SV desbloqueados y verificados.";
        return true;
    }

    private static bool TryGetSVFashionKey(ushort category, ushort item, out uint key)
    {
        key = category switch
        {
            0 when item is >= 7000 and < 8000 => SaveBlockAccessor9SV.KFashionUnlockedClothing,
            1 when item is >= 6000 and < 7000 => SaveBlockAccessor9SV.KFashionUnlockedLegwear,
            2 when item is >= 4000 and < 5000 => SaveBlockAccessor9SV.KFashionUnlockedFootwear,
            3 when item is >= 2000 and < 3000 => SaveBlockAccessor9SV.KFashionUnlockedGloves,
            4 when item is >= 3000 and < 4000 => SaveBlockAccessor9SV.KFashionUnlockedBag,
            5 when item is >= 5000 and < 6000 => SaveBlockAccessor9SV.KFashionUnlockedHeadwear,
            6 when item is >= 1000 and < 2000 => SaveBlockAccessor9SV.KFashionUnlockedEyewear,
            8 when item is >= 8000 and < 9000 => SaveBlockAccessor9SV.KFashionUnlockedPhoneCase,
            _ => 0,
        };
        return key != 0;
    }

    private static bool ContainsFashionItem(ReadOnlySpan<byte> data, ushort item)
    {
        while (data.Length >= 8)
        {
            ushort current = BinaryPrimitives.ReadUInt16LittleEndian(data);
            if (current == ushort.MaxValue)
                return false;
            if (current == item)
                return true;
            data = data[8..];
        }
        return false;
    }

    private static bool IsExactCardFormatForSave(DataMysteryGift gift, SaveFile sav) => (gift, sav) switch
    {
        (WC8, SAV8SWSH) => true,
        (WB8, SAV8BS) => true,
        (WA8, SAV8LA) => true,
        (WC9, SAV9SV) => true,
        _ => false,
    };

    private static bool Unsupported(WonderRewardKind kind, out string message)
    {
        message = $"Tipo de regalo no soportado de forma segura: {WonderCardClassifier.GetKindText(kind)}.";
        return false;
    }
}
