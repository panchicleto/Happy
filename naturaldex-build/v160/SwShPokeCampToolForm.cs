using System;
using System.Collections.Generic;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed class SwShPokeCampToolForm : Form
{
    private readonly SAV8SWSH _sav;

    private readonly CheckBox _fresh = NewCheck("Fresh Ball");
    private readonly CheckBox _heavy = NewCheck("Heavy Ball");
    private readonly CheckBox _soothe = NewCheck("Soothe Ball");
    private readonly CheckBox _mirror = NewCheck("Mirror Ball");
    private readonly CheckBox _tympole = NewCheck("Tympole Ball");
    private readonly CheckBox _champion = NewCheck("Champion Ball");
    private readonly CheckBox _goldenUnlocked = NewCheck("Golden cookware unlocked");
    private readonly CheckBox _goldenUse = NewCheck("Use golden cookware");
    private readonly NumericUpDown _tentColor = new() { Minimum = 0, Maximum = 17, Width = 80 };

    private static readonly Dictionary<string, uint> Keys = new()
    {
        ["fresh"] = 0xAFA33CBD,
        ["heavy"] = 0xE49088C4,
        ["soothe"] = 0x45E850BE,
        ["mirror"] = 0x9B6CD5A2,
        ["tympole"] = 0xEA3E6881,
        ["champion"] = 0x45FD94D6,
        ["goldenUnlocked"] = 0x72D4B15E,
        ["goldenUse"] = 0x88FE6F97,
        ["tent"] = 0x61952B51,
    };

    internal SwShPokeCampToolForm(SAV8SWSH sav)
    {
        _sav = sav;
        Text = "NDX — Sword/Shield Poké Camp";
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var root = new TableLayoutPanel
        {
            AutoSize = true,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 4,
        };

        root.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(620, 0),
            Text =
                "Editor de Poké Camp portado a NDX para PKHeX.Core 26.7.7.0. " +
                "Los cambios se realizan sobre un clon y sólo se aplican cuando confirmas.",
        }, 0, 0);

        var balls = new GroupBox { Text = "Camp toys / balls", AutoSize = true, Dock = DockStyle.Top };
        var ballFlow = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(8), WrapContents = true };
        ballFlow.Controls.AddRange([_fresh, _heavy, _soothe, _mirror, _tympole, _champion]);
        balls.Controls.Add(ballFlow);
        root.Controls.Add(balls, 0, 1);

        var kitchen = new GroupBox { Text = "Kitchen / Tent", AutoSize = true, Dock = DockStyle.Top };
        var kitchenFlow = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(8), WrapContents = true };
        kitchenFlow.Controls.Add(_goldenUnlocked);
        kitchenFlow.Controls.Add(_goldenUse);
        kitchenFlow.Controls.Add(new Label { Text = "Tent color:", AutoSize = true, Padding = new Padding(8, 7, 3, 0) });
        kitchenFlow.Controls.Add(_tentColor);
        kitchen.Controls.Add(kitchenFlow);
        root.Controls.Add(kitchen, 0, 2);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        var ok = new Button { Text = "APLICAR", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => Apply();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 3);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;

        LoadValues();
    }

    private void LoadValues()
    {
        _fresh.Checked = GetBool(Keys["fresh"]);
        _heavy.Checked = GetBool(Keys["heavy"]);
        _soothe.Checked = GetBool(Keys["soothe"]);
        _mirror.Checked = GetBool(Keys["mirror"]);
        _tympole.Checked = GetBool(Keys["tympole"]);
        _champion.Checked = GetBool(Keys["champion"]);
        _goldenUnlocked.Checked = GetBool(Keys["goldenUnlocked"]);
        _goldenUse.Checked = GetBool(Keys["goldenUse"]);

        try
        {
            uint tent = Convert.ToUInt32(_sav.Blocks.GetBlock(Keys["tent"]).GetValue());
            _tentColor.Value = Math.Min(_tentColor.Maximum, tent);
        }
        catch
        {
            _tentColor.Value = 0;
        }
    }

    private void Apply()
    {
        SetBool(Keys["fresh"], _fresh.Checked);
        SetBool(Keys["heavy"], _heavy.Checked);
        SetBool(Keys["soothe"], _soothe.Checked);
        SetBool(Keys["mirror"], _mirror.Checked);
        SetBool(Keys["tympole"], _tympole.Checked);
        SetBool(Keys["champion"], _champion.Checked);
        SetBool(Keys["goldenUnlocked"], _goldenUnlocked.Checked);
        SetBool(Keys["goldenUse"], _goldenUse.Checked);
        _sav.Blocks.GetBlock(Keys["tent"]).SetValue((uint)_tentColor.Value);
        _sav.State.Edited = true;
    }

    private bool GetBool(uint key)
    {
        try { return _sav.Blocks.GetBlock(key).Type == SCTypeCode.Bool2; }
        catch { return false; }
    }

    private void SetBool(uint key, bool value)
    {
        SCBlock block = _sav.Blocks.GetBlock(key);
        block.ChangeBooleanType(value ? SCTypeCode.Bool2 : SCTypeCode.Bool1);
    }

    private static CheckBox NewCheck(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(4) };
}
