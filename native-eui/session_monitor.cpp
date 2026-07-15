#include "session_monitor.h"

#include <cstdlib>
#include <fstream>
#include <iterator>
#include <regex>
#include <sstream>

namespace codex {
namespace {

std::filesystem::path defaultSessionsDirectory() {
    const char* profile = std::getenv("USERPROFILE");
    return profile && *profile ? std::filesystem::path(profile) / ".codex" / "sessions" : std::filesystem::path{};
}

std::string jsonString(const std::string& source, const std::string& key) {
    const std::string marker = "\"" + key + "\"";
    const std::size_t found = source.find(marker);
    if (found == std::string::npos) return {};
    std::size_t cursor = source.find(':', found + marker.size());
    if (cursor == std::string::npos) return {};
    cursor = source.find('"', cursor + 1);
    if (cursor == std::string::npos) return {};
    ++cursor;
    std::string value;
    bool escaping = false;
    for (; cursor < source.size(); ++cursor) {
        const char current = source[cursor];
        if (!escaping && current == '"') break;
        if (!escaping && current == '\\') {
            escaping = true;
            continue;
        }
        value += current;
        escaping = false;
    }
    return value;
}

std::string threadIdFromPath(const std::filesystem::path& path) {
    static const std::regex pattern(R"(([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\.jsonl$)", std::regex::icase);
    std::smatch match;
    const std::string filename = path.filename().string();
    return std::regex_search(filename, match, pattern) ? match[1].str() : std::string{};
}

std::string titleForThread(const std::string& threadId) {
    const char* profile = std::getenv("USERPROFILE");
    if (!profile || !*profile || threadId.empty()) return {};
    std::ifstream input(std::filesystem::path(profile) / ".codex" / "session_index.jsonl", std::ios::binary);
    if (!input) return {};
    std::string line;
    std::string title;
    while (std::getline(input, line)) {
        if (jsonString(line, "id") == threadId) {
            const std::string candidate = jsonString(line, "thread_name");
            if (!candidate.empty()) title = candidate;
        }
    }
    return title;
}

bool isCompletionRecord(const std::string& line) {
    return line.find("\"event_msg\"") != std::string::npos &&
           line.find("\"task_complete\"") != std::string::npos;
}

} // namespace

bool SessionMonitor::start(std::string* error) {
    stop();
    sessionsDirectory_ = defaultSessionsDirectory();
    std::error_code ec;
    if (sessionsDirectory_.empty() || !std::filesystem::is_directory(sessionsDirectory_, ec)) {
        if (error) *error = "未找到 Codex 本地会话目录。";
        return false;
    }
    for (std::filesystem::recursive_directory_iterator it(sessionsDirectory_, ec), end; !ec && it != end; it.increment(ec)) {
        if (!it->is_regular_file(ec) || it->path().extension() != ".jsonl") continue;
        positions_[it->path().string()] = it->file_size(ec);
    }
    if (ec) {
        positions_.clear();
        if (error) *error = "无法读取 Codex 会话目录。";
        return false;
    }
    running_ = true;
    nextPoll_ = std::chrono::steady_clock::now();
    return true;
}

void SessionMonitor::stop() {
    positions_.clear();
    completed_.clear();
    running_ = false;
}

std::vector<TaskCompletion> SessionMonitor::poll() {
    std::vector<TaskCompletion> output;
    if (!running_ || std::chrono::steady_clock::now() < nextPoll_) return output;
    nextPoll_ = std::chrono::steady_clock::now() + std::chrono::seconds(1);
    std::error_code ec;
    for (std::filesystem::recursive_directory_iterator it(sessionsDirectory_, ec), end; !ec && it != end; it.increment(ec)) {
        if (!it->is_regular_file(ec) || it->path().extension() != ".jsonl") continue;
        const std::filesystem::path path = it->path();
        const std::string key = path.string();
        const std::uintmax_t size = it->file_size(ec);
        std::uintmax_t& position = positions_[key];
        if (size < position) position = 0;
        if (size == position) continue;
        std::ifstream input(path, std::ios::binary);
        if (!input) continue;
        input.seekg(static_cast<std::streamoff>(position));
        const std::string data((std::istreambuf_iterator<char>(input)), std::istreambuf_iterator<char>());
        const std::size_t finalNewline = data.find_last_of('\n');
        if (finalNewline == std::string::npos) continue;
        const std::string threadId = threadIdFromPath(path);
        std::size_t lineStart = 0;
        while (lineStart <= finalNewline) {
            const std::size_t lineEnd = data.find('\n', lineStart);
            const std::string line = data.substr(lineStart, lineEnd - lineStart);
            if (!threadId.empty() && isCompletionRecord(line)) {
                const std::string turnId = jsonString(line, "turn_id");
                const std::string completedKey = threadId + ":" + (turnId.empty() ? line : turnId);
                if (completed_.insert(completedKey).second) output.push_back({threadId, turnId, titleForThread(threadId)});
            }
            if (lineEnd == std::string::npos || lineEnd >= finalNewline) break;
            lineStart = lineEnd + 1;
        }
        position += finalNewline + 1;
    }
    return output;
}

} // namespace codex
