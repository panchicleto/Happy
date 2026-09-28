using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

public sealed partial class NaturalDexForm
{
    private async void GenerateEventsClick(object? sender, EventArgs e)
    {
        if (_cts is not null)
            return;

        SaveFile? rollback = null;
        string? backupPath = null;

        _batch = null;
        _import.Enabled = false;
        _log.Clear();
        _cts = new CancellationTokenSource();
        SetBusy(true);

        try
        {
            SaveFile live = _provider.SAV;
            _status.Text = "Actualizando catálogo oficial de eventos...";

            await EnsureEventCatalogAsync(false, live.BlankPKM.Context, _cts.Token);
            if (string.IsNullOrWhiteSpace(_eventRoot))
                throw new InvalidOperationException("No se pudo resolver la carpeta local de EventsGallery.");

            _status.Text = "Leyendo Pokémon, objetos, ropa y demás regalos...";
            var entries = await Task.Run(
                () => WonderCardCatalogLoader.Load(_eventRoot, live, _cts.Token),
                _cts.Token);

            if (entries.Count == 0)
            {
                MessageBox.Show(this,
                    "No se encontraron Wonder Cards reconocibles en el catálogo local.",
                    "NaturalDex — eventos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            using var selector = new WonderCardSelectionForm(entries);
            if (selector.ShowDialog(this) != DialogResult.OK)
                return;

            var selected = selector.SelectedEntries;
            if (selected.Count == 0)
                return;

            _status.Text = $"Preflight legal de {selected.Count} evento(s)...";
            _progress.Minimum = 0;
            _progress.Maximum = Math.Max(1, selected.Count);
            _progress.Value = 0;

            bool strict = _strictValidation.Checked;
            WonderInjectionResult preflight = await Task.Run(
                () => WonderCardInjectionService.Preflight(live, selected, strict),
                _cts.Token);

            _log.Text = preflight.Summary;
            _progress.Value = _progress.Maximum;

            if (!preflight.Success || preflight.PreviewSave is null)
            {
                _status.Text = "Preflight rechazado: no se modificó el save.";
                MessageBox.Show(this,
                    preflight.Summary,
                    "NaturalDex — validación de eventos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string summary =
                $"Se verificaron {selected.Count} evento(s).\r\n\r\n" +
                $"Pokémon legales: {preflight.PokemonAdded}\r\n" +
                $"Recompensas aplicables: {preflight.RewardsApplied}\r\n" +
                $"Registros de Mystery Gift: {preflight.ReceiptsWritten}\r\n\r\n" +
                "Los eventos históricos transferidos no crean una Wonder Card falsa del juego actual.\r\n" +
                "¿Aplicar este preflight al save abierto?";

            if (MessageBox.Show(this, summary, "NaturalDex — confirmar eventos",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                _status.Text = "Inyección de eventos cancelada.";
                return;
            }

            rollback = live.Clone();
            backupPath = SaveBackupManager.Create(live);

            live.CopyChangesFrom(preflight.PreviewSave);

            byte[] expected = preflight.PreviewSave.Write().ToArray();
            byte[] committed = live.Write().ToArray();
            if (!expected.SequenceEqual(committed))
                throw new InvalidOperationException("La auditoría binaria posterior al commit no coincide con el preflight.");

            _provider.ReloadSlots();

            _status.Text =
                $"Eventos aplicados: {selected.Count}. Pokémon {preflight.PokemonAdded}, " +
                $"recompensas {preflight.RewardsApplied}, registros {preflight.ReceiptsWritten}.";

            _log.AppendText(Environment.NewLine + Environment.NewLine +
                "Backup: " + backupPath + Environment.NewLine +
                "Auditoría final: OK (save real = preflight).");

            MessageBox.Show(this,
                "Eventos aplicados correctamente.\r\n\r\n" +
                $"Backup: {backupPath}\r\n" +
                "Auditoría final: OK.",
                "NaturalDex — eventos",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Selección/inyección de eventos cancelada.";
        }
        catch (Exception ex)
        {
            if (rollback is not null)
            {
                try
                {
                    _provider.SAV.CopyChangesFrom(rollback);
                    _provider.ReloadSlots();
                }
                catch
                {
                    // The on-disk backup remains available even if UI rollback unexpectedly fails.
                }
            }

            _status.Text = "Error aplicando eventos; se restauró el snapshot cuando fue posible.";
            _log.Text = ex.ToString();

            string backupInfo = string.IsNullOrWhiteSpace(backupPath)
                ? string.Empty
                : "\r\n\r\nBackup: " + backupPath;

            MessageBox.Show(this,
                ex.GetBaseException().Message + backupInfo,
                "NaturalDex — eventos",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            SetBusy(false);
        }
    }
}
