using System;
using System.Collections.Generic;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed class SwShAdventureToolForm : Form
{
    private readonly SAV8SWSH _sav;
    private readonly CheckedListBox _caught = new() { CheckOnClick = true, Dock = DockStyle.Fill };
    private readonly NumericUpDown _note1 = NewSpeciesBox();
    private readonly NumericUpDown _note2 = NewSpeciesBox();
    private readonly NumericUpDown _note3 = NewSpeciesBox();
    private readonly NumericUpDown _hint = NewSpeciesBox();
    private readonly NumericUpDown _disconnect = NewUIntBox(9999);
    private readonly NumericUpDown _endless = NewUIntBox(9999999);

    private static readonly (string Name, uint Key)[] MaxLair =
    [
        ("Articuno",0xF75E52CF),("Zapdos",0xF75E5635),("Moltres",0xF75E511C),("Mewtwo",0xF75E4DB6),
        ("Raikou",0xF75E4C03),("Entei",0xF75E4A50),("Suicune",0xF75E4F69),("Lugia",0xF75E621A),("Ho-Oh",0xF75E63CD),
        ("Latias",0xF760948B),("Latios",0xF76092D8),("Kyogre",0xF760963E),("Groudon",0xF76097F1),("Rayquaza",0xF7609B57),
        ("Uxie",0xF76099A4),("Mesprit",0xF7609D0A),("Azelf",0xF7609EBD),("Dialga",0xF76086F3),("Palkia",0xF7608540),
        ("Heatran",0xF7582323),("Giratina",0xF7582170),("Cresselia",0xF75824D6),("Tornadus",0xF7582689),("Thundurus",0xF758283C),
        ("Reshiram",0xF7582BA2),("Zekrom",0xF7582D55),("Landorus",0xF75829EF),("Kyurem",0xF7582F08),
        ("Xerneas",0xF75830BB),("Yveltal",0xF75B3AF9),("Zygarde",0xF75B3946),
        ("Tapu Koko",0xF75B3793),("Tapu Lele",0xF75B35E0),("Tapu Bulu",0xF75B41C5),("Tapu Fini",0xF75B4012),
        ("Solgaleo",0xF75B3E5F),("Lunala",0xF75B3CAC),("Nihilego",0xF75B46DE),("Buzzwole",0xF769AAC6),
        ("Pheromosa",0xF769AC79),("Xurkitree",0xF769A760),("Celesteela",0xF769B192),("Kartana",0xF769A913),
        ("Guzzlord",0xF769B345),("Necrozma",0xF75B4891),("Stakataka",0xF769B85E),("Blacephalon",0xF769AFDF),
    ];

    internal SwShAdventureToolForm(SAV8SWSH sav)
    {
        _sav = sav;
        Text = "NDX — Dynamax Adventure / Crown Tundra";
        StartPosition = FormStartPosition.CenterParent;
        Width = 900;
        Height = 700;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildMaxLairTab());
        tabs.TabPages.Add(BuildCrownTab());

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var ok = new Button { Text = "APLICAR", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true };
        ok.Click += (_, _) => ApplyMaxLair();
        bottom.Controls.Add(ok);
        bottom.Controls.Add(cancel);

        Controls.Add(tabs);
        Controls.Add(bottom);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private TabPage BuildMaxLairTab()
    {
        var page = new TabPage("Dynamax Adventure");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(8) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        for (int i = 0; i < MaxLair.Length; i++)
        {
            _caught.Items.Add(MaxLair[i].Name);
            try
            {
                _caught.SetItemChecked(i, _sav.Blocks.GetBlock(MaxLair[i].Key).Type == SCTypeCode.Bool2);
            }
            catch { }
        }
        root.Controls.Add(_caught, 0, 0);
        root.SetRowSpan(_caught, 2);

        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
        AddField(fields, "Noted Pokémon 1 (Species ID)", _note1);
        AddField(fields, "Noted Pokémon 2 (Species ID)", _note2);
        AddField(fields, "Noted Pokémon 3 (Species ID)", _note3);
        AddField(fields, "Peonia hint (Species ID)", _hint);
        AddField(fields, "Disconnect streak", _disconnect);
        AddField(fields, "Endless streak", _endless);
        root.Controls.Add(fields, 1, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        var none = new Button { Text = "Reset caught flags", AutoSize = true };
        var all = new Button { Text = "Marcar todos capturados", AutoSize = true };
        none.Click += (_, _) => SetAllCaught(false);
        all.Click += (_, _) => SetAllCaught(true);
        buttons.Controls.Add(none);
        buttons.Controls.Add(all);
        root.Controls.Add(buttons, 1, 1);

        _note1.Value = ReadU32(0x6F669A35);
        _note2.Value = ReadU32(0x6F66951C);
        _note3.Value = ReadU32(0x6F6696CF);
        _hint.Value = ReadU32(0xF26B9151);
        _disconnect.Value = ClampTo(_disconnect, ReadU32(0x8EAEB8FF));
        _endless.Value = ClampTo(_endless, ReadU32(0x7F4B4B10));

        page.Controls.Add(root);
        return page;
    }

    private TabPage BuildCrownTab()
    {
        var page = new TabPage("Crown Tundra");
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(14),
        };
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(760, 0),
            Text = "Estos resets trabajan sobre el clon. Nada se guarda hasta pulsar APLICAR.",
        });

        var regi = new Button { Text = "RESET REGIS (capturas + elección Eleki/Drago)", AutoSize = true };
        regi.Click += (_, _) =>
        {
            uint[] keys = [0xEE3F84E6,0xDAB3DD3A,0xEE1FD86E,0xC4308A93,0x4F4AEC32,0x4F30F174];
            foreach (uint key in keys) SetBool(key, false);
            SetU32(0xCF90B39A, 0);
            MessageBox.Show(this, "Regis preparados para una nueva selección/captura en el clon.", "NDX");
        };

        var swords = new Button { Text = "RESET SWORDS OF JUSTICE / KELDEO", AutoSize = true };
        swords.Click += (_, _) =>
        {
            SetBool(0xBB305227, false);
            SetBool(0x750C83A4, false);
            SetBool(0x1A27DF2C, false);
            SetBool(0xA097DE31, false);
            SetU32(0x4D50B655, 100);
            SetU32(0x771E4C88, 100);
            SetU32(0xAD67A297, 100);
            MessageBox.Show(this, "Flags de captura limpiados y huellas al 100% en el clon.", "NDX");
        };

        var birds = new Button { Text = "RESET GALARIAN BIRDS", AutoSize = true };
        birds.Click += (_, _) =>
        {
            SetBool(0x4CAB7DA6, false);
            SetBool(0x284CBECF, false);
            SetBool(0xF1E493AA, false);
            MessageBox.Show(this, "Flags de captura de las aves de Galar limpiados en el clon.", "NDX");
        };

        panel.Controls.Add(regi);
        panel.Controls.Add(swords);
        panel.Controls.Add(birds);
        page.Controls.Add(panel);
        return page;
    }

    private void ApplyMaxLair()
    {
        for (int i = 0; i < MaxLair.Length; i++)
            SetBool(MaxLair[i].Key, _caught.GetItemChecked(i));

        SetU32(0x6F669A35, (uint)_note1.Value);
        SetU32(0x6F66951C, (uint)_note2.Value);
        SetU32(0x6F6696CF, (uint)_note3.Value);
        SetU32(0xF26B9151, (uint)_hint.Value);
        SetU32(0x8EAEB8FF, (uint)_disconnect.Value);
        SetU32(0x7F4B4B10, (uint)_endless.Value);
    }

    private void SetAllCaught(bool value)
    {
        for (int i = 0; i < _caught.Items.Count; i++)
            _caught.SetItemChecked(i, value);
    }

    private uint ReadU32(uint key)
    {
        try { return Convert.ToUInt32(_sav.Blocks.GetBlock(key).GetValue()); }
        catch { return 0; }
    }

    private void SetU32(uint key, uint value)
    {
        _sav.Blocks.GetBlock(key).SetValue(value);
    }

    private void SetBool(uint key, bool value)
    {
        SCBlock b = _sav.Blocks.GetBlock(key);
        b.ChangeBooleanType(value ? SCTypeCode.Bool2 : SCTypeCode.Bool1);
    }

    private static NumericUpDown NewSpeciesBox() => new() { Minimum = 0, Maximum = 2000, Width = 100 };
    private static NumericUpDown NewUIntBox(decimal max) => new() { Minimum = 0, Maximum = max, Width = 120 };
    private static decimal ClampTo(NumericUpDown box, uint value) => Math.Min(box.Maximum, value);

    private static void AddField(TableLayoutPanel table, string label, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 8, 0) }, 0, row);
        table.Controls.Add(control, 1, row);
    }
}
