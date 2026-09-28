using System;
using System.IO;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed class SVOverworldToolForm : Form
{
    private const uint KOverworld = 0x173304D8;
    private const int Entries = 20;
    private const int Extra = 0x7C;

    private readonly SAV9SV _sav;
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
    };

    internal SVOverworldToolForm(SAV9SV sav)
    {
        _sav = sav;
        Text = "NDX — Scarlet/Violet Overworld Viewer";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1000;
        Height = 600;

        _grid.Columns.Add("Slot", "#");
        _grid.Columns.Add("Species", "Species");
        _grid.Columns.Add("Form", "Form");
        _grid.Columns.Add("Shiny", "Shiny");
        _grid.Columns.Add("PID", "PID");
        _grid.Columns.Add("EC", "EC");
        _grid.Columns.Add("Level", "Met Lv.");
        _grid.Columns.Add("Version", "Origin");
        _grid.Columns.Add("Checksum", "PK9 checksum");
        _grid.Columns.Add("Legality", "Legality");

        var top = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            MaximumSize = new System.Drawing.Size(940, 0),
            Text =
                "Lee las 20 entidades almacenadas en el bloque Overworld de SV. " +
                "La extracción guarda el PK9 tal como está en el bloque; algunos campos que el juego no conserva pueden requerir reconstrucción posterior.",
        };

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        var refresh = new Button { Text = "REFRESCAR", AutoSize = true };
        var export = new Button { Text = "EXTRAER PK9...", AutoSize = true };
        var close = new Button { Text = "Cerrar", AutoSize = true };
        refresh.Click += (_, _) => LoadRows();
        export.Click += (_, _) => ExportSelected();
        close.Click += (_, _) => Close();
        bottom.Controls.Add(refresh);
        bottom.Controls.Add(export);
        bottom.Controls.Add(close);

        Controls.Add(_grid);
        Controls.Add(top);
        Controls.Add(bottom);
        LoadRows();
    }

    private void LoadRows()
    {
        _grid.Rows.Clear();
        SCBlock block;
        try { block = _sav.Blocks.GetBlock(KOverworld); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "NDX — SV Overworld", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        int entitySize = PokeCrypto.SIZE_9STORED;
        int stride = entitySize + Extra;
        int available = Math.Min(Entries, block.Data.Length / stride);
        for (int i = 0; i < available; i++)
        {
            int offset = i * stride;
            byte[] raw = block.Data.Slice(offset, entitySize).ToArray();
            PK9 pk;
            try { pk = new PK9(raw); }
            catch { continue; }

            if (pk.Species == 0)
            {
                int erow = _grid.Rows.Add(i + 1, "(vacío)", "", "", "", "", "", "", "", "");
                _grid.Rows[erow].Tag = pk;
                continue;
            }

            bool legal = false;
            try { legal = new LegalityAnalysis(pk, _sav.Personal, StorageSlotType.None).Valid; } catch { }
            int row = _grid.Rows.Add(
                i + 1,
                $"#{pk.Species} {SpeciesName(pk.Species)}",
                pk.Form,
                pk.IsShiny ? "Sí" : "No",
                pk.PID.ToString("X8"),
                pk.EncryptionConstant.ToString("X8"),
                pk.MetLevel,
                pk.Version,
                pk.Valid ? "OK" : "Bad",
                legal ? "Legal" : "Raw / revisar");
            _grid.Rows[row].Tag = pk;
        }
    }

    private void ExportSelected()
    {
        if (_grid.CurrentRow?.Tag is not PK9 pk || pk.Species == 0)
            return;

        using var sd = new SaveFileDialog
        {
            Filter = "PK9|*.pk9|Todos|*.*",
            FileName = $"{pk.Species:0000}_{SpeciesName(pk.Species)}_overworld.pk9",
        };
        if (sd.ShowDialog(this) == DialogResult.OK)
            File.WriteAllBytes(sd.FileName, pk.DecryptedBoxData);
    }

    private static string SpeciesName(ushort species)
    {
        try
        {
            var arr = GameInfo.Strings.Species;
            return species < arr.Count ? arr[species] : species.ToString();
        }
        catch { return species.ToString(); }
    }
}
