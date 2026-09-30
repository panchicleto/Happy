using System;
using System.Buffers.Binary;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

public sealed class SwShTrainerCardAlbumForm : Form
{
    private const uint KFriendLeagueCards = 0x28E707F5;
    private const uint KTrainerCard = 0x874DA6FA;
    private const int CardSize = 0x1D0;
    private const int AlbumSlots = 300;
    private const int AlbumSize = CardSize * AlbumSlots;

    private readonly SAV8SWSH _sav;
    private readonly SCBlock _album;
    private readonly SCBlock _ownCard;

    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
    };

    private readonly Label _summary = new()
    {
        AutoSize = true,
        Padding = new Padding(0, 4, 0, 8),
    };

    private readonly NumericUpDown _targetSlot = new()
    {
        Minimum = 1,
        Maximum = AlbumSlots,
        Value = 1,
        Width = 70,
    };

    public bool Edited { get; private set; }

    public SwShTrainerCardAlbumForm(SAV8SWSH sav)
    {
        _sav = sav;
        _album = sav.Blocks.GetBlock(KFriendLeagueCards);
        _ownCard = sav.Blocks.GetBlock(KTrainerCard);

        if (_album.Data.Length != AlbumSize)
            throw new InvalidDataException($"KFriendLeagueCards tiene {_album.Data.Length:X} bytes; se esperaban 0x{AlbumSize:X}.");
        if (_ownCard.Data.Length != CardSize)
            throw new InvalidDataException($"KTrainerCard tiene {_ownCard.Data.Length:X} bytes; se esperaban 0x{CardSize:X}.");

        Text = "NDX — SWSH Trainer Card Album";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 650);
        Size = new Size(1120, 760);

        BuildUI();
        RefreshGrid();
    }

    private void BuildUI()
    {
        _grid.Columns.Add("slot", "Slot");
        _grid.Columns.Add("ot", "Entrenador");
        _grid.Columns.Add("tid", "TID");
        _grid.Columns.Add("game", "Juego");
        _grid.Columns.Add("number", "N.º");
        _grid.Columns.Add("printed", "Impresión");

        _grid.Columns[0].FillWeight = 30;
        _grid.Columns[1].FillWeight = 120;
        _grid.Columns[2].FillWeight = 55;
        _grid.Columns[3].FillWeight = 55;
        _grid.Columns[4].FillWeight = 35;
        _grid.Columns[5].FillWeight = 90;

        _grid.SelectionChanged += (_, _) =>
        {
            if (_grid.SelectedRows.Count == 0)
                return;
            if (_grid.SelectedRows[0].Tag is int slot)
                _targetSlot.Value = slot + 1;
        };

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(12, 10, 12, 4),
        };

        top.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 13f, FontStyle.Bold),
            Text = "Pokémon Sword / Shield — Álbum de tarjetas de entrenador",
        });

        top.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1040, 0),
            Text =
                "El álbum de tarjetas recibidas usa 300 slots de 0x1D0 bytes. " +
                "NDX exporta cada tarjeta completa como .lc8 y el álbum entero como .lc8album. " +
                "Las importaciones se escriben sobre el clon del save y sólo se aplican al pulsar GUARDAR CAMBIOS.",
        });

        top.Controls.Add(_summary);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(10, 4, 10, 8),
        };

        AddButton(actions, "VISTA GALERÍA / SOPORTE", OpenGallery);
        AddButton(actions, "EDITAR MI TARJETA", EditOwnCard);
        AddButton(actions, "EXPORTAR MI TARJETA .LC8", ExportOwnCard);
        AddButton(actions, "COPIAR --card-set (MI TARJETA)", CopyOwnCardSet);
        AddButton(actions, "COPIAR MI TARJETA AL PRIMER HUECO", CopyOwnCardToFirstFree);
        AddButton(actions, "EDITAR SELECCIONADA", EditSelectedCard);
        AddButton(actions, "EXPORTAR SELECCIONADA .LC8", ExportSelectedCard);
        AddButton(actions, "COPIAR --card-set (SELECCIONADA)", CopySelectedCardSet);
        AddButton(actions, "EXPORTAR TODAS...", ExportAllCards);
        AddButton(actions, "BACKUP ÁLBUM .LC8ALBUM", ExportAlbum);
        AddButton(actions, "RESTAURAR ÁLBUM...", ImportAlbum);

        actions.Controls.Add(new Label
        {
            AutoSize = true,
            Padding = new Padding(10, 7, 0, 0),
            Text = "Slot destino:",
        });
        actions.Controls.Add(_targetSlot);

        AddButton(actions, "IMPORTAR .LC8 A SLOT", ImportCardToSelectedSlot);
        AddButton(actions, "IMPORTAR .LC8 AL PRIMER HUECO", ImportCardToFirstFree);
        AddButton(actions, "IMPORTAR CARPETA .LC8", ImportFolderToFreeSlots);
        AddButton(actions, "POKELDN SNAPSHOT → SLOT", ImportPokeLdnSnapshotToSlot);
        AddButton(actions, "MI TARJETA → SNAPSHOT POKELDN", InjectOwnCardIntoPokeLdnSnapshot);
        AddButton(actions, "SELECCIONADA → SNAPSHOT POKELDN", InjectSelectedIntoPokeLdnSnapshot);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10),
        };

        var save = new Button { Text = "GUARDAR CAMBIOS", AutoSize = true };
        var cancel = new Button { Text = "Cerrar sin aplicar", AutoSize = true };

        save.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        bottom.Controls.Add(save);
        bottom.Controls.Add(cancel);

        Controls.Add(_grid);
        Controls.Add(actions);
        Controls.Add(top);
        Controls.Add(bottom);
    }

    private static void AddButton(Control parent, string text, EventHandler click)
    {
        var button = new Button { Text = text, AutoSize = true };
        button.Click += click;
        parent.Controls.Add(button);
    }

    private Span<byte> GetSlotSpan(int slot)
    {
        if ((uint)slot >= AlbumSlots)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return _album.Data.Slice(slot * CardSize, CardSize);
    }

    private byte[] GetSlotBytes(int slot) => GetSlotSpan(slot).ToArray();

    private static bool IsEmpty(ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
        {
            if (b != 0)
                return false;
        }
        return true;
    }

    private int FindFirstFreeSlot()
    {
        for (int i = 0; i < AlbumSlots; i++)
        {
            if (IsEmpty(GetSlotSpan(i)))
                return i;
        }
        return -1;
    }

    private void RefreshGrid()
    {
        int selected = _grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is int s ? s : -1;
        _grid.Rows.Clear();

        int used = 0;
        for (int slot = 0; slot < AlbumSlots; slot++)
        {
            byte[] card = GetSlotBytes(slot);
            if (IsEmpty(card))
                continue;

            used++;
            CardInfo info = ParseCard(card);
            int row = _grid.Rows.Add(
                slot + 1,
                info.OT,
                info.TrainerID.ToString("000000"),
                info.Game,
                info.Number,
                info.Printed);
            _grid.Rows[row].Tag = slot;

            if (slot == selected)
                _grid.Rows[row].Selected = true;
        }

        CardInfo own = ParseCard(_ownCard.Data.ToArray());
        _summary.Text =
            $"Mi tarjeta: {own.OT} | TID {own.TrainerID:000000} | {own.Game} | N.º {own.Number}    " +
            $"Álbum: {used}/{AlbumSlots} ocupados · {AlbumSlots - used} libres.";

        if (_grid.Rows.Count > 0 && _grid.SelectedRows.Count == 0)
            _grid.Rows[0].Selected = true;
    }

    private CardInfo ParseCard(ReadOnlySpan<byte> data)
    {
        if (data.Length != CardSize)
            throw new InvalidDataException("Trainer Card inválida.");

        string ot;
        try
        {
            ot = _sav.GetString(data[..0x1A]);
        }
        catch
        {
            ot = "(nombre no legible)";
        }

        int tid = BinaryPrimitives.ReadInt32LittleEndian(data[0x1C..0x20]);
        byte game = data[0x24];
        string gameName = game switch
        {
            0 => "Sword",
            1 => "Shield",
            _ => $"Game {game}",
        };

        string number = Encoding.ASCII.GetString(data.Slice(0x39, 3)).TrimEnd('\0', ' ');
        uint timestamp = BinaryPrimitives.ReadUInt32LittleEndian(data[0x1A8..0x1AC]);
        string printed = FormatTimestamp(timestamp);

        return new CardInfo(ot, tid, gameName, number, printed);
    }

    private static string FormatTimestamp(uint value)
    {
        if (value is < 946684800 or > 4102444800)
            return value == 0 ? "—" : $"0x{value:X8}";
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(value).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
        }
        catch
        {
            return $"0x{value:X8}";
        }
    }

    private int GetSelectedSlot()
    {
        if (_grid.SelectedRows.Count > 0 && _grid.SelectedRows[0].Tag is int slot)
            return slot;
        return (int)_targetSlot.Value - 1;
    }

    private void OpenGallery(object? sender, EventArgs e)
    {
        using var gallery = new SwShTrainerCardGalleryForm(_sav);
        gallery.ShowDialog(this);
        if (gallery.Edited)
        {
            Edited = true;
            _sav.State.Edited = true;
            RefreshGrid();
        }
    }

    private void EditOwnCard(object? sender, EventArgs e)
    {
        byte[] original = _ownCard.Data.ToArray();
        using var form = new SwShTrainerCardEditorForm(_sav, original, "NDX — Editar mi Trainer Card");
        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        byte[] edited = form.Result;
        _ownCard.ChangeData(edited);
        if (!_ownCard.Data.SequenceEqual(edited))
            throw new InvalidDataException("La auditoría de mi Trainer Card falló después de escribir.");

        Edited = true;
        _sav.State.Edited = true;
        RefreshGrid();
    }

    private void EditSelectedCard(object? sender, EventArgs e)
    {
        int slot = GetSelectedSlot();
        byte[] original = GetSlotBytes(slot);
        if (IsEmpty(original))
        {
            MessageBox.Show(this, $"El slot {slot + 1} está vacío.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new SwShTrainerCardEditorForm(_sav, original, $"NDX — Editar Trainer Card · Slot {slot + 1}");
        if (form.ShowDialog(this) != DialogResult.OK)
            return;

        WriteCard(slot, form.Result);
    }

    private void CopyOwnCardSet(object? sender, EventArgs e)
    {
        Clipboard.SetText(SwShPokeLdnCardBridge.BuildCardSetArguments(_ownCard.Data, _sav));
        MessageBox.Show(this, "Parámetros --card-set de tu tarjeta copiados.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CopySelectedCardSet(object? sender, EventArgs e)
    {
        int slot = GetSelectedSlot();
        byte[] card = GetSlotBytes(slot);
        if (IsEmpty(card))
        {
            MessageBox.Show(this, $"El slot {slot + 1} está vacío.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Clipboard.SetText(SwShPokeLdnCardBridge.BuildCardSetArguments(card, _sav));
        MessageBox.Show(this, $"Parámetros --card-set del slot {slot + 1} copiados.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ImportPokeLdnSnapshotToSlot(object? sender, EventArgs e)
    {
        using var od = new OpenFileDialog
        {
            Filter = "pokeldn SWSH snapshot|*.bin;*.dat;*.snapshot|Todos|*.*",
            Title = "Selecciona un snapshot de pokeldn",
        };
        if (od.ShowDialog(this) != DialogResult.OK)
            return;

        byte[] snapshot;
        byte[] card;
        try
        {
            snapshot = SwShPokeLdnCardBridge.LoadSnapshot(od.FileName);
            card = SwShPokeLdnCardBridge.ExtractCard(snapshot);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        int slot = (int)_targetSlot.Value - 1;
        if (!IsEmpty(GetSlotSpan(slot)) &&
            MessageBox.Show(this, $"El slot {slot + 1} está ocupado. ¿Reemplazarlo con la League Card del snapshot?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        using var preview = new SwShTrainerCardEditorForm(_sav, card, $"pokeldn snapshot → Slot {slot + 1}");
        if (preview.ShowDialog(this) != DialogResult.OK)
            return;

        WriteCard(slot, preview.Result);
    }

    private void InjectOwnCardIntoPokeLdnSnapshot(object? sender, EventArgs e)
        => InjectCardIntoPokeLdnSnapshot(_ownCard.Data.ToArray(), "my-card");

    private void InjectSelectedIntoPokeLdnSnapshot(object? sender, EventArgs e)
    {
        int slot = GetSelectedSlot();
        byte[] card = GetSlotBytes(slot);
        if (IsEmpty(card))
        {
            MessageBox.Show(this, $"El slot {slot + 1} está vacío.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        InjectCardIntoPokeLdnSnapshot(card, $"slot-{slot + 1:000}");
    }

    private void InjectCardIntoPokeLdnSnapshot(byte[] card, string suffix)
    {
        using var od = new OpenFileDialog
        {
            Filter = "pokeldn SWSH snapshot|*.bin;*.dat;*.snapshot|Todos|*.*",
            Title = "Selecciona el snapshot base de pokeldn",
        };
        if (od.ShowDialog(this) != DialogResult.OK)
            return;

        byte[] snapshot;
        byte[] result;
        try
        {
            snapshot = SwShPokeLdnCardBridge.LoadSnapshot(od.FileName);
            result = SwShPokeLdnCardBridge.ReplaceCard(snapshot, card);
            byte[] verify = SwShPokeLdnCardBridge.ExtractCard(result);
            if (!verify.AsSpan().SequenceEqual(card))
                throw new InvalidDataException("La auditoría del snapshot falló después de inyectar la tarjeta.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string name = Path.GetFileNameWithoutExtension(od.FileName);
        using var sd = new SaveFileDialog
        {
            Filter = "pokeldn SWSH snapshot expandido|*.bin|Todos|*.*",
            FileName = $"{name}_{suffix}.bin",
        };
        if (sd.ShowDialog(this) != DialogResult.OK)
            return;

        File.WriteAllBytes(sd.FileName, result);
        MessageBox.Show(this,
            $"Snapshot guardado con la tarjeta inyectada.\r\n\r\nTamaño: {result.Length} bytes (formato expandido compatible con pokeldn).",
            Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportOwnCard(object? sender, EventArgs e)
    {
        CardInfo info = ParseCard(_ownCard.Data.ToArray());
        using var sd = new SaveFileDialog
        {
            Filter = "SWSH Trainer Card|*.lc8|Todos|*.*",
            FileName = BuildCardFileName("MyCard", info),
        };
        if (sd.ShowDialog(this) == DialogResult.OK)
            File.WriteAllBytes(sd.FileName, _ownCard.Data.ToArray());
    }

    private void ExportSelectedCard(object? sender, EventArgs e)
    {
        int slot = GetSelectedSlot();
        byte[] data = GetSlotBytes(slot);
        if (IsEmpty(data))
        {
            MessageBox.Show(this, $"El slot {slot + 1} está vacío.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        CardInfo info = ParseCard(data);
        using var sd = new SaveFileDialog
        {
            Filter = "SWSH Trainer Card|*.lc8|Todos|*.*",
            FileName = BuildCardFileName($"Slot{slot + 1:000}", info),
        };
        if (sd.ShowDialog(this) == DialogResult.OK)
            File.WriteAllBytes(sd.FileName, data);
    }

    private void ExportAllCards(object? sender, EventArgs e)
    {
        using var fd = new FolderBrowserDialog { Description = "Selecciona la carpeta donde exportar las tarjetas .lc8." };
        if (fd.ShowDialog(this) != DialogResult.OK)
            return;

        int count = 0;
        for (int slot = 0; slot < AlbumSlots; slot++)
        {
            byte[] data = GetSlotBytes(slot);
            if (IsEmpty(data))
                continue;

            CardInfo info = ParseCard(data);
            string file = Path.Combine(fd.SelectedPath, BuildCardFileName($"Slot{slot + 1:000}", info));
            File.WriteAllBytes(GetUniquePath(file), data);
            count++;
        }

        MessageBox.Show(this, $"Exportadas {count} tarjetas.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportAlbum(object? sender, EventArgs e)
    {
        using var sd = new SaveFileDialog
        {
            Filter = "SWSH Trainer Card Album|*.lc8album|Todos|*.*",
            FileName = "SWSH-TrainerCard-Album.lc8album",
        };
        if (sd.ShowDialog(this) == DialogResult.OK)
            File.WriteAllBytes(sd.FileName, _album.Data.ToArray());
    }

    private void ImportAlbum(object? sender, EventArgs e)
    {
        using var od = new OpenFileDialog { Filter = "SWSH Trainer Card Album|*.lc8album;*.bin|Todos|*.*" };
        if (od.ShowDialog(this) != DialogResult.OK)
            return;

        byte[] data = File.ReadAllBytes(od.FileName);
        if (data.Length != AlbumSize)
        {
            MessageBox.Show(this,
                $"Álbum inválido. Esperado 0x{AlbumSize:X} ({AlbumSize}) bytes; recibido 0x{data.Length:X} ({data.Length}).",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show(this,
                "Esto reemplazará los 300 slots del álbum en el clon actual. ¿Continuar?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _album.ChangeData(data);
        Edited = true;
        RefreshGrid();
    }

    private void CopyOwnCardToFirstFree(object? sender, EventArgs e)
    {
        int slot = FindFirstFreeSlot();
        if (slot < 0)
        {
            MessageBox.Show(this, "El álbum está lleno.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        WriteCard(slot, _ownCard.Data.ToArray());
        _targetSlot.Value = slot + 1;
    }

    private void ImportCardToSelectedSlot(object? sender, EventArgs e)
    {
        int slot = (int)_targetSlot.Value - 1;
        byte[]? data = PickCardFile();
        if (data is null)
            return;

        if (!IsEmpty(GetSlotSpan(slot)) &&
            MessageBox.Show(this,
                $"El slot {slot + 1} ya contiene una tarjeta. ¿Reemplazarla?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        WriteCard(slot, data);
    }

    private void ImportCardToFirstFree(object? sender, EventArgs e)
    {
        byte[]? data = PickCardFile();
        if (data is null)
            return;

        int slot = FindFirstFreeSlot();
        if (slot < 0)
        {
            MessageBox.Show(this, "El álbum está lleno.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        WriteCard(slot, data);
        _targetSlot.Value = slot + 1;
    }

    private void ImportFolderToFreeSlots(object? sender, EventArgs e)
    {
        using var fd = new FolderBrowserDialog { Description = "Selecciona una carpeta con archivos .lc8." };
        if (fd.ShowDialog(this) != DialogResult.OK)
            return;

        string[] files = Directory.GetFiles(fd.SelectedPath, "*.lc8", SearchOption.TopDirectoryOnly)
            .OrderBy(z => z, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            MessageBox.Show(this, "No se encontraron archivos .lc8 en esa carpeta.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int imported = 0;
        int invalid = 0;
        foreach (string file in files)
        {
            int slot = FindFirstFreeSlot();
            if (slot < 0)
                break;

            byte[] data;
            try { data = File.ReadAllBytes(file); }
            catch { invalid++; continue; }

            if (data.Length != CardSize || IsEmpty(data))
            {
                invalid++;
                continue;
            }

            WriteCard(slot, data, refresh: false);
            imported++;
        }

        RefreshGrid();
        MessageBox.Show(this,
            $"Importadas: {imported}\r\nInválidas/omitidas: {invalid}\r\nLibres restantes: {Enumerable.Range(0, AlbumSlots).Count(i => IsEmpty(GetSlotSpan(i)))}",
            Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private byte[]? PickCardFile()
    {
        using var od = new OpenFileDialog { Filter = "SWSH Trainer Card|*.lc8;*.bin|Todos|*.*" };
        if (od.ShowDialog(this) != DialogResult.OK)
            return null;

        byte[] data = File.ReadAllBytes(od.FileName);
        if (data.Length != CardSize)
        {
            MessageBox.Show(this,
                $"Tarjeta inválida. Esperado 0x{CardSize:X} ({CardSize}) bytes; recibido 0x{data.Length:X} ({data.Length}).",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (IsEmpty(data))
        {
            MessageBox.Show(this, "El archivo contiene una tarjeta vacía.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        return data;
    }

    private void WriteCard(int slot, byte[] data, bool refresh = true)
    {
        if (data.Length != CardSize)
            throw new InvalidDataException("Tamaño de Trainer Card inválido.");

        data.AsSpan().CopyTo(GetSlotSpan(slot));

        if (!GetSlotSpan(slot).SequenceEqual(data))
            throw new InvalidDataException($"La auditoría del slot {slot + 1} falló después de escribir.");

        Edited = true;
        _sav.State.Edited = true;

        if (refresh)
            RefreshGrid();
    }

    private static string BuildCardFileName(string prefix, CardInfo info)
    {
        string ot = SanitizeFileName(info.OT);
        string game = SanitizeFileName(info.Game);
        return $"{prefix}_{ot}_{info.TrainerID:000000}_{game}.lc8";
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        value = value.Trim();
        return string.IsNullOrWhiteSpace(value) ? "Trainer" : value;
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        string dir = Path.GetDirectoryName(path) ?? ".";
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        for (int i = 2; i < 10000; i++)
        {
            string candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
    }

    private readonly record struct CardInfo(string OT, int TrainerID, string Game, string Number, string Printed);
}


public sealed partial class NaturalDexForm
{
    private void OpenSwShTrainerCardAlbumTool(object? sender, EventArgs e)
    {
        if (_provider.SAV is not SAV8SWSH live)
            return;

        var work = (SAV8SWSH)live.Clone();
        using var form = new SwShTrainerCardAlbumForm(work);
        if (form.ShowDialog(this) != DialogResult.OK || !form.Edited)
            return;

        CommitSwitchClone(live, work, "SWSH Trainer Card Album");
    }
}
