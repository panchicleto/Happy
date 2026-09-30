using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

public sealed class SwShTrainerCardGalleryForm : Form
{
    private const uint KFriendLeagueCards = 0x28E707F5;
    private const int CardSize = 0x1D0;
    private const int AlbumSlots = 300;

    private readonly SAV8SWSH _sav;
    private readonly SCBlock _album;
    private readonly FlowLayoutPanel _gallery = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        WrapContents = true,
        Padding = new Padding(10),
    };
    private readonly ComboBox _filter = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 190,
    };
    private readonly TextBox _search = new() { Width = 200 };
    private readonly Label _summary = new() { AutoSize = true, Padding = new Padding(8, 7, 8, 0) };

    public bool Edited { get; private set; }

    public SwShTrainerCardGalleryForm(SAV8SWSH sav)
    {
        _sav = sav;
        _album = sav.Blocks.GetBlock(KFriendLeagueCards);
        if (_album.Data.Length != CardSize * AlbumSlots)
            throw new InvalidDataException("El bloque KFriendLeagueCards no coincide con el álbum esperado.");

        Text = "NDX — Galería de League Cards SWSH";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1000, 700);
        Size = new Size(1200, 800);

        BuildUI();
        RefreshGallery();
    }

    private void BuildUI()
    {
        _filter.Items.AddRange(new object[]
        {
            "Todas las ocupadas",
            "Compatibles",
            "Con advertencias",
            "Sword",
            "Shield",
            "Vacías",
        });
        _filter.SelectedIndex = 0;
        _filter.SelectedIndexChanged += (_, _) => RefreshGallery();
        _search.TextChanged += (_, _) => RefreshGallery();

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            WrapContents = true,
        };
        top.Controls.Add(new Label { Text = "Filtro:", AutoSize = true, Padding = new Padding(0, 7, 4, 0) });
        top.Controls.Add(_filter);
        top.Controls.Add(new Label { Text = "Buscar:", AutoSize = true, Padding = new Padding(12, 7, 4, 0) });
        top.Controls.Add(_search);
        top.Controls.Add(_summary);

        var legend = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(12),
            MaximumSize = new Size(1150, 0),
            Text =
                "Compatible = los campos conocidos tienen valores esperados y el raw 0x1D0 puede usarse con NDX/pokeldn.  " +
                "Advertencia = NDX puede conservar/exportar la tarjeta, pero detectó uno o más valores fuera del rango esperado.  " +
                "Los datos visuales no editados se preservan byte por byte.",
        };

        Controls.Add(_gallery);
        Controls.Add(top);
        Controls.Add(legend);
    }

    private void RefreshGallery()
    {
        _gallery.SuspendLayout();
        _gallery.Controls.Clear();

        string q = (_search.Text ?? string.Empty).Trim();
        string filter = Convert.ToString(_filter.SelectedItem) ?? "Todas las ocupadas";

        int used = 0, compatible = 0, warnings = 0, empty = 0, shown = 0;

        for (int slot = 0; slot < AlbumSlots; slot++)
        {
            byte[] raw = GetSlot(slot);
            CardSupport support = Analyze(raw);
            if (support.Empty) empty++; else used++;
            if (support.Level == SupportLevel.Compatible) compatible++;
            if (support.Level == SupportLevel.Warning) warnings++;

            if (!MatchesFilter(filter, support))
                continue;
            if (!string.IsNullOrEmpty(q) &&
                !support.Name.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !support.TrainerId.ToString("000000").Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !(slot + 1).ToString().Contains(q, StringComparison.OrdinalIgnoreCase))
                continue;

            _gallery.Controls.Add(BuildCardPanel(slot, raw, support));
            shown++;
        }

        _summary.Text = $"Mostrando {shown} · Ocupadas {used}/{AlbumSlots} · Compatibles {compatible} · Advertencias {warnings} · Vacías {empty}";
        _gallery.ResumeLayout();
    }

    private static bool MatchesFilter(string filter, CardSupport s) => filter switch
    {
        "Compatibles" => s.Level == SupportLevel.Compatible,
        "Con advertencias" => s.Level == SupportLevel.Warning,
        "Sword" => !s.Empty && s.Game == 0,
        "Shield" => !s.Empty && s.Game == 1,
        "Vacías" => s.Empty,
        _ => !s.Empty,
    };

    private Control BuildCardPanel(int slot, byte[] raw, CardSupport s)
    {
        var panel = new Panel
        {
            Width = 250,
            Height = 190,
            Margin = new Padding(8),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(10),
            Tag = slot,
        };

        string title = s.Empty ? $"Slot {slot + 1:000} — VACÍO" : $"{s.Name} · {s.TrainerId:000000}";
        string game = s.Empty ? "—" : s.Game switch { 0 => "Pokémon Sword", 1 => "Pokémon Shield", _ => $"Juego {s.Game}" };
        string status = s.Level switch
        {
            SupportLevel.Compatible => "✓ Compatible NDX / pokeldn",
            SupportLevel.Warning => "⚠ Compatible con advertencias",
            _ => "— Vacía / sin tarjeta",
        };

        var titleLabel = new Label
        {
            Text = title,
            AutoEllipsis = true,
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10f, FontStyle.Bold),
            Location = new Point(10, 10),
            Size = new Size(225, 24),
        };
        var gameLabel = new Label { Text = game, Location = new Point(10, 39), Size = new Size(225, 20) };
        var slotLabel = new Label
        {
            Text = $"Slot {slot + 1:000} · N.º {s.Number} · {s.Printed}",
            Location = new Point(10, 61),
            Size = new Size(225, 20),
        };
        var mons = new Label
        {
            Text = s.Empty ? "Sin datos" : "Equipo: " + string.Join(" · ", s.ShowcaseSpecies.Select(z => z == 0 ? "—" : $"#{z}")),
            Location = new Point(10, 84),
            Size = new Size(225, 36),
        };
        var statusLabel = new Label
        {
            Text = status,
            Location = new Point(10, 122),
            Size = new Size(225, 22),
            Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
        };

        var detail = new Button
        {
            Text = s.Empty ? "Ver slot" : "DETALLE / SOPORTE",
            Location = new Point(10, 151),
            Size = new Size(130, 28),
        };
        detail.Click += (_, _) => ShowDetails(slot);

        var edit = new Button
        {
            Text = "EDITAR",
            Location = new Point(145, 151),
            Size = new Size(90, 28),
            Enabled = !s.Empty,
        };
        edit.Click += (_, _) => EditSlot(slot);

        panel.Controls.Add(titleLabel);
        panel.Controls.Add(gameLabel);
        panel.Controls.Add(slotLabel);
        panel.Controls.Add(mons);
        panel.Controls.Add(statusLabel);
        panel.Controls.Add(detail);
        panel.Controls.Add(edit);
        return panel;
    }

    private void ShowDetails(int slot)
    {
        byte[] raw = GetSlot(slot);
        CardSupport s = Analyze(raw);

        var form = new Form
        {
            Text = $"NDX — League Card · Slot {slot + 1}",
            StartPosition = FormStartPosition.CenterParent,
            MinimumSize = new Size(680, 540),
            Size = new Size(760, 620),
        };

        var box = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font(FontFamily.GenericMonospace, 9f),
            Text = BuildSupportReport(slot, s),
        };

        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(10) };
        var edit = new Button { Text = "EDITAR TARJETA", AutoSize = true, Enabled = !s.Empty };
        var export = new Button { Text = "EXPORTAR .LC8", AutoSize = true, Enabled = !s.Empty };
        var cardset = new Button { Text = "COPIAR --card-set", AutoSize = true, Enabled = !s.Empty };
        var close = new Button { Text = "Cerrar", AutoSize = true };

        edit.Click += (_, _) =>
        {
            form.Close();
            EditSlot(slot);
        };
        export.Click += (_, _) =>
        {
            using var sd = new SaveFileDialog
            {
                Filter = "SWSH League Card|*.lc8|Todos|*.*",
                FileName = $"Slot{slot + 1:000}_{Safe(s.Name)}_{s.TrainerId:000000}.lc8",
            };
            if (sd.ShowDialog(form) == DialogResult.OK)
                File.WriteAllBytes(sd.FileName, raw);
        };
        cardset.Click += (_, _) =>
        {
            Clipboard.SetText(SwShPokeLdnCardBridge.BuildCardSetArguments(raw, _sav));
            MessageBox.Show(form, "Parámetros --card-set copiados.", form.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        close.Click += (_, _) => form.Close();

        actions.Controls.Add(edit);
        actions.Controls.Add(export);
        actions.Controls.Add(cardset);
        actions.Controls.Add(close);
        form.Controls.Add(box);
        form.Controls.Add(actions);
        form.ShowDialog(this);
    }

    private string BuildSupportReport(int slot, CardSupport s)
    {
        if (s.Empty)
            return $"SLOT {slot + 1:000}\r\n\r\nVacío. No contiene una League Card.";

        var sb = new StringBuilder();
        sb.AppendLine($"SLOT {slot + 1:000}");
        sb.AppendLine($"Entrenador : {s.Name}");
        sb.AppendLine($"Card ID    : {s.TrainerId:000000}");
        sb.AppendLine($"Juego      : {(s.Game == 0 ? "Sword" : s.Game == 1 ? "Shield" : s.Game.ToString())}");
        sb.AppendLine($"N.º        : {s.Number}");
        sb.AppendLine($"Impresión  : {s.Printed}");
        sb.AppendLine();
        sb.AppendLine(s.Level == SupportLevel.Compatible
            ? "ESTADO: COMPATIBLE CON NDX / POKELDN"
            : "ESTADO: COMPATIBLE CON ADVERTENCIAS");
        sb.AppendLine();
        sb.AppendLine("SOPORTE DE CAMPOS");
        sb.AppendLine("  ✓ Raw completo 0x1D0               Importar / exportar / conservar");
        sb.AppendLine("  ✓ Nombre / idioma / Card ID         Leer + editar");
        sb.AppendLine("  ✓ Sword / Shield / starter / género Leer + editar");
        sb.AppendLine("  ✓ Estadísticas Pokédex / Curry      Leer + editar");
        sb.AppendLine("  ✓ Fecha de inicio / impresión       Leer + editar");
        sb.AppendLine("  ✓ 6 Pokémon de exhibición           Leer + editar");
        sb.AppendLine("  ✓ Snapshot pokeldn @ 0x924          Extraer + inyectar");
        sb.AppendLine("  ✓ pokeldn --card-set                Generar argumentos");
        sb.AppendLine("  ~ Apariencia / ropa / pose          Preservar bytes; no editar todavía");
        sb.AppendLine("  ~ Bytes no documentados             Preservar sin modificar");
        sb.AppendLine();
        if (s.Warnings.Count == 0)
            sb.AppendLine("Validación conocida: sin advertencias.");
        else
        {
            sb.AppendLine("ADVERTENCIAS");
            foreach (string w in s.Warnings)
                sb.AppendLine("  - " + w);
        }
        sb.AppendLine();
        sb.AppendLine("Pokémon mostrados: " + string.Join(", ", s.ShowcaseSpecies.Select((z, i) => $"P{i + 1}=#{z}")));
        return sb.ToString();
    }

    private void EditSlot(int slot)
    {
        byte[] raw = GetSlot(slot);
        if (raw.All(z => z == 0))
            return;

        using var editor = new SwShTrainerCardEditorForm(_sav, raw, $"NDX — Editar League Card · Slot {slot + 1}");
        if (editor.ShowDialog(this) != DialogResult.OK)
            return;

        byte[] edited = editor.Result;
        edited.AsSpan().CopyTo(_album.Data.Slice(slot * CardSize, CardSize));
        if (!_album.Data.Slice(slot * CardSize, CardSize).SequenceEqual(edited))
            throw new InvalidDataException($"Falló la auditoría del slot {slot + 1}.");

        Edited = true;
        _sav.State.Edited = true;
        RefreshGallery();
    }

    private byte[] GetSlot(int slot) => _album.Data.Slice(slot * CardSize, CardSize).ToArray();

    private CardSupport Analyze(byte[] raw)
    {
        if (raw.Length != CardSize)
            return new CardSupport(true, SupportLevel.Empty, "", 0, 255, "", "—", Array.Empty<int>(), new List<string> { "Tamaño incorrecto." });

        if (raw.All(z => z == 0))
            return new CardSupport(true, SupportLevel.Empty, "", 0, 255, "", "—", new int[6], new());

        var warnings = new List<string>();
        string name;
        try { name = _sav.GetString(raw.AsSpan(0, 0x1A)); }
        catch { name = "(nombre no legible)"; warnings.Add("Nombre no se pudo decodificar con el idioma del save."); }

        int trainerId = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(0x1C, 4));
        if (trainerId is < 0 or > 999999)
            warnings.Add($"Trainer Card ID fuera de 000000–999999: {trainerId}.");

        byte language = raw[0x1B];
        if (language is 0 or > 10 or 6)
            warnings.Add($"Language ID poco habitual: {language}.");

        byte game = raw[0x24];
        if (game > 1)
            warnings.Add($"Game ID no reconocido como Sword/Shield: {game}.");

        byte starter = raw[0x25];
        if (starter > 2)
            warnings.Add($"Starter fuera del rango 0–2: {starter}.");

        int year = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0x170, 2));
        int month = raw[0x172];
        int day = raw[0x173];
        bool validMonth = month is >= 1 and <= 12;
        bool validDay = validMonth && day >= 1 && day <= DateTime.DaysInMonth(Math.Clamp(year, 1, 9999), month);
        if (year != 0 && (year < 2019 || year > 2100 || !validMonth || !validDay))
            warnings.Add($"Fecha de inicio dudosa: {year:D4}-{month:D2}-{day:D2}.");

        uint ts = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x1A8, 4));
        string printed = "—";
        if (ts != 0)
        {
            try
            {
                DateTimeOffset dto = DateTimeOffset.FromUnixTimeSeconds(ts);
                printed = dto.LocalDateTime.ToString("yyyy-MM-dd");
                if (dto.Year < 2019 || dto.Year > 2100)
                    warnings.Add($"Timestamp de impresión fuera del rango esperado: {dto:yyyy-MM-dd}.");
            }
            catch
            {
                printed = $"0x{ts:X8}";
                warnings.Add("Timestamp de impresión no convertible.");
            }
        }

        string number = Encoding.ASCII.GetString(raw, 0x39, 3).TrimEnd('\0', ' ');
        int[] mons = new int[6];
        for (int i = 0; i < 6; i++)
        {
            int at = 0xC8 + i * 0x1C;
            int species = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(at, 4));
            mons[i] = species;
            if (species is < 0 or > 898)
                warnings.Add($"Pokémon {i + 1}: species {species} fuera del rango SWSH esperado.");
            int gender = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(at + 8, 4));
            if (species != 0 && (gender < 0 || gender > 2))
                warnings.Add($"Pokémon {i + 1}: gender {gender} fuera del rango esperado.");
        }

        SupportLevel level = warnings.Count == 0 ? SupportLevel.Compatible : SupportLevel.Warning;
        return new CardSupport(false, level, name, trainerId, game, number, printed, mons, warnings);
    }

    private static string Safe(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "Trainer" : value.Trim();
    }

    private enum SupportLevel { Empty, Compatible, Warning }

    private sealed record CardSupport(
        bool Empty,
        SupportLevel Level,
        string Name,
        int TrainerId,
        byte Game,
        string Number,
        string Printed,
        int[] ShowcaseSpecies,
        List<string> Warnings);
}
