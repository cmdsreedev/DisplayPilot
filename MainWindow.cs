using System.Diagnostics;
using System.Text.Json;

namespace DisplayPilot;

public sealed partial class MainWindow : Form
{
    readonly Settings settings;
    readonly SwitchEngine engine;
    readonly bool preview;
    readonly List<DeviceRule> rules;
    readonly List<string> profileNames;
    readonly NotifyIcon tray;
    readonly ContextMenuStrip trayMenu = new();
    readonly ToolStripMenuItem trayMode = new() { Enabled = false };
    readonly ToolStripMenuItem trayInput = new() { Enabled = false };
    readonly ToolStripMenuItem trayAuto = new("Automatic switching");
    readonly Dictionary<string, Panel> pages = new();
    readonly Dictionary<string, ModernButton> navigation = new();
    readonly Dictionary<DeviceRule, ComboBox> deviceChoices = new();
    readonly HashSet<DeviceRule> expandedDevices = new();
    readonly ToolTip hints = new() { AutoPopDelay = 10000 };
    readonly Panel pageHost = new() { Dock = DockStyle.Fill, BackColor = Theme.Canvas };
    readonly Label pageTitle = Theme.Text("Overview", 28, true);
    readonly Label pageDescription = Theme.Text("Your devices. The right display.", 14, color: Theme.Muted);
    readonly Label mode = Theme.Text("PC", 42, true);
    readonly Label modeNote = Theme.Text("Assumed at startup", 13, color: Theme.Accent);
    readonly Label lastInput = Theme.Text("No input yet", 18, true);
    readonly Label automationState = Theme.Text("Ready for your next input", 13, color: Theme.Muted);
    readonly Label sidebarState = Theme.Text("●  Automatic switching", 12, color: Color.FromArgb(114, 232, 201));
    readonly Label feedback = Theme.Text("All changes saved", 13, color: Theme.Muted);
    readonly Label deviceSummary = Theme.Text("", 13, color: Theme.Muted);
    readonly Toggle automatic = new() { AccessibleName = "Automatic switching" };
    readonly Toggle settingsAutomatic = new() { AccessibleName = "Automatic switching in Settings" };
    readonly Toggle startMinimized = new() { AccessibleName = "Start minimized" };
    readonly Toggle startWithWindows = new() { AccessibleName = "Start with Windows" };
    readonly TextBox magicianPath = Theme.Input();
    readonly TextBox deviceSearch = Theme.Input();
    readonly ComboBox pc = Theme.Select(), tv = Theme.Select(), manual = Theme.Select();
    readonly NumericUpDown cooldown = new() { Minimum = 0, Maximum = 120, Font = Theme.Font(), Width = 100, BorderStyle = BorderStyle.FixedSingle };
    readonly CardStack deviceCards = new(), profileCards = new();
    ModernButton saveButton = null!, pcButton = null!, tvButton = null!, applyButton = null!;
    string lastName = "No input yet";
    string currentPage = "Overview";
    bool dirty, syncing, exiting, switchedThisSession;
    string? startupWarning;

    public MainWindow(bool forceMinimized = false, bool smoke = false)
    {
        preview = smoke;
        settings = smoke ? PreviewSettings() : Settings.Load(out startupWarning);
        if (!smoke) settings.StartWithWindows = Startup.Enabled;
        engine = new(settings, ChangeProfile);
        rules = settings.Devices.Select(Clone).ToList();
        profileNames = [.. settings.Profiles];

        Text = "DisplayPilot";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = Theme.Font();
        BackColor = Theme.Canvas;
        ForeColor = Theme.Ink;
        ClientSize = new Size(1120, 760);
        MinimumSize = new Size(980, 690);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!;
        DoubleBuffered = true;
        KeyPreview = true;
        BuildShell();
        BuildOverview(); BuildDevices(); BuildProfiles(); BuildSettings();
        InitializeEditors();
        RenderDevices(); RenderProfiles();

        trayMenu.Items.Add("Open DisplayPilot", null, (_, _) => Open());
        trayMenu.Items.Add(trayMode); trayMenu.Items.Add(trayInput);
        trayMenu.Items.Add(new ToolStripSeparator()); trayMenu.Items.Add(trayAuto);
        trayMenu.Items.Add("Switch to PC", null, async (_, _) => await Switch(settings.PcProfile, true));
        trayMenu.Items.Add("Switch to TV", null, async (_, _) => await Switch(settings.TvProfile, true));
        trayMenu.Items.Add("Exit", null, (_, _) => ExitApplication());
        tray = new NotifyIcon { Icon = Icon, Text = "DisplayPilot", Visible = !smoke, ContextMenuStrip = trayMenu };
        tray.DoubleClick += (_, _) => Open();
        trayAuto.Click += (_, _) => SetAutomatic(!settings.AutomaticSwitching);
        automatic.CheckedChanged += (_, _) => { if (!syncing) SetAutomatic(automatic.Checked); };
        settingsAutomatic.CheckedChanged += (_, _) => { if (!syncing) SetAutomatic(settingsAutomatic.Checked); };
        KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.S) { Save(); e.SuppressKeyPress = true; } };
        Navigate("Overview"); UpdateStatus();
        Shown += async (_, _) =>
        {
            try
            {
                if (smoke) { await RunUiSmoke(); return; }
                RawInput.Register(Handle);
                Discover(false);
                if (startupWarning != null) Announce(startupWarning, true);
                if (forceMinimized || settings.StartMinimized) Hide();
            }
            catch (Exception e) { Error(e); }
        };
    }

    static Settings PreviewSettings() => new()
    {
        Devices = [
            new() { Name = "Desk keyboard", Identifier = "DEMO_DESK_KEYBOARD", Target = "PC", Kind = "Keyboard" },
            new() { Name = "Desk mouse", Identifier = "DEMO_DESK_MOUSE", Target = "PC", Kind = "Mouse" },
            new() { Name = "Couch keyboard", Identifier = "DEMO_COUCH_KEYBOARD", Target = "TV", Kind = "Keyboard" },
            new() { Name = "Game controller", Identifier = "DEMO_GAMEPAD", Target = "Ignore", Kind = "HID (detection only)" }
        ]
    };
    static DeviceRule Clone(DeviceRule r) => new() { Name = r.Name, Identifier = r.Identifier, Target = r.Target, Kind = r.Kind };

    void InitializeEditors()
    {
        syncing = true;
        magicianPath.Text = settings.DisplayMagicianPath;
        cooldown.Value = settings.CooldownSeconds;
        startMinimized.Checked = settings.StartMinimized;
        startWithWindows.Checked = settings.StartWithWindows;
        RefreshChoices();
        pc.SelectedItem = settings.PcProfile; tv.SelectedItem = settings.TvProfile;
        manual.SelectedItem = settings.PcProfile;
        syncing = false;
        magicianPath.TextChanged += (_, _) => MarkDirty();
        cooldown.ValueChanged += (_, _) => MarkDirty();
        startMinimized.CheckedChanged += (_, _) => MarkDirty();
        startWithWindows.CheckedChanged += (_, _) => MarkDirty();
        pc.SelectionChangeCommitted += (_, _) => MarkDirty();
        tv.SelectionChangeCommitted += (_, _) => MarkDirty();
    }

    void RefreshChoices()
    {
        foreach (var combo in new[] { pc, tv, manual })
        {
            string? selected = combo.SelectedItem as string;
            combo.Items.Clear(); combo.Items.AddRange(profileNames.ToArray());
            combo.SelectedItem = selected;
            if (combo.SelectedIndex < 0 && combo.Items.Count > 0) combo.SelectedIndex = 0;
        }
        foreach (var (rule, combo) in deviceChoices)
        {
            combo.Items.Clear(); combo.Items.Add("Ignore"); combo.Items.AddRange(profileNames.ToArray());
            combo.SelectedItem = rule.Target;
        }
    }

    void MarkDirty()
    {
        if (syncing) return;
        dirty = true; saveButton.Enabled = true;
        feedback.Text = "Unsaved changes"; feedback.ForeColor = Theme.Ink;
    }

    bool Save()
    {
        try
        {
            // Moving focus commits an in-progress profile rename before validation.
            if (!ValidateChildren()) return false;
            var candidate = new Settings
            {
                Profiles = [.. profileNames],
                PcProfile = pc.SelectedItem as string ?? "",
                TvProfile = tv.SelectedItem as string ?? "",
                DisplayMagicianPath = magicianPath.Text.Trim(),
                CooldownSeconds = (int)cooldown.Value,
                StartMinimized = startMinimized.Checked,
                StartWithWindows = startWithWindows.Checked,
                AutomaticSwitching = settings.AutomaticSwitching,
                Devices = rules.Select(Clone).ToList()
            };
            candidate.Validate();
            if (string.IsNullOrWhiteSpace(candidate.DisplayMagicianPath)) throw new InvalidDataException("Choose the DisplayMagician location in Settings.");
            if (!preview)
            {
                Startup.Set(candidate.StartWithWindows);
                candidate.Save();
            }
            settings.Profiles = candidate.Profiles; settings.PcProfile = candidate.PcProfile; settings.TvProfile = candidate.TvProfile;
            settings.DisplayMagicianPath = candidate.DisplayMagicianPath; settings.CooldownSeconds = candidate.CooldownSeconds;
            settings.StartMinimized = candidate.StartMinimized; settings.StartWithWindows = candidate.StartWithWindows;
            settings.Devices = candidate.Devices;
            dirty = false; saveButton.Enabled = false;
            Announce("Changes saved · You're all set");
            UpdateStatus();
            return true;
        }
        catch (Exception e) { Error(e); return false; }
    }

    void Discover(bool announce = true)
    {
        int added = 0;
        foreach (var device in RawInput.Enumerate())
        {
            if (rules.Any(r => r.Identifier.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(id => device.Path.Contains(id, StringComparison.OrdinalIgnoreCase)))) continue;
            rules.Add(new DeviceRule { Name = device.Name, Identifier = device.Path, Kind = device.Kind });
            added++;
        }
        RenderDevices();
        if (added > 0) MarkDirty();
        if (announce) Announce(added > 0 ? $"Found {added} new devices. Choose a profile, then save." : "Device list is up to date.");
    }

    void DetectProfiles()
    {
        try
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DisplayMagician", "Profiles", "DisplayProfiles.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            int added = 0;
            foreach (var p in doc.RootElement.GetProperty("Profiles").EnumerateArray())
            {
                var name = p.GetProperty("Name").GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(name) || name.Equals("Ignore", StringComparison.OrdinalIgnoreCase)
                    || profileNames.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                profileNames.Add(name); added++;
            }
            RefreshChoices(); RenderProfiles();
            if (added > 0) MarkDirty();
            Announce(added > 0 ? $"Added {added} profiles. Save when you're ready." : "All detected profiles are already listed.");
        }
        catch (Exception e) { Error(new Exception("Couldn't detect profiles. You can add their names manually. " + e.Message)); }
    }

    bool RenameProfile(string oldName, string newName)
    {
        newName = newName.Trim();
        if (oldName == newName) return true;
        if (string.IsNullOrWhiteSpace(newName) || newName.Equals("Ignore", StringComparison.OrdinalIgnoreCase)
            || profileNames.Any(n => n != oldName && n.Equals(newName, StringComparison.OrdinalIgnoreCase)))
        {
            Announce("Use a unique profile name. Ignore is reserved for unassigned devices.", true);
            return false;
        }
        int index = profileNames.IndexOf(oldName);
        if (index < 0) return false;
        bool isPc = pc.SelectedItem as string == oldName, isTv = tv.SelectedItem as string == oldName;
        profileNames[index] = newName;
        foreach (var r in rules.Where(r => r.Target == oldName)) r.Target = newName;
        RefreshChoices();
        if (isPc) pc.SelectedItem = newName;
        if (isTv) tv.SelectedItem = newName;
        MarkDirty();
        return true;
    }

    void RemoveProfile(string name)
    {
        if (rules.Any(r => r.Target == name) || pc.SelectedItem as string == name || tv.SelectedItem as string == name)
        {
            Announce("This profile is in use. Reassign its devices and PC/TV shortcuts before removing it.", true);
            return;
        }
        profileNames.Remove(name); RefreshChoices(); RenderProfiles(); MarkDirty();
    }

    void SetAutomatic(bool enabled)
    {
        bool previous = settings.AutomaticSwitching;
        try
        {
            settings.AutomaticSwitching = enabled;
            if (!preview) settings.Save();
            Announce(enabled ? "Automatic switching is on" : "Automatic switching paused · Manual controls still work");
        }
        catch (Exception e) { settings.AutomaticSwitching = previous; Error(e); }
        UpdateStatus();
    }

    void UpdateStatus()
    {
        mode.Text = engine.Mode; lastInput.Text = lastName;
        modeNote.Text = engine.Busy ? "Switching displays…" : switchedThisSession ? "Last applied by DisplayPilot" : "Assumed at startup · No display change requested";
        automationState.Text = settings.AutomaticSwitching ? "Listening for input" : "Paused · Manual only";
        sidebarState.Text = settings.AutomaticSwitching ? "●  Automatic switching" : "○  Switching paused";
        syncing = true; automatic.Checked = settings.AutomaticSwitching; settingsAutomatic.Checked = settings.AutomaticSwitching; syncing = false;
        trayMode.Text = "Current profile: " + engine.Mode; trayInput.Text = "Last input: " + lastName;
        trayAuto.Checked = settings.AutomaticSwitching;
        string tooltip = $"DisplayPilot | {engine.Mode} | {(settings.AutomaticSwitching ? "AUTO" : "PAUSED")}";
        tray.Text = tooltip[..Math.Min(tooltip.Length, 63)];
        pcButton.Text = settings.PcProfile == "PC" ? "Switch to PC" : "PC  ·  " + settings.PcProfile;
        tvButton.Text = settings.TvProfile == "TV" ? "Switch to TV" : "TV  ·  " + settings.TvProfile;
        pcButton.Enabled = tvButton.Enabled = applyButton.Enabled = !engine.Busy;
        pcButton.Invalidate(); tvButton.Invalidate();
    }

    async Task Switch(string target, bool manualRequest = false)
    {
        if (engine.Busy) return;
        try
        {
            if (manualRequest && string.Equals(target, engine.Mode, StringComparison.OrdinalIgnoreCase))
            {
                Announce($"Already on {target} · No display change needed"); return;
            }
            var task = engine.Request(target, manualRequest);
            if (!task.IsCompleted) UpdateStatus();
            if (!await task) return;
            switchedThisSession = true; Announce("Switched to " + engine.Mode);
        }
        catch (Exception e) { Error(e); }
        if (!IsDisposed) UpdateStatus();
    }

    async Task ChangeProfile(string profile)
    {
        if (preview) return;
        var info = new ProcessStartInfo(settings.DisplayMagicianPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        info.ArgumentList.Add("ChangeProfile"); info.ArgumentList.Add(profile);
        using var process = Process.Start(info) ?? throw new Exception("DisplayMagician did not start.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            throw new Exception("DisplayMagician timed out. The assumed profile was not changed.");
        }
        await output; var err = await error;
        if (process.ExitCode != 0) throw new Exception($"DisplayMagician exited with code {process.ExitCode}. {err}");
    }

    void BrowseFile()
    {
        using var dialog = new OpenFileDialog { Filter = "DisplayMagician console|DisplayMagicianConsole.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) == DialogResult.OK) magicianPath.Text = dialog.FileName;
    }
    void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose the DisplayMagician installation folder", UseDescriptionForTitle = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var console = Path.Combine(dialog.SelectedPath, "DisplayMagicianConsole.exe");
        if (File.Exists(console)) magicianPath.Text = console;
        else Announce("No DisplayMagicianConsole.exe in that folder. Choose its installation folder.", true);
    }
    void OpenMagician()
    {
        try
        {
            var executable = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(magicianPath.Text.Trim()))!, "DisplayMagician.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("Choose the DisplayMagician installation folder first.");
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(executable)! });
        }
        catch (Exception e) { Error(e); }
    }
    void OpenDownload()
    {
        try { Process.Start(new ProcessStartInfo("https://github.com/terrymacdonald/DisplayMagician/releases/latest") { UseShellExecute = true }); }
        catch (Exception e) { Error(e); }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0xFF && engine != null && tray != null && !preview)
        {
            string id = RawInput.Read(m.LParam);
            if (id.Length > 0)
            {
                var rule = settings.Match(id); string name = rule?.Name ?? "Unmapped input device";
                if (lastName != name) { lastName = name; UpdateStatus(); }
                if (rule != null && rule.Target != "Ignore") _ = Switch(rule.Target);
            }
        }
        base.WndProc(ref m);
    }
    void Announce(string message, bool error = false)
    {
        feedback.Text = message + (dirty ? "  ·  Unsaved changes" : "");
        feedback.ForeColor = error ? Theme.Danger : Theme.Muted;
        hints.SetToolTip(feedback, feedback.Text);
        feedback.AccessibleDescription = feedback.Text;
    }
    void Error(Exception e)
    {
        Announce(e.Message, true);
        if (!Visible && !preview) tray.ShowBalloonTip(5000, "DisplayPilot", e.Message, ToolTipIcon.Error);
    }
    void Open() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void ExitApplication()
    {
        if (dirty)
        {
            var result = MessageBox.Show(this, "Save your changes before exiting?", "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel || (result == DialogResult.Yes && !Save())) return;
        }
        exiting = true; Close();
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        base.OnFormClosing(e);
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        tray.Dispose(); trayMenu.Dispose(); hints.Dispose(); base.OnFormClosed(e);
    }

    async Task RunUiSmoke()
    {
        try
        {
            await Task.Delay(100);
            automatic.Checked = false;
            if (settings.AutomaticSwitching || settingsAutomatic.Checked) throw new Exception("Automatic-switch controls are out of sync.");
            automatic.Checked = true;
            manual.DroppedDown = true;
            if (!manual.DroppedDown) throw new Exception("Profile dropdown did not open.");
            manual.DroppedDown = false;
            foreach (string page in pages.Keys)
            {
                Navigate(page); PerformLayout();
                using var bitmap = new Bitmap(Width, Height);
                DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
                bitmap.Save(Path.Combine(AppContext.BaseDirectory, "smoke-" + page.ToLowerInvariant() + ".png"));
            }
            if (!RenameProfile("TV", "Cinema") || rules[2].Target != "Cinema" || tv.SelectedItem as string != "Cinema")
                throw new Exception("Profile rename did not preserve assignments and shortcut.");
            if (!Save() || settings.Devices[2].Target != "Cinema" || dirty) throw new Exception("Save did not commit the draft.");
            int profileCount = profileNames.Count;
            RemoveProfile("PC");
            if (profileNames.Count != profileCount) throw new Exception("An assigned profile was removed.");
            if (engine.Mode != "PC" || engine.LastAttempt != DateTime.MinValue) throw new Exception("UI initialization invoked switching.");
            deviceSearch.Text = "couch";
            if (deviceChoices.Count != 1) throw new Exception("Device search did not filter rows.");
            deviceSearch.Text = "";
            ClientSize = new Size(980, 690);
            Navigate("Settings"); PerformLayout();
            using var compact = new Bitmap(Width, Height);
            DrawToBitmap(compact, new Rectangle(Point.Empty, Size));
            compact.Save(Path.Combine(AppContext.BaseDirectory, "smoke-compact.png"));
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-result.txt"), "PASS: four pages, dropdown opens, toggle synchronization, profile rename propagation, save, removal guard, device search, compact layout, no startup switch. Preview never reads/writes personal settings or invokes DisplayMagician.");
        }
        catch (Exception e)
        {
            Environment.ExitCode = 1;
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-result.txt"), e.ToString());
        }
        finally { exiting = true; Close(); }
    }
}
