using System;
using System.Drawing;
using System.Windows.Forms;
using RFTool.Advanced;
using RFTool.AdvancedSystem;

namespace RFTool;

public sealed partial class MainForm
{
    // =========================================================
    // TAB 5 - MUIZ ADVANCED SYSTEM
    // =========================================================

    private Panel advancedSidebar = null!;
    private Panel advancedContent = null!;
    private FlowLayoutPanel advancedMenu = null!;
    private Panel advancedPageHost = null!;
    private Label advancedTitle = null!;
    private Label advancedDescription = null!;
    
    private Button activeAdvancedButton = null!;
    private readonly AddressManager addressManager = new();

    // Mesin Advanced (target process, Freeze) dibuat saat pertama dibutuhkan.
    private AdvancedServices? advanced;

    private AdvancedServices AdvSvc =>
        advanced ??= new AdvancedServices(
            () => selectedProcess,
            addressManager);

    // Halaman yang sudah dibuat disimpan agar hasil scan tidak hilang saat pindah menu.
    private readonly Dictionary<string, Control> advancedPages = new();

    // Tab ADVANCED dikunci password (sama dengan kunci Base Pointer).
    private TabPage? advancedTab;
    private bool advancedUnlocked;

    private static readonly Dictionary<string, string> AdvancedDescriptions = new()
    {
        ["Memory Scanner"] = "Search and compare authorized memory data.",
        ["Memory Viewer"] = "View memory data from permitted test processes.",
        ["Address Manager"] = "Manage and organize saved address records.",
        ["Pointer Analyzer"] = "Analyze pointer structures in test data.",
        ["Pattern Analyzer"] = "Analyze byte patterns and signatures.",
        ["Code Analyzer"] = "Inspect program instructions.",
        ["Debug Tools"] = "Application diagnostics and debugging.",
        ["Script Manager"] = "Manage automation scripts."
    };
    private readonly Color AdvancedSidebarColor =
        Color.FromArgb(30, 30, 35);

    private readonly Color AdvancedActiveColor =
        Color.FromArgb(45, 75, 60);

    // =========================================================
    // BUILD TAB 5
    // =========================================================

    private void BuildTab5(TabPage tab)
    {
        // Pasang kunci password: tab ini tidak bisa dibuka sebelum password benar.
        advancedTab = tab;
        tabs.Selecting += Tabs_Selecting;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = BackColorMain,
            Margin = Padding.Empty
        };

        root.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Absolute,
                190));

        root.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100));

        root.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100));

        tab.Controls.Add(root);

        // =====================================================
        // SIDEBAR
        // =====================================================

        advancedSidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AdvancedSidebarColor,
            Padding = new Padding(8)
        };

        root.Controls.Add(
            advancedSidebar,
            0,
            0);

        var sidebarTitle = new Label
        {
            Text = "MUIZ ADVANCED",
            Dock = DockStyle.Top,
            Height = 45,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = ForeColorMain,
            Font = new Font(
                "Segoe UI",
                10F,
                FontStyle.Bold)
        };

        advancedSidebar.Controls.Add(
            sidebarTitle);

        advancedMenu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = AdvancedSidebarColor,
            Padding = new Padding(0, 5, 0, 5)
        };

        advancedSidebar.Controls.Add(
            advancedMenu);

        // Menu (Fill) harus di-dock paling akhir; tanpa ini judul
        // "MUIZ ADVANCED" menimpa tombol pertama (Memory Scanner).
        advancedMenu.BringToFront();

        // =====================================================
        // CONTENT AREA
        // =====================================================

        advancedContent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColorMain,
            Padding = new Padding(15)
        };
        
        advancedPageHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColorMain
        };
        
        advancedContent.Controls.Add(
            advancedPageHost);
        
        root.Controls.Add(
            advancedContent,
            1,
            0);

        advancedTitle = new Label
        {
            Text = "Memory Scanner",
            Dock = DockStyle.Top,
            Height = 38,
            ForeColor = ForeColorMain,
            Font = new Font(
                "Segoe UI",
                13F,
                FontStyle.Bold)
        };

        advancedContent.Controls.Add(
            advancedTitle);

        advancedDescription = new Label
        {
            Text = "Memory analysis and address management.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = ForeColorDim,
            Font = new Font(
                "Segoe UI",
                9F,
                FontStyle.Regular)
        };

        advancedContent.Controls.Add(
            advancedDescription);

        // =====================================================
        // MENU
        // =====================================================

        AddAdvancedMenu(
            "Memory Scanner",
            "Search and compare authorized memory data.");

        AddAdvancedMenu(
            "Memory Viewer",
            "View memory data from permitted test processes.");

        AddAdvancedMenu(
            "Address Manager",
            "Manage and organize saved address records.");

        AddAdvancedMenu(
            "Pointer Analyzer",
            "Analyze pointer structures in test data.");

        AddAdvancedMenu(
            "Pattern Analyzer",
            "Analyze byte patterns and signatures.");

        AddAdvancedMenu(
            "Code Analyzer",
            "Inspect program instructions.");

        AddAdvancedMenu(
            "Debug Tools",
            "Application diagnostics and debugging.");

        AddAdvancedMenu(
            "Script Manager",
            "Manage automation scripts.");

        // Default page
        ShowAdvancedPage(
            "Memory Scanner",
            "Search and compare authorized memory data.");
    }

    // =========================================================
    // ADD MENU BUTTON
    // =========================================================

    private void AddAdvancedMenu(
        string title,
        string description)
    {
        var button = new Button
        {
            Text = title,
            Width = 160,
            Height = 40,
            Margin = new Padding(0, 2, 0, 4),
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = AdvancedSidebarColor,
            ForeColor = ForeColorMain,
            Font = new Font(
                "Segoe UI",
                8.5F,
                FontStyle.Regular),
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor =
            Color.FromArgb(42, 55, 48);

        button.Click += (_, _) =>
        {
            ShowAdvancedPage(
                title,
                description);

            SetAdvancedActiveButton(button);
        };

        advancedMenu.Controls.Add(button);

        if (activeAdvancedButton == null)
        {
            SetAdvancedActiveButton(button);
        }
    }

    // =========================================================
    // ACTIVE MENU
    // =========================================================

    private void SetAdvancedActiveButton(
        Button selected)
    {
        foreach (
            Control control
            in advancedMenu.Controls)
        {
            if (control is Button button)
            {
                button.BackColor =
                    AdvancedSidebarColor;
            }
        }

        selected.BackColor =
            AdvancedActiveColor;

        activeAdvancedButton = selected;
    }

    // =========================================================
    // SHOW PAGE
    // =========================================================

    private void ShowAdvancedPage(
        string title,
        string description)
    {
        advancedTitle.Text = title;

        advancedDescription.Text =
            description;

        // Lepas halaman lama. Halaman yang di-cache tidak di-dispose.
        var old = advancedPageHost.Controls.Cast<Control>().ToList();

        advancedPageHost.Controls.Clear();

        foreach (Control control in old)
        {
            if (!advancedPages.ContainsValue(control))
                control.Dispose();
        }

        if (title == "Address Manager")
        {
            BuildAddressManagerPage();
            return;
        }

        if (!advancedPages.TryGetValue(title, out Control? page))
        {
            page = CreateAdvancedPage(title);
            advancedPages[title] = page;
        }

        page.Dock = DockStyle.Fill;

        advancedPageHost.Controls.Add(page);
    }

    // =========================================================
    // CREATE PAGE
    // =========================================================

    private Control CreateAdvancedPage(string title)
    {
        switch (title)
        {
            case "Memory Scanner":
            {
                var scanner = new MemoryScannerPanel(AdvSvc.Target, addressManager);
                scanner.BrowseRequested += BrowseInMemoryViewer;
                scanner.DisassembleRequested += DisassembleInCodeAnalyzer;
                scanner.PointerScanRequested += PointerScanFor;
                return scanner;
            }

            case "Memory Viewer":
            {
                var viewer = new MemoryViewerPanel(AdvSvc.Target, addressManager);
                viewer.DisassembleRequested += DisassembleInCodeAnalyzer;
                viewer.PointerScanRequested += PointerScanFor;
                return viewer;
            }

            case "Pointer Analyzer":
            {
                var pointer = new PointerAnalyzerPanel(AdvSvc.Target, addressManager);
                pointer.BrowseRequested += BrowseInMemoryViewer;
                return pointer;
            }

            case "Pattern Analyzer":
            {
                var pattern = new PatternAnalyzerPanel(AdvSvc.Target, addressManager);
                pattern.BrowseRequested += BrowseInMemoryViewer;
                pattern.DisassembleRequested += DisassembleInCodeAnalyzer;
                return pattern;
            }

            case "Code Analyzer":
            {
                var code = new CodeAnalyzerPanel(AdvSvc.Target, addressManager);
                code.BrowseRequested += BrowseInMemoryViewer;
                return code;
            }

            case "Script Manager":
                return new ScriptManagerPanel(AdvSvc);

            case "Debug Tools":
                return new DebugToolsPanel(AdvSvc.Target);

            default:
                return new Label
                {
                    Text = title + " - Coming Soon",
                    ForeColor = ForeColorDim,
                    TextAlign = ContentAlignment.MiddleCenter
                };
        }
    }

    // =========================================================
    // BROWSE: buka Memory Viewer di alamat tertentu
    // =========================================================

    private void BrowseInMemoryViewer(string addressText)
    {
        TargetProcess? target = AdvSvc.Target.Get();

        if (target == null)
        {
            MessageBox.Show("Attach process terlebih dahulu.");
            return;
        }

        if (!AddressExpression.TryEvaluate(
                target,
                addressText,
                out ulong address,
                out string error))
        {
            MessageBox.Show(error, "Address tidak valid");
            return;
        }

        const string title = "Memory Viewer";

        ShowAdvancedPage(
            title,
            "View memory data from permitted test processes.");

        foreach (Control control in advancedMenu.Controls)
        {
            if (control is Button button && button.Text == title)
                SetAdvancedActiveButton(button);
        }

        if (advancedPages.TryGetValue(title, out Control? page) &&
            page is MemoryViewerPanel viewer)
        {
            viewer.GoToAddress(address);
        }
    }

    // =========================================================
    // DISPOSE (dipanggil saat aplikasi ditutup)
    // =========================================================

    private void DisposeAdvancedTab()
    {
        addressInfoTimer?.Stop();

        foreach (Control page in advancedPages.Values)
            page.Dispose();

        advancedPages.Clear();

        advanced?.Dispose();
        advanced = null;
    }

    // =========================================================
    // KUNCI PASSWORD TAB ADVANCED
    // =========================================================

    private void Tabs_Selecting(object? sender, TabControlCancelEventArgs e)
    {
        if (e.TabPage != advancedTab || advancedUnlocked)
            return;

        string? password = PromptBasePointerPassword("Buka tab ADVANCED");

        if (password == null)
        {
            e.Cancel = true;
            return;
        }

        if (VerifyBasePointerPassword(password))
        {
            advancedUnlocked = true;
            statusLabel.Text = "Status: Tab ADVANCED terbuka";
            return;
        }

        e.Cancel = true;

        MessageBox.Show(
            this,
            "Password salah.",
            "ADVANCED",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    // =========================================================
    // NAVIGASI ANTAR HALAMAN ADVANCED
    // =========================================================

    private void OpenAdvancedPage(string title)
    {
        ShowAdvancedPage(title, AdvancedDescriptions[title]);

        foreach (Control control in advancedMenu.Controls)
        {
            if (control is Button button && button.Text == title)
                SetAdvancedActiveButton(button);
        }
    }

    private bool ResolveAdvancedAddress(string text, out ulong address)
    {
        address = 0;

        TargetProcess? target = AdvSvc.Target.Get();

        if (target == null)
        {
            MessageBox.Show("Attach process terlebih dahulu.");
            return false;
        }

        if (!AddressExpression.TryEvaluate(target, text, out address, out string error))
        {
            MessageBox.Show(error, "Address tidak valid");
            return false;
        }

        return true;
    }

    private void DisassembleInCodeAnalyzer(string addressText)
    {
        if (!ResolveAdvancedAddress(addressText, out ulong address))
            return;

        OpenAdvancedPage("Code Analyzer");

        if (advancedPages.TryGetValue("Code Analyzer", out Control? page) &&
            page is CodeAnalyzerPanel analyzer)
        {
            analyzer.GoToAddress(address);
        }
    }

    private void PointerScanFor(string addressText)
    {
        if (!ResolveAdvancedAddress(addressText, out ulong address))
            return;

        OpenAdvancedPage("Pointer Analyzer");

        if (advancedPages.TryGetValue("Pointer Analyzer", out Control? page) &&
            page is PointerAnalyzerPanel pointer)
        {
            pointer.SetTarget(address.ToString("X"));
        }
    }
}
