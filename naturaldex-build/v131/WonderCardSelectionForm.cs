using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed class WonderCardSelectionForm : Form
{
    private readonly IReadOnlyList<WonderCardEntry> _all;
    private readonly HashSet<string> _checked = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateOnly> _dateOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly DataGridView _grid = new();
    private readonly ComboBox _filter = new();
    private readonly TextBox _search = new();
    private readonly Label _summary = new();

    public IReadOnlyList<WonderCardEntry> SelectedEntries =>
        _all
            .Where(z => _checked.Contains(z.FilePath) && z.CanInject)
            .Select(ApplyDateOverride)
            .ToArray();

    private WonderCardEntry ApplyDateOverride(WonderCardEntry entry)
    {
        if (entry.VerifiedDate is not null)
            return entry;
        return _dateOverrides.TryGetValue(entry.FilePath, out var date)
            ? entry with { VerifiedDate = date }
            : entry;
    }

    public WonderCardSelectionForm(IReadOnlyList<WonderCardEntry> entries)
    {
        _all = entries;

        Text = "NaturalDex v1.3.1 — Selector de eventos / Wonder Cards";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 620);
        Size = new Size(1180, 760);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;

        BuildUi();
        ApplyFilter();
    }

    private void BuildUi()
    {
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = true,
        };

        _filter.DropDownStyle = ComboBoxStyle.DropDownList;
        _filter.Width = 170;
        _filter.Items.AddRange(new object[]
        {
            "Compatibles",
            "Todos",
            "Pokémon",
            "Shiny",
            "Objetos / ítems",
            "Ropa / accesorios",
            "BP / LP",
            "Dinero",
        });
        _filter.SelectedIndex = 0;
        _filter.SelectedIndexChanged += (_, _) => ApplyFilter();

        _search.Width = 250;
        _search.PlaceholderText = "Buscar ID, título, Pokémon, objeto...";
        _search.TextChanged += (_, _) => ApplyFilter();

        var all = MakeButton("Marcar compatibles", (_, _) =>
        {
            foreach (var e in FilteredEntries().Where(z => z.CanInject))
                _checked.Add(e.FilePath);
            ApplyFilter();
        });
        var none = MakeButton("Ninguno", (_, _) =>
        {
            foreach (var e in FilteredEntries())
                _checked.Remove(e.FilePath);
            ApplyFilter();
        });
        var pokemon = MakeButton("Pokémon", (_, _) =>
        {
            foreach (var e in FilteredEntries().Where(z => z.CanInject && z.Kind == WonderRewardKind.Pokemon))
                _checked.Add(e.FilePath);
            ApplyFilter();
        });
        var shiny = MakeButton("Shiny", (_, _) =>
        {
            foreach (var e in FilteredEntries().Where(z => z.CanInject && z.Kind == WonderRewardKind.Pokemon && z.IsShiny))
                _checked.Add(e.FilePath);
            ApplyFilter();
        });

        top.Controls.Add(new Label { Text = "Filtro:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        top.Controls.Add(_filter);
        top.Controls.Add(_search);
        top.Controls.Add(all);
        top.Controls.Add(none);
        top.Controls.Add(pokemon);
        top.Controls.Add(shiny);

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToOrderColumns = true;
        _grid.AllowUserToResizeRows = false;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.FixedSingle;

        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Pick",
            HeaderText = "✓",
            Width = 38,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Card",
            HeaderText = "Card",
            Width = 68,
            ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Type",
            HeaderText = "Tipo",
            Width = 115,
            ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Title",
            HeaderText = "Evento",
            Width = 260,
            ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Reward",
            HeaderText = "Recompensa",
            Width = 290,
            ReadOnly = true,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Date",
            HeaderText = "Fecha",
            Width = 105,
            ReadOnly = false,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "Compatibilidad / legalidad",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 260,
            ReadOnly = true,
        });

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += GridCellValueChanged;
        _grid.CellContentClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 0)
                return;
            var entry = _grid.Rows[e.RowIndex].Tag as WonderCardEntry;
            if (entry is { CanInject: false })
                System.Media.SystemSounds.Beep.Play();
        };

        _grid.CellValidating += GridCellValidating;
        _grid.CellEndEdit += GridCellEndEdit;

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(8),
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _summary.AutoSize = true;
        _summary.Anchor = AnchorStyles.Left;
        _summary.Padding = new Padding(0, 8, 0, 0);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var cancel = MakeButton("Cancelar", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        var inject = MakeButton("INYECTAR SELECCIONADOS", (_, _) =>
        {
            var selected = SelectedEntries;
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Selecciona al menos un evento compatible.", "NaturalDex — eventos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var missingDate = selected
                .Where(z => z.VerifiedDate is null)
                .Select(z => z.Display)
                .Take(8)
                .ToArray();

            if (missingDate.Length != 0)
            {
                MessageBox.Show(this,
                    "Estos eventos no tienen una fecha oficial verificable. Escribe una fecha de recepción manual en la columna Fecha antes de inyectarlos:\r\n\r\n" +
                    string.Join("\r\n", missingDate),
                    "NaturalDex — fecha requerida",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        });
        inject.AutoSize = true;
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(inject);

        bottom.Controls.Add(_summary, 0, 0);
        bottom.Controls.Add(buttons, 1, 0);

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(top);
    }

    private static Button MakeButton(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, AutoSize = true, Margin = new Padding(4) };
        b.Click += onClick;
        return b;
    }

    private IEnumerable<WonderCardEntry> FilteredEntries()
    {
        string search = _search.Text.Trim();
        IEnumerable<WonderCardEntry> query = _all;

        query = _filter.SelectedIndex switch
        {
            0 => query.Where(z => z.CanInject),
            2 => query.Where(z => z.Kind == WonderRewardKind.Pokemon),
            3 => query.Where(z => z.Kind == WonderRewardKind.Pokemon && z.IsShiny),
            4 => query.Where(z => z.Kind == WonderRewardKind.Item || z.Kind == WonderRewardKind.Underground),
            5 => query.Where(z => z.Kind == WonderRewardKind.Clothing),
            6 => query.Where(z => z.Kind == WonderRewardKind.Points),
            7 => query.Where(z => z.Kind == WonderRewardKind.Money),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(z =>
                z.CardID.ToString("0000").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                z.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                z.RewardSummary.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                z.RelativePath.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    private void ApplyFilter()
    {
        var items = FilteredEntries().ToArray();

        _grid.CellValueChanged -= GridCellValueChanged;
        _grid.Rows.Clear();

        foreach (var e in items)
        {
            string dateText = e.VerifiedDate?.ToString("yyyy-MM-dd")
                ?? (_dateOverrides.TryGetValue(e.FilePath, out var manualDate) ? manualDate.ToString("yyyy-MM-dd") : string.Empty);

            int rowIndex = _grid.Rows.Add(
                _checked.Contains(e.FilePath),
                e.CardID.ToString("0000"),
                WonderCardClassifier.GetKindText(e.Kind),
                e.Title,
                e.RewardSummary,
                dateText,
                e.CanInject ? e.Compatibility : "NO INYECTABLE — " + e.Compatibility);

            var row = _grid.Rows[rowIndex];
            row.Tag = e;
            row.Cells[0].ReadOnly = !e.CanInject;

            bool canEditDate = e.CanInject &&
                               e.Kind != WonderRewardKind.Pokemon &&
                               e.VerifiedDate is null;
            row.Cells["Date"].ReadOnly = !canEditDate;
            if (canEditDate)
                row.Cells["Date"].ToolTipText = "Fecha manual de recepción en formato AAAA-MM-DD. Se usará sólo porque no existe una fecha oficial verificable en el catálogo.";
            else if (e.VerifiedDate is not null)
                row.Cells["Date"].ToolTipText = "Fecha oficial/verificada; no se modifica manualmente.";

            if (!e.CanInject)
                row.DefaultCellStyle.ForeColor = SystemColors.GrayText;
        }

        _grid.CellValueChanged += GridCellValueChanged;
        UpdateSummary();
    }

    private void GridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 0)
            return;

        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not WonderCardEntry entry)
            return;

        bool value = row.Cells[0].Value is bool b && b;
        if (!entry.CanInject)
        {
            row.Cells[0].Value = false;
            _checked.Remove(entry.FilePath);
            return;
        }

        if (value)
            _checked.Add(entry.FilePath);
        else
            _checked.Remove(entry.FilePath);

        UpdateSummary();
    }

    private void GridCellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Date")
            return;

        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not WonderCardEntry entry || row.Cells[e.ColumnIndex].ReadOnly)
            return;

        string text = Convert.ToString(e.FormattedValue)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date) ||
            !EncounterDate.IsValidDateSwitch(date))
        {
            e.Cancel = true;
            MessageBox.Show(this,
                "La fecha debe escribirse como AAAA-MM-DD y ser una fecha válida de Nintendo Switch.",
                "NaturalDex — fecha inválida",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void GridCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Date")
            return;

        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not WonderCardEntry entry || row.Cells[e.ColumnIndex].ReadOnly)
            return;

        string text = Convert.ToString(row.Cells[e.ColumnIndex].Value)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            _dateOverrides.Remove(entry.FilePath);
            row.Cells[e.ColumnIndex].Value = string.Empty;
            return;
        }

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date) &&
            EncounterDate.IsValidDateSwitch(date))
        {
            _dateOverrides[entry.FilePath] = date;
            row.Cells[e.ColumnIndex].Value = date.ToString("yyyy-MM-dd");
        }
    }

    private void UpdateSummary()
    {
        int selected = _checked.Count(path => _all.Any(e => e.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase) && e.CanInject));
        int compatible = _all.Count(z => z.CanInject);
        int total = _all.Count;
        _summary.Text = $"{selected} seleccionados · {compatible} compatibles · {total} tarjetas encontradas";
    }
}
