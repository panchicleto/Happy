using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed class SwShRaidAdvancedForm : Form
{
    private readonly SAV8SWSH _sav;
    private readonly RaidSpawnDetail _raid;

    private readonly TextBox _seed = new() { Width = 180 };
    private readonly NumericUpDown _frames = new() { Minimum = 1, Maximum = 5000, Value = 100, Width = 90 };
    private readonly NumericUpDown _fixedIVs = new() { Minimum = 0, Maximum = 5, Value = 4, Width = 60 };
    private readonly DataGridView _frameGrid = NewGrid();

    private readonly TextBox _ec = new() { Width = 110 };
    private readonly TextBox _pid = new() { Width = 110 };
    private readonly NumericUpDown[] _ivs = Enumerable.Range(0, 6).Select(_ => new NumericUpDown { Minimum = 0, Maximum = 31, Width = 55 }).ToArray();
    private readonly DataGridView _seedGrid = NewGrid();
    private readonly Label _searchStatus = new() { AutoSize = true, Padding = new Padding(6) };

    internal SwShRaidAdvancedForm(SAV8SWSH sav, RaidSpawnDetail raid)
    {
        _sav = sav;
        _raid = raid;

        Text = "NDX — SWSH Raid Frames / Seed Finder";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1120;
        Height = 720;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildFramesTab());
        tabs.TabPages.Add(BuildSeedFinderTab());

        Controls.Add(tabs);
        LoadCurrentRaid();
    }

    private TabPage BuildFramesTab()
    {
        var page = new TabPage("Frames");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };

        top.Controls.Add(LabelFor("Seed:"));
        top.Controls.Add(_seed);
        top.Controls.Add(LabelFor("Frames:"));
        top.Controls.Add(_frames);
        top.Controls.Add(LabelFor("Flawless IVs:"));
        top.Controls.Add(_fixedIVs);

        var current = new Button { Text = "USAR SEED DEL DEN", AutoSize = true };
        var run = new Button { Text = "GENERAR FRAMES", AutoSize = true };
        var shiny = new Button { Text = "BUSCAR SIGUIENTE SHINY", AutoSize = true };
        current.Click += (_, _) => _seed.Text = _raid.Seed.ToString("X16");
        run.Click += (_, _) => GenerateFrames();
        shiny.Click += (_, _) => FindNextShiny();
        top.Controls.Add(current);
        top.Controls.Add(run);
        top.Controls.Add(shiny);

        _frameGrid.Columns.Add("Frame", "Frame");
        _frameGrid.Columns.Add("Seed", "Seed");
        _frameGrid.Columns.Add("EC", "EC");
        _frameGrid.Columns.Add("PID", "Final PID");
        _frameGrid.Columns.Add("RawPID", "Raw PID");
        _frameGrid.Columns.Add("Shiny", "Shiny");
        _frameGrid.Columns.Add("IVs", "IVs HP/Atk/Def/SpA/SpD/Spe");

        page.Controls.Add(_frameGrid);
        page.Controls.Add(top);
        return page;
    }

    private TabPage BuildSeedFinderTab()
    {
        var page = new TabPage("Seed Finder");
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(1000, 0),
            Text =
                "Recupera seeds usando la reversión Xoroshiro incluida en PKHeX.Core 26.7.7.0. " +
                "Prueba automáticamente raids normales y los ajustes shiny habituales, y valida EC/PID/IVs antes de mostrar un resultado.",
        }, 0, 0);

        var input = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 8, 0, 8), WrapContents = true };
        input.Controls.Add(LabelFor("EC:"));
        input.Controls.Add(_ec);
        input.Controls.Add(LabelFor("PID:"));
        input.Controls.Add(_pid);

        string[] names = ["HP", "Atk", "Def", "SpA", "SpD", "Spe"];
        for (int i = 0; i < _ivs.Length; i++)
        {
            input.Controls.Add(LabelFor(names[i] + ":"));
            input.Controls.Add(_ivs[i]);
        }

        var load = new Button { Text = "CARGAR FRAME 0 DEL DEN", AutoSize = true };
        var find = new Button { Text = "BUSCAR SEED", AutoSize = true };
        load.Click += (_, _) => LoadCurrentRaidGenerated();
        find.Click += (_, _) => FindSeeds();
        input.Controls.Add(load);
        input.Controls.Add(find);
        input.Controls.Add(_searchStatus);

        root.Controls.Add(input, 0, 1);

        _seedGrid.Columns.Add("Seed", "Seed");
        _seedGrid.Columns.Add("FixedIV", "Flawless IVs");
        _seedGrid.Columns.Add("ShinyRule", "Shiny rule");
        _seedGrid.Columns.Add("EC", "EC");
        _seedGrid.Columns.Add("PID", "PID");
        _seedGrid.Columns.Add("IVs", "IVs");
        root.Controls.Add(_seedGrid, 0, 2);

        page.Controls.Add(root);
        return page;
    }

    private void LoadCurrentRaid()
    {
        _seed.Text = _raid.Seed.ToString("X16");
        GenerateFrames();
    }

    private void LoadCurrentRaidGenerated()
    {
        var f = Generate(_raid.Seed, (int)_fixedIVs.Value, _sav.TID16, _sav.SID16, 0);
        _ec.Text = f.EC.ToString("X8");
        _pid.Text = f.FinalPID.ToString("X8");
        for (int i = 0; i < 6; i++)
            _ivs[i].Value = f.IVs[i];
    }

    private void GenerateFrames()
    {
        if (!TrySeed(out ulong seed))
            return;

        _frameGrid.Rows.Clear();
        int count = (int)_frames.Value;
        int flawless = (int)_fixedIVs.Value;
        ulong current = seed;

        for (int frame = 0; frame < count; frame++)
        {
            var f = Generate(current, flawless, _sav.TID16, _sav.SID16, 0);
            _frameGrid.Rows.Add(
                frame,
                current.ToString("X16"),
                f.EC.ToString("X8"),
                f.FinalPID.ToString("X8"),
                f.RawPID.ToString("X8"),
                ShinyText(f.FinalPID, _sav.TID16, _sav.SID16),
                string.Join("/", f.IVs));

            current = AdvanceSeed(current);
        }
    }

    private void FindNextShiny()
    {
        if (!TrySeed(out ulong seed))
            return;

        ulong current = seed;
        int flawless = (int)_fixedIVs.Value;
        const int max = 2_000_000;

        Cursor old = Cursor;
        Cursor = Cursors.WaitCursor;
        try
        {
            for (int frame = 0; frame < max; frame++)
            {
                var f = Generate(current, flawless, _sav.TID16, _sav.SID16, 0);
                if (GetTrainerShinyType(f.FinalPID, _sav.TID16, _sav.SID16) != 0)
                {
                    MessageBox.Show(this,
                        $"Siguiente shiny encontrado.\r\n\r\nFrame: {frame}\r\nSeed: {current:X16}\r\nPID: {f.FinalPID:X8}\r\nEC: {f.EC:X8}\r\nIVs: {string.Join("/", f.IVs)}",
                        "NDX — Raid Shiny Frame", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                current = AdvanceSeed(current);
            }
        }
        finally
        {
            Cursor = old;
        }

        MessageBox.Show(this, $"No se encontró shiny en los primeros {max:N0} frames.", "NDX — Raid Shiny Frame");
    }

    private void FindSeeds()
    {
        if (!TryHex32(_ec.Text, out uint ec) || !TryHex32(_pid.Text, out uint pid))
        {
            MessageBox.Show(this, "EC y PID deben ser valores hexadecimales de 8 dígitos.", "NDX — Seed Finder");
            return;
        }

        int[] targetIVs = _ivs.Select(z => (int)z.Value).ToArray();
        _seedGrid.Rows.Clear();
        _searchStatus.Text = "Buscando...";
        Cursor old = Cursor;
        Cursor = Cursors.WaitCursor;

        try
        {
            var rawCandidates = GetRawPIDCandidates(pid, _sav.TID16, _sav.SID16);
            var found = new HashSet<(ulong Seed, int Fixed, sbyte Rule)>();

            foreach (uint raw in rawCandidates)
            {
                var machine = new XoroMachineSkip(ec, raw);
                while (machine.MoveNext())
                {
                    ulong seed = machine.Current;

                    for (int fixedIVs = 0; fixedIVs <= 5; fixedIVs++)
                    {
                        for (sbyte shinyRule = 0; shinyRule <= 2; shinyRule++)
                        {
                            var f = Generate(seed, fixedIVs, _sav.TID16, _sav.SID16, shinyRule);
                            if (f.EC != ec || f.FinalPID != pid || !f.IVs.SequenceEqual(targetIVs))
                                continue;

                            if (!found.Add((seed, fixedIVs, shinyRule)))
                                continue;

                            _seedGrid.Rows.Add(
                                seed.ToString("X16"),
                                fixedIVs,
                                shinyRule switch { 1 => "Forced non-shiny", 2 => "Forced shiny", _ => "Normal" },
                                f.EC.ToString("X8"),
                                f.FinalPID.ToString("X8"),
                                string.Join("/", f.IVs));

                            if (found.Count >= 100)
                                break;
                        }
                        if (found.Count >= 100)
                            break;
                    }
                    if (found.Count >= 100)
                        break;
                }
                if (found.Count >= 100)
                    break;
            }

            _searchStatus.Text = found.Count == 0 ? "Sin coincidencias." : $"{found.Count} coincidencia(s).";
        }
        finally
        {
            Cursor = old;
        }
    }

    private bool TrySeed(out ulong seed)
    {
        if (ulong.TryParse(_seed.Text.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out seed))
            return true;
        MessageBox.Show(this, "Seed inválido. Usa 16 dígitos hexadecimales.", "NDX — Raid Frames");
        return false;
    }

    private static bool TryHex32(string text, out uint value) =>
        uint.TryParse(text.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

    private static IEnumerable<uint> GetRawPIDCandidates(uint finalPID, ushort tid, ushort sid)
    {
        var set = new HashSet<uint> { finalPID, finalPID ^ 0x10000000 };
        uint lo = finalPID & 0xFFFF;
        uint hiFinal = finalPID >> 16;

        for (uint shinyType = 1; shinyType <= 2; shinyType++)
        {
            uint hiRaw = (hiFinal ^ tid ^ sid ^ lo ^ (2u - shinyType)) & 0xFFFF;
            set.Add((hiRaw << 16) | lo);
        }

        return set;
    }

    private static RaidFrame Generate(ulong seed, int fixedIVs, ushort tid, ushort sid, sbyte fixedShiny)
    {
        var rng = new Xoroshiro128Plus(seed);
        uint ec = (uint)rng.NextInt();
        uint tidsid = (uint)rng.NextInt();
        uint rawPid = (uint)rng.NextInt();
        uint finalPid = GetFinalPID(tid, sid, rawPid, tidsid, GetShinyValue(((uint)sid << 16) | tid), fixedShiny);

        int[] ivs = [-1, -1, -1, -1, -1, -1];
        for (int i = 0; i < fixedIVs; i++)
        {
            int idx;
            do
            {
                do { idx = (int)rng.Next() & 7; }
                while (idx >= 6);
            }
            while (ivs[idx] != -1);
            ivs[idx] = 31;
        }

        for (int i = 0; i < 6; i++)
            if (ivs[i] == -1)
                ivs[i] = (int)rng.NextInt(32);

        return new RaidFrame(ec, rawPid, finalPid, ivs);
    }

    private static ulong AdvanceSeed(ulong seed)
    {
        var rng = new Xoroshiro128Plus(seed);
        return rng.Next();
    }

    private static uint GetShinyXor(uint val) => (val >> 16) ^ (val & 0xFFFF);
    private static uint GetShinyValue(uint num) => GetShinyXor(num) >> 4;

    private static uint GetRaidShinyType(uint pid, uint tidsid)
    {
        uint p = GetShinyXor(pid);
        uint t = GetShinyXor(tidsid);
        if (p == t)
            return 2;
        if ((p ^ t) < 0x10)
            return 1;
        return 0;
    }

    private static uint GetTrainerShinyType(uint pid, ushort tid, ushort sid)
    {
        uint xor = (uint)(tid ^ sid ^ (pid >> 16) ^ (pid & 0xFFFF));
        if (xor == 0)
            return 2;
        if (xor < 16)
            return 1;
        return 0;
    }

    private static string ShinyText(uint pid, ushort tid, ushort sid) => GetTrainerShinyType(pid, tid, sid) switch
    {
        2 => "Square",
        1 => "Star",
        _ => "No",
    };

    private static uint GetFinalPID(ushort tid, ushort sid, uint rawPid, uint tidsid, uint tsv, sbyte fixedShiny)
    {
        uint shinyType = GetRaidShinyType(rawPid, tidsid);
        if (fixedShiny == 2 && shinyType == 0)
            shinyType = 2;
        if (fixedShiny == 1)
            shinyType = 0;

        uint psv = GetShinyValue(rawPid);
        if (shinyType == 0)
            return psv == tsv ? rawPid ^ 0x10000000 : rawPid;

        if (psv == tsv)
            return rawPid;

        return (rawPid & 0xFFFF) | ((uint)(tid ^ sid) ^ rawPid ^ (2u - shinyType)) << 16;
    }

    private static DataGridView NewGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
    };

    private static Label LabelFor(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(0, 7, 3, 0) };

    private readonly record struct RaidFrame(uint EC, uint RawPID, uint FinalPID, int[] IVs);
}
