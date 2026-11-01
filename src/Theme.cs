using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/// <summary>Colors, fonts and the few custom-drawn controls that give the window its look.</summary>
static class Theme
{
    public static readonly Color Bg = Color.FromArgb(243, 244, 247);
    public static readonly Color Card = Color.White;
    public static readonly Color Border = Color.FromArgb(226, 228, 234);
    public static readonly Color Text = Color.FromArgb(27, 31, 42);
    public static readonly Color Muted = Color.FromArgb(107, 114, 128);
    public static readonly Color Faint = Color.FromArgb(170, 175, 186);
    public static readonly Color Accent = Color.FromArgb(242, 95, 28);
    public static readonly Color AccentDark = Color.FromArgb(210, 78, 16);
    public static readonly Color AccentSoft = Color.FromArgb(255, 237, 222);
    public static readonly Color Green = Color.FromArgb(21, 128, 61);
    public static readonly Color GreenSoft = Color.FromArgb(220, 252, 231);
    public static readonly Color Red = Color.FromArgb(200, 38, 38);
    public static readonly Color Side = Color.FromArgb(23, 27, 36);
    public static readonly Color SideItem = Color.FromArgb(36, 42, 56);
    public static readonly Color SideText = Color.FromArgb(232, 234, 240);
    public static readonly Color SideMuted = Color.FromArgb(138, 146, 166);
    public static readonly Color Console = Color.FromArgb(22, 26, 34);
    public static readonly Color ConsoleText = Color.FromArgb(190, 220, 200);

    public static float Scale = 1f;
    public static int S(int px) { return (int)Math.Round(px * Scale); }

    public static Font UI(float size, FontStyle style = FontStyle.Regular) { return new Font("Segoe UI", size, style); }
    public static Font Semibold(float size) { return new Font("Segoe UI Semibold", size); }
    public static Font Icons(float size)
    {
        var f = new Font("Segoe Fluent Icons", size);
        return f.Name == "Segoe Fluent Icons" ? f : new Font("Segoe MDL2 Assets", size);
    }

    // Glyphs shared by Segoe Fluent Icons and Segoe MDL2 Assets.
    public const string IconTv = "", IconSearch = "", IconAdd = "", IconRight = "",
        IconLeft = "", IconUp = "", IconDown = "", IconSave = "", IconUndo = "",
        IconFolder = "", IconCheck = "", IconPlug = "";

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public enum Style { Primary, Secondary, Danger, Side }

    /// <summary>A flat button with an optional icon glyph in front of the text.</summary>
    public static Button Button(string icon, string text, Style style, EventHandler click)
    {
        var b = new FlatButton { Glyph = icon, Text = text, Kind = style, Height = S(38), Cursor = Cursors.Hand };
        b.Font = style == Style.Primary ? Semibold(10f) : UI(9.5f);
        b.Width = TextRenderer.MeasureText(text, b.Font).Width + S(icon == null ? 34 : 60);
        b.Click += click;
        return b;
    }

    class FlatButton : Button
    {
        public string Glyph;
        public Style Kind;
        bool hover;

        public FlatButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            FlatStyle = FlatStyle.Flat;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent == null ? Bg : Parent.BackColor);
            Color fill, fore, border;
            switch (Kind)
            {
                case Style.Primary:
                    fill = !Enabled ? Color.FromArgb(246, 190, 160) : hover ? AccentDark : Accent;
                    fore = Color.White; border = fill; break;
                case Style.Side:
                    fill = !Enabled ? SideItem : hover ? AccentDark : Accent;
                    fore = Enabled ? Color.White : SideMuted; border = fill; break;
                case Style.Danger:
                    fill = hover && Enabled ? Color.FromArgb(254, 236, 236) : Card;
                    fore = Enabled ? Red : Faint; border = Enabled ? Color.FromArgb(240, 190, 190) : Border; break;
                default:
                    fill = hover && Enabled ? Color.FromArgb(247, 248, 250) : Card;
                    fore = Enabled ? Theme.Text : Faint; border = Border; break;
            }
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Round(r, S(7)))
            using (var b = new SolidBrush(fill))
            using (var p = new Pen(border))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            if (Glyph == null)
            {
                TextRenderer.DrawText(g, Text, Font, r, fore, flags | TextFormatFlags.HorizontalCenter);
                return;
            }
            using (var icons = Icons(Font.Size * 0.95f))
            {
                int iw = S(18), tw = TextRenderer.MeasureText(Text, Font).Width, gap = S(8);
                int x = (Width - (iw + gap + tw)) / 2;
                TextRenderer.DrawText(g, Glyph, icons, new Rectangle(x, 0, iw, Height), fore, flags | TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(g, Text, Font, new Rectangle(x + iw + gap, 0, tw + S(4), Height), fore, flags);
            }
        }
    }

    /// <summary>An on/off switch, drawn as a pill.</summary>
    public class Toggle : CheckBox
    {
        public Toggle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Size = new Size(S(44), S(24));
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent == null ? Card : Parent.BackColor);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color track = !Enabled ? Color.FromArgb(235, 236, 240) : Checked ? Accent : Color.FromArgb(205, 209, 217);
            using (var path = Round(r, Height / 2))
            using (var b = new SolidBrush(track))
                g.FillPath(b, path);
            int d = Height - S(6);
            int x = Checked ? Width - d - S(3) : S(3);
            using (var b = new SolidBrush(Enabled ? Color.White : Color.FromArgb(248, 248, 250)))
                g.FillEllipse(b, x, S(3), d, d);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

    /// <summary>Grey hint text shown in an empty text box.</summary>
    public static void Placeholder(TextBox box, string hint)
    {
        box.HandleCreated += (s, e) => SendMessage(box.Handle, 0x1501, (IntPtr)1, hint);
    }
}
