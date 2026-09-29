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
        _grid.Columns.Add("Tera", "Tera");
        _grid.Columns.Add("Marks", "Marks");
        _grid.Columns.Add("Scale", "Scale");
        _grid.Columns.Add("Size", "Mini/Jumbo");
        _grid.Columns.Add("Version", "Origin");
        _grid.Columns.Add("Checksum", "PK9 checksum");
        _grid.Columns.Add("Legality", "Legality");
        _grid.Columns.Add("Reconstruction", "Reconstruction");

        var top = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            MaximumSize = new System.Drawing.Size(940, 0),
            Text =
                "Lee las 20 entidades almacenadas en el bloque Overworld de SV. NDX v1.7 puede reconstruir ubicación, nivel, OT, Ball y obedience " +
                "probando encuentros Gen 9 de PKHeX y conservando PID/EC/IVs/shiny/Tera/marks siempre que LegalityAnalysis lo permita.",
        };

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        var refresh = new Button { Text = "REFRESCAR", AutoSize = true };
        var export = new Button { Text = "EXTRAER RAW PK9...", AutoSize = true };
        var reconstruct = new Button { Text = "RECONSTRUIR + EXTRAER LEGAL...", AutoSize = true };
        var reconstructAll = new Button { Text = "AUDITAR 20 ENTRADAS", AutoSize = true };
        var close = new Button { Text = "Cerrar", AutoSize = true };
        refresh.Click += (_, _) => LoadRows();
        export.Click += (_, _) => ExportSelected();
        reconstruct.Click += (_, _) => ReconstructSelected();
        reconstructAll.Click += (_, _) => AuditAll();
        close.Click += (_, _) => Close();
        bottom.Controls.Add(refresh);
        bottom.Controls.Add(export);
        bottom.Controls.Add(reconstruct);
        bottom.Controls.Add(reconstructAll);
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

        int entitySize = new PK9().SIZE_STORED;
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
                int erow = _grid.Rows.Add(i + 1, "(vacío)", "", "", "", "", "", "", "", "", "", "", "", "", "");
                _grid.Rows[erow].Tag = pk;
                continue;
            }

            bool legal = false;
            try { legal = new LegalityAnalysis(pk, _sav.Personal, StorageSlotType.None).Valid; } catch { }
            var reconstructed = legal ? new SVReconstructionResult(true, pk.Clone(), "AlreadyLegal", "Ya legal.", true)
                                      : SVOverworldReconstructor.Reconstruct(_sav, pk);
            string size = pk.Scale switch { 0 => "Mini", 255 => "Jumbo", _ => "" };

            int row = _grid.Rows.Add(
                i + 1,
                $"#{pk.Species} {SpeciesName(pk.Species)}",
                pk.Form,
                pk.IsShiny ? "Sí" : "No",
                pk.PID.ToString("X8"),
                pk.EncryptionConstant.ToString("X8"),
                pk.MetLevel,
                pk.TeraTypeOriginal,
                pk.MarkCount,
                pk.Scale,
                size,
                pk.Version,
                pk.Valid ? "OK" : "Bad",
                legal ? "Legal" : "Raw / revisar",
                reconstructed.Success
                    ? reconstructed.IdentityPreserved ? "Legal exacta" : "Legal regenerada"
                    : "Sin coincidencia");
            _grid.Rows[row].Tag = new SVRow(pk, reconstructed);
        }
    }

    private void ExportSelected()
    {
        PK9? pk = _grid.CurrentRow?.Tag switch
        {
            SVRow row => row.Raw,
            PK9 raw => raw,
            _ => null,
        };
        if (pk is null || pk.Species == 0)
            return;

        using var sd = new SaveFileDialog
        {
            Filter = "PK9|*.pk9|Todos|*.*",
            FileName = $"{pk.Species:0000}_{SpeciesName(pk.Species)}_overworld.pk9",
        };
        if (sd.ShowDialog(this) == DialogResult.OK)
            File.WriteAllBytes(sd.FileName, pk.DecryptedBoxData);
    }

    private void ReconstructSelected()
    {
        if (_grid.CurrentRow?.Tag is not SVRow row || row.Raw.Species == 0)
            return;

        SVReconstructionResult result = row.Reconstructed.Success
            ? row.Reconstructed
            : SVOverworldReconstructor.Reconstruct(_sav, row.Raw);

        if (!result.Success || result.Pokemon is null)
        {
            MessageBox.Show(this, result.Message, "NDX — SV Reconstruction",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var sd = new SaveFileDialog
        {
            Filter = "PK9|*.pk9|Todos|*.*",
            FileName = $"{result.Pokemon.Species:0000}_{SpeciesName(result.Pokemon.Species)}_legal.pk9",
        };
        if (sd.ShowDialog(this) != DialogResult.OK)
            return;

        File.WriteAllBytes(sd.FileName, result.Pokemon.DecryptedBoxData);
        MessageBox.Show(this,
            result.Message + "\r\n\r\n" +
            (result.IdentityPreserved
                ? "PID/EC e identidad de la entrada fueron preservados."
                : "PKHeX regeneró la correlación PID/EC necesaria para que el encuentro fuera legal."),
            "NDX — SV Reconstruction", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void AuditAll()
    {
        int used = 0;
        int already = 0;
        int exact = 0;
        int regenerated = 0;
        int failed = 0;

        foreach (DataGridViewRow gridRow in _grid.Rows)
        {
            if (gridRow.Tag is not SVRow row || row.Raw.Species == 0)
                continue;
            used++;

            bool legal = false;
            try { legal = new LegalityAnalysis(row.Raw, _sav.Personal, StorageSlotType.None).Valid; } catch { }
            if (legal)
            {
                already++;
                continue;
            }

            var result = row.Reconstructed.Success ? row.Reconstructed : SVOverworldReconstructor.Reconstruct(_sav, row.Raw);
            if (!result.Success)
                failed++;
            else if (result.IdentityPreserved)
                exact++;
            else
                regenerated++;
        }

        MessageBox.Show(this,
            $"Entradas usadas: {used}\r\n" +
            $"Ya legales: {already}\r\n" +
            $"Reconstruibles preservando PID/EC: {exact}\r\n" +
            $"Reconstruibles regenerando correlación: {regenerated}\r\n" +
            $"Sin coincidencia legal: {failed}",
            "NDX — Auditoría SV Overworld",
            MessageBoxButtons.OK, failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private sealed record SVRow(PK9 Raw, SVReconstructionResult Reconstructed);

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
