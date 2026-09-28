namespace DisplayPilot;

public sealed partial class MainWindow
{
    static ModernButton ActionButton(string title, Action action, bool primary = false, int width = 150)
    {
        var button = new ModernButton { Text = title, Primary = primary, Width = width, AccessibleName = title };
        button.Click += (_, _) => action();
        return button;
    }

    static TableLayoutPanel Column(Color background, params (Control Control, int Height)[] rows)
    {
        var column = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = rows.Length,
            BackColor = background,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        column.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < rows.Length; i++)
        {
            column.RowStyles.Add(new RowStyle(rows[i].Height < 0 ? SizeType.Percent : SizeType.Absolute, rows[i].Height < 0 ? 100 : rows[i].Height));
            Control child = rows[i].Control is TextBox input ? new InputFrame(input) : rows[i].Control;
            child.Dock = DockStyle.Fill; child.Margin = Padding.Empty;
            column.Controls.Add(child, 0, i);
        }
        return column;
    }

    static FlowLayoutPanel Buttons(params Control[] buttons)
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.White, Margin = Padding.Empty };
        foreach (Control button in buttons) row.Controls.Add(button is TextBox input ? new InputFrame(input) : button);
        return row;
    }

    static TableLayoutPanel Split(Control left, Control right, int rightWidth)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.White, Margin = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, rightWidth));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.Dock = right.Dock = DockStyle.Fill; left.Margin = Padding.Empty; right.Margin = new Padding(16, 0, 0, 0);
        row.Controls.Add(left, 0, 0); row.Controls.Add(right, 1, 0);
        return row;
    }

    static Surface Card(int height, Control content, Color? fill = null)
    {
        var card = new Surface { Height = height, Fill = fill ?? Color.White };
        content.Dock = DockStyle.Fill; content.BackColor = card.Fill;
        card.Controls.Add(content);
        return card;
    }

    Panel NewPage(string name)
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Visible = false };
        pages.Add(name, page); pageHost.Controls.Add(page);
        return page;
    }

    void BuildShell()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 212));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var side = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Padding = new Padding(18, 30, 18, 20), Margin = Padding.Empty };
        var brand = Theme.Text("DisplayPilot", 23, true, Color.White);
        var tagline = Theme.Text("MAKE THE SWITCH", 10, true, Color.FromArgb(127, 150, 169));
        var top = new Panel { Dock = DockStyle.Top, Height = 112, BackColor = Theme.Sidebar };
        top.Controls.Add(Column(Theme.Sidebar, (brand, 38), (tagline, 26)));
        side.Controls.Add(top);

        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 244, BackColor = Theme.Sidebar, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        foreach (var (name, glyph) in new[] { ("Overview", "\uE80F"), ("Devices", "\uE765"), ("Profiles", "\uE7F4"), ("Settings", "\uE713") })
        {
            var button = ActionButton(name, () => Navigate(name));
            button.Navigation = true; button.Glyph = glyph; button.Width = 176; button.Height = 48;
            button.Margin = new Padding(0, 0, 0, 10);
            navigation.Add(name, button); nav.Controls.Add(button);
        }
        side.Controls.Add(nav); nav.BringToFront();
        var bottom = Column(Theme.Sidebar,
            (sidebarState, 28),
            (Theme.Text("Runs quietly in your tray.", 12, color: Color.FromArgb(137, 154, 173)), 24),
            (Theme.Text("DisplayPilot  /  " + Application.ProductVersion.Split('+')[0], 11, color: Color.FromArgb(105, 125, 148)), 32));
        bottom.Dock = DockStyle.Bottom; bottom.Height = 90; side.Controls.Add(bottom);
        root.Controls.Add(side, 0, 0);

        var main = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Padding = new Padding(30, 24, 22, 12), Margin = Padding.Empty };
        var header = Column(Theme.Canvas, (pageTitle, 44), (pageDescription, 28));
        header.Dock = DockStyle.Top; header.Height = 90;
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 66, Padding = new Padding(0, 14, 0, 4), BackColor = Theme.Canvas };
        saveButton = ActionButton("Save changes", () => Save(), true, 152); saveButton.Dock = DockStyle.Right; saveButton.Enabled = false;
        hints.SetToolTip(saveButton, "Save all changes (Ctrl+S)");
        feedback.Dock = DockStyle.Fill;
        footer.Controls.Add(feedback); footer.Controls.Add(saveButton);
        main.Controls.Add(pageHost); main.Controls.Add(header); main.Controls.Add(footer);
        root.Controls.Add(main, 1, 0);
    }

    void Navigate(string page)
    {
        if (!pages.ContainsKey(page)) return;
        currentPage = page;
        foreach (var (name, panel) in pages) panel.Visible = name == page;
        pages[page].BringToFront();
        foreach (var (name, button) in navigation) { button.Selected = name == page; button.Invalidate(); }
        pageTitle.Text = page;
        pageDescription.Text = page switch
        {
            "Overview" => "Your devices. The right display.",
            "Devices" => "Choose what happens when you reach for a keyboard or mouse.",
            "Profiles" => "Your saved display layouts, ready to switch.",
            _ => "A few preferences. Then let DisplayPilot take care of the rest."
        };
    }

    void BuildOverview()
    {
        var page = NewPage("Overview"); var stack = new CardStack(); page.Controls.Add(stack);
        var heroText = Column(Theme.Tint,
            (Theme.Text("CURRENT PROFILE", 11, true, Theme.Accent), 28),
            (mode, 72), (modeNote, 28));
        var hero = Split(heroText, new DisplayArtwork(), 230); hero.BackColor = Theme.Tint;
        stack.Controls.Add(Card(166, hero, Theme.Tint));

        var autoText = Column(Color.White,
            (Theme.Text("Auto-switch", 17, true), 30), (automationState, 28));
        var autoHost = new Panel { BackColor = Color.White };
        automatic.Location = new Point(0, 10); autoHost.Controls.Add(automatic);
        var activity = new TableLayoutPanel { Height = 110, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 0, 0, 16), BackColor = Theme.Canvas };
        activity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); activity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        activity.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var automaticCard = Card(110, Split(autoText, autoHost, 72)); automaticCard.Dock = DockStyle.Fill; automaticCard.Margin = new Padding(0, 0, 8, 0);
        var inputCard = Card(110, Column(Color.White, (Theme.Text("LAST INPUT", 11, true, Theme.Muted), 24), (lastInput, 32)));
        inputCard.Dock = DockStyle.Fill; inputCard.Margin = new Padding(8, 0, 0, 0);
        activity.Controls.Add(automaticCard, 0, 0); activity.Controls.Add(inputCard, 1, 0); stack.Controls.Add(activity);

        pcButton = ActionButton("PC", async () => await Switch(settings.PcProfile, true), true, 185);
        pcButton.Glyph = "\uE7F4";
        tvButton = ActionButton("TV", async () => await Switch(settings.TvProfile, true), false, 185);
        tvButton.Glyph = "\uE714";
        manual.Width = 260; manual.Dock = DockStyle.None;
        applyButton = ActionButton("Switch profile", async () => { if (manual.SelectedItem is string p) await Switch(p, true); }, false, 145);
        stack.Controls.Add(Card(196, Column(Color.White,
            (Theme.Text("Switch displays", 17, true), 36),
            (Buttons(pcButton, tvButton), 52),
            (Buttons(manual, applyButton), 44),
            (Theme.Text("Manual controls work even when automatic switching is paused.", 12, color: Theme.Muted), 22))));

        var note = Theme.Text("Startup assumes your PC profile. Display changes made elsewhere aren't tracked.", 12, color: Theme.Muted);
        note.Height = 34; stack.Controls.Add(note);
    }

    void BuildDevices()
    {
        var page = NewPage("Devices");
        var top = new Panel { Dock = DockStyle.Top, Height = 102, BackColor = Theme.Canvas };
        deviceSearch.PlaceholderText = "Search devices…";
        deviceSearch.AccessibleName = "Search devices";
        deviceSearch.Dock = DockStyle.None; deviceSearch.Width = 225;
        deviceSearch.TextChanged += (_, _) => RenderDevices();
        var searchLabel = Theme.Text("Search", 13, color: Theme.Muted); searchLabel.Dock = DockStyle.None; searchLabel.Width = 52; searchLabel.Height = 36;
        var bar = Buttons(searchLabel, deviceSearch,
            ActionButton("Refresh", () => { try { Discover(); } catch (Exception e) { Error(e); } }, false, 112),
            ActionButton("Add device", () =>
            {
                var rule = new DeviceRule { Name = "New device", Identifier = "", Kind = "Keyboard / mouse" };
                rules.Add(rule); expandedDevices.Add(rule); deviceSearch.Clear(); RenderDevices(); MarkDirty();
            }, false, 125));
        bar.BackColor = Theme.Canvas;
        top.Controls.Add(Column(Theme.Canvas, (bar, 48), (deviceSummary, 26),
            (Theme.Text("HID controllers can be listed and assigned; controller input isn't active yet.", 12, color: Theme.Muted), 24)));
        page.Controls.Add(deviceCards); page.Controls.Add(top);
    }

    void RenderDevices()
    {
        deviceCards.SuspendLayout(); deviceChoices.Clear();
        foreach (Control c in deviceCards.Controls.Cast<Control>().ToArray()) c.Dispose();
        string query = deviceSearch.Text.Trim();
        var visible = rules.Where(r => query.Length == 0 || r.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || r.Identifier.Contains(query, StringComparison.OrdinalIgnoreCase) || r.Target.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        deviceSummary.Text = $"{rules.Count} devices  ·  {rules.Count(r => r.Target != "Ignore")} assigned  ·  New devices start on Ignore";
        foreach (var rule in visible)
        {
            bool expanded = expandedDevices.Contains(rule);
            var name = Theme.Input(rule.Name); name.AccessibleName = "Device name";
            name.TextChanged += (_, _) => { rule.Name = name.Text; MarkDirty(); };
            var choice = Theme.Select(); choice.AccessibleName = "Profile for " + rule.Name;
            choice.Items.Add("Ignore"); choice.Items.AddRange(profileNames.ToArray()); choice.SelectedItem = rule.Target;
            choice.SelectionChangeCommitted += (_, _) => { rule.Target = choice.SelectedItem as string ?? "Ignore"; MarkDirty(); };
            deviceChoices.Add(rule, choice);
            var top = Split(Column(Color.White, (Theme.Text(rule.Kind, 11, true, Theme.Muted), 22), (name, 32)),
                Column(Color.White, (Theme.Text("SWITCH TO", 11, true, Theme.Muted), 22), (choice, 34)), 218);
            var details = ActionButton(expanded ? "Hide details" : "Device details", () =>
            {
                if (!expandedDevices.Add(rule)) expandedDevices.Remove(rule);
                RenderDevices();
            }, false, 128);
            var remove = ActionButton("Remove", () => { rules.Remove(rule); expandedDevices.Remove(rule); RenderDevices(); MarkDirty(); }, false, 100);
            Control body;
            if (expanded)
            {
                var identifier = Theme.Input(rule.Identifier); identifier.AccessibleName = "Device identifier";
                identifier.TextChanged += (_, _) => { rule.Identifier = identifier.Text.Trim(); MarkDirty(); };
                hints.SetToolTip(identifier, rule.Identifier);
                body = Column(Color.White, (top, 64),
                    (Theme.Text("Identifier · separate alternative matches with |", 12, color: Theme.Muted), 26),
                    (identifier, 32), (Buttons(details, remove), 40));
            }
            else body = Column(Color.White, (top, 66), (Buttons(details), 38));
            deviceCards.Controls.Add(Card(expanded ? 214 : 150, body));
        }
        if (visible.Count == 0)
        {
            deviceCards.Controls.Add(Card(160, Column(Color.White,
                (Theme.Text(query.Length > 0 ? "No matching devices" : "Let's find your devices", 20, true), 40),
                (Theme.Text(query.Length > 0 ? "Try another name, identifier, or profile." : "Connect a keyboard or mouse, then select Refresh.", 14, color: Theme.Muted), 35),
                (Theme.Text("Assign a profile to start switching. Ignore leaves a device out.", 13, color: Theme.Muted), 30))));
        }
        deviceCards.ResumeLayout(true);
    }

    void BuildProfiles()
    {
        var page = NewPage("Profiles");
        var top = Buttons(ActionButton("Detect profiles", DetectProfiles, true, 158),
            ActionButton("Add profile", () =>
            {
                string name = "New profile"; int i = 2;
                while (profileNames.Contains(name, StringComparer.OrdinalIgnoreCase)) name = "New profile " + i++;
                profileNames.Add(name); RefreshChoices(); RenderProfiles(); MarkDirty();
            }, false, 138));
        top.BackColor = Theme.Canvas; top.Dock = DockStyle.Top; top.Height = 58;
        page.Controls.Add(profileCards); page.Controls.Add(top);
    }

    void RenderProfiles()
    {
        profileCards.SuspendLayout();
        foreach (Control c in profileCards.Controls.Cast<Control>().ToArray()) c.Dispose();
        profileCards.Controls.Add(Card(96, Column(Color.White,
            (Theme.Text("Use the names saved in DisplayMagician", 15, true), 28),
            (Theme.Text("Detection reads saved names. It won't change your display layout.", 13, color: Theme.Muted), 26))));
        foreach (string original in profileNames)
        {
            string current = original;
            var input = Theme.Input(current); input.AccessibleName = "Profile name";
            input.TextChanged += (_, _) => MarkDirty();
            input.Validating += (_, e) =>
            {
                if (RenameProfile(current, input.Text)) { current = input.Text.Trim(); input.Text = current; }
                else e.Cancel = true;
            };
            var remove = ActionButton("Remove", () => RemoveProfile(current), false, 96);
            var removeHost = new Panel { BackColor = Color.White }; remove.Location = new Point(0, 22); removeHost.Controls.Add(remove);
            var name = Column(Color.White, (Theme.Text("PROFILE NAME", 11, true, Theme.Muted), 24), (input, 34),
                (Theme.Text($"{rules.Count(r => r.Target == current)} assigned devices · Renaming updates their assignments", 12, color: Theme.Muted), 28));
            profileCards.Controls.Add(Card(132, Split(name, removeHost, 112)));
        }
        profileCards.ResumeLayout(true);
    }

    void BuildSettings()
    {
        var page = NewPage("Settings"); var stack = new CardStack(); page.Controls.Add(stack);
        var actions = Buttons(ActionButton("Choose folder", BrowseFolder, false, 138), ActionButton("Choose file", BrowseFile, false, 118),
            ActionButton("Open DisplayMagician", OpenMagician, false, 184));
        var download = new LinkLabel
        {
            Text = "Get DisplayMagician  ↗",
            Font = Theme.Font(13),
            LinkColor = Theme.Accent,
            ActiveLinkColor = Theme.Accent,
            VisitedLinkColor = Theme.Accent,
            BackColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AccessibleName = "Download DisplayMagician from its official releases"
        };
        download.LinkClicked += (_, _) => OpenDownload();
        magicianPath.AccessibleName = "DisplayMagician console location";
        stack.Controls.Add(Card(218, Column(Color.White,
            (Theme.Text("DisplayMagician", 18, true), 32),
            (Theme.Text("Choose where it's installed to connect your display profiles.", 13, color: Theme.Muted), 28),
            (magicianPath, 34), (actions, 48), (download, 28))));

        pc.AccessibleName = "PC shortcut and assumed startup profile"; tv.AccessibleName = "TV shortcut profile";
        var profileSelectors = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.White };
        profileSelectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); profileSelectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var pcField = Column(Color.White, (Theme.Text("PC / assumed startup profile", 13, color: Theme.Muted), 28), (pc, 36));
        var tvField = Column(Color.White, (Theme.Text("TV shortcut profile", 13, color: Theme.Muted), 28), (tv, 36));
        pcField.Margin = new Padding(0, 0, 12, 0); tvField.Margin = new Padding(12, 0, 0, 0);
        profileSelectors.Controls.Add(pcField, 0, 0); profileSelectors.Controls.Add(tvField, 1, 0);
        stack.Controls.Add(Card(156, Column(Color.White, (Theme.Text("Quick-switch profiles", 18, true), 36), (profileSelectors, 72))));

        cooldown.AccessibleName = "Switching cooldown in seconds";
        var number = new Panel { BackColor = Color.White }; cooldown.Dock = DockStyle.Top; number.Controls.Add(cooldown);
        stack.Controls.Add(Card(114, Split(Column(Color.White,
            (Theme.Text("Switching cooldown", 17, true), 30),
            (Theme.Text("Seconds to wait between automatic switches. Helps prevent bouncing.", 13, color: Theme.Muted), 32)), number, 116)));

        var launchRows = Column(Color.White,
            (Theme.Text("Launch & background", 18, true), 38),
            (ToggleRow("Start with Windows", "Ready when you sign in.", startWithWindows), 72),
            (ToggleRow("Start minimized", "Go straight to the system tray.", startMinimized), 72),
            (ToggleRow("Automatic switching", "Pause or resume immediately. This toggle saves on its own.", settingsAutomatic), 72));
        stack.Controls.Add(Card(300, launchRows));
        var tip = Theme.Text("Closing this window keeps switching active. Choose Exit from the tray to stop.", 12, color: Theme.Muted);
        tip.Height = 42; stack.Controls.Add(tip);
    }

    static Control ToggleRow(string title, string description, Toggle toggle)
    {
        var holder = new Panel { BackColor = Color.White }; toggle.Location = new Point(0, 10); holder.Controls.Add(toggle);
        return Split(Column(Color.White, (Theme.Text(title, 15, true), 28), (Theme.Text(description, 13, color: Theme.Muted), 26)), holder, 76);
    }
}
