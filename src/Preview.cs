using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

/// <summary>Pictures shown in the app: the TV's live screen, and a mock-up of the IsafellieOS home screen.</summary>
static class Preview
{
    static Image wallpaper;

    /// <summary>Takes a screenshot of the TV without showing anything on it. Returns null if it fails.</summary>
    public static Image TvScreen(string addr)
    {
        string file = Path.Combine(Path.GetTempPath(), "firesweep-screen-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            Adb.ExecOutToFile(addr, "screencap -p", file);
            if (!File.Exists(file) || new FileInfo(file).Length < 100) return null;
            // Load through memory so the temp file isn't kept locked.
            var img = new Bitmap(Image.FromStream(new MemoryStream(File.ReadAllBytes(file))));
            if (IsDark(img)) Caption(img, "The screen is dark: the TV is asleep, or it's playing protected video");
            return img;
        }
        catch { return null; }
        finally { try { File.Delete(file); } catch { } }
    }

    static bool IsDark(Bitmap b)
    {
        int bright = 0, n = 0;
        for (int y = 0; y < b.Height; y += b.Height / 24 + 1)
            for (int x = 0; x < b.Width; x += b.Width / 32 + 1, n++)
                if (b.GetPixel(x, y).GetBrightness() > 0.08f) bright++;
        return bright < n / 50 + 1;
    }

    static void Caption(Bitmap b, string text)
    {
        using (var g = Graphics.FromImage(b))
        using (var f = new Font("Segoe UI", b.Width / 40f, GraphicsUnit.Pixel))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        using (var br = new SolidBrush(Color.FromArgb(170, 175, 186)))
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.DrawString(text, f, br, new RectangleF(b.Width * 0.1f, 0, b.Width * 0.8f, b.Height), sf);
        }
    }

    static readonly Dictionary<string, Color[]> brand = new Dictionary<string, Color[]>
    {
        // background, text
        { "com.netflix.ninja", new[] { Color.White, Color.FromArgb(229, 9, 20) } },
        { "com.amazon.firetv.youtube", new[] { Color.White, Color.FromArgb(255, 0, 0) } },
        { "com.amazon.firetv.youtube.tv", new[] { Color.White, Color.FromArgb(255, 0, 0) } },
        { "com.amazon.firebat", new[] { Color.FromArgb(0, 168, 225), Color.White } },
        { "com.hbo.hbonow", new[] { Color.FromArgb(0, 18, 76), Color.White } },
        { "com.apple.atve.amazon.appletv", new[] { Color.Black, Color.White } },
        { "com.disney.disneyplus", new[] { Color.FromArgb(4, 60, 92), Color.White } },
        { "com.hulu.plus", new[] { Color.FromArgb(28, 231, 131), Color.FromArgb(4, 16, 32) } },
        { "com.peacock.peacockfiretv", new[] { Color.Black, Color.White } },
        { "com.cbs.ott", new[] { Color.FromArgb(0, 100, 255), Color.White } },
        { "com.plexapp.android", new[] { Color.FromArgb(31, 31, 31), Color.FromArgb(229, 160, 13) } },
        { "org.xbmc.kodi", new[] { Color.FromArgb(23, 178, 231), Color.White } },
        { "tv.pluto.android", new[] { Color.Black, Color.FromArgb(255, 240, 0) } },
        { "com.tubitv", new[] { Color.FromArgb(124, 28, 255), Color.White } },
        { "com.espn.gtv", new[] { Color.FromArgb(205, 0, 0), Color.White } },
        { "com.spotify.tv.android", new[] { Color.FromArgb(25, 20, 20), Color.FromArgb(30, 215, 96) } },
        { "com.sling", new[] { Color.FromArgb(0, 132, 255), Color.White } },
        { "com.fubo.firetv.screen", new[] { Color.FromArgb(250, 70, 22), Color.White } },
    };

    static Color[] Colors(string pkg)
    {
        Color[] c;
        if (brand.TryGetValue(pkg, out c)) return c;
        // Anything else gets a steady color from its package name.
        int h = 0;
        foreach (char ch in pkg) h = h * 31 + ch;
        var palette = new[] { Color.FromArgb(52, 73, 94), Color.FromArgb(91, 44, 111), Color.FromArgb(22, 106, 116), Color.FromArgb(120, 66, 18), Color.FromArgb(30, 61, 130) };
        return new[] { palette[Math.Abs(h) % palette.Length], Color.White };
    }

    /// <summary>Draws IsafellieOS's home screen as it would look with this dock, at the given size.</summary>
    public static Bitmap Home(List<string> dock, int width)
    {
        int height = width * 9 / 16;
        float u = width / 1920f;  // the launcher is laid out for a 1920x1080 screen
        var bmp = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            if (wallpaper == null)
            {
                var s = typeof(Preview).Assembly.GetManifestResourceStream("wallpaper.jpg");
                if (s != null) wallpaper = Image.FromStream(s);
            }
            if (wallpaper != null) g.DrawImage(wallpaper, 0, 0, width, height);
            else g.Clear(Color.FromArgb(40, 60, 80));

            using (var f = new Font("Segoe UI", 30 * u, GraphicsUnit.Pixel))
                g.DrawString("IsafellieOS", f, Brushes.White, 80 * u, 60 * u);
            using (var f = new Font("Segoe UI Light", 64 * u, GraphicsUnit.Pixel))
            {
                string time = DateTime.Now.ToString("h:mm");
                var size = g.MeasureString(time, f);
                g.DrawString(time, f, Brushes.White, width - 80 * u - size.Width, 40 * u);
            }

            // Tiles: the dock's apps, then All apps and Settings, on a translucent shelf.
            float tileW = 200 * u, tileH = 112 * u, smallW = 112 * u, gap = 26 * u, pad = 30 * u;
            float total = dock.Count * (tileW + gap) + 2 * smallW + gap + 2 * pad;
            if (total > width - 40 * u)
            {
                float k = (width - 40 * u) / total;
                tileW *= k; tileH *= k; smallW *= k; gap *= k; pad *= k; total = width - 40 * u;
            }
            float x = (width - total) / 2, y = height - 90 * u - tileH - 2 * pad;
            using (var shelf = Theme.Round(Rectangle.Round(new RectangleF(x, y, total, tileH + 2 * pad)), (int)(40 * u)))
            using (var b = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                g.FillPath(b, shelf);
            x += pad;
            float ty = y + pad;
            foreach (var pkg in dock)
            {
                var c = Colors(pkg);
                var r = Rectangle.Round(new RectangleF(x, ty, tileW, tileH));
                using (var path = Theme.Round(r, (int)(22 * u)))
                using (var b = new SolidBrush(c[0]))
                    g.FillPath(b, path);
                using (var f = new Font("Segoe UI Semibold", Math.Max(6f, 26 * u * (tileW / (200 * u))), GraphicsUnit.Pixel))
                using (var b = new SolidBrush(c[1]))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                    g.DrawString(Catalog.AppName(pkg), f, b, r, sf);
                x += tileW + gap;
            }
            foreach (var glyph in new[] { "", "" })  // grid of apps, gear
            {
                var r = Rectangle.Round(new RectangleF(x, ty, smallW, tileH));
                using (var path = Theme.Round(r, (int)(22 * u)))
                using (var b = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                    g.FillPath(b, path);
                using (var f = Theme.Icons(Math.Max(5f, 30 * u * 0.75f)))
                    TextRenderer.DrawText(g, glyph, f, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                x += smallW + gap;
            }
        }
        return bmp;
    }

    /// <summary>Shows an image large, in its own window.</summary>
    public static void ShowLarge(IWin32Window owner, Image img, string title)
    {
        if (img == null) return;
        var screen = Screen.FromHandle(owner.Handle).WorkingArea;
        int w = Math.Min(screen.Width * 8 / 10, img.Width), h = w * img.Height / img.Width;
        var f = new Form
        {
            Text = title, BackColor = Color.Black, StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(w, h), ShowIcon = false, MinimizeBox = false, KeyPreview = true,
        };
        f.Controls.Add(new PictureBox { Dock = DockStyle.Fill, Image = img, SizeMode = PictureBoxSizeMode.Zoom });
        f.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) f.Close(); };
        f.Show(owner);
    }
}
