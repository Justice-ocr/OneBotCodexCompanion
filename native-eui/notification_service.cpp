#include "notification_service.h"

#include "onebot_client.h"
#include "session_monitor.h"

#include <chrono>
#include <utility>

namespace notification {
namespace {

std::string completionMessage(const codex::TaskCompletion& completion) {
    const std::string title = completion.title.empty() ? completion.threadId : completion.title;
    return "Codex 任务已完成\n任务：" + title;
}

std::string completionTitle(const codex::TaskCompletion& completion) {
    return completion.title.empty() ? completion.threadId : completion.title;
}

} // namespace

Service::~Service() {
    stop();
}

void Service::start(const settings::AppSettings& settings) {
    if (running()) {
        update(settings);
        return;
    }
    stop();
    {
        std::lock_guard<std::mutex> lock(mutex_);
        settings_ = settings;
        stopRequested_ = false;
        status_ = "正在启动 Codex 本地会话监视。";
    }
    running_.store(true);
    worker_ = std::thread([this] { run(); });
}

void Service::update(const settings::AppSettings& settings) {
    {
        std::lock_guard<std::mutex> lock(mutex_);
        settings_ = settings;
    }
    wakeup_.notify_all();
}

void Service::stop() {
    {
        std::lock_guard<std::mutex> lock(mutex_);
        stopRequested_ = true;
    }
    wakeup_.notify_all();
    if (worker_.joinable()) worker_.join();
    running_.store(false);
}

std::string Service::status() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return status_;
}

void Service::setStatus(std::string value) {
    std::lock_guard<std::mutex> lock(mutex_);
    status_ = std::move(value);
}

settings::AppSettings Service::settingsSnapshot() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return settings_;
}

void Service::run() {
    codex::SessionMonitor monitor;
    while (true) {
        {
            std::unique_lock<std::mutex> lock(mutex_);
            if (stopRequested_) break;
        }

        if (!monitor.running()) {
            std::string error;
            if (!monitor.start(&error)) {
                setStatus(error.empty() ? "无法启动 Codex 本地会话监视，将重试。" : error + " 将重试。");
                std::unique_lock<std::mutex> lock(mutex_);
                wakeup_.wait_for(lock, std::chrono::seconds(3), [this] { return stopRequested_; });
                continue;
            }
            setStatus("正在监视 Codex 本地会话。窗口隐藏到托盘后仍会继续运行。");
        }

        for (const codex::TaskCompletion& completion : monitor.poll()) {
            const settings::AppSettings current = settingsSnapshot();
            const settings::Recipient& recipient = settings::recipientFor(current, completion.threadId);
            const onebot::RequestResult result = onebot::sendNotification(
                current.baseUrl,
                current.accessToken,
                recipient.type,
                recipient.id,
                completionMessage(completion));
            if (result.ok) {
                setStatus("已发送任务完成通知：" + completionTitle(completion));
            } else {
                setStatus(result.detail.empty() ? "任务完成通知发送失败。" : result.detail);
            }
        }

        std::unique_lock<std::mutex> lock(mutex_);
        wakeup_.wait_for(lock, std::chrono::milliseconds(250), [this] { return stopRequested_; });
    }
    monitor.stop();
    running_.store(false);
}

} // namespace notification
