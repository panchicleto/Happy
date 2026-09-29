using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

public sealed partial class NaturalDexForm
{
    private readonly TextBox _ndxAuditOutput = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Dock = DockStyle.Fill,
    };

    private readonly Label _ndxAuditStatus = new()
    {
        AutoSize = true,
        Text = "Auditor listo. No modifica el save.",
        Padding = new Padding(0, 7, 8, 0),
    };

    private void InitializeNdxSuiteTabs()
    {
        if (_mainTabs is null)
            return;
        if (_mainTabs.TabPages.Cast<TabPage>().Any(z => z.Text == "NDX Tools"))
            return;

        var hub = new TabPage("NDX Tools");
        hub.Controls.Add(BuildNdxHub());

        var audit = new TabPage("Save Auditor");
        audit.Controls.Add(BuildNdxAuditPage());

        _mainTabs.TabPages.Add(hub);
        _mainTabs.TabPages.Add(audit);
    }

    private Control BuildNdxHub()
    {
        SaveFile sav = _provider.SAV;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(14),
            AutoScroll = true,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, 14f, System.Drawing.FontStyle.Bold),
            Text = "NDX Tools v1.4.0",
        }, 0, 0);

        root.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(900, 0),
            Padding = new Padding(0, 4, 0, 12),
            Text =
                "Suite sobre NaturalDex v1.3.1. Mantiene el Living Dex, el gestor de Eventos/Wonder Cards " +
                "y añade auditoría global del save. Las herramientas se habilitan según el juego abierto.",
        }, 0, 1);

        var current = new GroupBox
        {
            Text = "Save abierto",
            Dock = DockStyle.Top,
            AutoSize = true,
        };
        current.Controls.Add(new Label
        {
            AutoSize = true,
            Padding = new Padding(10),
            MaximumSize = new System.Drawing.Size(900, 0),
            Text =
                $"{sav.GetType().Name} · {sav.Version} · Gen {sav.Generation}\r\n" +
                $"Entrenador: {sav.OT} · TID {sav.DisplayTID} · SID {sav.DisplaySID}\r\n" +
                GetSwitchModuleSummary(sav),
        });
        root.Controls.Add(current, 0, 2);

        var modules = new GroupBox
        {
            Text = "Módulos activos",
            Dock = DockStyle.Top,
            AutoSize = true,
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = true,
        };

        var living = new Button { Text = "Natural Encounter / Living Dex", AutoSize = true };
        living.Click += (_, _) => SelectSuiteTab("Living Dex");

        var gifts = new Button { Text = "Mystery Gifts / Wonder Cards", AutoSize = true };
        gifts.Click += (_, _) => SelectSuiteTab("Eventos / Wonder Cards");

        var auditor = new Button { Text = "Auditar save completo", AutoSize = true };
        auditor.Click += (_, _) =>
        {
            SelectSuiteTab("Save Auditor");
            RunNdxAudit();
        };

        flow.Controls.Add(living);
        flow.Controls.Add(gifts);
        flow.Controls.Add(auditor);
        modules.Controls.Add(flow);
        root.Controls.Add(modules, 0, 3);

        var roadmap = new GroupBox
        {
            Text = "Paquete Switch — integración modular",
            Dock = DockStyle.Fill,
        };
        roadmap.Controls.Add(new Label
        {
            AutoSize = true,
            Padding = new Padding(10),
            MaximumSize = new System.Drawing.Size(900, 0),
            Text =
                "Base común: Save Auditor + Natural Encounter Generator + Universal Mystery Gift Manager.\r\n\r\n" +
                "LGPE: soporte de save + legalidad.\r\n" +
                "Sword/Shield: eventos, WR8/Gift Album y expansión de raids/Dynamax/Crown Tundra.\r\n" +
                "BDSP: soporte de save y futuro Underground/Special Encounter Manager.\r\n" +
                "Legends Arceus: soporte de save y futuro Research/NPC Pokémon Manager.\r\n" +
                "Scarlet/Violet: soporte de save y futuro Overworld/Special Encounter Manager.\r\n" +
                "Legends Z-A: auditoría; escritura de historial de eventos sigue bloqueada cuando el layout no esté verificado.\r\n\r\n" +
                "NDX no fuerza legalidad, no fabrica HOME trackers y no escribe bloques no verificados.",
        });
        root.Controls.Add(roadmap, 0, 4);

        return root;
    }

    private Control BuildNdxAuditPage()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(900, 0),
            Text =
                "Analiza cajas y equipo con LegalityAnalysis, checksums, PID+EC repetidos, tipos de encuentro " +
                "y fechas futuras. Es de solo lectura: este módulo no modifica el save.",
        }, 0, 0);

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 8),
            WrapContents = true,
        };

        var run = new Button { Text = "EJECUTAR AUDITORÍA", AutoSize = true };
        run.Click += (_, _) => RunNdxAudit();

        var copy = new Button { Text = "COPIAR REPORTE", AutoSize = true };
        copy.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_ndxAuditOutput.Text))
                Clipboard.SetText(_ndxAuditOutput.Text);
        };

        var save = new Button { Text = "GUARDAR REPORTE...", AutoSize = true };
        save.Click += (_, _) => SaveNdxAuditReport();

        controls.Controls.Add(run);
        controls.Controls.Add(copy);
        controls.Controls.Add(save);
        controls.Controls.Add(_ndxAuditStatus);
        root.Controls.Add(controls, 0, 1);
        root.Controls.Add(_ndxAuditOutput, 0, 2);

        return root;
    }

    private void RunNdxAudit()
    {
        try
        {
            _ndxAuditStatus.Text = "Analizando save...";
            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                NDXAuditResult result = NDXSaveAuditor.Run(_provider.SAV);
                _ndxAuditOutput.Text = result.Summary;
                _ndxAuditStatus.Text =
                    $"Listo · {result.Pokemon} Pokémon · {result.Illegal} ilegales · " +
                    $"{result.DuplicateIdentities} PID+EC repetidos · {result.FutureDates} fechas futuras";
            }
            finally
            {
                Cursor = previous;
            }
        }
        catch (Exception ex)
        {
            _ndxAuditStatus.Text = "Error durante la auditoría.";
            _ndxAuditOutput.Text = ex.ToString();
            MessageBox.Show(this, ex.GetBaseException().Message, "NDX Save Auditor",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveNdxAuditReport()
    {
        if (string.IsNullOrWhiteSpace(_ndxAuditOutput.Text))
            RunNdxAudit();
        if (string.IsNullOrWhiteSpace(_ndxAuditOutput.Text))
            return;

        using var dialog = new SaveFileDialog
        {
            Title = "Guardar reporte NDX",
            Filter = "Archivo de texto|*.txt|Todos los archivos|*.*",
            FileName = $"NDX-Audit-{_provider.SAV.Version}-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        File.WriteAllText(dialog.FileName, _ndxAuditOutput.Text);
        _ndxAuditStatus.Text = "Reporte guardado: " + dialog.FileName;
    }

    private void SelectSuiteTab(string title)
    {
        if (_mainTabs is null)
            return;

        foreach (TabPage page in _mainTabs.TabPages)
        {
            if (!string.Equals(page.Text, title, StringComparison.OrdinalIgnoreCase))
                continue;
            _mainTabs.SelectedTab = page;
            return;
        }
    }

    private static string GetSwitchModuleSummary(SaveFile sav) => sav switch
    {
        SAV7b => "Switch / Let's Go: auditoría + generación natural disponible.",
        SAV8SWSH => "Switch / Sword-Shield: auditoría + generación natural + Wonder Cards; base lista para raids/Dynamax/Crown Tundra.",
        SAV8BS => "Switch / BDSP: auditoría + generación natural; base lista para Underground/Special Encounters.",
        SAV8LA => "Switch / Legends Arceus: auditoría + generación natural; base lista para Research/NPC Pokémon.",
        SAV9SV => "Switch / Scarlet-Violet: auditoría + generación natural + gestor de eventos compatible; base lista para Overworld.",
        SAV9ZA => "Switch / Legends Z-A: auditoría disponible; bloques/historial no verificados permanecen en solo lectura.",
        _ => "Formato compatible con NaturalDex; los módulos específicos de Switch se habilitan solo en saves Switch.",
    };
}
