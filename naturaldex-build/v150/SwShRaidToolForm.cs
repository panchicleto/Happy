using System;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed class SwShRaidToolForm : Form
{
    private readonly SAV8SWSH _sav;
    private readonly ComboBox _region = new() { DropDownStyle = ComboBoxStyle.DropDownList };
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
    private readonly PropertyGrid _property = new() { Dock = DockStyle.Fill };

    internal SwShRaidToolForm(SAV8SWSH sav)
    {
        _sav = sav;
        Text = "NDX — Sword/Shield Raid Viewer / Editor";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1100;
        Height = 720;

        _region.Items.AddRange(new object[] { "Galar", "Isle of Armor", "Crown Tundra" });
        _region.SelectedIndex = 0;
        _region.SelectedIndexChanged += (_, _) => RefreshRows();

        _grid.Columns.Add("Index", "#");
        _grid.Columns.Add("Hash", "Hash");
        _grid.Columns.Add("Seed", "Seed");
        _grid.Columns.Add("Stars", "Stars");
        _grid.Columns.Add("Roll", "Roll");
        _grid.Columns.Add("Type", "Type");
        _grid.Columns.Add("Flags", "Flags");
        _grid.Columns.Add("Active", "Active");
        _grid.Columns.Add("Rare", "Rare");
        _grid.Columns.Add("Event", "Event");
        _grid.SelectionChanged += (_, _) =>
        {
            _property.SelectedObject = _grid.CurrentRow?.Tag as RaidSpawnDetail;
        };
        _property.PropertyValueChanged += (_, _) => RefreshRows(preserveSelection: true);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        top.Controls.Add(new Label { Text = "Región:", AutoSize = true, Padding = new Padding(0, 7, 4, 0) });
        top.Controls.Add(_region);

        var activateCommon = new Button { Text = "Activar todos comunes", AutoSize = true };
        var activateRare = new Button { Text = "Activar todos raros", AutoSize = true };
        var deactivate = new Button { Text = "Desactivar todos", AutoSize = true };
        var refresh = new Button { Text = "Refrescar", AutoSize = true };
        activateCommon.Click += (_, _) => { CurrentList().ActivateAllRaids(false, false); RefreshRows(); };
        activateRare.Click += (_, _) => { CurrentList().ActivateAllRaids(true, false); RefreshRows(); };
        deactivate.Click += (_, _) => { CurrentList().DectivateAllRaids(); RefreshRows(); };
        refresh.Click += (_, _) => RefreshRows();
        top.Controls.Add(activateCommon);
        top.Controls.Add(activateRare);
        top.Controls.Add(deactivate);
        top.Controls.Add(refresh);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 720 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_property);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var ok = new Button { Text = "APLICAR", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, AutoSize = true };
        bottom.Controls.Add(ok);
        bottom.Controls.Add(cancel);

        Controls.Add(split);
        Controls.Add(top);
        Controls.Add(bottom);
        AcceptButton = ok;
        CancelButton = cancel;

        RefreshRows();
    }

    private RaidSpawnList8 CurrentList() => _region.SelectedIndex switch
    {
        1 => _sav.RaidArmor,
        2 => _sav.RaidCrown,
        _ => _sav.RaidGalar,
    };

    private void RefreshRows(bool preserveSelection = false)
    {
        int previous = preserveSelection ? (_grid.CurrentRow?.Index ?? -1) : -1;
        _grid.Rows.Clear();
        var list = CurrentList();

        for (int i = 0; i < list.CountUsed; i++)
        {
            RaidSpawnDetail raid = list.GetRaid(i);
            int row = _grid.Rows.Add(
                i,
                raid.Hash.ToString("X16"),
                raid.Seed.ToString("X16"),
                raid.Stars + 1,
                raid.RandRoll,
                raid.DenType,
                raid.Flags.ToString("X2"),
                raid.IsActive ? "Sí" : "No",
                raid.IsRare ? "Sí" : "No",
                raid.IsEvent ? "Sí" : "No");
            _grid.Rows[row].Tag = raid;
        }

        if (previous >= 0 && previous < _grid.Rows.Count)
        {
            _grid.ClearSelection();
            _grid.Rows[previous].Selected = true;
            _grid.CurrentCell = _grid.Rows[previous].Cells[0];
        }
        else if (_grid.Rows.Count != 0)
        {
            _grid.Rows[0].Selected = true;
            _grid.CurrentCell = _grid.Rows[0].Cells[0];
        }
    }
}
