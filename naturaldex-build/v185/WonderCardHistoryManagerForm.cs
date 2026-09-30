using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal sealed class WonderCardHistoryManagerForm : Form
{
    private const uint SWSH_MYSTERY = 0x112D5141;
    private const int SWSH_SIZE = 0x17C8;
    private const uint LA_SV_MYSTERY = 0x99E1625E;
    private const int LA_SV_SIZE = 0x7EB0;

    private readonly SaveFile _work;
    private readonly Label _game = new() { AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12f, FontStyle.Bold) };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(780, 0) };
    private readonly TextBox _details = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        WordWrap = false,
        Font = new Font(FontFamily.GenericMonospace, 9f),
    };
    private readonly Button _clearAll = new() { Text = "BORRAR TODO EL HISTORIAL", AutoSize = true };
    private readonly Button _save = new() { Text = "APLICAR AL SAVE", AutoSize = true, Enabled = false };

    public SaveFile WorkingSave => _work;
    public bool Changed { get; private set; }

    public WonderCardHistoryManagerForm(SaveFile live)
    {
        _work = live.Clone();

        Text = "NDX — Historial de Wonder Cards / Mystery Gifts";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 560);
        Size = new Size(900, 680);

        BuildUI();
        RefreshInfo();
    }

    private void BuildUI()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(_game, 0, 0);

        var warning = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            Padding = new Padding(0, 8, 0, 8),
            Text =
                "Esta herramienta borra únicamente el HISTORIAL / RECIBOS de Mystery Gift del save. " +
                "NO elimina Pokémon, objetos, ropa, dinero, BP/LP ni otras recompensas que ya fueron aplicadas. " +
                "NDX crea un backup completo del save antes de confirmar los cambios.",
        };
        root.Controls.Add(warning, 0, 1);
        root.Controls.Add(_status, 0, 2);

        var detailBox = new GroupBox { Text = "Estado del historial", Dock = DockStyle.Fill, Padding = new Padding(8) };
        detailBox.Controls.Add(_details);
        root.Controls.Add(detailBox, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 0, 0),
        };

        var cancel = new Button { Text = "Cerrar", AutoSize = true };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _save.Click += (_, _) =>
        {
            if (!Changed)
                return;
            DialogResult = DialogResult.OK;
            Close();
        };

        _clearAll.Click += (_, _) => ClearAllHistory();

        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_save);
        buttons.Controls.Add(_clearAll);
        root.Controls.Add(buttons, 0, 4);

        Controls.Add(root);
    }

    private void RefreshInfo()
    {
        _game.Text = GetGameName(_work);

        var info = Inspect(_work);
        _status.Text = info.Supported
            ? info.HasHistory
                ? "Se detectaron datos de Mystery Gift. Puedes limpiar el historial para volver a hacer pruebas."
                : "El historial compatible está vacío."
            : info.Reason;

        _clearAll.Enabled = info.Supported && info.HasHistory;
        _save.Enabled = Changed;

        _details.Text = info.Report +
            Environment.NewLine + Environment.NewLine +
            "Cambios pendientes: " + (Changed ? "SÍ — aún no aplicados al save real." : "No.");
    }

    private void ClearAllHistory()
    {
        var info = Inspect(_work);
        if (!info.Supported)
        {
            MessageBox.Show(this, info.Reason, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!info.HasHistory)
        {
            MessageBox.Show(this, "El historial ya está vacío.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string prompt =
            "¿Borrar TODO el historial de Wonder Cards / Mystery Gifts de este save?\r\n\r\n" +
            "Las recompensas existentes NO se eliminarán.\r\n" +
            "El cambio se hará primero sobre una copia y todavía tendrás que pulsar APLICAR AL SAVE.";

        if (MessageBox.Show(this, prompt, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            ClearHistory(_work);
            var after = Inspect(_work);
            if (!after.Supported || after.HasHistory)
                throw new InvalidOperationException("La auditoría posterior al borrado indica que el historial no quedó vacío.");

            Changed = true;
            _work.State.Edited = true;
            RefreshInfo();

            MessageBox.Show(this,
                "Historial limpiado en la copia de trabajo.\r\n\r\n" +
                "Pulsa APLICAR AL SAVE para confirmar.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static HistoryInfo Inspect(SaveFile sav)
    {
        if (sav is SAV8SWSH swsh)
        {
            var block = swsh.Blocks.GetBlock(SWSH_MYSTERY);
            if (block.Data.Length != SWSH_SIZE)
                return new(false, false, $"SWSH: tamaño inesperado del bloque 0x{SWSH_MYSTERY:X8}: 0x{block.Data.Length:X}.", "No se modificará.");

            int nonzero = CountNonZero(block.Data);
            return new(true, nonzero != 0, string.Empty,
                "Juego: Pokémon Sword / Shield\r\n" +
                $"Bloque Mystery Gift: 0x{SWSH_MYSTERY:X8}\r\n" +
                $"Tamaño: 0x{block.Data.Length:X} bytes\r\n" +
                $"Bytes con datos: {nonzero:N0}\r\n" +
                "Modo de borrado: restaurar el bloque dedicado de Mystery Gift a estado vacío.");
        }

        if (sav is SAV8BS bdsp)
        {
            var m = bdsp.MysteryRecords;

            int received = 0;
            for (int i = 0; i < MysteryBlock8b.RecvDataMax; i++)
                if (m.GetReceived(i).DeliveryID != 0)
                    received++;

            int oneDay = 0;
            for (int i = 0; i < MysteryBlock8b.OneDayMax; i++)
                if (m.GetOneDay(i).DeliveryID != 0)
                    oneDay++;

            int flags = 0;
            for (int i = 0; i < MysteryBlock8b.FlagMax; i++)
                if (m.GetFlag(i))
                    flags++;

            bool locked = m.TicksSerialLock != 0;
            bool has = received != 0 || oneDay != 0 || flags != 0 || locked;

            return new(true, has, string.Empty,
                "Juego: Pokémon Brilliant Diamond / Shining Pearl\r\n" +
                $"Recibos ocupados: {received}/{MysteryBlock8b.RecvDataMax}\r\n" +
                $"Registros OneDay: {oneDay}/{MysteryBlock8b.OneDayMax}\r\n" +
                $"Flags de recepción activos: {flags}/{MysteryBlock8b.FlagMax}\r\n" +
                $"Bloqueo de serial: {(locked ? "activo" : "no")}\r\n" +
                "Modo de borrado: limpiar recibos, flags, OneDay y serial lock usando MysteryBlock8b.");
        }

        if (sav is SAV8LA pla)
        {
            var block = pla.Blocks.GetBlock(LA_SV_MYSTERY);
            if (block.Data.Length != LA_SV_SIZE)
                return new(false, false, $"PLA: tamaño inesperado del bloque Mystery Gift: 0x{block.Data.Length:X}.", "No se modificará.");

            int nonzero = CountNonZero(block.Data);
            return new(true, nonzero != 0, string.Empty,
                "Juego: Pokémon Legends: Arceus\r\n" +
                $"Bloque Mystery Gift: 0x{LA_SV_MYSTERY:X8}\r\n" +
                $"Tamaño: 0x{block.Data.Length:X} bytes\r\n" +
                $"Bytes con datos: {nonzero:N0}\r\n" +
                "Modo de borrado: restaurar únicamente el bloque dedicado de Mystery Gift a estado vacío.");
        }

        if (sav is SAV9SV sv)
        {
            var block = sv.Blocks.GetBlock(LA_SV_MYSTERY);
            if (block.Data.Length != LA_SV_SIZE)
                return new(false, false, $"SV: tamaño inesperado del bloque Mystery Gift: 0x{block.Data.Length:X}.", "No se modificará.");

            int nonzero = CountNonZero(block.Data);
            return new(true, nonzero != 0, string.Empty,
                "Juego: Pokémon Scarlet / Violet\r\n" +
                $"Bloque Mystery Gift: 0x{LA_SV_MYSTERY:X8}\r\n" +
                $"Tamaño: 0x{block.Data.Length:X} bytes\r\n" +
                $"Bytes con datos: {nonzero:N0}\r\n" +
                "Modo de borrado: restaurar únicamente el bloque dedicado de Mystery Gift a estado vacío.\r\n" +
                "Los registros DLC ajenos a Wonder Card no se tocan.");
        }

        if (sav is SAV9ZA)
        {
            return new(false, false,
                "Pokémon Legends: Z-A: PKHeX.Core 26.7.7.0 todavía no expone un layout público/verificado del historial WA9. NDX no hará escrituras a ciegas.",
                "Z-A: borrado deshabilitado por seguridad.");
        }

        return new(false, false, "Este formato de save no está soportado por el limpiador de historial.", "No disponible.");
    }

    private static void ClearHistory(SaveFile sav)
    {
        if (sav is SAV8SWSH swsh)
        {
            var block = swsh.Blocks.GetBlock(SWSH_MYSTERY);
            EnsureSize(block.Data.Length, SWSH_SIZE, "SWSH");
            block.Data.Clear();
            swsh.State.Edited = true;
            return;
        }

        if (sav is SAV8BS bdsp)
        {
            var m = bdsp.MysteryRecords;

            for (int i = 0; i < MysteryBlock8b.RecvDataMax; i++)
                m.SetReceived(i, new RecvData8b(new Memory<byte>(new byte[RecvData8b.SIZE])));

            for (int i = 0; i < MysteryBlock8b.OneDayMax; i++)
                m.SetOneDay(i, new OneDay8b(new Memory<byte>(new byte[OneDay8b.SIZE])));

            for (int i = 0; i < MysteryBlock8b.FlagMax; i++)
                m.SetFlag(i, false);

            m.ResetLock();
            bdsp.State.Edited = true;
            return;
        }

        if (sav is SAV8LA pla)
        {
            var block = pla.Blocks.GetBlock(LA_SV_MYSTERY);
            EnsureSize(block.Data.Length, LA_SV_SIZE, "PLA");
            block.Data.Clear();
            pla.State.Edited = true;
            return;
        }

        if (sav is SAV9SV sv)
        {
            var block = sv.Blocks.GetBlock(LA_SV_MYSTERY);
            EnsureSize(block.Data.Length, LA_SV_SIZE, "SV");
            block.Data.Clear();
            sv.State.Edited = true;
            return;
        }

        throw new NotSupportedException("Este save no admite borrado seguro del historial de Mystery Gift.");
    }

    private static int CountNonZero(ReadOnlySpan<byte> data)
    {
        int count = 0;
        foreach (byte b in data)
            if (b != 0)
                count++;
        return count;
    }

    private static void EnsureSize(int actual, int expected, string game)
    {
        if (actual != expected)
            throw new InvalidDataException($"{game}: el bloque Mystery Gift mide 0x{actual:X}, se esperaba 0x{expected:X}.");
    }

    private static string GetGameName(SaveFile sav) => sav switch
    {
        SAV8SWSH => "Pokémon Sword / Shield",
        SAV8BS => "Pokémon Brilliant Diamond / Shining Pearl",
        SAV8LA => "Pokémon Legends: Arceus",
        SAV9SV => "Pokémon Scarlet / Violet",
        SAV9ZA => "Pokémon Legends: Z-A",
        _ => sav.GetType().Name,
    };

    private readonly record struct HistoryInfo(bool Supported, bool HasHistory, string Reason, string Report);
}

public sealed partial class NaturalDexForm
{
    private void ManageWonderCardHistoryClick(object? sender, EventArgs e)
    {
        if (_cts is not null)
            return;

        SaveFile live = _provider.SAV;
        using var form = new WonderCardHistoryManagerForm(live);
        if (form.ShowDialog(this) != DialogResult.OK || !form.Changed)
            return;

        SaveFile rollback = live.Clone();
        string? backupPath = null;

        try
        {
            backupPath = SaveBackupManager.Create(live);
            live.CopyChangesFrom(form.WorkingSave);

            byte[] expected = form.WorkingSave.Write().ToArray();
            byte[] committed = live.Write().ToArray();
            if (!expected.SequenceEqual(committed))
                throw new InvalidOperationException("La auditoría binaria del historial no coincide después del commit.");

            _provider.ReloadSlots();
            _eventStatus.Text = "Historial de Wonder Cards limpiado correctamente.";
            _eventLog.Text =
                "Historial / recibos de Mystery Gift eliminados.\r\n" +
                "Las recompensas ya aplicadas se conservaron.\r\n" +
                "Backup: " + backupPath + "\r\n" +
                "Auditoría binaria: OK.";

            MessageBox.Show(this,
                "Historial de Wonder Cards eliminado correctamente.\r\n\r\n" +
                "Las recompensas existentes no fueron eliminadas.\r\n" +
                "Backup: " + backupPath,
                "NDX — Wonder Cards",
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
                "NDX — Wonder Cards",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
