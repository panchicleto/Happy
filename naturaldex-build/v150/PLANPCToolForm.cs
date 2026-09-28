using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed class PLANPCToolForm : Form
{
    private readonly SAV8LA _sav;
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
    private readonly Label _status = new() { AutoSize = true, Padding = new Padding(8) };

    internal bool Edited { get; private set; }

    private sealed record Candidate(SCBlock Block, int Offset, PA8 Pokemon);

    internal PLANPCToolForm(SAV8LA sav)
    {
        _sav = sav;
        Text = "NDX — Legends Arceus NPC / Farm Pokémon";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1100;
        Height = 650;

        _grid.Columns.Add("Block", "Block Key");
        _grid.Columns.Add("Offset", "Offset");
        _grid.Columns.Add("Species", "Species");
        _grid.Columns.Add("Form", "Form");
        _grid.Columns.Add("Shiny", "Shiny");
        _grid.Columns.Add("Alpha", "Alpha");
        _grid.Columns.Add("Nickname", "Nickname");
        _grid.Columns.Add("PID", "PID");
        _grid.Columns.Add("EC", "EC");
        _grid.Columns.Add("Legality", "Legality");

        var info = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            MaximumSize = new System.Drawing.Size(1040, 0),
            Text =
                "Busca estructuras PA8 con checksum válido en bloques de PLA fuera de Box, Party, Mystery Gift y Spawners. " +
                "Esto permite localizar los Pokémon íntegros que el juego conserva para NPC/granja sin depender del hash privado de una DLL antigua. " +
                "Los reemplazos se hacen únicamente en el clon.",
        };

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        var scan = new Button { Text = "ESCANEAR", AutoSize = true };
        var export = new Button { Text = "EXTRAER PA8...", AutoSize = true };
        var replace = new Button { Text = "REEMPLAZAR CON PA8...", AutoSize = true };
        var apply = new Button { Text = "APLICAR CAMBIOS", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true };
        scan.Click += (_, _) => Scan();
        export.Click += (_, _) => ExportSelected();
        replace.Click += (_, _) => ReplaceSelected();
        apply.Click += (_, _) =>
        {
            if (!Edited)
            {
                MessageBox.Show(this, "No hay cambios para aplicar.", "NDX — PLA NPC");
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        bottom.Controls.Add(scan);
        bottom.Controls.Add(export);
        bottom.Controls.Add(replace);
        bottom.Controls.Add(apply);
        bottom.Controls.Add(cancel);
        bottom.Controls.Add(_status);

        Controls.Add(_grid);
        Controls.Add(info);
        Controls.Add(bottom);
        Scan();
    }

    private void Scan()
    {
        _grid.Rows.Clear();
        int size = new PA8().SIZE_STORED;
        var excluded = new HashSet<uint>
        {
            0x47E1CEAB, // boxes
            0x2985FE5D, // party
            0x99E1625E, // mystery gifts
            0x511622B3, // spawners
        };

        int found = 0;
        Cursor old = Cursor;
        Cursor = Cursors.WaitCursor;
        try
        {
            foreach (SCBlock block in _sav.AllBlocks)
            {
                if (excluded.Contains(block.Key) || block.Data.Length < size)
                    continue;

                // Most entity-containing save structures are naturally aligned.
                for (int offset = 0; offset + size <= block.Data.Length; offset += 8)
                {
                    PA8 pk;
                    try { pk = new PA8(block.Data.Slice(offset, size).ToArray()); }
                    catch { continue; }

                    if (!pk.Valid || pk.Species == 0 || pk.Species > _sav.MaxSpeciesID)
                        continue;
                    if (pk.Version != _sav.Version)
                        continue;

                    bool legal = false;
                    try { legal = new LegalityAnalysis(pk, _sav.Personal, StorageSlotType.None).Valid; } catch { }

                    var candidate = new Candidate(block, offset, pk);
                    int row = _grid.Rows.Add(
                        block.Key.ToString("X8"),
                        $"0x{offset:X}",
                        $"#{pk.Species} {SpeciesName(pk.Species)}",
                        pk.Form,
                        pk.IsShiny ? "Sí" : "No",
                        pk.IsAlpha ? "Sí" : "No",
                        pk.Nickname,
                        pk.PID.ToString("X8"),
                        pk.EncryptionConstant.ToString("X8"),
                        legal ? "Legal" : "Revisar");
                    _grid.Rows[row].Tag = candidate;
                    found++;

                    // Avoid rediscovering overlapping windows of the same entity.
                    offset += size - 8;
                }
            }
        }
        finally
        {
            Cursor = old;
        }

        _status.Text = $"Candidatos PA8 fuera de almacenamiento normal: {found}";
        if (found == 0)
            MessageBox.Show(this,
                "No se localizaron entidades PA8 candidatas en este save. El bloque NPC/granja puede estar vacío o usar una disposición que necesite un perfil adicional.",
                "NDX — PLA NPC", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportSelected()
    {
        if (_grid.CurrentRow?.Tag is not Candidate c)
            return;

        using var sd = new SaveFileDialog
        {
            Filter = "PA8|*.pa8|Todos|*.*",
            FileName = $"{c.Pokemon.Species:0000}_{SpeciesName(c.Pokemon.Species)}_npc.pa8",
        };
        if (sd.ShowDialog(this) == DialogResult.OK)
            File.WriteAllBytes(sd.FileName, c.Pokemon.DecryptedBoxData);
    }

    private void ReplaceSelected()
    {
        if (_grid.CurrentRow?.Tag is not Candidate c)
            return;

        using var od = new OpenFileDialog { Filter = "PA8|*.pa8|Todos|*.*" };
        if (od.ShowDialog(this) != DialogResult.OK)
            return;

        byte[] data = File.ReadAllBytes(od.FileName);
        if (data.Length < new PA8().SIZE_STORED)
        {
            MessageBox.Show(this, "El archivo es demasiado pequeño para PA8.", "NDX — PLA NPC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        PA8 pk;
        try { pk = new PA8(data); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "NDX — PLA NPC", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (!pk.Valid)
        {
            MessageBox.Show(this, "El PA8 no tiene checksum/sanity válido.", "NDX — PLA NPC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool legal = false;
        try { legal = new LegalityAnalysis(pk, _sav.Personal, StorageSlotType.None).Valid; } catch { }
        if (!legal && MessageBox.Show(this,
                "El PA8 no pasa LegalityAnalysis para este save. ¿Reemplazarlo de todos modos en el clon?",
                "NDX — PLA NPC", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        byte[] encrypted = pk.EncryptedBoxData;
        encrypted.CopyTo(c.Block.Data.Slice(c.Offset, new PA8().SIZE_STORED));
        _sav.State.Edited = true;
        Edited = true;
        Scan();
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
