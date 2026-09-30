using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

public sealed class SwShLeagueCardGameViewForm : Form
{
    private readonly SAV8SWSH _sav;
    private byte[] _card;
    private readonly SwShLeagueCardCanvas _canvas;
    private readonly Label _mode = new()
    {
        AutoSize = true,
        Padding = new Padding(8, 7, 8, 0),
        Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
    };

    public bool Edited { get; private set; }
    public byte[] Result => _card.ToArray();

    public SwShLeagueCardGameViewForm(SAV8SWSH sav, byte[] card, string title)
    {
        if (card.Length != 0x1D0)
            throw new InvalidDataException("League Card inválida: se esperaban 0x1D0 bytes.");

        _sav = sav;
        _card = card.ToArray();
        _canvas = new SwShLeagueCardCanvas(sav, _card)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(12),
        };

        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 760);
        Size = new Size(1080, 860);

        BuildUI();
        UpdateMode();
    }

    private void BuildUI()
    {
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            WrapContents = true,
        };

        var flip = new Button { Text = "↻ VOLTEAR TARJETA", AutoSize = true };
        var edit = new Button { Text = "EDITAR DATOS", AutoSize = true };
        var export = new Button { Text = "EXPORTAR PREVIEW PNG", AutoSize = true };
        var raw = new Button { Text = "EXPORTAR .LC8", AutoSize = true };
        var cardSet = new Button { Text = "COPIAR --card-set", AutoSize = true };
        var close = new Button { Text = "Cerrar", AutoSize = true };

        flip.Click += (_, _) =>
        {
            _canvas.ShowBack = !_canvas.ShowBack;
            _canvas.Invalidate();
            UpdateMode();
        };

        edit.Click += (_, _) =>
        {
            using var editor = new SwShTrainerCardEditorForm(_sav, _card, "NDX — Editar League Card");
            if (editor.ShowDialog(this) != DialogResult.OK)
                return;

            _card = editor.Result;
            _canvas.SetCard(_card);
            Edited = true;
        };

        export.Click += (_, _) =>
        {
            using var sd = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                FileName = _canvas.ShowBack ? "LeagueCard_Back.png" : "LeagueCard_Front.png",
            };
            if (sd.ShowDialog(this) != DialogResult.OK)
                return;

            using Bitmap bmp = _canvas.RenderToBitmap(1080, 1300);
            bmp.Save(sd.FileName, System.Drawing.Imaging.ImageFormat.Png);
        };

        raw.Click += (_, _) =>
        {
            var data = SwShLeagueCardData.Parse(_sav, _card);
            using var sd = new SaveFileDialog
            {
                Filter = "SWSH League Card|*.lc8|Todos|*.*",
                FileName = $"LeagueCard_{Safe(data.Name)}_{data.TrainerId:000000}.lc8",
            };
            if (sd.ShowDialog(this) == DialogResult.OK)
                File.WriteAllBytes(sd.FileName, _card);
        };

        cardSet.Click += (_, _) =>
        {
            Clipboard.SetText(SwShPokeLdnCardBridge.BuildCardSetArguments(_card, _sav));
            MessageBox.Show(this, "Parámetros --card-set copiados.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        close.Click += (_, _) =>
        {
            DialogResult = Edited ? DialogResult.OK : DialogResult.Cancel;
            Close();
        };

        top.Controls.Add(flip);
        top.Controls.Add(edit);
        top.Controls.Add(export);
        top.Controls.Add(raw);
        top.Controls.Add(cardSet);
        top.Controls.Add(_mode);
        top.Controls.Add(close);

        var note = new Label
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(12),
            MaximumSize = new Size(1040, 0),
            Text =
                "Vista NDX Game Style: el reverso usa directamente los datos guardados en TrainerCard8. " +
                "El frente reconstruye la composición visual de Sword/Shield y conserva los IDs reales de apariencia; " +
                "el modelo 3D, pose, fondo y efectos exactos todavía no pueden reconstruirse desde estos datos.",
        };

        Controls.Add(_canvas);
        Controls.Add(top);
        Controls.Add(note);
    }

    private void UpdateMode() => _mode.Text = _canvas.ShowBack ? "REVERSO · datos de la tarjeta" : "FRENTE · Game Style reconstruido";

    private static string Safe(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "Trainer" : value.Trim();
    }
}

internal sealed class SwShLeagueCardCanvas : Control
{
    private const int BaseWidth = 540;
    private const int BaseHeight = 650;

    private readonly SAV8SWSH _sav;
    private byte[] _card;

    public bool ShowBack { get; set; }

    public SwShLeagueCardCanvas(SAV8SWSH sav, byte[] card)
    {
        _sav = sav;
        _card = card.ToArray();
        DoubleBuffered = true;
        BackColor = SystemColors.ControlDarkDark;
        MinimumSize = new Size(540, 650);
    }

    public void SetCard(byte[] card)
    {
        _card = card.ToArray();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Render(e.Graphics, ClientRectangle);
    }

    public Bitmap RenderToBitmap(int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(24, 26, 32));
        Render(g, new Rectangle(0, 0, width, height));
        return bmp;
    }

    private void Render(Graphics g, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float scale = Math.Min((bounds.Width - 30f) / BaseWidth, (bounds.Height - 30f) / BaseHeight);
        float w = BaseWidth * scale;
        float h = BaseHeight * scale;
        float ox = bounds.Left + (bounds.Width - w) / 2f;
        float oy = bounds.Top + (bounds.Height - h) / 2f;

        var state = g.Save();
        g.TranslateTransform(ox, oy);
        g.ScaleTransform(scale, scale);

        using var shadow = RoundedRect(new RectangleF(7, 9, BaseWidth - 14, BaseHeight - 14), 24);
        using var shadowBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
        g.FillPath(shadowBrush, shadow);

        using var cardPath = RoundedRect(new RectangleF(0, 0, BaseWidth - 14, BaseHeight - 14), 24);
        g.SetClip(cardPath);

        SwShLeagueCardData data = SwShLeagueCardData.Parse(_sav, _card);
        if (ShowBack)
            DrawBack(g, data);
        else
            DrawFront(g, data);

        g.ResetClip();
        using var borderPen = new Pen(Color.FromArgb(230, 245, 245, 245), 3);
        g.DrawPath(borderPen, cardPath);
        g.Restore(state);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void DrawFront(Graphics g, SwShLeagueCardData d)
    {
        Color main = d.Game == 1 ? Color.FromArgb(194, 40, 73) : Color.FromArgb(27, 154, 214);
        Color accent = d.Game == 1 ? Color.FromArgb(63, 36, 102) : Color.FromArgb(18, 74, 143);

        using (var bg = new LinearGradientBrush(new Rectangle(0, 0, BaseWidth, BaseHeight), main, accent, 35f))
            g.FillRectangle(bg, 0, 0, BaseWidth, BaseHeight);

        using (var haze = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
        {
            g.FillEllipse(haze, -100, 70, 430, 430);
            g.FillEllipse(haze, 270, -100, 360, 360);
        }

        using (var diagonal = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
        {
            g.FillPolygon(diagonal, new[]
            {
                new PointF(-40, 480), new PointF(540, 250), new PointF(540, 340), new PointF(-40, 570)
            });
        }

        DrawGameBadge(g, d.Game);
        DrawDexBadges(g, d);

        DrawTrainerFigure(g, d);

        using var footer = new SolidBrush(Color.FromArgb(170, 12, 15, 22));
        g.FillRectangle(footer, 0, 500, BaseWidth, 136);

        using var numberFont = new Font("Segoe UI", 58, FontStyle.Bold);
        using var nameFont = new Font("Segoe UI", 31, FontStyle.Bold);
        using var subFont = new Font("Segoe UI", 12, FontStyle.Regular);
        using var white = new SolidBrush(Color.White);
        using var soft = new SolidBrush(Color.FromArgb(220, 235, 240, 250));

        string no = string.IsNullOrWhiteSpace(d.Number) ? "---" : d.Number;
        g.DrawString(no, numberFont, white, 22, 520);
        SizeF nameSize = g.MeasureString(d.Name, nameFont);
        g.DrawString(d.Name, nameFont, white, Math.Max(190, 500 - nameSize.Width), 530);

        string outfit = $"Hair {d.Hair:X} · Hat {d.Hat:X} · Top {d.Top:X}";
        SizeF outfitSize = g.MeasureString(outfit, subFont);
        g.DrawString(outfit, subFont, soft, Math.Max(188, 500 - outfitSize.Width), 579);

        using var ndxFont = new Font("Segoe UI", 9, FontStyle.Bold);
        g.DrawString("NDX GAME STYLE PREVIEW", ndxFont, soft, 22, 610);
    }

    private static void DrawGameBadge(Graphics g, byte game)
    {
        string text = game == 1 ? "SHIELD" : "SWORD";
        Color color = game == 1 ? Color.FromArgb(238, 48, 87) : Color.FromArgb(24, 184, 232);

        using var box = RoundedRect(new RectangleF(18, 18, 122, 55), 12);
        using var b = new SolidBrush(Color.FromArgb(220, 10, 15, 25));
        using var line = new Pen(color, 3);
        using var font = new Font("Segoe UI", 16, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        g.FillPath(b, box);
        g.DrawPath(line, box);
        g.DrawString(text, font, textBrush, 31, 32);
    }

    private static void DrawDexBadges(Graphics g, SwShLeagueCardData d)
    {
        DrawDexBadge(g, 378, 24, "G", d.GalarDexComplete);
        DrawDexBadge(g, 426, 24, "A", d.ArmorDexComplete);
        DrawDexBadge(g, 474, 24, "C", d.CrownDexComplete);
    }

    private static void DrawDexBadge(Graphics g, float x, float y, string label, bool active)
    {
        using var b = new SolidBrush(active ? Color.FromArgb(242, 202, 51) : Color.FromArgb(90, 30, 35, 45));
        using var p = new Pen(Color.FromArgb(230, 255, 255, 255), 2);
        using var f = new Font("Segoe UI", 12, FontStyle.Bold);
        using var t = new SolidBrush(active ? Color.FromArgb(55, 42, 0) : Color.WhiteSmoke);
        g.FillEllipse(b, x, y, 39, 39);
        g.DrawEllipse(p, x, y, 39, 39);
        g.DrawString(label, f, t, x + 10, y + 8);
    }

    private static void DrawTrainerFigure(Graphics g, SwShLeagueCardData d)
    {
        int seed = unchecked((int)(d.Skin ^ d.Hair ^ d.Jacket ^ d.Top ^ d.BottomOrDress));
        int r = 75 + Math.Abs(seed & 0x5F);
        int gr = 75 + Math.Abs((seed >> 8) & 0x5F);
        int b = 75 + Math.Abs((seed >> 16) & 0x5F);
        Color outfit = Color.FromArgb(Math.Min(220, r), Math.Min(220, gr), Math.Min(220, b));

        float cx = 270;
        using var shadow = new SolidBrush(Color.FromArgb(65, 0, 0, 0));
        g.FillEllipse(shadow, 145, 420, 270, 55);

        using var skin = new SolidBrush(Color.FromArgb(242, 205, 167));
        using var hair = new SolidBrush(Color.FromArgb(45, 36, 40));
        using var cloth = new SolidBrush(outfit);
        using var clothDark = new SolidBrush(Color.FromArgb(Math.Max(0, outfit.R - 45), Math.Max(0, outfit.G - 45), Math.Max(0, outfit.B - 45)));

        g.FillEllipse(hair, cx - 64, 104, 128, 132);
        g.FillEllipse(skin, cx - 49, 121, 98, 110);

        if (d.Gender == 1)
        {
            g.FillEllipse(hair, cx - 77, 153, 36, 156);
            g.FillEllipse(hair, cx + 41, 153, 36, 156);
        }

        using var torso = RoundedRect(new RectangleF(cx - 91, 225, 182, 185), 42);
        g.FillPath(cloth, torso);
        g.FillEllipse(clothDark, cx - 125, 238, 60, 155);
        g.FillEllipse(clothDark, cx + 65, 238, 60, 155);

        g.FillRectangle(clothDark, cx - 72, 388, 58, 85);
        g.FillRectangle(clothDark, cx + 14, 388, 58, 85);

        using var facePen = new Pen(Color.FromArgb(90, 30, 30, 30), 3);
        g.DrawArc(facePen, cx - 24, 174, 48, 26, 15, 150);

        using var f = new Font("Segoe UI", 10, FontStyle.Bold);
        using var br = new SolidBrush(Color.FromArgb(215, 255, 255, 255));
        string txt = "avatar reconstruido desde IDs de apariencia";
        SizeF s = g.MeasureString(txt, f);
        g.DrawString(txt, f, br, cx - s.Width / 2, 466);
    }

    private static void DrawBack(Graphics g, SwShLeagueCardData d)
    {
        using (var back = new SolidBrush(Color.FromArgb(240, 242, 245)))
            g.FillRectangle(back, 0, 0, BaseWidth, BaseHeight);

        using var top = new SolidBrush(Color.FromArgb(41, 49, 63));
        g.FillRectangle(top, 0, 0, BaseWidth, 112);

        using var title = new Font("Segoe UI", 22, FontStyle.Bold);
        using var nameFont = new Font("Segoe UI", 16, FontStyle.Bold);
        using var white = new SolidBrush(Color.White);
        using var dark = new SolidBrush(Color.FromArgb(35, 39, 48));
        using var muted = new SolidBrush(Color.FromArgb(90, 96, 108));
        using var rowPen = new Pen(Color.FromArgb(205, 210, 218), 1);

        g.DrawString("LEAGUE CARD", title, white, 18, 18);
        g.DrawString(d.Name, nameFont, white, 20, 62);
        g.DrawString($"ID {d.TrainerId:000000}   No. {d.Number}", nameFont, white, 290, 62);

        for (int i = 0; i < 6; i++)
        {
            float x = 18 + i * 84;
            DrawPokemonSlot(g, d.Pokemon[i], x, 126);
        }

        int y = 245;
        DrawDataRow(g, "First partner", StarterName(d.Starter), y, dark, muted, rowPen); y += 39;
        DrawDataRow(g, "Pokédex registered", d.PokeDexOwned.ToString(), y, dark, muted, rowPen); y += 39;
        DrawDataRow(g, "Adventure began", d.StartedText, y, dark, muted, rowPen); y += 39;
        DrawDataRow(g, "Curry Dex", d.CurryTypesOwned.ToString(), y, dark, muted, rowPen); y += 39;
        DrawDataRow(g, "Rotom Rally best", d.RotoRallyScore.ToString("N0"), y, dark, muted, rowPen); y += 39;
        DrawDataRow(g, "Pokémon caught", d.CaughtPokemon.ToString("N0"), y, dark, muted, rowPen); y += 39;
        DrawDataRow(g, "Shiny Pokémon found", d.ShinyPokemonFound.ToString(), y, dark, muted, rowPen); y += 39;

        using var foot = new SolidBrush(Color.FromArgb(225, 228, 234));
        g.FillRectangle(foot, 0, 535, BaseWidth, 101);

        using var footTitle = new Font("Segoe UI", 11, FontStyle.Bold);
        using var footValue = new Font("Segoe UI", 10, FontStyle.Regular);
        g.DrawString($"Trainer Card ID: {d.TrainerId:000000}", footTitle, dark, 20, 551);
        g.DrawString($"Uniform No.: {d.Number}", footTitle, dark, 20, 578);
        g.DrawString($"Game: {(d.Game == 1 ? "Pokémon Shield" : "Pokémon Sword")} · Printed: {d.PrintedText}", footValue, muted, 20, 606);
    }

    private static void DrawPokemonSlot(Graphics g, SwShLeagueCardPokemon p, float x, float y)
    {
        using var slot = RoundedRect(new RectangleF(x, y, 72, 94), 10);
        using var sb = new SolidBrush(Color.White);
        using var sp = new Pen(Color.FromArgb(205, 210, 218), 1);
        g.FillPath(sb, slot);
        g.DrawPath(sp, slot);

        if (p.Species == 0)
        {
            using var dashFont = new Font("Segoe UI", 18, FontStyle.Bold);
            using var mute = new SolidBrush(Color.LightGray);
            g.DrawString("—", dashFont, mute, x + 24, y + 25);
            return;
        }

        using Bitmap? sprite = PkhexSpriteBridge.TryGetSprite(p);
        if (sprite is not null)
            g.DrawImage(sprite, new RectangleF(x + 9, y + 8, 54, 54));
        else
        {
            using var circle = new SolidBrush(Color.FromArgb(225, 232, 240));
            g.FillEllipse(circle, x + 13, y + 10, 46, 46);
        }

        using var f = new Font("Segoe UI", 8, FontStyle.Bold);
        using var t = new SolidBrush(Color.FromArgb(45, 50, 60));
        string name = GetSpeciesName(p.Species);
        if (name.Length > 11)
            name = name[..11];
        g.DrawString(name, f, t, x + 4, y + 65);
        if (p.Shiny)
        {
            using var shine = new SolidBrush(Color.Goldenrod);
            g.DrawString("★", f, shine, x + 55, y + 4);
        }
    }

    private static void DrawDataRow(Graphics g, string key, string value, int y, Brush dark, Brush muted, Pen line)
    {
        using var keyFont = new Font("Segoe UI", 11, FontStyle.Regular);
        using var valueFont = new Font("Segoe UI", 11, FontStyle.Bold);
        g.DrawString(key, keyFont, muted, 24, y + 8);
        SizeF sz = g.MeasureString(value, valueFont);
        g.DrawString(value, valueFont, dark, 510 - sz.Width, y + 8);
        g.DrawLine(line, 20, y + 37, 510, y + 37);
    }

    private static string StarterName(byte starter) => starter switch
    {
        0 => "Grookey",
        1 => "Scorbunny",
        2 => "Sobble",
        _ => $"Unknown ({starter})",
    };

    private static string GetSpeciesName(ushort species)
    {
        try
        {
            var arr = GameInfo.Strings.Species;
            if (species < arr.Count)
                return arr[species];
        }
        catch { }
        return $"#{species}";
    }
}

internal static class PkhexSpriteBridge
{
    private static readonly Dictionary<string, Bitmap> Cache = new();
    private static MethodInfo? _getSprite;
    private static bool _searched;

    public static Bitmap? TryGetSprite(SwShLeagueCardPokemon p)
    {
        if (p.Species == 0)
            return null;

        string key = $"{p.Species}-{p.Form}-{p.Gender}-{p.FormArgument}-{p.Shiny}";
        if (Cache.TryGetValue(key, out Bitmap? cached))
            return (Bitmap)cached.Clone();

        try
        {
            MethodInfo? method = FindMethod();
            if (method is null)
                return null;

            object? result = method.Invoke(null, new object[]
            {
                p.Species,
                p.Form,
                p.Gender,
                unchecked((uint)Math.Max(0, p.FormArgument)),
                0,
                false,
                p.Shiny ? Shiny.Always : Shiny.Never,
                EntityContext.Gen8,
            });

            if (result is not Bitmap bmp)
                return null;

            Cache[key] = (Bitmap)bmp.Clone();
            return (Bitmap)bmp.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static MethodInfo? FindMethod()
    {
        if (_searched)
            return _getSprite;
        _searched = true;

        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type =
                asm.GetType("PKHeX.Drawing.PokeSprite.SpriteUtil", false) ??
                asm.GetType("PKHeX.WinForms.SpriteUtil", false);
            if (type is null)
                continue;

            _getSprite = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetSprite" && m.GetParameters().Length == 8);
            if (_getSprite is not null)
                return _getSprite;
        }

        return null;
    }
}

internal sealed class SwShLeagueCardData
{
    public string Name { get; init; } = "";
    public int TrainerId { get; init; }
    public byte Language { get; init; }
    public byte Game { get; init; }
    public byte Starter { get; init; }
    public byte Gender { get; init; }
    public string Number { get; init; } = "";
    public ushort PokeDexOwned { get; init; }
    public ushort ShinyPokemonFound { get; init; }
    public ushort CurryTypesOwned { get; init; }
    public int RotoRallyScore { get; init; }
    public int CaughtPokemon { get; init; }
    public bool GalarDexComplete { get; init; }
    public bool ArmorDexComplete { get; init; }
    public bool CrownDexComplete { get; init; }
    public ushort StartedYear { get; init; }
    public byte StartedMonth { get; init; }
    public byte StartedDay { get; init; }
    public uint TimestampPrinted { get; init; }
    public ulong Skin { get; init; }
    public ulong Hair { get; init; }
    public ulong Brow { get; init; }
    public ulong Lashes { get; init; }
    public ulong Contacts { get; init; }
    public ulong Lips { get; init; }
    public ulong Glasses { get; init; }
    public ulong Hat { get; init; }
    public ulong Jacket { get; init; }
    public ulong Top { get; init; }
    public ulong Bag { get; init; }
    public ulong Gloves { get; init; }
    public ulong BottomOrDress { get; init; }
    public ulong Sock { get; init; }
    public ulong Shoe { get; init; }
    public SwShLeagueCardPokemon[] Pokemon { get; init; } = new SwShLeagueCardPokemon[6];

    public string StartedText
        => StartedYear == 0 ? "—" : $"{StartedYear:D4}-{StartedMonth:D2}-{StartedDay:D2}";

    public string PrintedText
    {
        get
        {
            if (TimestampPrinted == 0)
                return "—";
            try { return DateTimeOffset.FromUnixTimeSeconds(TimestampPrinted).LocalDateTime.ToString("yyyy-MM-dd HH:mm"); }
            catch { return $"0x{TimestampPrinted:X8}"; }
        }
    }

    public static SwShLeagueCardData Parse(SAV8SWSH sav, byte[] raw)
    {
        if (raw.Length != 0x1D0)
            throw new InvalidDataException("League Card inválida.");

        var mons = new SwShLeagueCardPokemon[6];
        for (int i = 0; i < 6; i++)
        {
            int at = 0xC8 + i * 0x1C;
            mons[i] = new SwShLeagueCardPokemon(
                BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(at + 0x00, 2)),
                raw[at + 0x04],
                raw[at + 0x08],
                raw[at + 0x0C] != 0,
                BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(at + 0x10, 4)),
                BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(at + 0x18, 4))
            );
        }

        return new SwShLeagueCardData
        {
            Name = sav.GetString(raw.AsSpan(0x00, 0x1A)),
            Language = raw[0x1B],
            TrainerId = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(0x1C, 4)),
            PokeDexOwned = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0x20, 2)),
            ShinyPokemonFound = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0x22, 2)),
            Game = raw[0x24],
            Starter = raw[0x25],
            CurryTypesOwned = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0x26, 2)),
            RotoRallyScore = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(0x28, 4)),
            CaughtPokemon = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(0x2C, 4)),
            GalarDexComplete = raw[0x30] == 1,
            Gender = raw[0x38],
            Number = System.Text.Encoding.ASCII.GetString(raw, 0x39, 3).TrimEnd('\0', ' '),
            Skin = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x40, 8)),
            Hair = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x48, 8)),
            Brow = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x50, 8)),
            Lashes = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x58, 8)),
            Contacts = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x60, 8)),
            Lips = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x68, 8)),
            Glasses = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x70, 8)),
            Hat = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x78, 8)),
            Jacket = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x80, 8)),
            Top = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x88, 8)),
            Bag = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x90, 8)),
            Gloves = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0x98, 8)),
            BottomOrDress = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0xA0, 8)),
            Sock = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0xA8, 8)),
            Shoe = BinaryPrimitives.ReadUInt64LittleEndian(raw.AsSpan(0xB0, 8)),
            Pokemon = mons,
            StartedYear = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0x170, 2)),
            StartedMonth = raw[0x172],
            StartedDay = raw[0x173],
            TimestampPrinted = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x1A8, 4)),
            ArmorDexComplete = raw[0x1B4] == 1,
            CrownDexComplete = raw[0x1B5] == 1,
        };
    }
}

internal readonly record struct SwShLeagueCardPokemon(
    ushort Species,
    byte Form,
    byte Gender,
    bool Shiny,
    uint EncryptionConstant,
    int FormArgument);
