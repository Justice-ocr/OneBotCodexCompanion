using System.Drawing;

namespace OneBotCodexCompanion;

public sealed class MainForm : Form
{
    private static readonly Color Surface = Color.FromArgb(24, 24, 27);
    private static readonly Color Raised = Color.FromArgb(39, 39, 42);
    private static readonly Color Border = Color.FromArgb(63, 63, 70);
    private static readonly Color Accent = Color.FromArgb(225, 29, 72);
    private readonly SettingsStore _settingsStore = new();
    private readonly OneBotClient _oneBotClient = new();
    private readonly SessionMonitor _sessionMonitor = new();
    private AppSettings _settings = new();
    private string _token = "";
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
    private readonly Label _status = new();
    private readonly Button _saveButton = ButtonInput("保存设置", primary: true);
    private readonly Button _testButton = ButtonInput("测试并发送");
    private readonly Button _sendButton = ButtonInput("发送消息", primary: true);
    private readonly Button _monitorButton = ButtonInput("启动监视", primary: true);

    public MainForm()
    {
        Text = "OneBot Codex 通知助手";
        MinimumSize = new Size(880, 720);
        Size = new Size(980, 780);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Surface;
        ForeColor = Color.FromArgb(244, 244, 245);
        Font = new Font("Segoe UI", 10F);

        _routeList.View = View.Details;
        _routeList.FullRowSelect = true;
        _routeList.GridLines = false;
        _routeList.BackColor = Raised;
        _routeList.ForeColor = ForeColor;
        _routeList.BorderStyle = BorderStyle.FixedSingle;
        _routeList.Columns.Add("对话 ID", 280);
        _routeList.Columns.Add("类型", 90);
        _routeList.Columns.Add("收件人 ID", 180);
        _routeList.Height = 160;

        _monitorEnabled.Text = "任务完成后自动发送通知";
        _monitorEnabled.AutoSize = true;
        _monitorEnabled.ForeColor = ForeColor;
        _monitorEnabled.FlatStyle = FlatStyle.Flat;

        _status.AutoSize = false;
        _status.Height = 44;
        _status.ForeColor = Color.FromArgb(161, 161, 170);
        _status.TextAlign = ContentAlignment.MiddleLeft;

        _sessionMonitor.StatusChanged += message => BeginInvoke(() => SetStatus(message, false));
        _sessionMonitor.GenerationCompleted += thread => BeginInvoke(async () => await NotifyCompletionAsync(thread));
        FormClosing += async (_, _) => await _sessionMonitor.DisposeAsync();

        BuildLayout();
        Shown += async (_, _) => await LoadSettingsAsync();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(22),
            BackColor = Surface,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(Header(), 0, 0);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.Normal,
            BackColor = Surface,
            ForeColor = ForeColor,
            Padding = new Point(14, 7),
        };
        tabs.TabPages.Add(ConnectionPage());
        tabs.TabPages.Add(RoutesPage());
        tabs.TabPages.Add(MonitorPage());
        root.Controls.Add(tabs, 0, 1);
        root.Controls.Add(_status, 0, 2);
        Controls.Add(root);
    }

    private Control Header()
    {
        var panel = new Panel { Height = 88, Dock = DockStyle.Fill, BackColor = Raised, Padding = new Padding(18, 14, 18, 12), Margin = new Padding(0, 0, 0, 12) };
        var accent = new Panel { BackColor = Accent, Width = 4, Dock = DockStyle.Left };
        var title = new Label
        {
            Text = "OneBot Codex 通知助手",
            Font = new Font("Segoe UI Semibold", 18F),
            AutoSize = true,
            Location = new Point(18, 8),
        };
        var subtitle = new Label
        {
            Text = "本地安全发送 OneBot 通知，并可选监视 Codex 任务完成状态。",
            ForeColor = Color.FromArgb(161, 161, 170),
            AutoSize = true,
            Location = new Point(20, 46),
        };
        var badge = new Label
        {
            Text = "本地运行",
            AutoSize = true,
            BackColor = Color.FromArgb(63, 63, 70),
            ForeColor = Color.FromArgb(212, 212, 216),
            Padding = new Padding(9, 4, 9, 4),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(760, 16),
        };
        panel.Resize += (_, _) => badge.Left = panel.ClientSize.Width - badge.Width - 16;
        panel.Controls.Add(accent);
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(badge);
        return panel;
    }

    private TabPage ConnectionPage()
    {
        var page = NewPage("连接设置");
        var form = NewForm();
        var urlField = Labeled("OneBot HTTP 地址", _baseUrl);
        var tokenField = Labeled("访问令牌", _tokenInput);
        var recipientTypeField = Labeled("默认收件人类型", _recipientType);
        var recipientIdField = Labeled("群号或 QQ 号", _recipientId);
        var messageFormatField = Labeled("消息格式", _messageFormat);
        var manualMessageField = Labeled("手动发送消息", _manualMessage);
        form.Controls.Add(urlField, 0, 0);
        form.SetColumnSpan(urlField, 2);
        form.Controls.Add(tokenField, 0, 1);
        form.SetColumnSpan(tokenField, 2);
        form.Controls.Add(recipientTypeField, 0, 2);
        form.Controls.Add(recipientIdField, 1, 2);
        form.Controls.Add(messageFormatField, 0, 3);
        form.Controls.Add(ButtonPanel(_testButton, _saveButton), 1, 3);
        form.Controls.Add(manualMessageField, 0, 4);
        form.SetColumnSpan(manualMessageField, 2);
        _manualMessage.Height = 110;
        form.Controls.Add(ButtonPanel(_sendButton), 1, 5);
        page.Controls.Add(form);

        _saveButton.Click += async (_, _) => await SaveSettingsAsync();
        _testButton.Click += async (_, _) => await TestConnectionAsync();
        _sendButton.Click += async (_, _) => await SendManualAsync();
        return page;
    }

    private TabPage RoutesPage()
    {
        var page = NewPage("对话路由");
        var layout = NewForm();
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var threadField = Labeled("Codex 对话 ID", _threadId);
        layout.Controls.Add(threadField, 0, 0);
        layout.SetColumnSpan(threadField, 2);
        layout.Controls.Add(Labeled("收件人类型", _routeType), 0, 1);
        layout.Controls.Add(Labeled("群号或 QQ 号", _routeRecipientId), 1, 1);
        var setRoute = ButtonInput("新增或更新路由", primary: true);
        var deleteRoute = ButtonInput("移除选中路由");
        layout.Controls.Add(ButtonPanel(setRoute, deleteRoute), 1, 2);
        layout.Controls.Add(_routeList, 0, 3);
        layout.SetColumnSpan(_routeList, 2);
        var note = new Label
        {
            Text = "CDP 监视检测到指定对话完成时，将优先使用该路由的收件人。",
            ForeColor = Color.FromArgb(161, 161, 170),
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
        layout.Controls.Add(note, 0, 4);
        layout.SetColumnSpan(note, 2);
        page.Controls.Add(layout);

        setRoute.Click += async (_, _) => await SaveRouteAsync();
        deleteRoute.Click += async (_, _) => await DeleteRouteAsync();
        _routeList.SelectedIndexChanged += (_, _) => HydrateRouteSelection();
        return page;
    }

    private TabPage MonitorPage()
    {
        var page = NewPage("自动通知");
        var layout = NewForm();
        layout.Controls.Add(_monitorEnabled, 0, 0);
        layout.SetColumnSpan(_monitorEnabled, 2);
        layout.Controls.Add(ButtonPanel(_monitorButton), 1, 1);
        var note = new Label
        {
            Text = "监视器直接读取 Codex 本地会话日志中的 task_complete 事件。它不依赖浏览器调试端口，API 登录、官方账号登录和不同模型服务商都使用同一完成信号。程序启动后只会监视新完成的任务，不会重复发送历史通知。",
            ForeColor = Color.FromArgb(161, 161, 170),
            AutoSize = true,
            Dock = DockStyle.Fill,
            MaximumSize = new Size(700, 0),
            Padding = new Padding(0, 12, 0, 0),
        };
        layout.Controls.Add(note, 0, 2);
        layout.SetColumnSpan(note, 2);
        page.Controls.Add(layout);
        _monitorButton.Click += async (_, _) => await ToggleMonitorAsync();
        return page;
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
        var threadId = _routeList.SelectedItems[0].Text;
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
    }

    private async Task NotifyCompletionAsync(string threadId)
    {
        try
        {
            var recipient = !string.IsNullOrWhiteSpace(threadId) && _settings.ThreadRoutes.TryGetValue(threadId, out var route)
                ? route
                : _settings.DefaultRecipient;
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
        _settings.DefaultRecipient = new Recipient
        {
            TargetType = _recipientType.SelectedItem?.ToString() ?? "private",
            TargetId = _recipientId.Text.Trim(),
        };
        _settings.MessageFormat = _messageFormat.SelectedItem?.ToString() ?? "array";
        _settings.MonitorEnabled = _monitorEnabled.Checked;
    }

    private void RefreshRoutes()
    {
        _routeList.Items.Clear();
        foreach (var pair in _settings.ThreadRoutes.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var item = new ListViewItem(pair.Key);
            item.SubItems.Add(pair.Value.TargetType);
            item.SubItems.Add(pair.Value.TargetId);
            _routeList.Items.Add(item);
        }
    }

    private void HydrateRouteSelection()
    {
        if (_routeList.SelectedItems.Count == 0) return;
        var item = _routeList.SelectedItems[0];
        _threadId.Text = item.Text;
        _routeType.SelectedItem = item.SubItems[1].Text;
        _routeRecipientId.Text = item.SubItems[2].Text;
    }

    private void SetStatus(string message, bool success, bool error = false)
    {
        _status.Text = message;
        _status.ForeColor = error ? Color.FromArgb(251, 113, 133) : success ? Color.FromArgb(52, 211, 153) : Color.FromArgb(161, 161, 170);
    }

    private static TabPage NewPage(string title) => new(title) { BackColor = Surface, ForeColor = Color.FromArgb(244, 244, 245), Padding = new Padding(16) };

    private static TableLayoutPanel NewForm()
    {
        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(4) };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var index = 0; index < 8; index++) form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return form;
    }

    private static Control Labeled(string text, Control control)
    {
        var panel = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1, Margin = new Padding(6) };
        var label = new Label { Text = text, AutoSize = true, ForeColor = Color.FromArgb(212, 212, 216), Margin = new Padding(0, 0, 0, 6) };
        control.Dock = DockStyle.Top;
        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(control, 0, 1);
        return panel;
    }

    private static FlowLayoutPanel ButtonPanel(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 24, 6, 0) };
        foreach (var button in buttons) panel.Controls.Add(button);
        return panel;
    }

    private static TextBox TextInput(bool password = false, bool multiline = false) => new()
    {
        BackColor = Raised,
        ForeColor = Color.FromArgb(250, 250, 250),
        BorderStyle = BorderStyle.FixedSingle,
        Height = multiline ? 88 : 32,
        UseSystemPasswordChar = password,
        Multiline = multiline,
        Margin = new Padding(0),
    };

    private static ComboBox SelectInput(params string[] options)
    {
        var input = new ComboBox { BackColor = Raised, ForeColor = Color.FromArgb(250, 250, 250), DropDownStyle = ComboBoxStyle.DropDownList, Height = 32, Margin = new Padding(0) };
        input.Items.AddRange(options);
        input.SelectedIndex = 0;
        return input;
    }

    private static Button ButtonInput(string text, bool primary = false) => new()
    {
        Text = text,
        AutoSize = true,
        BackColor = primary ? Accent : Border,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        FlatAppearance = { BorderSize = 0 },
        Padding = new Padding(12, 7, 12, 7),
        Margin = new Padding(6, 0, 0, 0),
        Cursor = Cursors.Hand,
    };
}
