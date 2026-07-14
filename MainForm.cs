using System.Drawing;

namespace OneBotCodexCompanion;

public sealed class MainForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(16, 17, 22);
    private static readonly Color Sidebar = Color.FromArgb(19, 20, 26);
    private static readonly Color Surface = Color.FromArgb(37, 40, 50);
    private static readonly Color SurfaceRaised = Color.FromArgb(47, 52, 64);
    private static readonly Color Border = Color.FromArgb(70, 74, 88);
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
    private readonly RoundedPanel _monitorCard = new();
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
            Padding = new Padding(0),
            BackColor = Canvas,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(SidebarPanel(), 0, 0);

        var main = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            BorderColor = Border,
            CornerRadius = 34,
            Padding = new Padding(40, 34, 40, 20),
            Margin = new Padding(34, 34, 34, 34),
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
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Sidebar, Padding = new Padding(0) };
        var mark = new RoundedPanel { BackColor = Accent, CornerRadius = 8, Width = 42, Height = 10, Location = new Point(104, 34) };
        var title = new Label
        {
            Text = "Codex 通知助手",
            Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold),
            AutoSize = false,
            Width = 190,
            Height = 28,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(30, 64),
        };
        var subtitle = new Label
        {
            Text = "OneBot 任务完成通知",
            ForeColor = TextMuted,
            Font = new Font("Microsoft YaHei UI", 8.5F),
            AutoSize = false,
            Width = 190,
            Height = 22,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(30, 92),
        };
        var navigation = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            Location = new Point(30, 158),
            Width = 190,
            Height = 230,
            WrapContents = false,
            BackColor = Sidebar,
        };
        navigation.Controls.Add(NavigationButton("connection", "连接设置"));
        navigation.Controls.Add(NavigationButton("routes", "对话路由"));
        navigation.Controls.Add(NavigationButton("monitor", "自动通知"));
        _monitorCard.BackColor = Surface;
        _monitorCard.BorderColor = Border;
        _monitorCard.CornerRadius = 14;
        _monitorCard.Size = new Size(190, 48);
        _monitorCard.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        _monitorCard.Location = new Point(30, panel.Height - 78);
        panel.Resize += (_, _) => _monitorCard.Top = panel.ClientSize.Height - 78;
        _monitorBadge.AutoSize = false;
        _monitorBadge.Dock = DockStyle.Fill;
        _monitorBadge.BackColor = Color.Transparent;
        _monitorBadge.ForeColor = TextMuted;
        _monitorBadge.TextAlign = ContentAlignment.MiddleCenter;
        _monitorBadge.Padding = new Padding(8, 0, 8, 0);
        _monitorBadge.Text = "监视未启动";
        panel.Controls.Add(mark);
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(navigation);
        _monitorCard.Controls.Add(_monitorBadge);
        panel.Controls.Add(_monitorCard);
        return panel;
    }

    private Button NavigationButton(string page, string text)
    {
        var button = new RoundedButton
        {
            Text = text,
            AutoSize = false,
            Width = 190,
            Height = 50,
            FillColor = Surface,
            HoverColor = SurfaceRaised,
            TextColor = TextMuted,
            ForeColor = TextMuted,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 18, 0),
            Margin = new Padding(0, 0, 0, 14),
            CornerRadius = 14,
            BorderColor = Border,
            Cursor = Cursors.Hand,
        };
        button.Click += (_, _) => ShowPage(page);
        _navigationButtons[page] = button;
        return button;
    }

    private Control StatusBar()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 28, ColumnCount = 2, BackColor = Surface, Padding = new Padding(0) };
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
        AddContentCard(page, form, 500);
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
        AddContentCard(page, layout, 490);
        setRoute.Click += async (_, _) => await SaveRouteAsync();
        deleteRoute.Click += async (_, _) => await DeleteRouteAsync();
        _routeList.SelectedIndexChanged += (_, _) => HydrateRouteSelection();
        return page;
    }

    private Panel MonitorPage()
    {
        var page = NewPage("monitor", "自动通知", "监视 Codex 本地会话事件，并在任务完成时发送 OneBot 消息。");
        var panel = new Panel { Dock = DockStyle.Fill, Height = 220, BackColor = SurfaceRaised, Padding = new Padding(24) };
        var title = new Label { Text = "本地任务监视", Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold), AutoSize = true, Location = new Point(24, 24) };
        var description = new Label
        {
            Text = "读取 Codex 本地会话日志中的 task_complete 事件。API 登录、官方账号登录及不同模型服务商均使用同一完成信号。",
            ForeColor = TextMuted,
            Location = new Point(24, 58),
            MaximumSize = new Size(620, 0),
            AutoSize = true,
        };
        _monitorEnabled.Location = new Point(24, 118);
        _monitorButton.Location = new Point(24, 150);
        panel.Controls.Add(title);
        panel.Controls.Add(description);
        panel.Controls.Add(_monitorEnabled);
        panel.Controls.Add(_monitorButton);
        AddContentCard(page, panel, 220);
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
            Padding = new Padding(0),
            AutoScroll = true,
            Visible = false,
        };
        var heading = new Label { Text = title, Font = new Font("Microsoft YaHei UI", 23F, FontStyle.Bold), AutoSize = true, Location = new Point(0, 2) };
        var description = new Label { Text = subtitle, ForeColor = TextMuted, AutoSize = true, Location = new Point(2, 48) };
        var divider = new Panel { BackColor = Border, Height = 1, Location = new Point(0, 88), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        page.Resize += (_, _) => divider.Width = Math.Max(0, page.ClientSize.Width);
        page.Controls.Add(heading);
        page.Controls.Add(description);
        page.Controls.Add(divider);
        return page;
    }

    private void AddContentCard(Panel page, Control content, int height)
    {
        var card = new RoundedPanel
        {
            BackColor = SurfaceRaised,
            BorderColor = Border,
            CornerRadius = 18,
            Height = height,
            Top = 118,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
        };
        content.Dock = DockStyle.Fill;
        content.BackColor = SurfaceRaised;
        content.Padding = new Padding(22);
        card.Controls.Add(content);
        page.Controls.Add(card);

        void PlaceCard()
        {
            card.Left = 0;
            card.Width = Math.Min(760, Math.Max(320, page.ClientSize.Width - SystemInformation.VerticalScrollBarWidth));
        }

        page.Resize += (_, _) => PlaceCard();
        PlaceCard();
    }

    private void ShowPage(string page)
    {
        _activePage = page;
        foreach (Control control in _contentHost.Controls) control.Visible = control.Name == page;
        foreach (var pair in _navigationButtons)
        {
            var active = pair.Key == page;
            var button = (RoundedButton)pair.Value;
            button.FillColor = active ? Accent : Surface;
            button.HoverColor = active ? AccentHover : SurfaceRaised;
            button.TextColor = active ? Color.White : TextMuted;
            button.BorderColor = active ? Color.FromArgb(105, 153, 241) : Border;
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
        _monitorCard.BackColor = running ? Color.FromArgb(31, 80, 64) : Surface;
        _monitorCard.BorderColor = running ? Color.FromArgb(61, 151, 113) : Border;
        _monitorCard.Invalidate();
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
        var form = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = false, ColumnCount = 2, Padding = new Padding(0, 0, 0, 12) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var index = 0; index < 8; index++) form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return form;
    }

    private static Control Labeled(string text, string hint, Control control)
    {
        var panel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Margin = new Padding(6, 8, 6, 8) };
        var label = new Label { Text = text, AutoSize = true, ForeColor = TextPrimary, Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 7) };
        var height = control is TextBox textBox && textBox.Multiline ? 104 : 42;
        var field = new RoundedPanel { BackColor = Surface, BorderColor = Border, CornerRadius = 10, Height = height, Dock = DockStyle.Top };
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = control is TextBox { Multiline: true } ? new Padding(12, 10, 12, 10) : new Padding(12, 9, 12, 7) };
        if (control is TextBox input)
        {
            input.BorderStyle = BorderStyle.None;
            input.BackColor = Surface;
            input.ForeColor = TextPrimary;
        }
        else if (control is ComboBox select)
        {
            select.BackColor = Surface;
            select.ForeColor = TextPrimary;
        }
        control.Dock = DockStyle.Fill;
        host.Controls.Add(control);
        field.Controls.Add(host);
        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(field, 0, 1);
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
        var color = danger ? Color.FromArgb(131, 54, 63) : primary ? Accent : Color.FromArgb(58, 64, 77);
        var button = new RoundedButton
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(102, 36),
            FillColor = color,
            HoverColor = danger ? Color.FromArgb(154, 64, 74) : primary ? AccentHover : Color.FromArgb(62, 68, 82),
            BorderColor = primary ? Color.FromArgb(105, 153, 241) : danger ? Color.FromArgb(173, 76, 86) : Color.FromArgb(98, 108, 128),
            TextColor = Color.White,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            CornerRadius = 11,
            Padding = new Padding(13, 6, 13, 6),
            Margin = new Padding(8, 0, 0, 0),
            Cursor = Cursors.Hand,
        };
        return button;
    }
}
