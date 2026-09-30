using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed class HomeBoxManagerForm : Form
{
    private readonly SaveFile _work;
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = true,
    };
    private readonly Label _summary = new()
    {
        AutoSize = true,
        Padding = new Padding(8, 7, 8, 0),
    };
    private readonly Button _apply = new()
    {
        Text = "APLICAR AL SAVE",
        AutoSize = true,
        Enabled = false,
    };

    public SaveFile WorkingSave => _work;
    public bool Changed { get; private set; }

    public HomeBoxManagerForm(SaveFile live)
    {
        _work = live.Clone();

        Text = "NDX — Administrador de Cajas";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(780, 560);
        Size = new Size(920, 700);

        BuildUI();
        RefreshGrid();
    }

    private void BuildUI()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(860, 0),
            Padding = new Padding(8),
            Text =
                "Muestra las cajas con el nombre guardado realmente en el save. Puedes vaciar una, varias cajas seleccionadas " +
                "o todas las cajas. Los nombres y wallpapers no se cambian; únicamente se eliminan los Pokémon almacenados en los slots. " +
                "El equipo actual no se toca.",
        };
        root.Controls.Add(intro, 0, 0);

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(8),
        };

        var selectAll = new Button { Text = "SELECCIONAR TODAS", AutoSize = true };
        var clearSelection = new Button { Text = "QUITAR SELECCIÓN", AutoSize = true };
        var emptySelected = new Button { Text = "VACIAR SELECCIONADAS", AutoSize = true };
        var emptyAll = new Button { Text = "VACIAR TODAS LAS CAJAS", AutoSize = true };
        var refresh = new Button { Text = "REESCANEAR", AutoSize = true };

        selectAll.Click += (_, _) =>
        {
            foreach (DataGridViewRow row in _grid.Rows)
                row.Cells["Selected"].Value = true;
        };

        clearSelection.Click += (_, _) =>
        {
            foreach (DataGridViewRow row in _grid.Rows)
                row.Cells["Selected"].Value = false;
        };

        emptySelected.Click += (_, _) => EmptySelectedBoxes();
        emptyAll.Click += (_, _) => EmptyAllBoxes();
        refresh.Click += (_, _) => RefreshGrid();

        top.Controls.Add(selectAll);
        top.Controls.Add(clearSelection);
        top.Controls.Add(emptySelected);
        top.Controls.Add(emptyAll);
        top.Controls.Add(refresh);
        top.Controls.Add(_summary);
        root.Controls.Add(top, 0, 1);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };

        var cancel = new Button { Text = "Cerrar sin aplicar", AutoSize = true };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _apply.Click += (_, _) =>
        {
            if (!Changed)
                return;
            DialogResult = DialogResult.OK;
            Close();
        };

        bottom.Controls.Add(cancel);
        bottom.Controls.Add(_apply);
        root.Controls.Add(bottom, 0, 3);

        Controls.Add(root);
    }

    private void ConfigureGrid()
    {
        _grid.Columns.Clear();

        var selected = new DataGridViewCheckBoxColumn
        {
            Name = "Selected",
            HeaderText = "✓",
            Width = 42,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
        };
        _grid.Columns.Add(selected);
        _grid.Columns.Add("Box", "Caja");
        _grid.Columns.Add("Name", "Nombre");
        _grid.Columns.Add("Occupied", "Ocupados");
        _grid.Columns.Add("Empty", "Vacíos");
        _grid.Columns.Add("Capacity", "Capacidad");

        _grid.Columns["Box"].ReadOnly = true;
        _grid.Columns["Name"].ReadOnly = true;
        _grid.Columns["Occupied"].ReadOnly = true;
        _grid.Columns["Empty"].ReadOnly = true;
        _grid.Columns["Capacity"].ReadOnly = true;

        _grid.Columns["Name"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    private void RefreshGrid()
    {
        var checkedBoxes = new HashSet<int>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is int box && Convert.ToBoolean(row.Cells["Selected"].Value ?? false))
                checkedBoxes.Add(box);
        }

        _grid.SuspendLayout();
        _grid.Rows.Clear();

        int totalOccupied = 0;
        for (int box = 0; box < _work.BoxCount; box++)
        {
            int occupied = CountOccupied(_work, box);
            totalOccupied += occupied;
            int empty = _work.BoxSlotCount - occupied;
            string name = GetBoxName(_work, box);

            int row = _grid.Rows.Add(
                checkedBoxes.Contains(box),
                box + 1,
                name,
                occupied,
                empty,
                _work.BoxSlotCount);

            _grid.Rows[row].Tag = box;
            if (occupied == 0)
                _grid.Rows[row].DefaultCellStyle.ForeColor = SystemColors.GrayText;
        }

        _grid.ResumeLayout();

        int capacity = _work.BoxCount * _work.BoxSlotCount;
        _summary.Text = $"{_work.BoxCount} cajas · {totalOccupied}/{capacity} slots ocupados · {capacity - totalOccupied} vacíos";
        _apply.Enabled = Changed;
    }

    private void EmptySelectedBoxes()
    {
        var boxes = GetCheckedBoxes();
        if (boxes.Count == 0)
        {
            MessageBox.Show(this, "Selecciona al menos una caja.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int pokemon = boxes.Sum(z => CountOccupied(_work, z));
        if (pokemon == 0)
        {
            MessageBox.Show(this, "Las cajas seleccionadas ya están vacías.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string names = string.Join(", ", boxes.Take(8).Select(z => $"Caja {z + 1} \"{GetBoxName(_work, z)}\""));
        if (boxes.Count > 8)
            names += $", +{boxes.Count - 8} más";

        string prompt =
            $"Se eliminarán {pokemon} Pokémon de {boxes.Count} caja(s):\r\n\r\n{names}\r\n\r\n" +
            "Los nombres de las cajas se conservarán. El equipo no se modifica.\r\n\r\n¿Continuar?";

        if (MessageBox.Show(this, prompt, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        EmptyBoxes(boxes);
    }

    private void EmptyAllBoxes()
    {
        var boxes = Enumerable.Range(0, _work.BoxCount).ToArray();
        int pokemon = boxes.Sum(z => CountOccupied(_work, z));

        if (pokemon == 0)
        {
            MessageBox.Show(this, "Todas las cajas ya están vacías.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string prompt =
            $"ATENCIÓN: se eliminarán TODOS los Pokémon almacenados en cajas.\r\n\r\n" +
            $"Cajas: {_work.BoxCount}\r\nPokémon a eliminar: {pokemon}\r\n\r\n" +
            "Los nombres y wallpapers de las cajas se conservarán. El equipo no se modifica.\r\n\r\n" +
            "¿Deseas continuar?";

        if (MessageBox.Show(this, prompt, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        if (MessageBox.Show(this,
                "CONFIRMACIÓN FINAL: vaciar todas las cajas no se puede deshacer desde esta ventana.\r\n" +
                "NDX hará backup antes de aplicar al save real.\r\n\r\n¿Vaciar todas?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
            return;

        EmptyBoxes(boxes);
    }

    private void EmptyBoxes(IEnumerable<int> boxes)
    {
        int removed = 0;
        var originalNames = new Dictionary<int, string>();

        foreach (int box in boxes.Distinct())
        {
            if (box < 0 || box >= _work.BoxCount)
                continue;

            originalNames[box] = GetBoxName(_work, box);

            for (int slot = 0; slot < _work.BoxSlotCount; slot++)
            {
                PKM current = _work.GetBoxSlotAtIndex(box, slot);
                if (current.Species == 0)
                    continue;

                _work.SetBoxSlotAtIndex(_work.BlankPKM, box, slot);
                removed++;
            }
        }

        foreach (var kvp in originalNames)
        {
            if (CountOccupied(_work, kvp.Key) != 0)
                throw new InvalidOperationException($"La Caja {kvp.Key + 1} no quedó completamente vacía.");

            string afterName = GetBoxName(_work, kvp.Key);
            if (!string.Equals(kvp.Value, afterName, StringComparison.Ordinal))
                throw new InvalidOperationException($"El nombre de la Caja {kvp.Key + 1} cambió durante el borrado.");
        }

        Changed |= removed > 0;
        if (Changed)
            _work.State.Edited = true;

        RefreshGrid();

        MessageBox.Show(this,
            $"Se eliminaron {removed} Pokémon en la copia de trabajo.\r\n\r\n" +
            "Pulsa APLICAR AL SAVE para confirmar los cambios.",
            Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private List<int> GetCheckedBoxes()
    {
        var result = new List<int>();
        _grid.EndEdit();

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is not int box)
                continue;
            if (Convert.ToBoolean(row.Cells["Selected"].Value ?? false))
                result.Add(box);
        }

        return result;
    }

    private static int CountOccupied(SaveFile sav, int box)
    {
        int occupied = 0;
        for (int slot = 0; slot < sav.BoxSlotCount; slot++)
            if (sav.GetBoxSlotAtIndex(box, slot).Species != 0)
                occupied++;
        return occupied;
    }

    private static string GetBoxName(SaveFile sav, int box)
    {
        try
        {
            if (sav is IBoxDetailNameRead named)
            {
                string value = named.GetBoxName(box);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }
        catch
        {
            // Fall through to stable display name.
        }

        return $"Caja {box + 1}";
    }
}

public sealed partial class NaturalDexForm
{
    private void OpenHomeBoxManager()
    {
        SaveFile live = _provider.SAV;

        using var form = new HomeBoxManagerForm(live);
        if (form.ShowDialog(this) != DialogResult.OK || !form.Changed)
            return;

        SaveFile rollback = live.Clone();
        string? backupPath = null;

        try
        {
            backupPath = SaveBackupManager.Create(live);

            byte[] expected = form.WorkingSave.Write().ToArray();
            live.CopyChangesFrom(form.WorkingSave);
            byte[] committed = live.Write().ToArray();

            if (!expected.SequenceEqual(committed))
                throw new InvalidOperationException("La auditoría binaria del administrador de cajas no coincide después del commit.");

            _provider.ReloadSlots();

            if (_homePlan is not null)
                BuildHomePlan(useExistingCatalog: true);

            MessageBox.Show(this,
                "Cambios de cajas aplicados correctamente.\r\n\r\n" +
                $"Backup: {backupPath}\r\n" +
                "Nombres de cajas preservados · Auditoría binaria OK.",
                "NDX — Administrador de Cajas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            try
            {
                live.CopyChangesFrom(rollback);
                _provider.ReloadSlots();
            }
            catch
            {
                // The automatic backup remains available.
            }

            string backup = string.IsNullOrWhiteSpace(backupPath) ? string.Empty : "\r\n\r\nBackup: " + backupPath;
            MessageBox.Show(this,
                ex.GetBaseException().Message + backup,
                "NDX — Administrador de Cajas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
