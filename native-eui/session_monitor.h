#pragma once

#include <chrono>
#include <filesystem>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace codex {

struct TaskCompletion {
    std::string threadId;
    std::string turnId;
    std::string title;
};

class SessionMonitor {
public:
    bool start(std::string* error = nullptr);
    void stop();
    bool running() const { return running_; }
    std::vector<TaskCompletion> poll();

private:
    std::unordered_map<std::string, std::uintmax_t> positions_;
    std::unordered_set<std::string> completed_;
    std::chrono::steady_clock::time_point nextPoll_{};
    std::filesystem::path sessionsDirectory_;
    bool running_ = false;
};

} // namespace codex
