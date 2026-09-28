using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

public sealed partial class NaturalDexForm
{
    private void InitializeV150Tabs()
    {
        if (_mainTabs is null)
            return;
        if (_mainTabs.TabPages.Cast<TabPage>().Any(z => z.Text == "Switch Tools"))
            return;

        var page = new TabPage("Switch Tools");
        page.Controls.Add(BuildSwitchToolsPage());
        _mainTabs.TabPages.Add(page);
    }

    private Control BuildSwitchToolsPage()
    {
        SaveFile sav = _provider.SAV;
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(14),
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, 14f, System.Drawing.FontStyle.Bold),
            Text = "NDX Switch Tools v1.5.0",
        }, 0, 0);

        root.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(920, 0),
            Padding = new Padding(0, 4, 0, 12),
            Text =
                "Módulos integrados sobre NaturalDex v1.3.1. Sólo se muestran herramientas compatibles con el save abierto. " +
                "Los editores trabajan sobre un clon y crean backup antes de aplicar cambios.",
        }, 0, 1);

        var current = new GroupBox
        {
            Text = $"Herramientas para {sav.GetType().Name} / {sav.Version}",
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

        if (sav is SAV8SWSH)
        {
            AddToolButton(flow, "SWSH Raid Viewer / Editor", OpenSwShRaidTool);
            AddToolButton(flow, "Dynamax Adventure / Crown Tundra", OpenSwShAdventureTool);
            AddToolButton(flow, "Curry Dex — Importar / Exportar bloque", OpenCurryDexTool);
        }
        else if (sav is SAV8LA)
        {
            AddToolButton(flow, "PLA — Pokémon entregados a NPC / granja", OpenPLANPCTool);
        }
        else if (sav is SAV9SV)
        {
            AddToolButton(flow, "SV — Overworld Viewer", OpenSVOverworldTool);
        }
        else if (sav is SAV9ZA)
        {
            AddToolButton(flow, "Z-A — Shiny Stash Viewer", OpenZAStashTool);
        }
        else
        {
            flow.Controls.Add(new Label
            {
                AutoSize = true,
                Padding = new Padding(8),
                Text = "Este save no corresponde a uno de los módulos Switch especializados.",
            });
        }

        current.Controls.Add(flow);
        root.Controls.Add(current, 0, 2);

        var integrated = new GroupBox
        {
            Text = "Funciones ya absorbidas por NDX",
            Dock = DockStyle.Fill,
        };
        integrated.Controls.Add(new Label
        {
            AutoSize = true,
            Padding = new Padding(10),
            MaximumSize = new System.Drawing.Size(920, 0),
            Text =
                "Wonder Records Tool → integrado en Eventos / Wonder Cards: importación de WC, historial de regalo, fecha y validación.\r\n" +
                "Dynamax Adventure Reset → integrado en el módulo Crown Tundra.\r\n" +
                "SWSH Raid Plugin → visor/editor de dens nativo; se conservan Hash, Seed, Stars, Roll, tipo y flags.\r\n" +
                "Complete Curry Dex Block → importación/exportación segura del bloque KCurryDex.\r\n" +
                "SV Overworld Viewer → visor de las 20 entradas almacenadas y extracción de PK9.\r\n" +
                "Shiny Stash Map → visor/extractor del stash de Z-A; el mapa/teleport live no se escribe si no hay conexión validada.\r\n" +
                "PLA NPC Editor → escáner/editor de entidades PA8 fuera de cajas/equipo para localizar el bloque NPC/granja sin depender de una DLL antigua.",
        });
        root.Controls.Add(integrated, 0, 3);
        return root;
    }

    private static void AddToolButton(Control parent, string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true };
        b.Click += click;
        parent.Controls.Add(b);
    }

    private void OpenSwShRaidTool(object? sender, EventArgs e)
    {
        if (_provider.SAV is not SAV8SWSH live)
            return;

        var work = (SAV8SWSH)live.Clone();
        using var form = new SwShRaidToolForm(work);
        if (form.ShowDialog(this) != DialogResult.OK)
            return;
        CommitSwitchClone(live, work, "SWSH Raid Tool");
    }

    private void OpenSwShAdventureTool(object? sender, EventArgs e)
    {
        if (_provider.SAV is not SAV8SWSH live)
            return;

        var work = (SAV8SWSH)live.Clone();
        using var form = new SwShAdventureToolForm(work);
        if (form.ShowDialog(this) != DialogResult.OK)
            return;
        CommitSwitchClone(live, work, "Dynamax Adventure / Crown Tundra");
    }

    private void OpenCurryDexTool(object? sender, EventArgs e)
    {
        if (_provider.SAV is not SAV8SWSH live)
            return;

        const uint KCurryDex = 0x6EB72940;
        SCBlock block;
        try { block = live.Blocks.GetBlock(KCurryDex); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "NDX — Curry Dex", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var picker = new Form
        {
            Text = "NDX — Curry Dex",
            StartPosition = FormStartPosition.CenterParent,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
        };
        var panel = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(12), FlowDirection = FlowDirection.TopDown };
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(600, 0),
            Text = $"Bloque KCurryDex detectado: {block.Data.Length} bytes. Puedes exportarlo como backup o importar un bloque del mismo tamaño.",
        });
        var export = new Button { Text = "EXPORTAR KCurryDex...", AutoSize = true };
        var import = new Button { Text = "IMPORTAR KCurryDex...", AutoSize = true };
        var close = new Button { Text = "Cerrar", AutoSize = true };
        panel.Controls.Add(export);
        panel.Controls.Add(import);
        panel.Controls.Add(close);
        picker.Controls.Add(panel);

        export.Click += (_, _) =>
        {
            using var sd = new SaveFileDialog { Filter = "Curry Dex block|*.bin|Todos|*.*", FileName = "KCurryDex.bin" };
            if (sd.ShowDialog(picker) == DialogResult.OK)
                File.WriteAllBytes(sd.FileName, block.Data.ToArray());
        };

        import.Click += (_, _) =>
        {
            using var od = new OpenFileDialog { Filter = "Curry Dex block|*.bin|Todos|*.*" };
            if (od.ShowDialog(picker) != DialogResult.OK)
                return;
            byte[] data = File.ReadAllBytes(od.FileName);
            if (data.Length != block.Data.Length)
            {
                MessageBox.Show(picker,
                    $"Tamaño incorrecto. Esperado {block.Data.Length} bytes; archivo {data.Length} bytes.",
                    "NDX — Curry Dex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var work = (SAV8SWSH)live.Clone();
            work.Blocks.GetBlock(KCurryDex).ChangeData(data);
            if (MessageBox.Show(picker,
                    "El bloque tiene el tamaño correcto. ¿Aplicarlo al save abierto? Se creará un backup.",
                    "NDX — Curry Dex", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            CommitSwitchClone(live, work, "Curry Dex");
            picker.Close();
        };

        close.Click += (_, _) => picker.Close();
        picker.ShowDialog(this);
    }

    private void OpenSVOverworldTool(object? sender, EventArgs e)
    {
        if (_provider.SAV is SAV9SV sav)
            new SVOverworldToolForm(sav).ShowDialog(this);
    }

    private void OpenZAStashTool(object? sender, EventArgs e)
    {
        if (_provider.SAV is SAV9ZA sav)
            new ZAStashToolForm(sav).ShowDialog(this);
    }

    private void OpenPLANPCTool(object? sender, EventArgs e)
    {
        if (_provider.SAV is not SAV8LA live)
            return;

        var work = (SAV8LA)live.Clone();
        using var form = new PLANPCToolForm(work);
        if (form.ShowDialog(this) != DialogResult.OK || !form.Edited)
            return;
        CommitSwitchClone(live, work, "PLA NPC / Farm");
    }

    private void CommitSwitchClone(SaveFile live, SaveFile work, string label)
    {
        string backup;
        try
        {
            backup = SaveBackupManager.Create(live);
            live.CopyChangesFrom(work);
            _provider.ReloadSlots();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.GetBaseException().Message, "NDX — " + label, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        MessageBox.Show(this,
            $"{label}: cambios aplicados.\r\n\r\nBackup: {backup}",
            "NDX Tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
