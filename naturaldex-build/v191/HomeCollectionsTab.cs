using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

public sealed partial class NaturalDexForm
{
    private readonly ComboBox _homeCollection = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 300,
    };

    private readonly ComboBox _homeBall = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 150,
    };

    private readonly CheckBox _homeOnlyMissing = new()
    {
        Text = "Mostrar solo faltantes",
        AutoSize = true,
    };

    private readonly Label _homeSummary = new()
    {
        AutoSize = true,
        Padding = new Padding(8, 7, 8, 0),
    };

    private readonly Label _homeNotes = new()
    {
        AutoSize = true,
        MaximumSize = new System.Drawing.Size(980, 0),
        Padding = new Padding(8),
    };

    private readonly DataGridView _homeGrid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        ReadOnly = true,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
    };

    private HomeCollectionPlan? _homePlan;

    private void InitializeHomeCollectionsTab()
    {
        if (_mainTabs is null)
            return;
        if (_mainTabs.TabPages.Cast<TabPage>().Any(z => z.Text == "HOME Collections"))
            return;

        var page = new TabPage("HOME Collections");
        page.Controls.Add(BuildHomeCollectionsPage());
        _mainTabs.TabPages.Add(page);
    }

    private Control BuildHomeCollectionsPage()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 5,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(980, 0),
            Padding = new Padding(8),
            Text =
                "HOME Collections organiza y audita colecciones pensadas para transferir después a Pokémon HOME. " +
                "NDX nunca fabrica un HOME Tracker: los Pokémon generados se validan en el save del juego y HOME asignará sus datos propios al transferirlos. " +
                "Las colecciones complejas funcionan como tracker/manifiesto cuando no existe una generación automática segura.",
        };
        root.Controls.Add(intro, 0, 0);

        foreach (HomeCollectionPreset p in HomeCollectionCatalog.GetPresets())
            _homeCollection.Items.Add(p);
        _homeCollection.DisplayMember = nameof(HomeCollectionPreset.Name);
        if (_homeCollection.Items.Count != 0)
            _homeCollection.SelectedIndex = 0;

        foreach (Ball ball in Enum.GetValues<Ball>().Where(z => (int)z > 0 && (int)z <= 255))
            _homeBall.Items.Add(ball);
        if (_homeBall.Items.Count != 0)
            _homeBall.SelectedIndex = 0;

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(8),
        };
        controls.Controls.Add(new Label { Text = "Colección:", AutoSize = true, Padding = new Padding(0, 7, 4, 0) });
        controls.Controls.Add(_homeCollection);
        controls.Controls.Add(new Label { Text = "Ball:", AutoSize = true, Padding = new Padding(10, 7, 4, 0) });
        controls.Controls.Add(_homeBall);
        controls.Controls.Add(_homeOnlyMissing);
        controls.Controls.Add(_homeSummary);
        root.Controls.Add(controls, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(8),
        };
        var analyze = new Button { Text = "ANALIZAR COLECCIÓN", AutoSize = true };
        var generate = new Button { Text = "APLICAR PRESET Y GENERAR", AutoSize = true };
        var exportCsv = new Button { Text = "EXPORTAR MANIFEST CSV", AutoSize = true };
        var exportJson = new Button { Text = "EXPORTAR MANIFEST JSON", AutoSize = true };
        var refresh = new Button { Text = "REESCANEAR CAJAS", AutoSize = true };
        var boxes = new Button { Text = "ADMINISTRAR CAJAS...", AutoSize = true };

        analyze.Click += async (_, _) => await AnalyzeHomeCollectionAsync();
        generate.Click += (_, _) => GenerateHomeCollectionPreset();
        exportCsv.Click += (_, _) => ExportHomeCollectionCsv();
        exportJson.Click += (_, _) => ExportHomeCollectionJson();
        refresh.Click += (_, _) =>
        {
            if (_homePlan is null)
                return;
            BuildHomePlan(useExistingCatalog: true);
        };
        boxes.Click += (_, _) => OpenHomeBoxManager();

        _homeOnlyMissing.CheckedChanged += (_, _) => RenderHomePlan();
        _homeCollection.SelectedIndexChanged += (_, _) => UpdateHomeControls();
        _homeBall.SelectedIndexChanged += (_, _) =>
        {
            if (SelectedHomePreset()?.Kind == HomeCollectionKind.BallDex && _homePlan is not null)
                BuildHomePlan(useExistingCatalog: true);
        };

        actions.Controls.Add(analyze);
        actions.Controls.Add(generate);
        actions.Controls.Add(refresh);
        actions.Controls.Add(boxes);
        actions.Controls.Add(exportCsv);
        actions.Controls.Add(exportJson);
        root.Controls.Add(actions, 0, 2);

        ConfigureHomeGrid();
        var box = new GroupBox
        {
            Text = "Estado de la colección",
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
        };
        box.Controls.Add(_homeGrid);
        root.Controls.Add(box, 0, 3);

        root.Controls.Add(_homeNotes, 0, 4);
        UpdateHomeControls();
        return root;
    }

    private void ConfigureHomeGrid()
    {
        _homeGrid.Columns.Clear();
        _homeGrid.Columns.Add("Index", "#");
        _homeGrid.Columns.Add("Group", "Grupo");
        _homeGrid.Columns.Add("Species", "Pokémon");
        _homeGrid.Columns.Add("Variant", "Variante");
        _homeGrid.Columns.Add("Requirement", "Requisito");
        _homeGrid.Columns.Add("Owned", "Estado");
        _homeGrid.Columns.Add("Supported", "Juego actual");
        _homeGrid.Columns.Add("Location", "Ubicación");
        _homeGrid.Columns.Add("Notes", "Notas");
        _homeGrid.Columns["Notes"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    private HomeCollectionPreset? SelectedHomePreset()
        => _homeCollection.SelectedItem as HomeCollectionPreset;

    private Ball? SelectedHomeBall()
        => _homeBall.SelectedItem is Ball ball ? ball : null;

    private void UpdateHomeControls()
    {
        HomeCollectionPreset? preset = SelectedHomePreset();
        if (preset is null)
            return;

        _homeBall.Enabled = preset.Kind is HomeCollectionKind.BallDex;
        _homeNotes.Text =
            $"{preset.Name} · {preset.Support}\r\n{preset.Description}\r\n\r\n" +
            "Generator = puede enviar un preset al generador legal de NaturalDex. " +
            "Tracker = audita cajas/manifiesto sin inventar una forma de obtención. " +
            "Hybrid = puede generar una parte y auditar el resto.";

        _homePlan = null;
        _homeGrid.Rows.Clear();
        _homeSummary.Text = "Sin analizar";
    }

    private async Task AnalyzeHomeCollectionAsync()
    {
        HomeCollectionPreset? preset = SelectedHomePreset();
        if (preset is null)
            return;

        if (preset.Kind is HomeCollectionKind.EventDex or HomeCollectionKind.MythicalEventDex)
        {
            try
            {
                _homeNotes.Text = "Cargando EventsGallery para construir EventDex...";
                _eventCatalog ??= await EnsureEventCatalogAsync(false, _provider.SAV.BlankPKM.Context, CancellationToken.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "No fue posible cargar EventsGallery. El resto de HOME Collections sigue disponible.\r\n\r\n" +
                    ex.GetBaseException().Message,
                    "NDX — HOME Collections",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        BuildHomePlan(useExistingCatalog: true);
    }

    private void BuildHomePlan(bool useExistingCatalog)
    {
        HomeCollectionPreset? preset = SelectedHomePreset();
        if (preset is null)
            return;

        try
        {
            _homePlan = HomeCollectionCatalog.Build(
                _provider.SAV,
                preset.Kind,
                SelectedHomeBall(),
                useExistingCatalog ? _eventCatalog : null);

            RenderHomePlan();
        }
        catch (Exception ex)
        {
            _homePlan = null;
            _homeGrid.Rows.Clear();
            _homeSummary.Text = "Error";
            _homeNotes.Text = ex.ToString();
            MessageBox.Show(this, ex.GetBaseException().Message, "NDX — HOME Collections",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RenderHomePlan()
    {
        _homeGrid.SuspendLayout();
        _homeGrid.Rows.Clear();

        if (_homePlan is null)
        {
            _homeGrid.ResumeLayout();
            return;
        }

        SaveFile sav = _provider.SAV;
        IEnumerable<HomeCollectionStatus> rows = _homePlan.Status;
        if (_homeOnlyMissing.Checked)
            rows = rows.Where(z => !z.Owned);

        foreach (HomeCollectionStatus s in rows)
        {
            HomeCollectionEntry e = s.Entry;
            string name;
            try
            {
                name = SpeciesName.GetSpeciesNameGeneration(e.Species, 2, sav.Generation);
                if (string.IsNullOrWhiteSpace(name))
                    name = $"#{e.Species}";
            }
            catch
            {
                name = $"#{e.Species}";
            }

            string variant = BuildVariantText(e);
            int row = _homeGrid.Rows.Add(
                e.Index,
                e.Group,
                $"#{e.Species:0000} {name}",
                variant,
                e.Requirement,
                s.Owned ? "✓ Ya está" : "Falta",
                s.SupportedInCurrentGame ? "Compatible" : "Otro juego/formato",
                s.Owned ? s.MatchedBy : "—",
                e.Notes);

            if (!s.Owned)
                _homeGrid.Rows[row].DefaultCellStyle.Font = new System.Drawing.Font(_homeGrid.Font, System.Drawing.FontStyle.Bold);
        }

        _homeSummary.Text =
            $"{_homePlan.Owned}/{_homePlan.Total} · Faltan {_homePlan.Missing} · " +
            $"Compatibles aquí {_homePlan.Supported} · Externos {_homePlan.External}";
        _homeNotes.Text =
            $"{_homePlan.Preset.Name} · {_homePlan.Preset.Support}\r\n" +
            _homePlan.Notes +
            (_homePlan.Total == 0 ? "\r\n\r\nNo hay entradas para este save/configuración." : string.Empty);

        _homeGrid.ResumeLayout();
    }

    private static string BuildVariantText(HomeCollectionEntry e)
    {
        var parts = new List<string>();
        if (e.Form >= 0)
            parts.Add("Form " + e.Form);
        if (e.Gender == 0)
            parts.Add("♂");
        else if (e.Gender == 1)
            parts.Add("♀");
        if (e.Shiny == true)
            parts.Add("Shiny");
        if (e.Ball.HasValue)
            parts.Add(((Ball)e.Ball.Value).ToString());
        if (e.Origin.HasValue)
            parts.Add("Origen " + e.Origin.Value);
        if (e.Language.HasValue)
            parts.Add(((LanguageID)e.Language.Value).ToString());
        if (e.Alpha == true)
            parts.Add("Alpha");
        if (e.RequireMark)
            parts.Add("Mark");
        if (e.RequireRibbon)
            parts.Add("Ribbon");
        if (e.RequireEvent)
            parts.Add(e.EventCardID.HasValue ? $"Event {e.EventCardID:0000}" : "Event");
        return parts.Count == 0 ? "Normal" : string.Join(" · ", parts);
    }

    private void GenerateHomeCollectionPreset()
    {
        HomeCollectionPreset? preset = SelectedHomePreset();
        if (preset is null)
            return;

        DexLayoutMode? layout = preset.GeneratorLayout;
        if (preset.Kind == HomeCollectionKind.BallDex)
            layout = DexLayoutMode.BasePlusDLC;

        if (!layout.HasValue)
        {
            MessageBox.Show(this,
                "Esta colección funciona como tracker/manifiesto porque no existe una generación automática universal que sea legal para todas sus entradas.\r\n\r\n" +
                "Usa ANALIZAR COLECCIÓN para ver faltantes y compatibilidad.",
                "NDX — HOME Collections",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if ((int)layout.Value >= _dexLayout.Items.Count)
        {
            MessageBox.Show(this, "El preset de generación no está disponible en esta build.", "NDX — HOME Collections",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _mode.SelectedIndex = preset.GeneratorShiny ? 1 : 0;
        _dexLayout.SelectedIndex = (int)layout.Value;
        _skipExisting.Checked = true;

        if (preset.ForceCurrentGameOrigin)
            _sisterVersionFallback.Checked = false;

        if (preset.Kind == HomeCollectionKind.BallDex)
        {
            if (SelectedHomeBall() is not Ball ball)
            {
                MessageBox.Show(this, "Selecciona una Ball primero.", "NDX — HOME Collections",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _ballMode.SelectedIndex = 2; // Fixed
            _fixedBall.SelectedItem = ball;
        }

        if (_mainTabs is not null && _mainTabs.TabPages.Count > 0)
            _mainTabs.SelectedIndex = 0;

        MessageBox.Show(this,
            $"Preset aplicado: {preset.Name}\r\n\r\n" +
            "NDX usará el mismo preflight, LegalityAnalysis, eventos, huecos y backup de Living Dex. " +
            "Las entradas que no puedan existir legalmente en este juego quedarán como fallo/hueco; no se forzarán.",
            "NDX — HOME Collections",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        GenerateClick(this, EventArgs.Empty);
    }

    private void ExportHomeCollectionCsv()
    {
        if (_homePlan is null)
        {
            MessageBox.Show(this, "Analiza una colección primero.", "NDX — HOME Collections",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sd = new SaveFileDialog
        {
            Filter = "CSV|*.csv|Todos|*.*",
            FileName = SafeHomeFileName(_homePlan.Preset.Name) + ".csv",
        };
        if (sd.ShowDialog(this) != DialogResult.OK)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("Index,Group,Species,Form,Gender,Shiny,Ball,Origin,Language,Alpha,Mark,Ribbon,EventCard,Owned,Supported,Location,Requirement,Notes");
        foreach (HomeCollectionStatus s in _homePlan.Status)
        {
            HomeCollectionEntry e = s.Entry;
            string line = string.Join(",",
                e.Index,
                Csv(e.Group),
                e.Species,
                e.Form,
                e.Gender,
                e.Shiny?.ToString() ?? "",
                e.Ball?.ToString() ?? "",
                Csv(e.Origin?.ToString() ?? ""),
                e.Language?.ToString() ?? "",
                e.Alpha?.ToString() ?? "",
                e.RequireMark,
                e.RequireRibbon,
                e.EventCardID?.ToString() ?? "",
                s.Owned,
                s.SupportedInCurrentGame,
                Csv(s.MatchedBy),
                Csv(e.Requirement),
                Csv(e.Notes));
            sb.AppendLine(line);
        }
        File.WriteAllText(sd.FileName, sb.ToString(), Encoding.UTF8);
    }

    private void ExportHomeCollectionJson()
    {
        if (_homePlan is null)
        {
            MessageBox.Show(this, "Analiza una colección primero.", "NDX — HOME Collections",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sd = new SaveFileDialog
        {
            Filter = "NDX HOME manifest|*.ndxhome.json|JSON|*.json|Todos|*.*",
            FileName = SafeHomeFileName(_homePlan.Preset.Name) + ".ndxhome.json",
        };
        if (sd.ShowDialog(this) != DialogResult.OK)
            return;

        var export = new
        {
            format = "NDX-HOME-Collection/1",
            created = DateTimeOffset.Now,
            game = _provider.SAV.Version.ToString(),
            trainer = _provider.SAV.OT,
            collection = _homePlan.Preset.Name,
            support = _homePlan.Preset.Support.ToString(),
            totals = new
            {
                total = _homePlan.Total,
                owned = _homePlan.Owned,
                missing = _homePlan.Missing,
                supportedHere = _homePlan.Supported,
                external = _homePlan.External,
            },
            entries = _homePlan.Status.Select(s => new
            {
                s.Entry.Index,
                s.Entry.Group,
                s.Entry.Species,
                s.Entry.Form,
                s.Entry.Gender,
                s.Entry.Shiny,
                s.Entry.Ball,
                Origin = s.Entry.Origin?.ToString(),
                s.Entry.Language,
                s.Entry.Alpha,
                s.Entry.RequireMark,
                s.Entry.RequireRibbon,
                s.Entry.RequireEvent,
                s.Entry.EventCardID,
                s.Entry.Requirement,
                s.Entry.Notes,
                s.Owned,
                s.SupportedInCurrentGame,
                s.BoxSlot,
                s.MatchedBy,
            }),
        };

        string json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(sd.FileName, json, Encoding.UTF8);
    }

    private static string Csv(string value)
        => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

    private static string SafeHomeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "HOME-Collection" : value.Trim();
    }
}
