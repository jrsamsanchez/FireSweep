using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

class MainForm : Form
{
    const string GuideUrl = "https://github.com/jrsamsanchez/FireSweep/blob/main/docs/enable-adb.md";
    readonly string dataRoot;
    readonly Image logo = LoadLogo();
    ListBox devices, appsList, dockList;
    TextBox ipBox, logBox;
    Button scanBtn, addBtn, openBtn, applyBtn, backupBtn, restoreBtn, folderBtn, addDock, removeDock, upDock, downDock;
    Label tvTitle, tvSub, dockNote, pendingLabel;
    PictureBox screenBox, homePreview;
    LinkLabel screenRefresh;
    FlowLayoutPanel changesList;
    Panel changesPage, dockPage, tabBar;
    int tab;
    readonly List<ChangeRow> rows = new List<ChangeRow>();
    Tv tv;
    bool busy, dockChanged, filling;

    class AppEntry
    {
        public string Pkg;
        public override string ToString() { string n = Catalog.AppName(Pkg); return n == Pkg ? Pkg : n; }
    }

    class Device
    {
        public string Addr, Name = "Checking...";
        public bool Ready, Ticked;  // Ticked: also gets the changes when Apply runs
        public override string ToString() { return Addr; }
    }

    /// <summary>One change in the list: its switch, its status pill and the item it stands for.</summary>
    class ChangeRow
    {
        public Item Item;
        public bool Available;
        public Panel Panel;
        public Theme.Toggle Toggle;
        public Label Status;
    }

    static int S(int px) { return Theme.S(px); }

    static Image LoadLogo()
    {
        var s = typeof(MainForm).Assembly.GetManifestResourceStream("logo.png");
        return s == null ? null : Image.FromStream(s);
    }

    public MainForm(string dataRoot)
    {
        this.dataRoot = dataRoot;
        using (var g = CreateGraphics()) Theme.Scale = g.DpiX / 96f;
        Font = Theme.UI(9.5f);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Text = "FireSweep";
        ClientSize = new Size(S(1240), S(820));
        MinimumSize = new Size(S(980), S(640));
        StartPosition = FormStartPosition.CenterScreen;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        DoubleBuffered = true;

        var content = BuildContent();
        Controls.Add(content);
        Controls.Add(BuildSidebar());
        content.BringToFront();
        ShowTab(0);
        UpdateButtons();

        string selfTest = Environment.GetEnvironmentVariable("FIRESWEEP_SELFTEST");
        if (selfTest == null) Shown += (s, e) => Scan();
        else { Opacity = 0; ShowInTaskbar = false; Shown += async (s, e) => await SelfTest(selfTest); }
    }

    static Label Caption(string text, Color color)
    {
        return new Label { Text = text.ToUpperInvariant(), ForeColor = color, Font = Theme.Semibold(8.5f), AutoSize = false, Height = S(22), Dock = DockStyle.Top };
    }

    static Panel Spacer(int h, Color back) { return new Panel { Height = S(h), Dock = DockStyle.Top, BackColor = back }; }

    // ---- sidebar: finding and picking a TV ----

    Control BuildSidebar()
    {
        var side = new Panel { Dock = DockStyle.Left, Width = S(320), BackColor = Theme.Side, Padding = new Padding(S(18), S(18), S(18), S(14)) };

        var brand = new Panel { Dock = DockStyle.Top, Height = S(58), BackColor = Theme.Side };
        brand.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (logo != null) g.DrawImage(logo, new Rectangle(0, S(5), S(42), S(42)));
            using (var f = Theme.Semibold(14f)) TextRenderer.DrawText(g, "FireSweep", f, new Point(S(52), S(4)), Theme.SideText);
            using (var f = Theme.UI(8.5f)) TextRenderer.DrawText(g, "Clean up the Fire TVs on your network", f, new Point(S(54), S(30)), Theme.SideMuted);
        };

        scanBtn = Theme.Button(Theme.IconSearch, "Scan the network", Theme.Style.Side, (s, e) => Scan());
        scanBtn.Dock = DockStyle.Top;

        // Typing an address connects straight away, for TVs the scan can't see.
        var ipRow = new Panel { Dock = DockStyle.Top, Height = S(38), BackColor = Theme.Side };
        var ipFrame = new Panel { BackColor = Theme.SideItem, Padding = new Padding(S(10), S(9), S(8), S(6)), Dock = DockStyle.Fill };
        ipBox = new TextBox
        {
            BorderStyle = BorderStyle.None, BackColor = Theme.SideItem, ForeColor = Theme.SideText,
            Font = Theme.UI(10.5f), Dock = DockStyle.Fill,
        };
        Theme.Placeholder(ipBox, "e.g. 192.168.1.50");
        ipBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { AddByAddress(); e.SuppressKeyPress = true; } };
        ipFrame.Controls.Add(ipBox);
        addBtn = Theme.Button(Theme.IconPlug, "Connect", Theme.Style.Side, (s, e) => AddByAddress());
        addBtn.Dock = DockStyle.Right;
        ipRow.Controls.Add(ipFrame);
        ipRow.Controls.Add(new Panel { Dock = DockStyle.Right, Width = S(8), BackColor = Theme.Side });
        ipRow.Controls.Add(addBtn);

        devices = new ListBox
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Side, ForeColor = Theme.SideText,
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = S(60), IntegralHeight = false,
        };
        devices.DrawItem += DrawDevice;
        devices.MouseDown += (s, e) =>
        {
            // The box at the right of a TV ticks it to get the same changes when Apply runs.
            int i = devices.IndexFromPoint(e.Location);
            if (i < 0 || e.X < devices.Width - S(44)) return;
            var d = (Device)devices.Items[i];
            if (!d.Ready || IsCurrent(d) || busy) return;
            d.Ticked = !d.Ticked;
            devices.Invalidate();
            UpdateButtons();
        };
        devices.DoubleClick += (s, e) => OpenSelected();
        devices.SelectedIndexChanged += (s, e) => { devices.Invalidate(); UpdateButtons(); };

        openBtn = Theme.Button(Theme.IconPlug, "Connect to this TV", Theme.Style.Side, (s, e) => OpenSelected());
        openBtn.Dock = DockStyle.Bottom;
        var help = new Label
        {
            Dock = DockStyle.Bottom, Height = S(150), ForeColor = Theme.SideMuted, Font = Theme.UI(8.5f), Padding = new Padding(0, S(12), 0, 0),
            Text = "Don't see your TV? On the TV, turn on Settings > My Fire TV > Developer options > ADB debugging. " +
                   "No Developer options? Open Settings > My Fire TV > About and press Select on the TV's name 7 times.\n\n" +
                   "The first time you connect, the TV asks to allow this PC. Tick \"Always allow\" and choose OK.",
        };

        side.Controls.Add(devices);
        side.Controls.Add(Spacer(8, Theme.Side));
        var listHead = new Panel { Dock = DockStyle.Top, Height = S(22), BackColor = Theme.Side };
        var tickAll = new LinkLabel
        {
            Dock = DockStyle.Right, AutoSize = true, Text = "Tick all", Font = Theme.UI(8.5f),
            LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentDark, VisitedLinkColor = Theme.Accent, LinkBehavior = LinkBehavior.HoverUnderline,
        };
        tickAll.LinkClicked += (s, e) =>
        {
            var others = devices.Items.Cast<Device>().Where(d => d.Ready && !IsCurrent(d)).ToList();
            bool tick = others.Any(d => !d.Ticked);
            foreach (var d in others) d.Ticked = tick;
            tickAll.Text = tick ? "Untick all" : "Tick all";
            devices.Invalidate();
            UpdateButtons();
        };
        listHead.Controls.Add(Caption("TVs found", Theme.SideMuted));
        listHead.Controls.Add(tickAll);
        side.Controls.Add(listHead);
        side.Controls.Add(Spacer(18, Theme.Side));
        side.Controls.Add(ipRow);
        side.Controls.Add(Caption("Or type its IP address", Theme.SideMuted));
        side.Controls.Add(Spacer(16, Theme.Side));
        side.Controls.Add(scanBtn);
        side.Controls.Add(Spacer(14, Theme.Side));
        side.Controls.Add(brand);
        var guide = new LinkLabel
        {
            Dock = DockStyle.Bottom, Height = S(26), Text = "Step-by-step setup guide", Font = Theme.UI(9f),
            LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentDark, VisitedLinkColor = Theme.Accent, LinkBehavior = LinkBehavior.HoverUnderline,
        };
        guide.LinkClicked += (s, e) => Process.Start(GuideUrl);

        // A live picture of the connected TV's screen, under the TV list.
        var screenCard = new Panel { Dock = DockStyle.Bottom, Height = S(190), BackColor = Theme.Side, Visible = false, Padding = new Padding(0, S(8), 0, 0) };
        screenBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Console, Cursor = Cursors.Hand };
        screenBox.Click += (s, e) => Preview.ShowLarge(this, screenBox.Image, tv == null ? "TV screen" : tv.Maker + " " + tv.Model + " screen");
        new ToolTip().SetToolTip(screenBox, "What the TV shows right now. Click to enlarge.");
        var screenHead = new Panel { Dock = DockStyle.Top, Height = S(24), BackColor = Theme.Side };
        screenRefresh = new LinkLabel
        {
            Dock = DockStyle.Right, AutoSize = true, Text = "Refresh", Font = Theme.UI(8.5f),
            LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentDark, VisitedLinkColor = Theme.Accent, LinkBehavior = LinkBehavior.HoverUnderline,
        };
        screenRefresh.LinkClicked += (s, e) => RefreshScreen();
        screenHead.Controls.Add(Caption("On the TV now", Theme.SideMuted));
        screenHead.Controls.Add(screenRefresh);
        screenCard.Controls.Add(screenBox);
        screenCard.Controls.Add(screenHead);
        side.Controls.Add(screenCard);
        side.Controls.Add(guide);
        side.Controls.Add(help);
        side.Controls.Add(openBtn);
        return side;
    }

    void DrawDevice(object sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var d = (Device)devices.Items[e.Index];
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(Theme.Side)) g.FillRectangle(b, e.Bounds);
        bool sel = (e.State & DrawItemState.Selected) != 0;
        bool current = tv != null && tv.Addr == d.Addr;
        var r = new Rectangle(e.Bounds.X, e.Bounds.Y + S(3), e.Bounds.Width - 1, e.Bounds.Height - S(6));
        if (sel || current)
        {
            using (var path = Theme.Round(r, S(8)))
            using (var b = new SolidBrush(Theme.SideItem)) g.FillPath(b, path);
            if (sel) using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, r.X, r.Y + S(12), S(3), r.Height - S(24));
        }
        Color dot = d.Ready ? Color.FromArgb(74, 222, 128) : d.Name == "Checking..." ? Theme.SideMuted : Color.FromArgb(251, 191, 36);
        using (var f = Theme.Icons(14f))
            TextRenderer.DrawText(g, Theme.IconTv, f, new Rectangle(r.X + S(12), r.Y, S(26), r.Height), Theme.SideText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        using (var b = new SolidBrush(dot)) g.FillEllipse(b, r.X + S(32), r.Y + r.Height / 2 + S(4), S(8), S(8));
        using (var f = Theme.Semibold(10f))
            TextRenderer.DrawText(g, d.Name, f, new Rectangle(r.X + S(50), r.Y + S(9), r.Width - S(96), S(22)), Theme.SideText, TextFormatFlags.EndEllipsis);
        using (var f = Theme.UI(8.5f))
            TextRenderer.DrawText(g, d.Addr + (current ? "   •   connected" : ""), f, new Rectangle(r.X + S(50), r.Y + S(30), r.Width - S(96), S(20)), Theme.SideMuted, TextFormatFlags.EndEllipsis);

        // Tick box: the connected TV always gets the changes; others when ticked.
        if (!d.Ready) return;
        bool on = current || d.Ticked;
        var box = new Rectangle(r.Right - S(34), r.Y + (r.Height - S(20)) / 2, S(20), S(20));
        using (var path = Theme.Round(box, S(5)))
        {
            if (on) using (var b = new SolidBrush(current ? Theme.SideMuted : Theme.Accent)) g.FillPath(b, path);
            else using (var p = new Pen(Theme.SideMuted, S(2) / 1.5f)) g.DrawPath(p, path);
        }
        if (on) using (var f = Theme.Icons(9f))
            TextRenderer.DrawText(g, Theme.IconCheck, f, box, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    bool IsCurrent(Device d) { return tv != null && tv.Addr == d.Addr; }

    IEnumerable<Device> TickedOthers()
    {
        return devices.Items.Cast<Device>().Where(d => d.Ready && d.Ticked && !IsCurrent(d));
    }

    // ---- main area ----

    Control BuildContent()
    {
        var content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(S(28), S(22), S(28), S(18)) };

        tvTitle = new Label { Dock = DockStyle.Top, Height = S(40), Font = Theme.Semibold(18f), Text = "Pick a TV to get started" };
        tvSub = new Label
        {
            Dock = DockStyle.Top, Height = S(26), ForeColor = Theme.Muted, Font = Theme.UI(9.5f),
            Text = "Scan the network or type the TV's IP address on the left.",
        };

        tabBar = new Panel { Dock = DockStyle.Top, Height = S(44), BackColor = Theme.Bg };
        tabBar.Paint += PaintTabs;
        tabBar.MouseClick += (s, e) => { for (int i = 0; i < 2; i++) if (TabRect(i).Contains(e.Location)) ShowTab(i); };
        tabBar.Cursor = Cursors.Hand;

        changesList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true,
            BackColor = Theme.Bg, Padding = new Padding(0, S(4), 0, S(8)),
        };
        changesList.Resize += (s, e) => FitRows();
        var empty = new Label
        {
            Text = "Your TV's options show up here once it's connected.", ForeColor = Theme.Muted, AutoSize = true,
            Margin = new Padding(0, S(20), 0, 0), Font = Theme.UI(10f),
        };
        changesList.Controls.Add(empty);
        changesPage = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        changesPage.Controls.Add(changesList);
        dockPage = BuildDockPage();

        var pages = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        pages.Controls.Add(changesPage);
        pages.Controls.Add(dockPage);

        content.Controls.Add(pages);
        content.Controls.Add(Spacer(10, Theme.Bg));
        content.Controls.Add(tabBar);
        content.Controls.Add(tvSub);
        content.Controls.Add(tvTitle);
        content.Controls.Add(BuildActionBar());
        return content;
    }

    Rectangle TabRect(int i)
    {
        string[] names = { "Changes", "Home screen dock" };
        int x = 0;
        using (var f = Theme.Semibold(10.5f))
            for (int j = 0; j < i; j++) x += TextRenderer.MeasureText(names[j], f).Width + S(30);
        int w;
        using (var f = Theme.Semibold(10.5f)) w = TextRenderer.MeasureText(names[i], f).Width + S(6);
        return new Rectangle(x, 0, w, tabBar.Height);
    }

    void PaintTabs(object sender, PaintEventArgs e)
    {
        string[] names = { "Changes", "Home screen dock" };
        var g = e.Graphics;
        using (var p = new Pen(Theme.Border)) g.DrawLine(p, 0, tabBar.Height - 1, tabBar.Width, tabBar.Height - 1);
        using (var f = Theme.Semibold(10.5f))
            for (int i = 0; i < 2; i++)
            {
                var r = TabRect(i);
                TextRenderer.DrawText(g, names[i], f, r, i == tab ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
                if (i == tab) using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, r.X, r.Bottom - S(3), r.Width, S(3));
            }
    }

    void ShowTab(int i)
    {
        tab = i;
        changesPage.Visible = i == 0;
        dockPage.Visible = i == 1;
        tabBar.Invalidate();
    }

    Control BuildActionBar()
    {
        var bar = new Panel { Dock = DockStyle.Bottom, Height = S(196), BackColor = Theme.Bg, Padding = new Padding(0, S(12), 0, 0) };

        var buttons = new Panel { Dock = DockStyle.Top, Height = S(42), BackColor = Theme.Bg };
        applyBtn = Theme.Button(Theme.IconCheck, "Apply changes", Theme.Style.Primary, (s, e) => ApplyChanges());
        backupBtn = Theme.Button(Theme.IconSave, "Back up now", Theme.Style.Secondary, (s, e) => Run("back up", () => tv.Backup()));
        folderBtn = Theme.Button(Theme.IconFolder, "TV folder", Theme.Style.Secondary, (s, e) => Process.Start("explorer.exe", "\"" + tv.Folder + "\""));
        restoreBtn = Theme.Button(Theme.IconUndo, "Restore everything", Theme.Style.Danger, (s, e) => RestoreAll());
        pendingLabel = new Label { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, Padding = new Padding(S(14), 0, 0, 0) };
        applyBtn.Width += S(20);
        applyBtn.Dock = DockStyle.Left;
        foreach (var b in new[] { restoreBtn, folderBtn, backupBtn }) b.Dock = DockStyle.Right;
        buttons.Controls.Add(pendingLabel);
        buttons.Controls.Add(applyBtn);
        buttons.Controls.Add(restoreBtn);
        buttons.Controls.Add(new Panel { Dock = DockStyle.Right, Width = S(8) });
        buttons.Controls.Add(folderBtn);
        buttons.Controls.Add(new Panel { Dock = DockStyle.Right, Width = S(8) });
        buttons.Controls.Add(backupBtn);

        var console = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Console, Padding = new Padding(S(12), S(10), S(4), S(6)) };
        logBox = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
            BackColor = Theme.Console, ForeColor = Theme.ConsoleText, Font = new Font("Consolas", 9f),
        };
        console.Controls.Add(logBox);

        bar.Controls.Add(console);
        bar.Controls.Add(Spacer(12, Theme.Bg));
        bar.Controls.Add(buttons);
        return bar;
    }

    static Panel CardPanel()
    {
        var p = new Panel { BackColor = Theme.Card, Padding = new Padding(S(14)) };
        p.Paint += (s, e) => { using (var pen = new Pen(Theme.Border)) e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1); };
        return p;
    }

    Panel BuildDockPage()
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(0, S(8), 0, 0) };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Theme.Bg };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(150)));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        appsList = DockListBox(false);
        dockList = DockListBox(true);
        appsList.DoubleClick += (s, e) => DockAdd();
        dockList.DoubleClick += (s, e) => DockRemove();

        grid.Controls.Add(ListCard("Apps on this TV", appsList), 0, 0);
        grid.Controls.Add(ListCard("On the dock, left to right", dockList), 2, 0);

        var mid = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, BackColor = Theme.Bg, Padding = new Padding(S(10), S(50), S(10), 0) };
        addDock = Theme.Button(Theme.IconRight, "Add", Theme.Style.Secondary, (s, e) => DockAdd());
        removeDock = Theme.Button(Theme.IconLeft, "Remove", Theme.Style.Secondary, (s, e) => DockRemove());
        upDock = Theme.Button(Theme.IconUp, "Earlier", Theme.Style.Secondary, (s, e) => DockMove(-1));
        downDock = Theme.Button(Theme.IconDown, "Later", Theme.Style.Secondary, (s, e) => DockMove(1));
        foreach (var b in new[] { addDock, removeDock, upDock, downDock })
        {
            b.Width = S(128);
            b.Margin = new Padding(0, 0, 0, S(10));
            mid.Controls.Add(b);
        }
        grid.Controls.Add(mid, 1, 0);

        // Below the lists: how the home screen will look with this dock.
        var previewRow = new Panel { Dock = DockStyle.Bottom, Height = S(236), BackColor = Theme.Bg, Padding = new Padding(0, S(14), 0, 0) };
        homePreview = new PictureBox { Dock = DockStyle.Left, Width = S(392), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Console, Cursor = Cursors.Hand };
        homePreview.Click += (s, e) => Preview.ShowLarge(this, Preview.Home(CurrentDock(), 1600), "IsafellieOS preview");
        new ToolTip().SetToolTip(homePreview, "How IsafellieOS will look with this dock. Click to enlarge.");
        dockNote = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Muted, Padding = new Padding(S(18), S(4), 0, 0) };
        previewRow.Controls.Add(dockNote);
        previewRow.Controls.Add(homePreview);
        page.Controls.Add(grid);
        page.Controls.Add(previewRow);
        return page;
    }

    Control ListCard(string title, ListBox list)
    {
        var card = CardPanel();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0);
        card.Controls.Add(list);
        var head = new Label { Text = title, Dock = DockStyle.Top, Height = S(30), Font = Theme.Semibold(10.5f), ForeColor = Theme.Text };
        card.Controls.Add(head);
        return card;
    }

    ListBox DockListBox(bool numbered)
    {
        var list = new ListBox
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = S(38), IntegralHeight = false, BackColor = Theme.Card,
        };
        list.SelectedIndexChanged += (s, e) => { list.Invalidate(); UpdateButtons(); };
        list.DrawItem += (s, e) =>
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.Card)) g.FillRectangle(b, e.Bounds);
            var r = new Rectangle(e.Bounds.X, e.Bounds.Y + S(2), e.Bounds.Width - S(2), e.Bounds.Height - S(4));
            bool sel = (e.State & DrawItemState.Selected) != 0;
            if (sel) using (var path = Theme.Round(r, S(6))) using (var b = new SolidBrush(Theme.AccentSoft)) g.FillPath(b, path);
            int x = r.X + S(10);
            if (numbered)
            {
                var c = new Rectangle(x, r.Y + (r.Height - S(22)) / 2, S(22), S(22));
                using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, c);
                using (var f = Theme.Semibold(8.5f))
                    TextRenderer.DrawText(g, (e.Index + 1).ToString(), f, c, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                x += S(32);
            }
            using (var f = Theme.UI(10f))
                TextRenderer.DrawText(g, list.Items[e.Index].ToString(), f, new Rectangle(x, r.Y, r.Right - x, r.Height),
                    sel ? Theme.AccentDark : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        };
        return list;
    }

    // ---- logging and busy state ----

    void Log(string line) { LogFor(tv, line); }

    /// <summary>Shows a line in the log pane and adds it to that TV's activity.log.</summary>
    void LogFor(Tv t, string line)
    {
        if (InvokeRequired) { BeginInvoke(new Action<Tv, string>(LogFor), t, line); return; }
        logBox.AppendText((t != null && t != tv ? "[" + t.Model + " " + t.Addr + "] " : "") + line + "\r\n");
        if (t != null && t.Folder != null)
            try { File.AppendAllText(Path.Combine(t.Folder, "activity.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss  ") + line + "\r\n"); } catch { }
    }

    void SetBusy(bool b)
    {
        busy = b;
        UseWaitCursor = b;
        foreach (var r in rows) r.Toggle.Enabled = r.Available && !b;
        UpdateButtons();
    }

    void UpdateButtons()
    {
        bool hasTv = tv != null && !busy;
        scanBtn.Enabled = addBtn.Enabled = !busy;
        openBtn.Enabled = !busy && devices.SelectedItem != null;
        int pending = PendingChanges().Count();
        bool dockPending = dockChanged && LauncherWanted();
        int others = TickedOthers().Count();
        applyBtn.Enabled = hasTv && (pending > 0 || dockPending || others > 0);
        applyBtn.Text = others == 0 ? "Apply changes" : "Apply to " + (others + 1) + " TVs";
        applyBtn.Width = TextRenderer.MeasureText(applyBtn.Text, applyBtn.Font).Width + S(80);
        pendingLabel.Text = tv == null ? "" : pending == 0 && !dockPending ? "No changes waiting on this TV"
            : pending == 0 ? "Dock change waiting"
            : pending + (pending == 1 ? " change waiting" : " changes waiting") + (dockPending ? ", plus the dock" : "");
        if (tv != null && others > 0) pendingLabel.Text += ". Ticked TVs will be set to match.";
        backupBtn.Enabled = folderBtn.Enabled = restoreBtn.Enabled = hasTv;
        addDock.Enabled = hasTv && appsList.SelectedItem != null;
        removeDock.Enabled = hasTv && dockList.SelectedItem != null;
        upDock.Enabled = hasTv && dockList.SelectedIndex > 0;
        downDock.Enabled = hasTv && dockList.SelectedIndex >= 0 && dockList.SelectedIndex < dockList.Items.Count - 1;
    }

    /// <summary>Runs work off the UI thread, logging any error, then refreshes the TV's state.</summary>
    async void Run(string what, Action work, bool refresh = false)
    {
        SetBusy(true);
        try
        {
            await Task.Run(work);
            if (refresh && tv != null) { await Task.Run(() => tv.Refresh()); ShowTv(); RefreshScreen(); }
        }
        catch (Exception ex) { Log("Couldn't " + what + ": " + ex.Message); }
        finally { SetBusy(false); }
    }

    // ---- finding TVs ----

    static IEnumerable<IPAddress> LocalHosts()
    {
        var seen = new HashSet<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                byte[] ip = ua.Address.GetAddressBytes();
                if (ip[0] == 169 && ip[1] == 254) continue;
                uint addr = (uint)(ip[0] << 24 | ip[1] << 16 | ip[2] << 8 | ip[3]);
                byte[] mb = ua.IPv4Mask.GetAddressBytes();
                uint mask = (uint)(mb[0] << 24 | mb[1] << 16 | mb[2] << 8 | mb[3]);
                if (mask < 0xFFFFFC00) mask = 0xFFFFFF00;  // scan at most a /22; on bigger networks just the local /24
                uint net = addr & mask, last = net | ~mask;
                for (uint a = net + 1; a < last; a++)
                {
                    if (a == addr) continue;
                    var host = new IPAddress(new[] { (byte)(a >> 24), (byte)(a >> 16), (byte)(a >> 8), (byte)a });
                    if (seen.Add(host.ToString())) yield return host;
                }
            }
        }
    }

    static async Task<bool> PortOpen(IPAddress ip, SemaphoreSlim gate)
    {
        await gate.WaitAsync();
        try
        {
            using (var c = new TcpClient())
            {
                var connect = c.ConnectAsync(ip, 5555);
                var first = await Task.WhenAny(connect, Task.Delay(600));
                return first == connect && !connect.IsFaulted && c.Connected;
            }
        }
        catch { return false; }
        finally { gate.Release(); }
    }

    void Scan() { var t = ScanAsync(); }

    async Task ScanAsync()
    {
        SetBusy(true);
        devices.Items.Clear();
        Log("Looking for TVs with ADB debugging on...");
        try
        {
            var gate = new SemaphoreSlim(128);
            var hosts = LocalHosts().ToList();
            var checks = hosts.Select(h => PortOpen(h, gate)).ToList();
            await Task.WhenAll(checks);
            var found = hosts.Where((h, i) => checks[i].Result).Select(h => h.ToString() + ":5555").ToList();
            // Also list TVs adb is already connected to (USB, or on another subnet).
            var known = await Task.Run(() => Adb.Run("devices"));
            foreach (var l in known.Split('\n').Skip(1))
            {
                var parts = l.Split('\t');
                if (parts.Length == 2 && !found.Contains(parts[0].Trim())) found.Add(parts[0].Trim());
            }
            Log(found.Count == 0 ? "No TVs found. Check ADB debugging is on, or type the TV's IP address on the left."
                                 : "Found " + found.Count + " device(s) with ADB open. Checking what they are...");
            foreach (var addr in found) await Describe(addr);
        }
        catch (Exception ex) { Log("Scan failed: " + ex.Message); }
        finally { SetBusy(false); }
        if (devices.Items.Count == 1) devices.SelectedIndex = 0;
    }

    Device FindDevice(string addr) { return devices.Items.Cast<Device>().FirstOrDefault(d => d.Addr == addr); }

    async Task<Device> Describe(string addr)
    {
        var d = FindDevice(addr);
        if (d == null) { d = new Device { Addr = addr }; devices.Items.Add(d); }
        await Task.Run(() =>
        {
            string c = addr.Contains(":") && !addr.Contains("::") ? Adb.Run("connect " + addr, 10000) : "";
            string r = Adb.Shell(addr, "getprop ro.product.manufacturer; getprop ro.product.model", 10000);
            if (r.Contains("unauthorized") || c.Contains("authenticate")) d.Name = "Allow this PC on the TV";
            else if (r.StartsWith("error") || r.Contains("offline") || c.Contains("failed") || c.Contains("refused") || c.Contains("cannot")) d.Name = "Not answering";
            else { d.Name = string.Join(" ", r.Split('\n').Select(x => x.Trim())); d.Ready = true; }
        });
        devices.Invalidate();
        return d;
    }

    async void AddByAddress()
    {
        string a = ipBox.Text.Trim();
        if (a.Length == 0 || busy) { ipBox.Focus(); return; }
        if (!a.Contains(":")) a += ":5555";
        IPAddress ignored;
        if (!IPAddress.TryParse(a.Split(':')[0], out ignored)) { Log("That doesn't look like an IP address. Type something like 192.168.1.50"); return; }
        ipBox.Clear();
        SetBusy(true);
        Log("Trying " + a + "...");
        Device d;
        try { d = await Describe(a); }
        finally { SetBusy(false); }
        devices.SelectedItem = d;
        if (d.Ready) await OpenSelectedAsync();
        else Log(d.Name == "Not answering" ? "Nothing answered at " + a + ". Check the address and that ADB debugging is on."
                                           : "The TV is asking to allow this PC. Tick \"Always allow\", choose OK, then choose Connect to this TV.");
    }

    // ---- one TV ----

    void OpenSelected() { var t = OpenSelectedAsync(); }

    async Task OpenSelectedAsync()
    {
        var d = devices.SelectedItem as Device;
        if (busy || d == null) return;
        string addr = d.Addr;
        SetBusy(true);
        Log("Connecting to " + addr + "...");
        try
        {
            var t = new Tv(addr, Log);
            await Task.Run(() =>
            {
                if (addr.Contains(":")) Adb.Run("connect " + addr, 10000);
                t.Open(dataRoot);
                t.Refresh();
            });
            tv = t;
            d.Name = t.Maker + " " + t.Model;
            d.Ready = true;
            devices.Invalidate();
            Log("Connected: " + t.Maker + " " + t.Model + ", " + t.FireOs + ". Its folder is " + t.Folder);
            if (!t.Maker.Equals("Amazon", StringComparison.OrdinalIgnoreCase))
                Log("Note: this isn't an Amazon device, so most of the options won't apply to it.");
            if (!t.HasBackup) await Task.Run(() => t.Backup());
            await Task.Run(() => t.WriteRestoreScript());
            var apps = await Task.Run(() => t.LaunchableApps());
            var dock = await Task.Run(() => t.GetDock());
            ShowTv();
            ShowDock(apps, dock);
            RefreshScreen();
        }
        catch (Exception ex)
        {
            string m = ex.Message;
            if (m.Contains("unauthorized")) m = "The TV hasn't allowed this PC yet. Look at the TV, tick \"Always allow\", choose OK, then connect again.";
            Log("Couldn't connect: " + m);
        }
        finally { SetBusy(false); }
    }

    void ShowTv()
    {
        tvTitle.Text = tv.Maker + " " + tv.Model;
        tvSub.Text = tv.FireOs + "   •   " + tv.Addr + "   •   serial " + tv.Serial;
        filling = true;
        changesList.SuspendLayout();
        changesList.Controls.Clear();
        rows.Clear();
        bool untouched = tv.J.Empty;
        string group = null;
        foreach (var it in Catalog.Items)
        {
            if (it.Group != group)
            {
                group = it.Group;
                var cap = Caption(group, Theme.Muted);
                cap.Dock = DockStyle.None;
                cap.Margin = new Padding(S(2), S(group == Catalog.Items[0].Group ? 2 : 16), 0, S(4));
                changesList.Controls.Add(cap);
            }
            bool? on = tv.IsOn(it);
            var row = MakeRow(it, on != null);
            row.Toggle.Checked = on == true || (on == false && untouched && it.Recommended);
            rows.Add(row);
            changesList.Controls.Add(row.Panel);
            Mark(row);
        }
        changesList.ResumeLayout();
        FitRows();
        filling = false;
        UpdateButtons();
    }

    ChangeRow MakeRow(Item it, bool available)
    {
        var row = new ChangeRow { Item = it, Available = available };
        var p = CardPanel();
        p.Height = S(62);
        p.Margin = new Padding(0, 0, 0, S(6));
        p.Padding = new Padding(S(16), 0, S(14), 0);

        row.Toggle = new Theme.Toggle { Enabled = available && !busy };
        row.Toggle.CheckedChanged += (s, e) => { if (!filling) { Mark(row); UpdateButtons(); } };
        var togglePanel = new Panel { Dock = DockStyle.Left, Width = S(62), BackColor = Theme.Card };
        togglePanel.Controls.Add(row.Toggle);
        togglePanel.Resize += (s, e) => row.Toggle.Location = new Point(0, (togglePanel.Height - row.Toggle.Height) / 2);

        row.Status = new Label { Dock = DockStyle.Right, Width = S(210), BackColor = Theme.Card };
        row.Status.Paint += (s, e) => PaintStatus(row, e.Graphics);

        string details = it.Note;
        if (it.Kind == Kind.Packages) details = (details.Length > 0 ? details + "  •  " : "") + string.Join(", ", it.Packages);
        var text = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Card };
        var title = new Label { Text = it.Label, Dock = DockStyle.Top, Height = S(32), TextAlign = ContentAlignment.BottomLeft, Font = Theme.Semibold(10.5f), ForeColor = available ? Theme.Text : Theme.Faint, AutoEllipsis = true };
        var sub = new Label { Text = details, Dock = DockStyle.Fill, ForeColor = available ? Theme.Muted : Theme.Faint, Font = Theme.UI(8.5f), AutoEllipsis = true };
        new ToolTip().SetToolTip(sub, details);
        text.Controls.Add(sub);
        text.Controls.Add(title);

        p.Controls.Add(text);
        p.Controls.Add(row.Status);
        p.Controls.Add(togglePanel);
        // Clicking anywhere on the card flips the switch.
        foreach (Control c in new Control[] { p, text, title, sub })
            c.Click += (s, e) => { if (row.Toggle.Enabled) row.Toggle.Checked = !row.Toggle.Checked; };
        if (available) foreach (Control c in new Control[] { p, text, title, sub }) c.Cursor = Cursors.Hand;
        row.Panel = p;
        return row;
    }

    void FitRows()
    {
        int w = changesList.ClientSize.Width - changesList.Padding.Horizontal - S(4);
        foreach (Control c in changesList.Controls) if (c is Panel || c is Label) c.Width = Math.Max(S(300), w);
    }

    /// <summary>Works out a row's status text and colors: what's on the TV now, and what Apply would do.</summary>
    void StatusOf(ChangeRow row, out string text, out Color fore, out Color back)
    {
        var it = row.Item;
        if (!row.Available) { text = "Not on this TV"; fore = Theme.Faint; back = Theme.Bg; return; }
        bool on = tv.IsOn(it) == true;
        bool wants = row.Toggle.Checked;
        if (wants != on) { text = wants ? "Will apply" : "Will undo"; fore = Theme.AccentDark; back = Theme.AccentSoft; return; }
        if (it.Kind == Kind.Launcher && on && tv.LauncherVersion < Catalog.LauncherVersion)
        { text = "Will update"; fore = Theme.AccentDark; back = Theme.AccentSoft; return; }
        if (it.Kind == Kind.Launcher) text = on ? "Installed" : tv.LauncherVersion > 0 ? "Installed, not home" : "Not installed";
        else text = on ? "Done" : "Not applied";
        fore = on ? Theme.Green : Theme.Muted;
        back = on ? Theme.GreenSoft : Theme.Bg;
    }

    void PaintStatus(ChangeRow row, Graphics g)
    {
        if (tv == null) return;
        string text; Color fore, back;
        StatusOf(row, out text, out fore, out back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var f = Theme.Semibold(9f))
        {
            int w = TextRenderer.MeasureText(text, f).Width + S(22), h = S(26);
            var r = new Rectangle(row.Status.Width - w - 1, (row.Status.Height - h) / 2, w, h);
            using (var path = Theme.Round(r, h / 2)) using (var b = new SolidBrush(back)) g.FillPath(b, path);
            TextRenderer.DrawText(g, text, f, r, fore, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    void Mark(ChangeRow row) { row.Status.Invalidate(); row.Toggle.Invalidate(); }

    IEnumerable<ChangeRow> PendingChanges()
    {
        if (tv == null) return new ChangeRow[0];
        return rows.Where(r =>
        {
            if (!r.Available) return false;
            bool on = tv.IsOn(r.Item) == true;
            return r.Toggle.Checked != on || (r.Item.Kind == Kind.Launcher && r.Toggle.Checked && tv.LauncherVersion < Catalog.LauncherVersion);
        });
    }

    bool LauncherWanted() { return rows.Any(r => r.Toggle.Checked && r.Item.Kind == Kind.Launcher); }

    /// <summary>
    /// Read-only check used while building: scans, opens each TV found, and saves pictures of the
    /// window and the log to dir. It never applies anything. Set FIRESWEEP_SELFTEST=dir to run it.
    /// </summary>
    async Task SelfTest(string dir)
    {
        Directory.CreateDirectory(dir);
        await ScanAsync();
        Snap(dir, "0-scan");
        string only = Environment.GetEnvironmentVariable("FIRESWEEP_SELFTEST_TV");
        foreach (var d in devices.Items.Cast<Device>().ToList())
        {
            if (only != null && !d.Addr.StartsWith(only)) continue;
            devices.SelectedItem = d;
            await OpenSelectedAsync();
            await RefreshScreenAsync();
            string name = d.Addr.Replace(":", "_");
            // Hide the serial number in pictures meant for the README.
            if (Environment.GetEnvironmentVariable("FIRESWEEP_SELFTEST_HIDE") != null)
            {
                tvSub.Text = tvSub.Text.Replace(tv.Serial, "hidden");
                logBox.Text = logBox.Text.Replace(tv.Serial, "hidden").Replace(Environment.UserName, "you");
            }
            ShowTab(0); Snap(dir, name + "-1-changes");
            // Show what pending changes look like by flipping two switches (nothing is applied).
            var demo = rows.Where(r => r.Available && (r.Item.Label.Contains("Silk") || r.Item.Label.Contains("Alexa"))).ToList();
            foreach (var r in demo) r.Toggle.Checked = !r.Toggle.Checked;
            if (demo.Count > 0) { changesList.ScrollControlIntoView(demo.Last().Panel); Snap(dir, name + "-3-pending"); }
            foreach (var r in demo) r.Toggle.Checked = !r.Toggle.Checked;
            changesList.AutoScrollPosition = new Point(0, 0);
            ShowTab(1); Snap(dir, name + "-2-dock");
        }
        // Tick the other TVs to show the multi-TV state (nothing is applied).
        foreach (var d in devices.Items.Cast<Device>()) d.Ticked = d.Ready && !IsCurrent(d);
        devices.Invalidate(); ShowTab(0); UpdateButtons(); Snap(dir, "4-multi");
        File.WriteAllText(Path.Combine(dir, "log.txt"), logBox.Text);
        Close();
    }

    void Snap(string dir, string name)
    {
        Application.DoEvents();
        using (var bmp = new Bitmap(Width, Height))
        {
            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
            bmp.Save(Path.Combine(dir, name + ".png"));
        }
    }

    // ---- the dock picker ----

    void ShowDock(List<string> apps, List<string> dock)
    {
        dockChanged = dock == null;  // nothing on the TV to read yet: the list below gets sent on Apply
        if (dock == null) dock = Catalog.DefaultDock.ToList();
        dock = dock.Where(p => apps.Contains(p)).ToList();
        dockList.Items.Clear();
        appsList.Items.Clear();
        foreach (var p in dock) dockList.Items.Add(new AppEntry { Pkg = p });
        foreach (var p in apps.Where(p => !dock.Contains(p)).OrderBy(p => Catalog.AppName(p), StringComparer.OrdinalIgnoreCase))
            appsList.Items.Add(new AppEntry { Pkg = p });
        dockNote.Text = tv.LauncherVersion >= 2
            ? "This is the dock IsafellieOS shows now. Changes go to the TV when you choose Apply changes."
            : "The dock is sent to the TV when IsafellieOS is installed or updated (turn on \"Use IsafellieOS as the home screen\", then Apply changes). " +
              "It can also be changed from the TV: press Menu on an app.";
        dockNote.Text += "\n\nLeft: a preview of IsafellieOS with this dock. Click it to enlarge. On the TV, the tiles use each app's own banner.";
        UpdatePreview();
        UpdateButtons();
    }

    void DockEdited() { dockChanged = true; dockList.Invalidate(); UpdatePreview(); UpdateButtons(); }

    List<string> CurrentDock() { return dockList.Items.Cast<AppEntry>().Select(e => e.Pkg).ToList(); }

    void UpdatePreview()
    {
        var old = homePreview.Image;
        homePreview.Image = Preview.Home(CurrentDock(), S(784));
        if (old != null) old.Dispose();
    }

    void RefreshScreen() { var t = RefreshScreenAsync(); }

    async Task RefreshScreenAsync()
    {
        if (tv == null) return;
        var t = tv;
        screenBox.Parent.Visible = true;
        screenRefresh.Text = "Updating...";
        var img = await Task.Run(() => Preview.TvScreen(t.Addr));
        if (t != tv) { if (img != null) img.Dispose(); return; }
        var old = screenBox.Image;
        screenBox.Image = img;
        if (old != null) old.Dispose();
        screenRefresh.Text = img == null ? "Couldn't get it. Retry" : "Refresh";
    }

    void DockAdd()
    {
        var e = appsList.SelectedItem as AppEntry;
        if (e == null) return;
        appsList.Items.Remove(e);
        dockList.Items.Add(e);
        dockList.SelectedItem = e;
        DockEdited();
    }

    void DockRemove()
    {
        var e = dockList.SelectedItem as AppEntry;
        if (e == null) return;
        dockList.Items.Remove(e);
        appsList.Items.Add(e);
        DockEdited();
    }

    void DockMove(int by)
    {
        int i = dockList.SelectedIndex, j = i + by;
        if (i < 0 || j < 0 || j >= dockList.Items.Count) return;
        var e = dockList.Items[i];
        dockList.Items.RemoveAt(i);
        dockList.Items.Insert(j, e);
        dockList.SelectedIndex = j;
        DockEdited();
    }

    // ---- applying and restoring ----

    void ApplyChanges()
    {
        var pending = PendingChanges().ToList();
        var dock = dockList.Items.Cast<AppEntry>().Select(e => e.Pkg).ToList();
        bool sendDock = LauncherWanted() && (dockChanged || pending.Any(r => r.Item.Kind == Kind.Launcher));
        var others = TickedOthers().ToList();
        var lines = pending.Select(r => (r.Toggle.Checked ? "Do:  " : "Undo:  ") + r.Item.Label).ToList();
        if (sendDock) lines.Add("Set the dock: " + string.Join(", ", dock.Select(p => Catalog.AppName(p))));
        if (lines.Count == 0 && others.Count == 0) return;
        string text = lines.Count == 0 ? "Nothing to change on " + tv.Model + "." : "On " + tv.Model + " (" + tv.Addr + "):\n" + string.Join("\n", lines);
        if (others.Count > 0)
            text += "\n\nAlso set these TVs to match every switch here" + (LauncherWanted() ? ", dock included" : "") + ":\n" +
                    string.Join("\n", others.Select(d => d.Name + " (" + d.Addr + ")")) +
                    "\nEach one is backed up first and only gets the changes it needs.";
        if (MessageBox.Show(this, text + "\n\nEverything here can be undone later. Go ahead?",
                others.Count == 0 ? "Apply to " + tv.Model : "Apply to " + (others.Count + 1) + " TVs",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

        var work = pending.Select(r => new KeyValuePair<Item, bool>(r.Item, r.Toggle.Checked)).ToList();
        // What the ticked TVs should end up like: every switch that applies to the connected TV.
        var wanted = rows.Where(r => r.Available).Select(r => new KeyValuePair<Item, bool>(r.Item, r.Toggle.Checked)).ToList();
        bool launcher = LauncherWanted();
        bool showHome = work.Any(w => w.Key.Kind == Kind.Launcher && w.Value);
        var t = tv;
        var addrs = others.Select(d => d.Addr).ToList();
        Run("apply the changes", () =>
        {
            ApplyTo(t, work, sendDock ? dock : null, showHome, t.Log);
            foreach (var addr in addrs)
            {
                var other = new Tv(addr, null);
                other.Log = line => LogFor(other, line);
                try
                {
                    Adb.Run("connect " + addr, 10000);
                    other.Open(dataRoot);
                    other.Refresh();
                    var needed = wanted.Where(w =>
                    {
                        bool? on = other.IsOn(w.Key);
                        if (on == null) return false;
                        return on.Value != w.Value || (w.Key.Kind == Kind.Launcher && w.Value && other.LauncherVersion < Catalog.LauncherVersion);
                    }).ToList();
                    bool home = needed.Any(w => w.Key.Kind == Kind.Launcher && w.Value);
                    if (needed.Count == 0 && !launcher) { other.Log("Already matches; nothing to change."); continue; }
                    ApplyTo(other, needed, launcher ? dock : null, home, other.Log);
                }
                catch (Exception ex) { LogFor(other, "Couldn't apply to " + addr + ": " + ex.Message); }
            }
            Log("All done. To undo, switch it back off and apply again, or use Restore everything (or \"Restore this TV.cmd\" in the TV's folder).");
        }, refresh: true);
        dockChanged = false;
    }

    /// <summary>Backs a TV up if needed, makes the changes, sets the dock and rewrites its restore script.</summary>
    static void ApplyTo(Tv t, List<KeyValuePair<Item, bool>> work, List<string> dock, bool showHome, Action<string> log)
    {
        if (!t.HasBackup) t.Backup();
        foreach (var w in work)
        {
            t.Apply(w.Key, w.Value);
            if (w.Key.Kind == Kind.Launcher) t.Refresh();
        }
        if (dock != null && t.LauncherVersion >= 2) t.SetDock(dock);
        if (showHome) t.ShowLauncher();
        t.WriteRestoreScript();
        log("Done with " + t.Model + ".");
    }

    void RestoreAll()
    {
        if (tv.J.Empty && tv.LauncherVersion == 0)
        {
            Log("Nothing to restore: FireSweep hasn't changed anything on this TV.");
            return;
        }
        if (MessageBox.Show(this, "Turn every Amazon app back on, put the privacy settings back, and remove IsafellieOS from this TV?",
                "Restore " + tv.Model, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
        var t = tv;
        Run("restore", () => t.RestoreAll(), refresh: true);
    }
}
