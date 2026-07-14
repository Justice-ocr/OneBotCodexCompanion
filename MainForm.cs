using System.Drawing;

namespace OneBotCodexCompanion;

public sealed class MainForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(17, 18, 23);
    private static readonly Color Sidebar = Color.FromArgb(22, 23, 29);
    private static readonly Color Surface = Color.FromArgb(33, 33, 42);
    private static readonly Color SurfaceRaised = Color.FromArgb(42, 42, 53);
    private static readonly Color Border = Color.FromArgb(67, 67, 81);
    private static readonly Color TextPrimary = Color.FromArgb(242, 245, 249);
    private static readonly Color TextMuted = Color.FromArgb(161, 173, 190);
    private static readonly Color Accent = Color.FromArgb(45, 137, 239);
    private static readonly Color AccentHover = Color.FromArgb(67, 153, 245);
    private static readonly Color Success = Color.FromArgb(48, 189, 133);
    private static readonly Color Danger = Color.FromArgb(238, 98, 107);
    private readonly SettingsStore _settingsStore = new();
    private readonly OneBotClient _oneBotClient = new();
    private readonly SessionMonitor _sessionMonitor = new();
    private readonly Dictionary<string, Button> _navigationButtons = new();
    private readonly Panel _contentHost = new();
    private readonly Label _status = new();
    private readonly Panel _statusDot = new();
    private readonly Label _monitorBadge = new();
    private AppSettings _settings = new();
    private string _token = "";
    private string _activePage = "connection";
    private readonly TextBox _baseUrl = TextInput();
    private readonly TextBox _tokenInput = TextInput(password: true);
    private readonly ComboBox _recipientType = SelectInput("private", "group");
    private readonly TextBox _recipientId = TextInput();
    private readonly ComboBox _messageFormat = SelectInput("array", "string");
    private readonly TextBox _manualMessage = TextInput(multiline: true);
    private readonly TextBox _threadId = TextInput();
    private readonly ComboBox _routeType = SelectInput("private", "group");
    private readonly TextBox _routeRecipientId = TextInput();
    private readonly ListView _routeList = new();
    private readonly CheckBox _monitorEnabled = new();
    private readonly Button _saveButton = ButtonInput("保存设置", primary: true);
    private readonly Button _testButton = ButtonInput("测试连接");
    private readonly Button _sendButton = ButtonInput("发送消息", primary: true);
    private readonly Button _monitorButton = ButtonInput("启动监视", primary: true);

    public MainForm()
    {
        Text = "OneBot Codex 通知助手";
        MinimumSize = new Size(960, 680);
        Size = new Size(1120, 780);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Canvas;
        ForeColor = TextPrimary;
        Font = new Font("Microsoft YaHei UI", 9.5F);

        ConfigureRouteList();
        ConfigureMonitorToggle();
        _status.AutoSize = false;
        _status.Dock = DockStyle.Fill;
        _status.ForeColor = TextMuted;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Padding = new Padding(10, 0, 0, 0);

        _sessionMonitor.StatusChanged += message => BeginInvoke(() => SetStatus(message, false));
        _sessionMonitor.GenerationCompleted += thread => BeginInvoke(async () => await NotifyCompletionAsync(thread));
        FormClosing += async (_, _) => await _sessionMonitor.DisposeAsync();

        BuildLayout();
        Shown += async (_, _) => await LoadSettingsAsync();
    }

    private void ConfigureRouteList()
    {
        _routeList.View = View.Details;
        _routeList.FullRowSelect = true;
        _routeList.HideSelection = false;
        _routeList.MultiSelect = false;
        _routeList.GridLines = false;
        _routeList.BackColor = SurfaceRaised;
        _routeList.ForeColor = TextPrimary;
        _routeList.BorderStyle = BorderStyle.FixedSingle;
        _routeList.Font = new Font("Microsoft YaHei UI", 9F);
        _routeList.Columns.Add("任务", 210);
        _routeList.Columns.Add("收件人", 165);
        _routeList.Columns.Add("类型", 85);
        _routeList.Columns.Add("对话 ID", 250);
        _routeList.Height = 200;
    }

    private void ConfigureMonitorToggle()
    {
        _monitorEnabled.Text = "任务完成后自动发送通知";
        _monitorEnabled.AutoSize = true;
        _monitorEnabled.ForeColor = TextPrimary;
        _monitorEnabled.FlatStyle = FlatStyle.Flat;
        _monitorEnabled.Padding = new Padding(2);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(22),
            BackColor = Canvas,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 216));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(SidebarPanel(), 0, 0);

        var main = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            BorderColor = Border,
            CornerRadius = 22,
            Padding = new Padding(28, 26, 28, 14),
            Margin = new Padding(18, 0, 0, 0),
        };
        _contentHost.Dock = DockStyle.Fill;
        _contentHost.BackColor = Surface;
        _contentHost.Controls.Add(ConnectionPage());
        _contentHost.Controls.Add(RoutesPage());
        _contentHost.Controls.Add(MonitorPage());
        main.Controls.Add(_contentHost);
        main.Controls.Add(StatusBar());
        root.Controls.Add(main, 1, 0);
        Controls.Add(root);
        ShowPage("connection");
    }

    private Control SidebarPanel()
    {
        var panel = new RoundedPanel { Dock = DockStyle.Fill, BackColor = Sidebar, BorderColor = Border, CornerRadius = 22, Padding = new Padding(16) };
        var mark = new Panel { BackColor = Accent, Width = 30, Height = 6, Location = new Point(20, 24) };
        var title = new Label
        {
            Text = "OneBot Codex",
            Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 46),
        };
        var subtitle = new Label
        {
            Text = "任务完成通知助手",
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(21, 74),
        };
        var navigation = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            Location = new Point(16, 122),
            Width = 184,
            Height = 210,
            WrapContents = false,
            BackColor = Sidebar,
        };
        navigation.Controls.Add(NavigationButton("connection", "连接设置"));
        navigation.Controls.Add(NavigationButton("routes", "对话路由"));
        navigation.Controls.Add(NavigationButton("monitor", "自动通知"));
        _monitorBadge.AutoSize = true;
        _monitorBadge.BackColor = Sidebar;
        _monitorBadge.ForeColor = TextMuted;
        _monitorBadge.Padding = new Padding(4);
        _monitorBadge.Text = "监视未启动";
        _monitorBadge.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _monitorBadge.Location = new Point(20, panel.Height - 48);
        panel.Resize += (_, _) => _monitorBadge.Top = panel.ClientSize.Height - 42;
        panel.Controls.Add(mark);
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(navigation);
        panel.Controls.Add(_monitorBadge);
        return panel;
    }

    private Button NavigationButton(string page, string text)
    {
        var button = new RoundedButton
        {
            Text = text,
            AutoSize = false,
            Width = 176,
            Height = 44,
            FillColor = Sidebar,
            HoverColor = SurfaceRaised,
            TextColor = TextMuted,
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0),
            Margin = new Padding(0, 0, 0, 10),
            Cursor = Cursors.Hand,
        };
        button.Click += (_, _) => ShowPage(page);
        _navigationButtons[page] = button;
        return button;
    }

    private Control StatusBar()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 28, ColumnCount = 2, BackColor = Surface, Padding = new Padding(0, 0, 0, 0) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 14));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _statusDot.BackColor = TextMuted;
        _statusDot.Width = 8;
        _statusDot.Height = 8;
        _statusDot.Anchor = AnchorStyles.Left;
        panel.Controls.Add(_statusDot, 0, 0);
        panel.Controls.Add(_status, 1, 0);
        return panel;
    }

    private Panel ConnectionPage()
    {
        var page = NewPage("connection", "连接设置", "配置 OneBot HTTP 服务与默认通知收件人。");
        var form = NewForm();
        form.Controls.Add(Labeled("OneBot HTTP 地址", "例如 http://127.0.0.1:3000", _baseUrl), 0, 0);
        form.SetColumnSpan(form.GetControlFromPosition(0, 0)!, 2);
        form.Controls.Add(Labeled("访问令牌", "仅存储在当前 Windows 用户的加密配置中", _tokenInput), 0, 1);
        form.SetColumnSpan(form.GetControlFromPosition(0, 1)!, 2);
        form.Controls.Add(Labeled("默认收件人类型", "private 为私聊，group 为群聊", _recipientType), 0, 2);
        form.Controls.Add(Labeled("群号或 QQ 号", "用于接收默认任务通知", _recipientId), 1, 2);
        form.Controls.Add(Labeled("消息格式", "根据 OneBot 实现选择 array 或 string", _messageFormat), 0, 3);
        form.Controls.Add(ButtonPanel(_saveButton, _testButton), 1, 3);
        form.Controls.Add(Labeled("手动发送消息", "用于确认消息内容和收件人", _manualMessage), 0, 4);
        form.SetColumnSpan(form.GetControlFromPosition(0, 4)!, 2);
        _manualMessage.Height = 96;
        form.Controls.Add(ButtonPanel(_sendButton), 1, 5);
        page.Controls.Add(form);
        _saveButton.Click += async (_, _) => await SaveSettingsAsync();
        _testButton.Click += async (_, _) => await TestConnectionAsync();
        _sendButton.Click += async (_, _) => await SendManualAsync();
        return page;
    }

    private Panel RoutesPage()
    {
        var page = NewPage("routes", "对话路由", "为指定 Codex 任务设置不同的 OneBot 收件人。");
        var layout = NewForm();
        layout.Controls.Add(Labeled("Codex 对话 ID", "从 Codex 会话记录中识别的对话 ID", _threadId), 0, 0);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 0)!, 2);
        layout.Controls.Add(Labeled("收件人类型", "private 或 group", _routeType), 0, 1);
        layout.Controls.Add(Labeled("群号或 QQ 号", "该任务完成时的收件人", _routeRecipientId), 1, 1);
        var setRoute = ButtonInput("保存路由", primary: true);
        var deleteRoute = ButtonInput("移除路由", danger: true);
        layout.Controls.Add(ButtonPanel(setRoute, deleteRoute), 1, 2);
        _routeList.Margin = new Padding(6, 12, 6, 4);
        layout.Controls.Add(_routeList, 0, 3);
        layout.SetColumnSpan(_routeList, 2);
        var note = new Label
        {
            Text = "通知会优先使用匹配的对话路由；未配置的任务将发送到默认收件人。",
            ForeColor = TextMuted,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(6, 8, 0, 0),
        };
        layout.Controls.Add(note, 0, 4);
        layout.SetColumnSpan(note, 2);
        page.Controls.Add(layout);
        setRoute.Click += async (_, _) => await SaveRouteAsync();
        deleteRoute.Click += async (_, _) => await DeleteRouteAsync();
        _routeList.SelectedIndexChanged += (_, _) => HydrateRouteSelection();
        return page;
    }

    private Panel MonitorPage()
    {
        var page = NewPage("monitor", "自动通知", "监视 Codex 本地会话事件，并在任务完成时发送 OneBot 消息。");
        var panel = new Panel { Dock = DockStyle.Top, Height = 142, BackColor = Surface, Padding = new Padding(0), Margin = new Padding(6) };
        var title = new Label { Text = "本地任务监视", Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold), AutoSize = true, Location = new Point(0, 8) };
        var description = new Label
        {
            Text = "读取 Codex 本地会话日志中的 task_complete 事件。API 登录、官方账号登录及不同模型服务商均使用同一完成信号。",
            ForeColor = TextMuted,
            Location = new Point(0, 38),
            MaximumSize = new Size(700, 0),
            AutoSize = true,
        };
        _monitorEnabled.Location = new Point(0, 84);
        _monitorButton.Location = new Point(0, 108);
        panel.Controls.Add(title);
        panel.Controls.Add(description);
        panel.Controls.Add(_monitorEnabled);
        panel.Controls.Add(_monitorButton);
        page.Controls.Add(panel);
        _monitorButton.Click += async (_, _) => await ToggleMonitorAsync();
        return page;
    }

    private Panel NewPage(string pageName, string title, string subtitle)
    {
        var page = new Panel
        {
            Name = pageName,
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(8, 86, 8, 14),
            AutoScroll = true,
            Visible = false,
        };
        var heading = new Label { Text = title, Font = new Font("Microsoft YaHei UI", 21F, FontStyle.Bold), AutoSize = true, Location = new Point(8, 12) };
        var description = new Label { Text = subtitle, ForeColor = TextMuted, AutoSize = true, Location = new Point(10, 52) };
        var divider = new Panel { BackColor = Border, Height = 1, Location = new Point(8, 78), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        page.Resize += (_, _) => divider.Width = Math.Max(0, page.ClientSize.Width - 16);
        page.Controls.Add(heading);
        page.Controls.Add(description);
        page.Controls.Add(divider);
        return page;
    }

    private void ShowPage(string page)
    {
        _activePage = page;
        foreach (Control control in _contentHost.Controls) control.Visible = control.Name == page;
        foreach (var pair in _navigationButtons)
        {
            var active = pair.Key == page;
            var button = (RoundedButton)pair.Value;
            button.FillColor = active ? Accent : Sidebar;
            button.HoverColor = active ? AccentHover : SurfaceRaised;
            button.TextColor = active ? Color.White : TextMuted;
            button.ForeColor = button.TextColor;
            button.Font = new Font("Microsoft YaHei UI", 9.5F, active ? FontStyle.Bold : FontStyle.Regular);
            button.Invalidate();
        }
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await _settingsStore.LoadAsync();
        _token = SettingsStore.ReadToken(_settings);
        _baseUrl.Text = _settings.BaseUrl;
        _tokenInput.Text = _token;
        _recipientType.SelectedItem = _settings.DefaultRecipient.TargetType;
        _recipientId.Text = _settings.DefaultRecipient.TargetId;
        _messageFormat.SelectedItem = _settings.MessageFormat;
        _monitorEnabled.Checked = _settings.MonitorEnabled;
        RefreshRoutes();
        SetStatus("设置已加载。访问令牌已使用 Windows DPAPI 保护。", false);
        if (_settings.MonitorEnabled) await StartMonitorAsync();
        UpdateMonitorBadge();
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            ApplyConnectionInputs();
            await _settingsStore.SaveAsync(_settings, _token);
            SetStatus("设置已保存到本机。", true);
            if (_settings.MonitorEnabled && !_sessionMonitor.IsRunning) await StartMonitorAsync();
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, false, true);
        }
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            ApplyConnectionInputs();
            SetStatus("正在检测 OneBot 并发送测试消息...", false);
            var result = await _oneBotClient.TestAndSendAsync(_settings, _token, _settings.DefaultRecipient, CancellationToken.None);
            SetStatus($"已连接到 {result}，测试消息已发送。", true);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, false, true);
        }
    }

    private async Task SendManualAsync()
    {
        try
        {
            ApplyConnectionInputs();
            var message = _manualMessage.Text.Trim();
            await _oneBotClient.SendAsync(_settings, _token, _settings.DefaultRecipient, message, CancellationToken.None);
            _manualMessage.Clear();
            SetStatus("通知已发送。", true);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, false, true);
        }
    }

    private async Task SaveRouteAsync()
    {
        var threadId = _threadId.Text.Trim();
        var targetId = _routeRecipientId.Text.Trim();
        if (string.IsNullOrWhiteSpace(threadId) || string.IsNullOrWhiteSpace(targetId))
        {
            SetStatus("请填写对话 ID 和收件人 ID。", false, true);
            return;
        }
        _settings.ThreadRoutes[threadId] = new Recipient { TargetType = _routeType.SelectedItem?.ToString() ?? "private", TargetId = targetId };
        await _settingsStore.SaveAsync(_settings, _token);
        RefreshRoutes();
        SetStatus("对话路由已保存。", true);
    }

    private async Task DeleteRouteAsync()
    {
        if (_routeList.SelectedItems.Count == 0) return;
        var threadId = _routeList.SelectedItems[0].Tag?.ToString() ?? _routeList.SelectedItems[0].Text;
        _settings.ThreadRoutes.Remove(threadId);
        await _settingsStore.SaveAsync(_settings, _token);
        RefreshRoutes();
        SetStatus("对话路由已移除。", true);
    }

    private async Task ToggleMonitorAsync()
    {
        if (_sessionMonitor.IsRunning)
        {
            await _sessionMonitor.StopAsync();
            _monitorButton.Text = "启动监视";
            SetStatus("本地 Codex 会话监视已停止。", false);
            UpdateMonitorBadge();
            return;
        }
        _settings.MonitorEnabled = _monitorEnabled.Checked;
        await _settingsStore.SaveAsync(_settings, _token);
        await StartMonitorAsync();
    }

    private async Task StartMonitorAsync()
    {
        try
        {
            await _sessionMonitor.StartAsync();
            _monitorButton.Text = "停止监视";
            SetStatus("本地 Codex 会话监视已启动。", true);
        }
        catch (Exception exception)
        {
            _settings.MonitorEnabled = false;
            _monitorEnabled.Checked = false;
            _monitorButton.Text = "启动监视";
            SetStatus($"本地 Codex 会话监视启动失败：{exception.Message}", false, true);
        }
        UpdateMonitorBadge();
    }

    private async Task NotifyCompletionAsync(string threadId)
    {
        try
        {
            var recipient = !string.IsNullOrWhiteSpace(threadId) && _settings.ThreadRoutes.TryGetValue(threadId, out var route) ? route : _settings.DefaultRecipient;
            var title = ThreadTitleResolver.GetTitle(threadId);
            var taskLabel = !string.IsNullOrWhiteSpace(title) ? title : threadId;
            var message = string.IsNullOrWhiteSpace(taskLabel) ? "Codex 任务已完成。" : $"Codex 任务已完成。\n任务：{taskLabel}";
            await _oneBotClient.SendAsync(_settings, _token, recipient, message, CancellationToken.None);
            SetStatus("任务完成通知已发送。", true);
        }
        catch (Exception exception)
        {
            SetStatus($"任务完成通知发送失败：{exception.Message}", false, true);
        }
    }

    private void ApplyConnectionInputs()
    {
        _settings.BaseUrl = _baseUrl.Text.Trim();
        _token = _tokenInput.Text.Trim();
        _settings.DefaultRecipient = new Recipient { TargetType = _recipientType.SelectedItem?.ToString() ?? "private", TargetId = _recipientId.Text.Trim() };
        _settings.MessageFormat = _messageFormat.SelectedItem?.ToString() ?? "array";
        _settings.MonitorEnabled = _monitorEnabled.Checked;
    }

    private void RefreshRoutes()
    {
        _routeList.Items.Clear();
        foreach (var pair in _settings.ThreadRoutes.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var title = ThreadTitleResolver.GetTitle(pair.Key);
            var item = new ListViewItem(string.IsNullOrWhiteSpace(title) ? "未命名任务" : title) { Tag = pair.Key };
            item.SubItems.Add(pair.Value.TargetId);
            item.SubItems.Add(pair.Value.TargetType == "group" ? "群聊" : "私聊");
            item.SubItems.Add(pair.Key);
            _routeList.Items.Add(item);
        }
    }

    private void HydrateRouteSelection()
    {
        if (_routeList.SelectedItems.Count == 0) return;
        var item = _routeList.SelectedItems[0];
        _threadId.Text = item.Tag?.ToString() ?? item.SubItems[3].Text;
        _routeType.SelectedItem = item.SubItems[2].Text == "群聊" ? "group" : "private";
        _routeRecipientId.Text = item.SubItems[1].Text;
    }

    private void UpdateMonitorBadge()
    {
        var running = _sessionMonitor.IsRunning;
        _monitorBadge.Text = running ? "监视运行中" : "监视未启动";
        _monitorBadge.BackColor = running ? Color.FromArgb(29, 84, 65) : SurfaceRaised;
        _monitorBadge.ForeColor = running ? Color.FromArgb(122, 224, 179) : TextMuted;
    }

    private void SetStatus(string message, bool success, bool error = false)
    {
        _status.Text = message;
        _status.ForeColor = error ? Color.FromArgb(255, 162, 168) : success ? Color.FromArgb(122, 224, 179) : TextMuted;
        _statusDot.BackColor = error ? Danger : success ? Success : TextMuted;
    }

    private static TableLayoutPanel NewForm()
    {
        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 0, 0, 12) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var index = 0; index < 8; index++) form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return form;
    }

    private static Control Labeled(string text, string hint, Control control)
    {
        var panel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Margin = new Padding(6, 8, 6, 8) };
        var label = new Label { Text = text, AutoSize = true, ForeColor = TextPrimary, Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 7) };
        control.Dock = DockStyle.Top;
        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(control, 0, 1);
        return panel;
    }

    private static FlowLayoutPanel ButtonPanel(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 18, 6, 0), WrapContents = false };
        foreach (var button in buttons) panel.Controls.Add(button);
        return panel;
    }

    private static TextBox TextInput(bool password = false, bool multiline = false) => new()
    {
        BackColor = SurfaceRaised,
        ForeColor = TextPrimary,
        BorderStyle = BorderStyle.FixedSingle,
        Height = multiline ? 88 : 34,
        UseSystemPasswordChar = password,
        Multiline = multiline,
        Margin = new Padding(0),
    };

    private static ComboBox SelectInput(params string[] options)
    {
        var input = new ComboBox { BackColor = SurfaceRaised, ForeColor = TextPrimary, DropDownStyle = ComboBoxStyle.DropDownList, Height = 34, Margin = new Padding(0), FlatStyle = FlatStyle.Flat };
        input.Items.AddRange(options);
        input.SelectedIndex = 0;
        return input;
    }

    private static Button ButtonInput(string text, bool primary = false, bool danger = false)
    {
        var color = danger ? Color.FromArgb(131, 54, 63) : primary ? Accent : SurfaceRaised;
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(102, 36),
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            Padding = new Padding(13, 6, 13, 6),
            Margin = new Padding(8, 0, 0, 0),
            Cursor = Cursors.Hand,
        };
        if (primary)
        {
            button.MouseEnter += (_, _) => button.BackColor = AccentHover;
            button.MouseLeave += (_, _) => button.BackColor = Accent;
        }
        return button;
    }
}
