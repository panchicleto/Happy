using System;
using System.IO;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed class ZAStashToolForm : Form
{
    private const uint KStoredShinyEntity = 0xF3A8569D;
    private const int Entries = 10;
    private const int EntrySize = 0x1F0;
    private const int EntityOffset = 8;

    private readonly SAV9ZA _sav;
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

    internal ZAStashToolForm(SAV9ZA sav)
    {
        _sav = sav;
        Text = "NDX — Legends Z-A Shiny Stash";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1000;
        Height = 600;

        _grid.Columns.Add("Slot", "#");
        _grid.Columns.Add("Species", "Species");
        _grid.Columns.Add("Form", "Form");
        _grid.Columns.Add("Shiny", "Shiny");
        _grid.Columns.Add("Alpha", "Alpha");
        _grid.Columns.Add("PID", "PID");
        _grid.Columns.Add("EC", "EC");
        _grid.Columns.Add("Header", "Entry Hash");
        _grid.Columns.Add("Checksum", "PA9 checksum");

        var info = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            MaximumSize = new System.Drawing.Size(940, 0),
            Text =
                "Lee las 10 posiciones del Stored Shiny Entity block de Legends Z-A. " +
                "Esta versión permite inspeccionar y extraer PA9. El mapa/teleport live permanece de solo lectura hasta validar una conexión sys-botbase.",
        };

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        var refresh = new Button { Text = "REFRESCAR", AutoSize = true };
        var export = new Button { Text = "EXTRAER PA9...", AutoSize = true };
        var close = new Button { Text = "Cerrar", AutoSize = true };
        refresh.Click += (_, _) => LoadRows();
        export.Click += (_, _) => ExportSelected();
        close.Click += (_, _) => Close();
        bottom.Controls.Add(refresh);
        bottom.Controls.Add(export);
        bottom.Controls.Add(close);

        Controls.Add(_grid);
        Controls.Add(info);
        Controls.Add(bottom);
        LoadRows();
    }

    private void LoadRows()
    {
        _grid.Rows.Clear();
        SCBlock block;
        try { block = _sav.Blocks.GetBlock(KStoredShinyEntity); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "NDX — Z-A Shiny Stash", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        int available = Math.Min(Entries, block.Data.Length / EntrySize);
        for (int i = 0; i < available; i++)
        {
            int start = i * EntrySize;
            ulong header = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(block.Data.Slice(start, 8));
            byte[] raw = block.Data.Slice(start + EntityOffset, new PA9().SIZE_STORED).ToArray();
            PA9 pk;
            try { pk = new PA9(raw); }
            catch { continue; }

            if (pk.Species == 0)
            {
                int erow = _grid.Rows.Add(i + 1, "(vacío)", "", "", "", "", "", header.ToString("X16"), "");
                _grid.Rows[erow].Tag = pk;
                continue;
            }

            int row = _grid.Rows.Add(
                i + 1,
                $"#{pk.Species} {SpeciesName(pk.Species)}",
                pk.Form,
                pk.IsShiny ? "Sí" : "No",
                pk.IsAlpha ? "Sí" : "No",
                pk.PID.ToString("X8"),
                pk.EncryptionConstant.ToString("X8"),
                header.ToString("X16"),
                pk.Valid ? "OK" : "Bad");
            _grid.Rows[row].Tag = pk;
        }
    }

    private void ExportSelected()
    {
        if (_grid.CurrentRow?.Tag is not PA9 pk || pk.Species == 0)
            return;

        using var sd = new SaveFileDialog
        {
            Filter = "PA9|*.pa9|Todos|*.*",
            FileName = $"{pk.Species:0000}_{SpeciesName(pk.Species)}_stash.pa9",
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
