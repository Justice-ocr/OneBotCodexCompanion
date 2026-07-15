#include "autostart.h"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <string>

namespace autostart {
namespace {

constexpr wchar_t kRunKey[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Run";
constexpr wchar_t kValueName[] = L"OneBotCodexCompanionNative";

std::wstring executableCommand() {
    DWORD capacity = MAX_PATH;
    while (capacity < 32768) {
        std::wstring path(capacity, L'\0');
        const DWORD length = GetModuleFileNameW(nullptr, path.data(), capacity);
        if (length == 0) return {};
        if (length < capacity - 1) {
            path.resize(length);
            return L"\"" + path + L"\" --background";
        }
        capacity *= 2;
    }
    return {};
}

std::string windowsError(LSTATUS status) {
    wchar_t buffer[256]{};
    const DWORD length = FormatMessageW(FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                                        nullptr, static_cast<DWORD>(status), 0, buffer,
                                        static_cast<DWORD>(std::size(buffer)), nullptr);
    if (length == 0) return "Windows 错误码 " + std::to_string(status) + "。";
    const int utf8Length = WideCharToMultiByte(CP_UTF8, 0, buffer, static_cast<int>(length), nullptr, 0, nullptr, nullptr);
    std::string output(static_cast<std::size_t>(utf8Length), '\0');
    WideCharToMultiByte(CP_UTF8, 0, buffer, static_cast<int>(length), output.data(), utf8Length, nullptr, nullptr);
    while (!output.empty() && (output.back() == '\r' || output.back() == '\n')) output.pop_back();
    return output;
}

} // namespace

bool isEnabled() {
    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, kRunKey, 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) return false;
    DWORD type = 0;
    DWORD byteLength = 0;
    LSTATUS status = RegQueryValueExW(key, kValueName, nullptr, &type, nullptr, &byteLength);
    if (status != ERROR_SUCCESS || (type != REG_SZ && type != REG_EXPAND_SZ) || byteLength < sizeof(wchar_t)) {
        RegCloseKey(key);
        return false;
    }
    std::wstring registered(byteLength / sizeof(wchar_t), L'\0');
    status = RegQueryValueExW(key, kValueName, nullptr, &type,
                              reinterpret_cast<BYTE*>(registered.data()), &byteLength);
    RegCloseKey(key);
    if (status != ERROR_SUCCESS) return false;
    while (!registered.empty() && registered.back() == L'\0') registered.pop_back();
    const std::wstring expected = executableCommand();
    return !expected.empty() && CompareStringOrdinal(registered.c_str(), -1, expected.c_str(), -1, TRUE) == CSTR_EQUAL;
}

bool setEnabled(bool enabled, std::string* error) {
    HKEY key = nullptr;
    const LSTATUS opened = RegCreateKeyExW(HKEY_CURRENT_USER, kRunKey, 0, nullptr, 0,
                                            KEY_SET_VALUE | KEY_QUERY_VALUE, nullptr, &key, nullptr);
    if (opened != ERROR_SUCCESS) {
        if (error) *error = "无法打开开机启动设置：" + windowsError(opened);
        return false;
    }
    LSTATUS status = ERROR_SUCCESS;
    if (enabled) {
        const std::wstring command = executableCommand();
        if (command.empty()) {
            RegCloseKey(key);
            if (error) *error = "无法获取程序路径。";
            return false;
        }
        status = RegSetValueExW(key, kValueName, 0, REG_SZ,
                                reinterpret_cast<const BYTE*>(command.c_str()),
                                static_cast<DWORD>((command.size() + 1) * sizeof(wchar_t)));
    } else {
        status = RegDeleteValueW(key, kValueName);
        if (status == ERROR_FILE_NOT_FOUND) status = ERROR_SUCCESS;
    }
    RegCloseKey(key);
    if (status == ERROR_SUCCESS) return true;
    if (error) *error = "无法更新开机启动设置：" + windowsError(status);
    return false;
}

} // namespace autostart
