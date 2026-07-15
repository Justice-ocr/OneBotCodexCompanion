#include "eui_neo.h"
#include "autostart.h"
#include "onebot_client.h"
#include "settings_store.h"
#include "session_monitor.h"

#include <algorithm>
#include <string>

#ifdef _WIN32
#define GLFW_EXPOSE_NATIVE_WIN32
#include <windows.h>
#include <shellapi.h>
#include <GLFW/glfw3.h>
#include <GLFW/glfw3native.h>
#endif

namespace app {

namespace {

enum class Page { Connection, Routes, Monitor };

struct AppState {
    settings::AppSettings settings = settings::load();
    Page page = Page::Connection;
    eui::Signal<std::string> oneBotUrl{settings.baseUrl};
    eui::Signal<std::string> accessToken{settings.accessToken};
    eui::Signal<std::string> recipientType{settings.defaultRecipient.type};
    eui::Signal<std::string> recipientId{settings.defaultRecipient.id};
    eui::Signal<std::string> threadId;
    eui::Signal<std::string> routeRecipientId;
    bool notificationsEnabled = true;
    bool monitorRunning = settings.monitorEnabled;
    bool startWithWindows = autostart::isEnabled();
    bool testInProgress = false;
    codex::SessionMonitor sessionMonitor;
    std::string status = "等待配置";
};

AppState state;

constexpr float kSidebarWidth = 250.0f;
constexpr float kPageInset = 40.0f;
constexpr float kContentWidth = 680.0f;

bool backgroundLaunchRequested() {
#ifdef _WIN32
    int argumentCount = 0;
    wchar_t** arguments = CommandLineToArgvW(GetCommandLineW(), &argumentCount);
    if (!arguments) return false;
    bool requested = false;
    for (int index = 1; index < argumentCount; ++index) {
        if (std::wstring(arguments[index]) == L"--background") {
            requested = true;
            break;
        }
    }
    LocalFree(arguments);
    return requested;
#else
    return false;
#endif
}

void requestBackgroundStartup() {
#ifdef _WIN32
    static const bool shouldHide = backgroundLaunchRequested();
    static bool requested = false;
    if (!shouldHide || requested) return;
    GLFWwindow* window = glfwGetCurrentContext();
    if (!window) return;
    requested = true;
    PostMessageW(glfwGetWin32Window(window), WM_CLOSE, 0, 0);
#endif
}

void syncSettings() {
    state.settings.baseUrl = state.oneBotUrl.get();
    state.settings.accessToken = state.accessToken.get();
    state.settings.defaultRecipient.type = state.recipientType.get();
    state.settings.defaultRecipient.id = state.recipientId.get();
    state.settings.monitorEnabled = state.monitorRunning;
    state.settings.startWithWindows = state.startWithWindows;
}

bool saveSettings() {
    syncSettings();
    std::string error;
    if (settings::save(state.settings, &error)) return true;
    state.status = error.empty() ? "保存设置失败。" : error;
    return false;
}

void updateMonitor() {
    if (!state.monitorRunning) {
        if (state.sessionMonitor.running()) state.sessionMonitor.stop();
        return;
    }
    if (!state.sessionMonitor.running()) {
        std::string error;
        if (!state.sessionMonitor.start(&error)) {
            state.monitorRunning = false;
            saveSettings();
            state.status = error.empty() ? "无法启动本地会话监视。" : error;
            return;
        }
        state.status = "正在监视 Codex 本地会话。";
    }
    for (const codex::TaskCompletion& completion : state.sessionMonitor.poll()) {
        const settings::Recipient& recipient = settings::recipientFor(state.settings, completion.threadId);
        const std::string title = completion.title.empty() ? completion.threadId : completion.title;
        const std::string message = "Codex 任务已完成\n任务：" + title;
        const std::string requestKey = "onebot.notify." + completion.threadId + "." + completion.turnId;
        const std::string url = state.oneBotUrl.get();
        const std::string token = state.accessToken.get();
        const std::string recipientType = recipient.type;
        const std::string recipientId = recipient.id;
        core::async::runOnce(requestKey, [url, token, recipientType, recipientId, message] {
            return onebot::sendNotification(url, token, recipientType, recipientId, message);
        }, [title](const core::async::Result<onebot::RequestResult>& result) {
            if (result.ok && result.value.ok) state.status = "已发送任务完成通知：" + title;
            else state.status = result.ok ? result.value.detail : result.error;
        });
    }
}

components::theme::ThemeColorTokens theme() {
    return components::theme::dark();
}

eui::Color textPrimary() {
    return components::theme::pageVisuals(theme()).titleColor;
}

eui::Color textMuted() {
    return components::theme::pageVisuals(theme()).subtitleColor;
}

void pageTitle(eui::Ui& ui, const std::string& title, const std::string& subtitle, float width) {
    ui.text("page.title")
        .position(kPageInset, 34.0f)
        .size(std::max(0.0f, width - kPageInset * 2.0f), 42.0f)
        .text(title)
        .fontSize(31.0f)
        .lineHeight(40.0f)
        .fontWeight(820)
        .color(textPrimary())
        .build();

    ui.text("page.subtitle")
        .position(kPageInset, 86.0f)
        .size(std::max(0.0f, width - kPageInset * 2.0f), 26.0f)
        .text(subtitle)
        .fontSize(16.0f)
        .lineHeight(22.0f)
        .color(textMuted())
        .build();
}

void panel(eui::Ui& ui, const std::string& id, float y, float width, float height) {
    components::panel(ui, id, theme())
        .position(kPageInset, y)
        .size(width, height)
        .radius(18.0f)
        .border(1.0f, components::theme::withOpacity(theme().border, 0.72f))
        .shadow(20.0f, 0.0f, 7.0f, {0.0f, 0.0f, 0.0f, 0.22f})
        .build();
}

void label(eui::Ui& ui, const std::string& id, const std::string& text, float x, float y, float width) {
    ui.text(id)
        .position(x, y)
        .size(width, 22.0f)
        .text(text)
        .fontSize(14.0f)
        .lineHeight(20.0f)
        .fontWeight(740)
        .color(textPrimary())
        .build();
}

void composeConnection(eui::Ui& ui, float width) {
    pageTitle(ui, "连接设置", "配置 OneBot HTTP 服务与默认通知收件人。", width);
    const float cardWidth = std::min(kContentWidth, std::max(320.0f, width - kPageInset * 2.0f));
    const float left = kPageInset + 24.0f;
    const float fieldWidth = cardWidth - 48.0f;
    panel(ui, "connection.card", 140.0f, cardWidth, 400.0f);

    label(ui, "connection.url.label", "OneBot HTTP 地址", left, 166.0f, fieldWidth);
    components::input(ui, "connection.url")
        .position(left, 194.0f)
        .size(fieldWidth, 42.0f)
        .placeholder("http://59.110.13.83:3000")
        .bind(state.oneBotUrl)
        .theme(theme())
        .build();

    label(ui, "connection.token.label", "访问令牌", left, 254.0f, fieldWidth);
    components::input(ui, "connection.token")
        .position(left, 282.0f)
        .size(fieldWidth, 42.0f)
        .placeholder("可留空")
        .bind(state.accessToken)
        .theme(theme())
        .build();

    const float half = (fieldWidth - 16.0f) * 0.5f;
    label(ui, "connection.type.label", "默认收件人类型", left, 342.0f, half);
    label(ui, "connection.id.label", "群号或 QQ 号", left + half + 16.0f, 342.0f, half);
    components::input(ui, "connection.type")
        .position(left, 370.0f)
        .size(half, 42.0f)
        .bind(state.recipientType)
        .theme(theme())
        .build();
    components::input(ui, "connection.recipient")
        .position(left + half + 16.0f, 370.0f)
        .size(half, 42.0f)
        .placeholder("收件人 ID")
        .bind(state.recipientId)
        .theme(theme())
        .build();

    components::button(ui, "connection.save")
        .position(left, 452.0f)
        .size(130.0f, 42.0f)
        .text("保存设置")
        .theme(theme(), true)
        .onClick([] { if (saveSettings()) state.status = "设置已保存。"; })
        .build();
    components::button(ui, "connection.test")
        .position(left + 142.0f, 452.0f)
        .size(130.0f, 42.0f)
        .text(state.testInProgress ? "正在测试" : "测试并发送")
        .theme(theme(), false)
        .onClick([] {
            if (state.testInProgress) return;
            state.testInProgress = true;
            state.status = "正在验证 OneBot 连接并发送真实测试消息...";
            const std::string url = state.oneBotUrl.get();
            const std::string token = state.accessToken.get();
            const std::string targetType = state.recipientType.get();
            const std::string targetId = state.recipientId.get();
            core::async::restart("onebot.test", [url, token, targetType, targetId] {
                return onebot::testAndSend(url, token, targetType, targetId, "OneBot Codex 通知助手测试消息。");
            }, [](const core::async::Result<onebot::RequestResult>& result) {
                state.testInProgress = false;
                if (result.ok && result.value.ok) state.status = result.value.detail;
                else state.status = result.ok ? result.value.detail : result.error;
                if (state.status.empty()) state.status = "测试请求未完成。";
            });
        })
        .build();
}

void composeRoutes(eui::Ui& ui, float width) {
    pageTitle(ui, "对话路由", "为指定 Codex 任务设置不同的 OneBot 收件人。", width);
    const float cardWidth = std::min(kContentWidth, std::max(320.0f, width - kPageInset * 2.0f));
    const float left = kPageInset + 24.0f;
    const float fieldWidth = cardWidth - 48.0f;
    panel(ui, "routes.card", 140.0f, cardWidth, 300.0f);
    label(ui, "routes.thread.label", "Codex 对话 ID", left, 166.0f, fieldWidth);
    components::input(ui, "routes.thread")
        .position(left, 194.0f)
        .size(fieldWidth, 42.0f)
        .placeholder("对话 ID")
        .bind(state.threadId)
        .theme(theme())
        .build();
    label(ui, "routes.recipient.label", "路由收件人", left, 254.0f, fieldWidth);
    components::input(ui, "routes.recipient")
        .position(left, 282.0f)
        .size(fieldWidth, 42.0f)
        .placeholder("群号或 QQ 号")
        .bind(state.routeRecipientId)
        .theme(theme())
        .build();
    components::button(ui, "routes.save")
        .position(left, 352.0f)
        .size(130.0f, 42.0f)
        .text("保存路由")
        .theme(theme(), true)
        .onClick([] {
            const std::string threadId = state.threadId.get();
            const std::string targetId = state.routeRecipientId.get();
            if (threadId.empty() || targetId.empty()) {
                state.status = "请填写对话 ID 和路由收件人。";
                return;
            }
            settings::upsertRoute(state.settings, {threadId, {state.recipientType.get(), targetId}});
            if (saveSettings()) state.status = "路由已保存。";
        })
        .build();
}

void composeMonitor(eui::Ui& ui, float width) {
    pageTitle(ui, "自动通知", "监视 Codex 本地会话事件，并在任务完成时发送 OneBot 消息。", width);
    const float cardWidth = std::min(kContentWidth, std::max(320.0f, width - kPageInset * 2.0f));
    const float left = kPageInset + 24.0f;
    panel(ui, "monitor.card", 140.0f, cardWidth, 280.0f);
    ui.text("monitor.status")
        .position(left, 170.0f)
        .size(cardWidth - 48.0f, 30.0f)
        .text(state.monitorRunning ? "监视运行中" : "监视未启动")
        .fontSize(20.0f)
        .lineHeight(26.0f)
        .fontWeight(760)
        .color(state.monitorRunning ? eui::Color{0.34f, 0.82f, 0.57f, 1.0f} : textPrimary())
        .build();
    ui.text("monitor.note")
        .position(left, 212.0f)
        .size(cardWidth - 48.0f, 42.0f)
        .text("原生版将读取 task_complete 事件，并按任务路由发送通知。")
        .fontSize(15.0f)
        .lineHeight(22.0f)
        .color(textMuted())
        .wrap(true)
        .build();
    ui.stack("monitor.autostart.container")
        .position(left, 260.0f)
        .size(cardWidth - 48.0f, 30.0f)
        .content([&] {
            components::checkbox(ui, "monitor.autostart")
                .size(cardWidth - 48.0f, 30.0f)
                .checked(state.startWithWindows)
                .text("随 Windows 登录启动")
                .theme(theme())
                .onChange([](bool enabled) {
                    std::string error;
                    if (!autostart::setEnabled(enabled, &error)) {
                        state.status = error.empty() ? "无法更新开机启动设置。" : error;
                        return;
                    }
                    state.startWithWindows = enabled;
                    if (saveSettings()) state.status = enabled ? "已启用开机启动。" : "已关闭开机启动。";
                })
                .build();
        })
        .build();
    components::button(ui, "monitor.toggle")
        .position(left, 312.0f)
        .size(130.0f, 42.0f)
        .text(state.monitorRunning ? "停止监视" : "启动监视")
        .theme(theme(), true)
        .onClick([] {
            state.monitorRunning = !state.monitorRunning;
            if (saveSettings()) state.status = state.monitorRunning ? "监视已启动。" : "监视已停止。";
        })
        .build();
}

void composePage(eui::Ui& ui, float width, float height) {
    ui.stack("page.canvas")
        .size(width, height)
        .content([&] {
            ui.rect("page.background")
                .size(width, height)
                .color(theme().background)
                .build();
            if (state.page == Page::Connection) composeConnection(ui, width);
            if (state.page == Page::Routes) composeRoutes(ui, width);
            if (state.page == Page::Monitor) composeMonitor(ui, width);
            ui.text("page.status")
                .position(kPageInset, std::max(0.0f, height - 34.0f))
                .size(std::max(0.0f, width - kPageInset * 2.0f), 20.0f)
                .text(state.status)
                .fontSize(13.0f)
                .lineHeight(18.0f)
                .color(textMuted())
                .build();
        })
        .build();
}

} // namespace

const DslAppConfig& dslAppConfig() {
    static const DslAppConfig config = DslAppConfig{}
        .title("OneBot Codex Companion")
        .pageId("onebot_codex_companion")
        .clearColor({0.10f, 0.10f, 0.12f, 1.0f})
        .windowSize(1180, 760)
        .tray(true)
        .trayTitle("OneBot Codex Companion")
        .trayIcon("assets/icon.ico")
        .fps(90.0);
    return config;
}

void compose(eui::Ui& ui, const eui::Screen& screen) {
    requestBackgroundStartup();
    updateMonitor();
    const float pageWidth = std::max(0.0f, screen.width - kSidebarWidth);
    ui.row("root")
        .size(screen.width, screen.height)
        .content([&] {
            components::navbar(ui, "sidebar")
                .theme(theme())
                .size(kSidebarWidth, screen.height)
                .brand("OneBot Codex", 0xF5FD)
                .subtitle("任务完成通知")
                .selected(static_cast<int>(state.page))
                .items({
                    {"connection", "连接设置", 0xF013, static_cast<int>(Page::Connection)},
                    {"routes", "对话路由", 0xF0C9, static_cast<int>(Page::Routes)},
                    {"monitor", "自动通知", 0xF0F3, static_cast<int>(Page::Monitor)},
                })
                .footer(state.monitorRunning ? "监视运行中" : "监视未启动", 0xF0E7, [] {
                    state.monitorRunning = !state.monitorRunning;
                    saveSettings();
                })
                .onChange([](int value) { state.page = static_cast<Page>(value); })
                .build();
            composePage(ui, pageWidth, screen.height);
        })
        .build();
}

} // namespace app
