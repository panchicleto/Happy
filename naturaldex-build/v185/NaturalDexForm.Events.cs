using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

public sealed partial class NaturalDexForm
{
    private readonly Button _eventsUpdateCatalog = new()
    {
        Text = "ACTUALIZAR CATÁLOGO OFICIAL",
        AutoSize = true,
    };

    private readonly Button _eventsBrowseCatalog = new()
    {
        Text = "SELECCIONAR EVENTOS OFICIALES",
        AutoSize = true,
    };

    private readonly Button _eventsLoadFile = new()
    {
        Text = "CARGAR WONDER CARD LOCAL...",
        AutoSize = true,
    };

    private readonly Button _eventsManageHistory = new()
    {
        Text = "ADMINISTRAR / BORRAR HISTORIAL DEL SAVE...",
        AutoSize = true,
    };

    private readonly Button _eventsCancel = new()
    {
        Text = "Cancelar",
        AutoSize = true,
        Enabled = false,
    };

    private readonly Label _eventStatus = new()
    {
        Text = "Listo.",
        AutoSize = true,
    };

    private readonly ProgressBar _eventProgress = new()
    {
        Dock = DockStyle.Fill,
    };

    private readonly TextBox _eventLog = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill,
        WordWrap = false,
    };

    private TabControl? _mainTabs;

    private void InitializeEventsTab()
    {
        if (_mainTabs is not null || Controls.Count == 0)
            return;

        Control livingRoot = Controls[0];

        // These buttons belonged to the old one-page layout. Their replacements live
        // in the Events tab so the Living Dex screen stays focused.
        _updateEvents.Parent?.Controls.Remove(_updateEvents);
        _generateEvents.Parent?.Controls.Remove(_generateEvents);
        _updateEvents.Visible = false;
        _generateEvents.Visible = false;

        Controls.Remove(livingRoot);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
        };

        var livingTab = new TabPage("Living Dex");
        livingRoot.Dock = DockStyle.Fill;
        livingTab.Controls.Add(livingRoot);

        var eventsTab = new TabPage("Eventos / Wonder Cards");
        eventsTab.Controls.Add(BuildEventsPage());

        tabs.TabPages.Add(livingTab);
        tabs.TabPages.Add(eventsTab);
        Controls.Add(tabs);

        _mainTabs = tabs;

        _eventsUpdateCatalog.Click += UpdateEventsCatalogTabClick;
        _eventsBrowseCatalog.Click += GenerateEventsClick;
        _eventsLoadFile.Click += LoadWonderCardFilesClick;
        _eventsManageHistory.Click += ManageWonderCardHistoryClick;
        _eventsCancel.Click += (_, _) => _cts?.Cancel();
    }

    private Control BuildEventsPage()
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
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var info = new GroupBox
        {
            Text = "Inyección segura de eventos",
            Dock = DockStyle.Top,
            AutoSize = true,
        };
        info.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(900, 0),
            Padding = new Padding(8),
            Text =
                "Puedes usar el catálogo oficial de EventsGallery o cargar tus propios archivos Wonder Card. " +
                "Nada se escribe directamente: primero se valida compatibilidad, fecha, recompensa, historial de Mystery Gift " +
                "y, para Pokémon, LegalityAnalysis. Todo el preflight se hace sobre un clon del save.",
        });
        root.Controls.Add(info, 0, 0);

        var official = new GroupBox
        {
            Text = "Catálogo oficial — EventsGallery",
            Dock = DockStyle.Top,
            AutoSize = true,
        };
        var officialFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = true,
        };
        officialFlow.Controls.Add(_eventsUpdateCatalog);
        officialFlow.Controls.Add(_eventsBrowseCatalog);
        official.Controls.Add(officialFlow);
        root.Controls.Add(official, 0, 1);

        var local = new GroupBox
        {
            Text = "Wonder Card local",
            Dock = DockStyle.Top,
            AutoSize = true,
        };
        var localFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = true,
        };
        localFlow.Controls.Add(_eventsLoadFile);
        localFlow.Controls.Add(_eventsManageHistory);
        localFlow.Controls.Add(new Label
        {
            AutoSize = true,
            Padding = new Padding(8, 7, 0, 0),
            Text = "Puedes seleccionar uno o varios archivos; los incompatibles se mostrarán como NO INYECTABLES.",
        });
        local.Controls.Add(localFlow);
        root.Controls.Add(local, 0, 2);

        var status = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 8, 0, 8),
        };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        status.Controls.Add(_eventStatus, 0, 0);
        status.Controls.Add(_eventsCancel, 1, 0);
        status.Controls.Add(_eventProgress, 0, 1);
        status.SetColumnSpan(_eventProgress, 2);
        root.Controls.Add(status, 0, 3);

        var report = new GroupBox
        {
            Text = "Preflight / auditoría",
            Dock = DockStyle.Fill,
        };
        report.Controls.Add(_eventLog);
        root.Controls.Add(report, 0, 4);

        return root;
    }

    private void SetEventBusy(bool busy)
    {
        _eventsUpdateCatalog.Enabled = !busy;
        _eventsBrowseCatalog.Enabled = !busy;
        _eventsLoadFile.Enabled = !busy;
        _eventsManageHistory.Enabled = !busy;
        _eventsCancel.Enabled = busy;
        SetBusy(busy);
    }

    private void BeginEventOperation(string status)
    {
        _batch = null;
        _import.Enabled = false;
        _eventLog.Clear();
        _eventProgress.Minimum = 0;
        _eventProgress.Maximum = 1;
        _eventProgress.Value = 0;
        _eventStatus.Text = status;
        _cts = new CancellationTokenSource();
        SetEventBusy(true);
    }

    private void EndEventOperation()
    {
        _cts?.Dispose();
        _cts = null;
        SetEventBusy(false);
    }

    private async Task<EventGalleryCatalog> EnsureEventCatalogForTabAsync(
        bool force,
        EntityContext context,
        CancellationToken token)
    {
        EventGalleryUpdateResult result = await EventGalleryClient.EnsureCurrentAsync(
            force,
            new Progress<string>(message => _eventStatus.Text = message),
            token);

        _eventRoot = result.Root;
        _eventCatalog = await Task.Run(
            () => EventGalleryCatalog.Load(
                result.Root,
                context,
                new Progress<string>(message => _eventStatus.Text = message),
                token),
            token);

        string commit = string.IsNullOrWhiteSpace(result.Commit)
            ? "desconocido"
            : result.Commit[..Math.Min(8, result.Commit.Length)];

        _eventStatus.Text = $"Catálogo listo: {_eventCatalog.Entries.Count} Pokémon de evento · commit {commit}.";
        return _eventCatalog;
    }

    private async void UpdateEventsCatalogTabClick(object? sender, EventArgs e)
    {
        if (_cts is not null)
            return;

        BeginEventOperation("Actualizando catálogo oficial de EventsGallery...");

        try
        {
            await EnsureEventCatalogForTabAsync(true, _provider.SAV.BlankPKM.Context, _cts!.Token);
            _eventLog.Text =
                "Catálogo oficial actualizado correctamente." + Environment.NewLine +
                "La siguiente selección leerá también objetos, ropa, puntos y otros tipos de Wonder Card compatibles.";
        }
        catch (OperationCanceledException)
        {
            _eventStatus.Text = "Actualización cancelada.";
        }
        catch (Exception ex)
        {
            _eventStatus.Text = "Error actualizando EventsGallery.";
            _eventLog.Text = ex.ToString();
            MessageBox.Show(this, ex.GetBaseException().Message, "NaturalDex — EventsGallery",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            EndEventOperation();
        }
    }

    private async void GenerateEventsClick(object? sender, EventArgs e)
    {
        if (_cts is not null)
            return;

        BeginEventOperation("Preparando catálogo oficial de eventos...");

        try
        {
            SaveFile live = _provider.SAV;
            await EnsureEventCatalogForTabAsync(false, live.BlankPKM.Context, _cts!.Token);

            if (string.IsNullOrWhiteSpace(_eventRoot))
                throw new InvalidOperationException("No se pudo resolver la carpeta local de EventsGallery.");

            _eventStatus.Text = "Leyendo Pokémon, objetos, ropa y demás regalos...";
            var entries = await Task.Run(
                () => WonderCardCatalogLoader.Load(_eventRoot, live, _cts.Token),
                _cts.Token);

            await SelectAndInjectEventsAsync(entries, "catálogo oficial", _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _eventStatus.Text = "Selección/inyección de eventos cancelada.";
        }
        catch (Exception ex)
        {
            _eventStatus.Text = "Error preparando el catálogo de eventos.";
            _eventLog.Text = ex.ToString();
            MessageBox.Show(this, ex.GetBaseException().Message, "NaturalDex — eventos",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            EndEventOperation();
        }
    }

    private async void LoadWonderCardFilesClick(object? sender, EventArgs e)
    {
        if (_cts is not null)
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "Seleccionar Wonder Card",
            Multiselect = true,
            CheckFileExists = true,
            Filter =
                "Wonder Cards|*.pgt;*.pcd;*.wc4;*.pgf;*.wc5full;*.wc6;*.wc6full;*.wc7;*.wc7full;*.wr7;*.wb7;*.wb7full;*.wc8;*.wc8full;*.wb8;*.wa8;*.wc9;*.wa9|" +
                "Todos los archivos|*.*",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.FileNames.Length == 0)
            return;

        BeginEventOperation($"Leyendo {dialog.FileNames.Length} archivo(s) local(es)...");

        try
        {
            SaveFile live = _provider.SAV;
            var entries = await Task.Run(
                () => WonderCardCatalogLoader.LoadFiles(dialog.FileNames, live, _cts!.Token),
                _cts!.Token);

            if (entries.Count == 0)
            {
                _eventStatus.Text = "No se reconocieron Wonder Cards válidas.";
                MessageBox.Show(this,
                    "Los archivos seleccionados no contienen Wonder Cards reconocibles por esta versión de PKHeX.Core.",
                    "NaturalDex — archivo Wonder Card",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            _eventLog.Text =
                $"Archivos seleccionados: {dialog.FileNames.Length}{Environment.NewLine}" +
                $"Wonder Cards reconocidas: {entries.Count}{Environment.NewLine}" +
                "Las tarjetas incompatibles seguirán apareciendo para que puedas ver el motivo del rechazo.";

            await SelectAndInjectEventsAsync(entries, "archivo local", _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _eventStatus.Text = "Carga/inyección de Wonder Card cancelada.";
        }
        catch (Exception ex)
        {
            _eventStatus.Text = "Error leyendo la Wonder Card local.";
            _eventLog.Text = ex.ToString();
            MessageBox.Show(this, ex.GetBaseException().Message, "NaturalDex — archivo Wonder Card",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            EndEventOperation();
        }
    }

    private async Task SelectAndInjectEventsAsync(
        IReadOnlyList<WonderCardEntry> entries,
        string sourceLabel,
        CancellationToken token)
    {
        if (entries.Count == 0)
        {
            _eventStatus.Text = "No se encontraron Wonder Cards reconocibles.";
            MessageBox.Show(this,
                "No se encontraron Wonder Cards reconocibles.",
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

        SaveFile live = _provider.SAV;
        SaveFile? rollback = null;
        string? backupPath = null;

        try
        {
            _eventStatus.Text = $"Preflight legal de {selected.Count} evento(s) desde {sourceLabel}...";
            _eventProgress.Minimum = 0;
            _eventProgress.Maximum = Math.Max(1, selected.Count);
            _eventProgress.Value = 0;

            bool strict = _strictValidation.Checked;
            WonderInjectionResult preflight = await Task.Run(
                () => WonderCardInjectionService.Preflight(live, selected, strict),
                token);

            _eventLog.Text = preflight.Summary;
            _eventProgress.Value = _eventProgress.Maximum;

            if (!preflight.Success || preflight.PreviewSave is null)
            {
                _eventStatus.Text = "Preflight rechazado: no se modificó el save.";
                MessageBox.Show(this,
                    preflight.Summary,
                    "NaturalDex — validación de eventos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string summary =
                $"Fuente: {sourceLabel}\r\n" +
                $"Eventos verificados: {selected.Count}\r\n\r\n" +
                $"Pokémon legales: {preflight.PokemonAdded}\r\n" +
                $"Recompensas aplicables: {preflight.RewardsApplied}\r\n" +
                $"Registros de Mystery Gift: {preflight.ReceiptsWritten}\r\n\r\n" +
                "Los eventos históricos transferidos no crean una Wonder Card falsa del juego actual.\r\n" +
                "¿Aplicar este preflight al save abierto?";

            if (MessageBox.Show(this, summary, "NaturalDex — confirmar eventos",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                _eventStatus.Text = "Inyección de eventos cancelada.";
                return;
            }

            rollback = live.Clone();
            backupPath = SaveBackupManager.Create(live);

            live.CopyChangesFrom(preflight.PreviewSave);

            byte[] expected = preflight.PreviewSave.Write().ToArray();
            byte[] committed = live.Write().ToArray();
            if (!expected.SequenceEqual(committed))
                throw new InvalidOperationException("La auditoría binaria posterior al commit no coincide con el preflight.");

            if (!WonderCardInjectionService.AuditCommitted(live, selected, out string auditReason))
                throw new InvalidOperationException("Auditoría de legalidad posterior al commit: " + auditReason);

            _provider.ReloadSlots();

            _eventStatus.Text =
                $"Eventos aplicados: {selected.Count}. Pokémon {preflight.PokemonAdded}, " +
                $"recompensas {preflight.RewardsApplied}, registros {preflight.ReceiptsWritten}.";

            _eventLog.AppendText(Environment.NewLine + Environment.NewLine +
                "Fuente: " + sourceLabel + Environment.NewLine +
                "Backup: " + backupPath + Environment.NewLine +
                "Auditoría final: OK (save real = preflight; Pokémon revalidados).");

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
            if (rollback is not null)
            {
                live.CopyChangesFrom(rollback);
                _provider.ReloadSlots();
            }
            throw;
        }
        catch (Exception ex)
        {
            if (rollback is not null)
            {
                try
                {
                    live.CopyChangesFrom(rollback);
                    _provider.ReloadSlots();
                }
                catch
                {
                    // The on-disk backup remains available even if UI rollback unexpectedly fails.
                }
            }

            _eventStatus.Text = "Error aplicando eventos; se restauró el snapshot cuando fue posible.";
            _eventLog.Text = ex.ToString();

            string backupInfo = string.IsNullOrWhiteSpace(backupPath)
                ? string.Empty
                : "\r\n\r\nBackup: " + backupPath;

            MessageBox.Show(this,
                ex.GetBaseException().Message + backupInfo,
                "NaturalDex — eventos",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
