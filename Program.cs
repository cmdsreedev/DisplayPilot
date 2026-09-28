using System.Diagnostics;
using System.ComponentModel;
using System.Text.Json;
namespace DisplayPilot;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\DisplayPilot", out bool first);
        if (!first) { MessageBox.Show("DisplayPilot is already running. Open it from the system tray.", "DisplayPilot"); return; }
        Application.Run(new MainWindow(args.Contains("--minimized"), args.Contains("--smoke-test")));
    }
}
public sealed class MainWindow : Form
{
    readonly Settings settings;
    readonly SwitchEngine engine;
    readonly NotifyIcon tray;
    readonly ContextMenuStrip menu = new();
    readonly Label mode = new(), last = new(), status = new();
    readonly CheckBox settingsAuto = new() { Text = "Automatic switching", AutoSize = true };
    readonly CheckBox auto = new() { Text = "Automatic switching", AutoSize = true };
    readonly DataGridView devices = new(), profiles = new();
    readonly TextBox path = new();
    readonly ComboBox pc = new(), tv = new(), manual = new();
    readonly NumericUpDown cooldown = new() { Minimum = 0, Maximum = 120, Width = 120 };
    readonly CheckBox minimized = new() { Text = "Start minimized to the tray", AutoSize = true }, startup = new() { Text = "Start with Windows", AutoSize = true };
    readonly BindingList<DeviceRule> rules;
    readonly BindingList<ProfileRow> profileRows;
    readonly ToolStripMenuItem trayMode = new() { Enabled = false }, trayInput = new() { Enabled = false }, trayAuto = new("Automatic switching");
    string lastName = "No input yet";
    bool exiting, syncing;
    public sealed class ProfileRow { public string Name { get; set; } = ""; }
    public MainWindow(bool forceMinimized = false, bool smoke = false)
    {
        settings = Settings.Load(out var warning); settings.StartWithWindows = Startup.Enabled;
        engine = new(settings, ChangeProfile);
        Text = "DisplayPilot"; Size = new(1100, 860); MinimumSize = new(900, 620); StartPosition = FormStartPosition.CenterScreen;
        Font = new("Segoe UI", 10); BackColor = Color.FromArgb(243, 246, 251); Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!;
        rules = new(settings.Devices.Select(Clone).ToList()); profileRows = new(settings.Profiles.Select(n => new ProfileRow { Name = n }).ToList());
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(24), RowCount = 3, ColumnCount = 1 }; shell.RowStyles.Add(new(SizeType.Absolute, 76)); shell.RowStyles.Add(new(SizeType.Percent, 100)); shell.RowStyles.Add(new(SizeType.Absolute, 45)); Controls.Add(shell);
        var title = new Label { Text = "DisplayPilot", Font = new("Segoe UI Semibold", 19), AutoSize = true }; shell.Controls.Add(title, 0, 0);
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new(20, 9) }; shell.Controls.Add(tabs, 0, 1);
        var dash = Page(tabs, "Dashboard"); var devicePage = Page(tabs, "Devices"); var profilePage = Page(tabs, "Profiles"); var prefs = Page(tabs, "Settings");
        var d = Stack(dash); mode.Font = new("Segoe UI Semibold", 28); mode.AutoSize = true; last.AutoSize = true; status.AutoSize = true; status.MaximumSize = new(850, 0);
        d.Controls.Add(Label("CURRENT PROFILE", 10)); d.Controls.Add(mode); d.Controls.Add(Label("Starts assuming the PC profile. Status follows successful switches made here.", 10)); d.Controls.Add(last); d.Controls.Add(auto);
        var quick = new FlowLayoutPanel { AutoSize = true }; quick.Controls.Add(Button("Switch to PC", async () => await Switch(settings.PcProfile, true))); quick.Controls.Add(Button("Switch to TV", async () => await Switch(settings.TvProfile, true))); d.Controls.Add(quick);
        manual.DropDownStyle = ComboBoxStyle.DropDownList; manual.Width = 280; d.Controls.Add(Label("Switch to another saved profile", 11)); d.Controls.Add(manual); d.Controls.Add(Button("Apply profile", async () => { if (manual.SelectedItem is string p) await Switch(p, true); })); d.Controls.Add(status);
        BuildGrid(devices); devices.AutoGenerateColumns = false; devices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Friendly name", FillWeight = 24 }); devices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Identifier", HeaderText = "Identifier (| means OR)", FillWeight = 43 }); devices.Columns.Add(new DataGridViewComboBoxColumn { DataPropertyName = "Target", HeaderText = "Profile", Name = "Target", FillWeight = 16 }); devices.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Kind", HeaderText = "Type", FillWeight = 20, ReadOnly = true }); devices.DataSource = rules;
        GridPage(devicePage, devices, "Assign devices to any profile, or Ignore. Controllers: detection only.", Button("Refresh devices", () => { Discover(); return Task.CompletedTask; }), Button("Save mappings", () => { Save(); return Task.CompletedTask; }));
        BuildGrid(profiles); profiles.AutoGenerateColumns = false; profiles.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "DisplayMagician profile name" }); profiles.DataSource = profileRows;
        GridPage(profilePage, profiles, "Add profile names or detect saved DisplayMagician profiles.", Button("Detect profiles", () => { DetectProfiles(); return Task.CompletedTask; }), Button("Save profiles", () => { Save(); return Task.CompletedTask; }));
        var f = Stack(prefs);
        f.Controls.Add(Label("DISPLAYMAGICIAN", 12));
        var download = new LinkLabel { Text = "Download DisplayMagician (official releases)", AutoSize = true, Margin = new(4, 4, 4, 8) };
        download.LinkClicked += (_, _) => { try { Process.Start(new ProcessStartInfo("https://github.com/terrymacdonald/DisplayMagician/releases/latest") { UseShellExecute = true }); } catch (Exception e) { Error(e); } };
        f.Controls.Add(download);
        path.Width = 650; path.Text = settings.DisplayMagicianPath; f.Controls.Add(path);
        var magicianActions = new FlowLayoutPanel { AutoSize = true, WrapContents = true, MaximumSize = new(850, 0) };
        magicianActions.Controls.Add(Button("Choose file…", () => { using var dialog = new OpenFileDialog { Filter = "DisplayMagician console|DisplayMagicianConsole.exe", CheckFileExists = true }; if (dialog.ShowDialog(this) == DialogResult.OK) path.Text = dialog.FileName; return Task.CompletedTask; }));
        magicianActions.Controls.Add(Button("Choose folder…", () => {
            using var dialog = new FolderBrowserDialog { Description = "Select the folder containing DisplayMagicianConsole.exe", UseDescriptionForTitle = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) {
                var console = Path.Combine(dialog.SelectedPath, "DisplayMagicianConsole.exe");
                if (File.Exists(console)) path.Text = console;
                else MessageBox.Show(this, "That folder does not contain DisplayMagicianConsole.exe. Select the DisplayMagician installation folder.", "DisplayMagician not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return Task.CompletedTask;
        }));
        magicianActions.Controls.Add(Button("Open DisplayMagician", () => {
            try {
                var executable = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path.Text.Trim()))!, "DisplayMagician.exe");
                if (!File.Exists(executable)) throw new FileNotFoundException("DisplayMagician.exe was not found. Choose its installation folder first.");
                Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(executable)! });
            } catch (Exception e) { Error(e); }
            return Task.CompletedTask;
        }));
        f.Controls.Add(magicianActions);
        f.Controls.Add(Label("Choose the installation folder, then Save all settings below.", 9));
        pc.Width = tv.Width = 300; pc.DropDownStyle = tv.DropDownStyle = ComboBoxStyle.DropDown; f.Controls.Add(Label("PC button / assumed startup profile", 10)); f.Controls.Add(pc); f.Controls.Add(Label("TV button profile", 10)); f.Controls.Add(tv); f.Controls.Add(Label("Switching cooldown (seconds)", 10)); cooldown.Value = settings.CooldownSeconds; f.Controls.Add(cooldown); minimized.Checked = settings.StartMinimized; startup.Checked = settings.StartWithWindows; f.Controls.Add(minimized); f.Controls.Add(startup); f.Controls.Add(settingsAuto); settingsAuto.CheckedChanged += (_, _) => { if (!syncing) SetAuto(settingsAuto.Checked); }; f.Controls.Add(Button("Save all settings", () => { Save(); return Task.CompletedTask; }));
        shell.Controls.Add(Label("Close the window to keep switching in the tray.  •  DisplayPilot 1.0", 10), 0, 2);
        menu.Items.Add("Open DisplayPilot", null, (_, _) => Open()); menu.Items.Add(trayMode); menu.Items.Add(trayInput); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add(trayAuto); menu.Items.Add("Switch to PC", null, async (_, _) => await Switch(settings.PcProfile, true)); menu.Items.Add("Switch to TV", null, async (_, _) => await Switch(settings.TvProfile, true)); menu.Items.Add("Exit", null, (_, _) => { exiting = true; Close(); });
        tray = new() { Icon = Icon, Visible = true, ContextMenuStrip = menu, Text = "DisplayPilot" }; tray.DoubleClick += (_, _) => Open(); trayAuto.Click += (_, _) => SetAuto(!settings.AutomaticSwitching); auto.CheckedChanged += (_, _) => { if (!syncing) SetAuto(auto.Checked); };
        RefreshChoices(); pc.Text = settings.PcProfile; tv.Text = settings.TvProfile; manual.SelectedItem = settings.PcProfile; UpdateStatus();
        Shown += (_, _) => { try { if (!smoke) RawInput.Register(Handle); Discover(); if (warning != null) status.Text = warning; } catch (Exception e) { Error(e); } if (forceMinimized || settings.StartMinimized) Hide(); if (smoke) { foreach (TabPage page in tabs.TabPages) { tabs.SelectedTab = page; page.PerformLayout(); using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save(Path.Combine(AppContext.BaseDirectory, "smoke-" + page.Text.ToLowerInvariant() + ".png")); } var timer = new System.Windows.Forms.Timer { Interval = 1200 }; timer.Tick += (_, _) => { timer.Stop(); timer.Dispose(); exiting = true; Close(); }; timer.Start(); } };
    }
    static DeviceRule Clone(DeviceRule r) => new() { Name = r.Name, Identifier = r.Identifier, Target = r.Target, Kind = r.Kind };
    static Label Label(string text, int size) => new() { Text = text, Font = new("Segoe UI", size), AutoSize = true, MaximumSize = new(850, 0), Margin = new(4, 6, 4, 5) };
    static Button Button(string text, Func<Task> action) { var b = new Button { Text = text, AutoSize = true, MinimumSize = new(140, 36), FlatStyle = FlatStyle.Flat, BackColor = Color.White, Margin = new(4, 8, 12, 8) }; b.Click += async (_, _) => await action(); return b; }
    static TabPage Page(TabControl tabs, string title) { var p = new TabPage(title) { BackColor = Color.White, Padding = new(18) }; tabs.TabPages.Add(p); return p; }
    static FlowLayoutPanel Stack(Control parent) { var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true }; parent.Controls.Add(p); return p; }
    static void BuildGrid(DataGridView g) { g.Dock = DockStyle.Fill; g.BackgroundColor = Color.White; g.BorderStyle = BorderStyle.None; g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; g.RowHeadersWidth = 28; g.RowTemplate.Height = 34; g.ColumnHeadersHeight = 38; g.EnableHeadersVisualStyles = false; g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 237, 248); g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 248, 252); g.DataError += (_, e) => { e.ThrowException = false; }; }
    static void GridPage(Control page, Control grid, string help, params Button[] buttons) { var p = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 }; p.RowStyles.Add(new(SizeType.Absolute, 65)); p.RowStyles.Add(new(SizeType.Percent, 100)); p.RowStyles.Add(new(SizeType.Absolute, 55)); p.Controls.Add(Label(help, 10), 0, 0); p.Controls.Add(grid, 0, 1); var bar = new FlowLayoutPanel { Dock = DockStyle.Fill }; bar.Controls.AddRange(buttons); p.Controls.Add(bar, 0, 2); page.Controls.Add(p); }
    void RefreshChoices() { var names = profileRows.Select(p => p.Name.Trim()).Where(n => n.Length > 0).Concat(rules.Select(r => r.Target)).Where(n => n != "Ignore").Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); ((DataGridViewComboBoxColumn)devices.Columns["Target"]!).DataSource = new[] { "Ignore" }.Concat(names).ToArray(); foreach (var c in new[] { pc, tv, manual }) { var old = c.Text; c.Items.Clear(); c.Items.AddRange(names); c.Text = old; } if (manual.SelectedIndex < 0 && manual.Items.Count > 0) manual.SelectedIndex = 0; }
    void Discover() { foreach (var device in RawInput.Enumerate()) { if (rules.Any(r => r.Identifier.Split('|').Any(id => device.Path.Contains(id, StringComparison.OrdinalIgnoreCase)))) continue; rules.Add(new() { Name = device.Name, Identifier = device.Path, Kind = device.Kind }); } RefreshChoices(); }
    void DetectProfiles() { try { var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DisplayMagician", "Profiles", "DisplayProfiles.json"); using var doc = JsonDocument.Parse(File.ReadAllText(file)); int added = 0; foreach (var p in doc.RootElement.GetProperty("Profiles").EnumerateArray()) { var name = p.GetProperty("Name").GetString(); if (!string.IsNullOrWhiteSpace(name) && !profileRows.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { profileRows.Add(new() { Name = name }); added++; } } RefreshChoices(); MessageBox.Show(this, $"Found {doc.RootElement.GetProperty("Profiles").GetArrayLength()} profiles; added {added}. Save to keep them.", "Profiles detected"); } catch (Exception e) { Error(new Exception("Could not read saved DisplayMagician profiles. You can enter names manually. " + e.Message)); } }
    void Save() { try { Validate(); devices.EndEdit(); profiles.EndEdit(); BindingContext?[rules]?.EndCurrentEdit(); BindingContext?[profileRows]?.EndCurrentEdit(); var names = profileRows.Select(p => p.Name.Trim()).Where(n => n.Length > 0).ToList(); if (names.Count == 0 || names.Any(n => n.Equals("Ignore", StringComparison.OrdinalIgnoreCase)) || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count) throw new Exception("Use unique, nonempty profile names; Ignore is reserved."); if (!names.Contains(pc.Text.Trim()) || !names.Contains(tv.Text.Trim())) throw new Exception("PC and TV button profiles must be listed on the Profiles page."); if (!File.Exists(path.Text.Trim())) throw new Exception("Select an existing DisplayMagicianConsole.exe."); var candidate = new Settings { Profiles = names, PcProfile = pc.Text.Trim(), TvProfile = tv.Text.Trim(), DisplayMagicianPath = path.Text.Trim(), CooldownSeconds = (int)cooldown.Value, StartMinimized = minimized.Checked, StartWithWindows = startup.Checked, AutomaticSwitching = settings.AutomaticSwitching, Devices = rules.Select(Clone).ToList() }; candidate.Validate(); Startup.Set(candidate.StartWithWindows); candidate.Save(); settings.Profiles = candidate.Profiles; settings.PcProfile = candidate.PcProfile; settings.TvProfile = candidate.TvProfile; settings.DisplayMagicianPath = candidate.DisplayMagicianPath; settings.CooldownSeconds = candidate.CooldownSeconds; settings.StartMinimized = candidate.StartMinimized; settings.StartWithWindows = candidate.StartWithWindows; settings.Devices = candidate.Devices; RefreshChoices(); status.Text = "Settings saved."; MessageBox.Show(this, "Settings saved.", "DisplayPilot"); } catch (Exception e) { Error(e); } }
    void SetAuto(bool enabled) { settings.AutomaticSwitching = enabled; try { settings.Save(); } catch (Exception e) { Error(e); } UpdateStatus(); }
    void UpdateStatus() { mode.Text = engine.Mode; last.Text = "Last input: " + lastName; syncing = true; auto.Checked = settings.AutomaticSwitching; settingsAuto.Checked = settings.AutomaticSwitching; trayAuto.Checked = settings.AutomaticSwitching; syncing = false; trayMode.Text = "Current profile: " + engine.Mode; trayInput.Text = "Last input: " + lastName; var text = $"DisplayPilot | {engine.Mode} | {(settings.AutomaticSwitching ? "AUTO" : "PAUSED")}"; tray.Text = text[..Math.Min(text.Length, 63)]; }
    async Task Switch(string target, bool manual = false) { try { if (await engine.Request(target, manual)) status.Text = "Switched to " + engine.Mode + " at " + DateTime.Now.ToShortTimeString(); } catch (Exception e) { Error(e); } if (!IsDisposed) UpdateStatus(); }
    async Task ChangeProfile(string profile) { var info = new ProcessStartInfo(settings.DisplayMagicianPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true }; info.ArgumentList.Add("ChangeProfile"); info.ArgumentList.Add(profile); using var process = Process.Start(info) ?? throw new Exception("DisplayMagician did not start."); var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)); try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { try { process.Kill(true); } catch { } throw new Exception("DisplayMagician timed out. The assumed profile was not changed."); } await output; var err = await error; if (process.ExitCode != 0) throw new Exception($"DisplayMagician exited with code {process.ExitCode}. {err}"); }
    protected override void WndProc(ref Message m) { if (m.Msg == 0xFF && engine != null) { var id = RawInput.Read(m.LParam); if (id.Length > 0) { var rule = settings.Match(id); lastName = rule?.Name ?? "Unmapped input device"; UpdateStatus(); if (rule != null && rule.Target != "Ignore") _ = Switch(rule.Target); } } base.WndProc(ref m); }
    void Error(Exception e) { status.Text = e.Message; tray.ShowBalloonTip(5000, "DisplayPilot", e.Message, ToolTipIcon.Error); }
    void Open() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    protected override void OnFormClosing(FormClosingEventArgs e) { if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } base.OnFormClosing(e); }
    protected override void OnFormClosed(FormClosedEventArgs e) { tray.Dispose(); menu.Dispose(); base.OnFormClosed(e); }
}



