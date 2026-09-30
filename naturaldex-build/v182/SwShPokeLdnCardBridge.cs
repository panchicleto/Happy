using System;
using System.Buffers.Binary;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal static class SwShPokeLdnCardBridge
{
    public const int CardSize = 0x1D0;
    public const int SnapshotLength = 3456;
    public const int ShortSnapshotLength = 2965;
    public const int ShortThirdFragmentOffset = 2808;
    public const int TrainerCardOffset = 0x924;

    public static byte[] LoadSnapshot(string path)
    {
        byte[] raw = File.ReadAllBytes(path);
        if (raw.Length == SnapshotLength)
            return raw;
        if (raw.Length != ShortSnapshotLength)
            throw new InvalidDataException(
                $"Snapshot pokeldn inválido: {raw.Length} bytes. Se esperaban {SnapshotLength} o {ShortSnapshotLength}.");

        using var input = new MemoryStream(raw, ShortThirdFragmentOffset, raw.Length - ShortThirdFragmentOffset, false);
        using var z = new ZLibStream(input, CompressionMode.Decompress);
        using var tail = new MemoryStream();
        z.CopyTo(tail);

        byte[] inflated = tail.ToArray();
        byte[] whole = new byte[ShortThirdFragmentOffset + inflated.Length];
        Buffer.BlockCopy(raw, 0, whole, 0, ShortThirdFragmentOffset);
        Buffer.BlockCopy(inflated, 0, whole, ShortThirdFragmentOffset, inflated.Length);
        if (whole.Length != SnapshotLength)
            throw new InvalidDataException(
                $"El snapshot corto se expandió a {whole.Length} bytes; se esperaban {SnapshotLength}.");
        return whole;
    }

    public static byte[] ExtractCard(ReadOnlySpan<byte> snapshot)
    {
        if (snapshot.Length != SnapshotLength)
            throw new InvalidDataException("El snapshot pokeldn debe estar expandido a 3456 bytes.");
        return snapshot.Slice(TrainerCardOffset, CardSize).ToArray();
    }

    public static byte[] ReplaceCard(ReadOnlySpan<byte> snapshot, ReadOnlySpan<byte> card)
    {
        if (snapshot.Length != SnapshotLength)
            throw new InvalidDataException("El snapshot pokeldn debe estar expandido a 3456 bytes.");
        if (card.Length != CardSize)
            throw new InvalidDataException("La League Card debe medir 0x1D0 bytes.");

        byte[] result = snapshot.ToArray();
        card.CopyTo(result.AsSpan(TrainerCardOffset, CardSize));
        return result;
    }

    public static string BuildCardSetArguments(ReadOnlySpan<byte> card, SAV8SWSH sav)
    {
        if (card.Length != CardSize)
            throw new InvalidDataException("La League Card debe medir 0x1D0 bytes.");

        static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        string name = sav.GetString(card[..0x1A]);
        int trainerId = BinaryPrimitives.ReadInt32LittleEndian(card[0x1C..0x20]);

        var sb = new StringBuilder();
        void Add(string key, string value) => sb.Append(" --card-set ").Append(key).Append('=').Append(value);

        Add("name", Q(name));
        Add("language", card[0x1B].ToString(CultureInfo.InvariantCulture));
        Add("trainer_id", trainerId.ToString(CultureInfo.InvariantCulture));
        Add("dex_owned", BinaryPrimitives.ReadUInt16LittleEndian(card[0x20..0x22]).ToString(CultureInfo.InvariantCulture));
        Add("shiny_found", BinaryPrimitives.ReadUInt16LittleEndian(card[0x22..0x24]).ToString(CultureInfo.InvariantCulture));
        Add("game", card[0x24].ToString(CultureInfo.InvariantCulture));
        Add("starter", card[0x25].ToString(CultureInfo.InvariantCulture));
        Add("curry_types", BinaryPrimitives.ReadUInt16LittleEndian(card[0x26..0x28]).ToString(CultureInfo.InvariantCulture));
        Add("roto_rally_score", BinaryPrimitives.ReadInt32LittleEndian(card[0x28..0x2C]).ToString(CultureInfo.InvariantCulture));
        Add("caught", BinaryPrimitives.ReadInt32LittleEndian(card[0x2C..0x30]).ToString(CultureInfo.InvariantCulture));
        Add("dex_complete", card[0x30].ToString(CultureInfo.InvariantCulture));
        Add("gender", card[0x38].ToString(CultureInfo.InvariantCulture));
        Add("started_year", BinaryPrimitives.ReadUInt16LittleEndian(card[0x170..0x172]).ToString(CultureInfo.InvariantCulture));
        Add("started_month", card[0x172].ToString(CultureInfo.InvariantCulture));
        Add("started_day", card[0x173].ToString(CultureInfo.InvariantCulture));
        Add("timestamp_printed", BinaryPrimitives.ReadUInt32LittleEndian(card[0x1A8..0x1AC]).ToString(CultureInfo.InvariantCulture));
        Add("armor_dex_complete", card[0x1B4].ToString(CultureInfo.InvariantCulture));
        Add("crown_dex_complete", card[0x1B5].ToString(CultureInfo.InvariantCulture));

        const int pokeBase = 0xC8;
        const int pokeSize = 0x1C;
        for (int i = 0; i < 6; i++)
        {
            ReadOnlySpan<byte> p = card.Slice(pokeBase + i * pokeSize, pokeSize);
            int n = i + 1;
            Add($"poke{n}_species", BinaryPrimitives.ReadInt32LittleEndian(p[0x00..0x04]).ToString(CultureInfo.InvariantCulture));
            Add($"poke{n}_form", BinaryPrimitives.ReadInt32LittleEndian(p[0x04..0x08]).ToString(CultureInfo.InvariantCulture));
            Add($"poke{n}_gender", BinaryPrimitives.ReadInt32LittleEndian(p[0x08..0x0C]).ToString(CultureInfo.InvariantCulture));
            Add($"poke{n}_shiny", p[0x0C].ToString(CultureInfo.InvariantCulture));
            Add($"poke{n}_ec", BinaryPrimitives.ReadUInt32LittleEndian(p[0x10..0x14]).ToString(CultureInfo.InvariantCulture));
            Add($"poke{n}_form_argument", BinaryPrimitives.ReadInt32LittleEndian(p[0x18..0x1C]).ToString(CultureInfo.InvariantCulture));
        }

        return sb.ToString().TrimStart();
    }
}

public sealed class SwShTrainerCardEditorForm : Form
{
    private readonly SAV8SWSH _sav;
    private readonly byte[] _card;

    private readonly TextBox _name = new() { Width = 160 };
    private readonly NumericUpDown _language = Num(0, 255);
    private readonly NumericUpDown _trainerId = Num(0, 999999);
    private readonly NumericUpDown _dexOwned = Num(0, 9999);
    private readonly NumericUpDown _shinyFound = Num(0, 9999);
    private readonly ComboBox _game = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly ComboBox _starter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly NumericUpDown _curry = Num(0, 9999);
    private readonly NumericUpDown _roto = Num(0, 99999);
    private readonly NumericUpDown _caught = Num(0, 99999);
    private readonly CheckBox _dexComplete = new() { Text = "Galar Dex completa", AutoSize = true };
    private readonly CheckBox _armorComplete = new() { Text = "Armor Dex completa", AutoSize = true };
    private readonly CheckBox _crownComplete = new() { Text = "Crown Dex completa", AutoSize = true };
    private readonly NumericUpDown _gender = Num(0, 255);
    private readonly TextBox _number = new() { Width = 70, MaxLength = 3 };
    private readonly DateTimePicker _started = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DateTimePicker _printed = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm:ss", Width = 175 };
    private readonly CheckBox _printedEnabled = new() { Text = "Timestamp", AutoSize = true };

    private readonly DataGridView _party = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        Height = 210,
    };

    public byte[] Result => _card.ToArray();

    public SwShTrainerCardEditorForm(SAV8SWSH sav, byte[] source, string title)
    {
        if (source.Length != SwShPokeLdnCardBridge.CardSize)
            throw new InvalidDataException("Trainer Card inválida.");

        _sav = sav;
        _card = source.ToArray();

        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(860, 650);
        Size = new Size(980, 740);

        _game.Items.AddRange(new object[] { "Sword", "Shield" });
        _starter.Items.AddRange(new object[] { "Grookey", "Scorbunny", "Sobble" });

        BuildUI();
        LoadCard();
    }

    private static NumericUpDown Num(decimal min, decimal max) => new()
    {
        Minimum = min,
        Maximum = max,
        Width = 100,
        ThousandsSeparator = false,
    };

    private void BuildUI()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4,
        };

        var basic = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            Padding = new Padding(4),
        };
        basic.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        basic.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        basic.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        basic.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        AddField(basic, 0, "Entrenador", _name);
        AddField(basic, 1, "Trainer Card ID", _trainerId);
        AddField(basic, 2, "Idioma", _language);
        AddField(basic, 3, "Juego", _game);
        AddField(basic, 4, "Starter", _starter);
        AddField(basic, 5, "Género", _gender);
        AddField(basic, 6, "N.º tarjeta", _number);
        AddField(basic, 7, "Pokédex registrados", _dexOwned);
        AddField(basic, 8, "Shinies encontrados", _shinyFound);
        AddField(basic, 9, "Pokémon capturados", _caught);
        AddField(basic, 10, "Curry tipos", _curry);
        AddField(basic, 11, "Roto Rally", _roto);
        AddField(basic, 12, "Inicio aventura", _started);

        var printPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        printPanel.Controls.Add(_printedEnabled);
        printPanel.Controls.Add(_printed);
        AddField(basic, 13, "Impresión", printPanel);

        var dexFlags = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(0, 6, 0, 6) };
        dexFlags.Controls.Add(_dexComplete);
        dexFlags.Controls.Add(_armorComplete);
        dexFlags.Controls.Add(_crownComplete);

        _party.Columns.Add("slot", "#");
        _party.Columns.Add("species", "Species");
        _party.Columns.Add("form", "Form");
        _party.Columns.Add("gender", "Gender");
        var shiny = new DataGridViewCheckBoxColumn { Name = "shiny", HeaderText = "Shiny" };
        _party.Columns.Add(shiny);
        _party.Columns.Add("ec", "EC");
        _party.Columns.Add("arg", "Form Arg.");
        _party.Columns[0].ReadOnly = true;
        _party.Columns[0].FillWeight = 25;
        for (int i = 0; i < 6; i++)
            _party.Rows.Add(i + 1, 0, 0, 0, false, 0u, -1);

        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 8, 0, 8) };
        var copyArgs = new Button { Text = "COPIAR --card-set (pokeldn)", AutoSize = true };
        var raw = new Button { Text = "EXPORTAR ESTA TARJETA .LC8", AutoSize = true };
        copyArgs.Click += (_, _) =>
        {
            SaveCard();
            Clipboard.SetText(SwShPokeLdnCardBridge.BuildCardSetArguments(_card, _sav));
            MessageBox.Show(this, "Parámetros --card-set copiados al portapapeles.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        raw.Click += (_, _) =>
        {
            SaveCard();
            using var sd = new SaveFileDialog { Filter = "SWSH League Card|*.lc8|Todos|*.*", FileName = "TrainerCard.lc8" };
            if (sd.ShowDialog(this) == DialogResult.OK)
                File.WriteAllBytes(sd.FileName, _card);
        };
        tools.Controls.Add(copyArgs);
        tools.Controls.Add(raw);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "APLICAR", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true };
        ok.Click += (_, _) =>
        {
            try
            {
                SaveCard();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.GetBaseException().Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        root.Controls.Add(basic);
        root.Controls.Add(dexFlags);
        root.Controls.Add(new GroupBox { Text = "6 Pokémon mostrados en la tarjeta", Dock = DockStyle.Fill, Controls = { _party } });
        root.Controls.Add(tools);
        Controls.Add(root);
        Controls.Add(buttons);
    }

    private static void AddField(TableLayoutPanel panel, int index, string label, Control value)
    {
        int row = index / 2;
        int pair = index % 2;
        int c = pair * 2;
        panel.RowCount = Math.Max(panel.RowCount, row + 1);
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 7, 8, 0) }, c, row);
        panel.Controls.Add(value, c + 1, row);
    }

    private void LoadCard()
    {
        _name.Text = _sav.GetString(_card.AsSpan(0, 0x1A));
        _language.Value = _card[0x1B];
        _trainerId.Value = Clamp(BinaryPrimitives.ReadInt32LittleEndian(_card.AsSpan(0x1C, 4)), _trainerId);
        _dexOwned.Value = Clamp(BinaryPrimitives.ReadUInt16LittleEndian(_card.AsSpan(0x20, 2)), _dexOwned);
        _shinyFound.Value = Clamp(BinaryPrimitives.ReadUInt16LittleEndian(_card.AsSpan(0x22, 2)), _shinyFound);
        _game.SelectedIndex = _card[0x24] <= 1 ? _card[0x24] : 0;
        _starter.SelectedIndex = _card[0x25] <= 2 ? _card[0x25] : 0;
        _curry.Value = Clamp(BinaryPrimitives.ReadUInt16LittleEndian(_card.AsSpan(0x26, 2)), _curry);
        _roto.Value = Clamp(BinaryPrimitives.ReadInt32LittleEndian(_card.AsSpan(0x28, 4)), _roto);
        _caught.Value = Clamp(BinaryPrimitives.ReadInt32LittleEndian(_card.AsSpan(0x2C, 4)), _caught);
        _dexComplete.Checked = _card[0x30] != 0;
        _gender.Value = Clamp(_card[0x38], _gender);
        _number.Text = Encoding.ASCII.GetString(_card, 0x39, 3).TrimEnd('\0', ' ');

        int year = BinaryPrimitives.ReadUInt16LittleEndian(_card.AsSpan(0x170, 2));
        int month = _card[0x172];
        int day = _card[0x173];
        if (year is >= 2000 and <= 2100 && month is >= 1 and <= 12 && day is >= 1 and <= DateTime.DaysInMonth(year, month))
            _started.Value = new DateTime(year, month, day);

        uint ts = BinaryPrimitives.ReadUInt32LittleEndian(_card.AsSpan(0x1A8, 4));
        _printedEnabled.Checked = ts != 0;
        if (ts is >= 946684800 and <= 4102444800)
            _printed.Value = DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime;

        _armorComplete.Checked = _card[0x1B4] != 0;
        _crownComplete.Checked = _card[0x1B5] != 0;

        for (int i = 0; i < 6; i++)
        {
            int at = 0xC8 + i * 0x1C;
            var row = _party.Rows[i];
            row.Cells["species"].Value = BinaryPrimitives.ReadInt32LittleEndian(_card.AsSpan(at + 0x00, 4));
            row.Cells["form"].Value = BinaryPrimitives.ReadInt32LittleEndian(_card.AsSpan(at + 0x04, 4));
            row.Cells["gender"].Value = BinaryPrimitives.ReadInt32LittleEndian(_card.AsSpan(at + 0x08, 4));
            row.Cells["shiny"].Value = _card[at + 0x0C] != 0;
            row.Cells["ec"].Value = BinaryPrimitives.ReadUInt32LittleEndian(_card.AsSpan(at + 0x10, 4));
            row.Cells["arg"].Value = BinaryPrimitives.ReadInt32LittleEndian(_card.AsSpan(at + 0x18, 4));
        }
    }

    private static decimal Clamp(long value, NumericUpDown n) => Math.Min(n.Maximum, Math.Max(n.Minimum, value));

    private void SaveCard()
    {
        _sav.SetString(_card.AsSpan(0, 0x1A), _name.Text, _sav.MaxStringLengthTrainer, StringConverterOption.ClearZero);
        _card[0x1B] = (byte)_language.Value;
        BinaryPrimitives.WriteInt32LittleEndian(_card.AsSpan(0x1C, 4), (int)_trainerId.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(_card.AsSpan(0x20, 2), (ushort)_dexOwned.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(_card.AsSpan(0x22, 2), (ushort)_shinyFound.Value);
        _card[0x24] = (byte)Math.Max(0, _game.SelectedIndex);
        _card[0x25] = (byte)Math.Max(0, _starter.SelectedIndex);
        BinaryPrimitives.WriteUInt16LittleEndian(_card.AsSpan(0x26, 2), (ushort)_curry.Value);
        BinaryPrimitives.WriteInt32LittleEndian(_card.AsSpan(0x28, 4), (int)_roto.Value);
        BinaryPrimitives.WriteInt32LittleEndian(_card.AsSpan(0x2C, 4), (int)_caught.Value);
        _card[0x30] = _dexComplete.Checked ? (byte)1 : (byte)0;
        _card[0x38] = (byte)_gender.Value;

        Span<byte> num = _card.AsSpan(0x39, 3);
        num.Clear();
        byte[] ascii = Encoding.ASCII.GetBytes(_number.Text ?? string.Empty);
        ascii.AsSpan(0, Math.Min(3, ascii.Length)).CopyTo(num);

        BinaryPrimitives.WriteUInt16LittleEndian(_card.AsSpan(0x170, 2), (ushort)_started.Value.Year);
        _card[0x172] = (byte)_started.Value.Month;
        _card[0x173] = (byte)_started.Value.Day;

        uint ts = 0;
        if (_printedEnabled.Checked)
            ts = checked((uint)new DateTimeOffset(_printed.Value).ToUnixTimeSeconds());
        BinaryPrimitives.WriteUInt32LittleEndian(_card.AsSpan(0x1A8, 4), ts);
        _card[0x1B4] = _armorComplete.Checked ? (byte)1 : (byte)0;
        _card[0x1B5] = _crownComplete.Checked ? (byte)1 : (byte)0;

        for (int i = 0; i < 6; i++)
        {
            int at = 0xC8 + i * 0x1C;
            DataGridViewRow row = _party.Rows[i];
            int species = ToInt(row.Cells["species"].Value);
            int form = ToInt(row.Cells["form"].Value);
            int gender = ToInt(row.Cells["gender"].Value);
            bool shiny = Convert.ToBoolean(row.Cells["shiny"].Value ?? false);
            uint ec = ToUInt(row.Cells["ec"].Value);
            int arg = ToInt(row.Cells["arg"].Value, -1);

            BinaryPrimitives.WriteInt32LittleEndian(_card.AsSpan(at + 0x00, 4), species);
            BinaryPrimitives.WriteInt32LittleEndian(_card.AsSpan(at + 0x04, 4), form);
            BinaryPrimitives.WriteInt32LittleEndian(_card.AsSpan(at + 0x08, 4), gender);
            _card[at + 0x0C] = shiny ? (byte)1 : (byte)0;
            BinaryPrimitives.WriteUInt32LittleEndian(_card.AsSpan(at + 0x10, 4), ec);
            BinaryPrimitives.WriteInt32LittleEndian(_card.AsSpan(at + 0x18, 4), arg);
        }
    }

    private static int ToInt(object? value, int fallback = 0)
        => int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int v) ? v : fallback;

    private static uint ToUInt(object? value)
        => uint.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out uint v) ? v : 0;
}
