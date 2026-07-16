#pragma once

#include "settings_store.h"

#include <atomic>
#include <condition_variable>
#include <mutex>
#include <string>
#include <thread>

namespace notification {

class Service {
public:
    Service() = default;
    ~Service();

    Service(const Service&) = delete;
    Service& operator=(const Service&) = delete;

    void start(const settings::AppSettings& settings);
    void update(const settings::AppSettings& settings);
    void stop();

    bool running() const { return running_.load(); }
    std::string status() const;

private:
    void run();
    void setStatus(std::string value);
    settings::AppSettings settingsSnapshot() const;

    mutable std::mutex mutex_;
    std::condition_variable wakeup_;
    settings::AppSettings settings_;
    std::string status_;
    bool stopRequested_ = false;
    std::atomic<bool> running_{false};
    std::thread worker_;
};

} // namespace notification
