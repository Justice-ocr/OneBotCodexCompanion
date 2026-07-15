#include "onebot_client.h"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <winhttp.h>

#include <algorithm>
#include <memory>
#include <string>
#include <utility>
#include <vector>

namespace onebot {
namespace {

using InternetHandle = std::unique_ptr<std::remove_pointer_t<HINTERNET>, decltype(&WinHttpCloseHandle)>;

std::wstring toWide(const std::string& value) {
    if (value.empty()) return {};
    const int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()), nullptr, 0);
    if (length <= 0) return {};
    std::wstring output(static_cast<std::size_t>(length), L'\0');
    MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(), static_cast<int>(value.size()), output.data(), length);
    return output;
}

std::string fromBytes(const std::vector<char>& value) {
    return {value.begin(), value.end()};
}

std::string jsonEscape(const std::string& value) {
    std::string output;
    output.reserve(value.size() + 16);
    for (const unsigned char character : value) {
        switch (character) {
        case '\\': output += "\\\\"; break;
        case '"': output += "\\\""; break;
        case '\n': output += "\\n"; break;
        case '\r': output += "\\r"; break;
        case '\t': output += "\\t"; break;
        default:
            if (character < 0x20) {
                const char* hex = "0123456789abcdef";
                output += "\\u00";
                output += hex[(character >> 4) & 0x0f];
                output += hex[character & 0x0f];
            } else {
                output += static_cast<char>(character);
            }
        }
    }
    return output;
}

bool isOneBotOk(const std::string& body) {
    return body.find("\"status\":\"ok\"") != std::string::npos ||
           body.find("\"status\" : \"ok\"") != std::string::npos;
}

std::string shorten(std::string value) {
    constexpr std::size_t maximum = 180;
    if (value.size() > maximum) value.resize(maximum);
    return value;
}

struct HttpResponse {
    bool completed = false;
    DWORD statusCode = 0;
    std::string body;
    std::string error;
};

HttpResponse send(const std::string& baseUrl,
                  const std::string& action,
                  const std::string& token,
                  const std::string& method,
                  const std::string& body = {}) {
    const std::wstring url = toWide(baseUrl + (baseUrl.empty() || baseUrl.back() == '/' ? "" : "/") + action);
    if (url.empty()) return {false, 0, {}, "OneBot 地址必须是有效的 UTF-8 HTTP 地址。"};

    URL_COMPONENTS parts{};
    parts.dwStructSize = sizeof(parts);
    parts.dwSchemeLength = static_cast<DWORD>(-1);
    parts.dwHostNameLength = static_cast<DWORD>(-1);
    parts.dwUrlPathLength = static_cast<DWORD>(-1);
    parts.dwExtraInfoLength = static_cast<DWORD>(-1);
    if (!WinHttpCrackUrl(url.c_str(), static_cast<DWORD>(url.size()), 0, &parts) ||
        (parts.nScheme != INTERNET_SCHEME_HTTP && parts.nScheme != INTERNET_SCHEME_HTTPS)) {
        return {false, 0, {}, "OneBot 地址必须以 http:// 或 https:// 开头。"};
    }

    const std::wstring host(parts.lpszHostName, parts.dwHostNameLength);
    std::wstring path(parts.lpszUrlPath, parts.dwUrlPathLength);
    if (parts.dwExtraInfoLength > 0) path.append(parts.lpszExtraInfo, parts.dwExtraInfoLength);
    if (path.empty()) path = L"/";

    InternetHandle session(WinHttpOpen(L"OneBot Codex Companion/1.0", WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
                                       WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0), WinHttpCloseHandle);
    if (!session) return {false, 0, {}, "无法创建 HTTP 会话。"};
    WinHttpSetTimeouts(session.get(), 5000, 5000, 10000, 15000);

    InternetHandle connection(WinHttpConnect(session.get(), host.c_str(), parts.nPort, 0), WinHttpCloseHandle);
    if (!connection) return {false, 0, {}, "无法连接 OneBot 服务。"};

    const std::wstring methodWide = toWide(method);
    const DWORD flags = parts.nScheme == INTERNET_SCHEME_HTTPS ? WINHTTP_FLAG_SECURE : 0;
    InternetHandle request(WinHttpOpenRequest(connection.get(), methodWide.c_str(), path.c_str(), nullptr,
                                               WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, flags), WinHttpCloseHandle);
    if (!request) return {false, 0, {}, "无法创建 OneBot 请求。"};

    std::wstring headers = L"Accept: application/json\r\n";
    if (!token.empty()) headers += L"Authorization: Bearer " + toWide(token) + L"\r\n";
    if (!body.empty()) headers += L"Content-Type: application/json; charset=utf-8\r\n";
    if (!WinHttpSendRequest(request.get(), headers.c_str(), static_cast<DWORD>(headers.size()),
                            body.empty() ? WINHTTP_NO_REQUEST_DATA : const_cast<char*>(body.data()),
                            static_cast<DWORD>(body.size()), static_cast<DWORD>(body.size()), 0) ||
        !WinHttpReceiveResponse(request.get(), nullptr)) {
        return {false, 0, {}, "OneBot 请求失败，错误码 " + std::to_string(GetLastError()) + "。"};
    }

    DWORD statusCode = 0;
    DWORD statusLength = sizeof(statusCode);
    WinHttpQueryHeaders(request.get(), WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                        WINHTTP_HEADER_NAME_BY_INDEX, &statusCode, &statusLength, WINHTTP_NO_HEADER_INDEX);

    std::vector<char> response;
    while (true) {
        DWORD available = 0;
        if (!WinHttpQueryDataAvailable(request.get(), &available)) {
            return {false, statusCode, fromBytes(response), "读取 OneBot 响应失败。"};
        }
        if (available == 0) break;
        const std::size_t offset = response.size();
        response.resize(offset + available);
        DWORD received = 0;
        if (!WinHttpReadData(request.get(), response.data() + offset, available, &received)) {
            return {false, statusCode, fromBytes(response), "读取 OneBot 响应失败。"};
        }
        response.resize(offset + received);
    }
    return {true, statusCode, fromBytes(response), {}};
}

} // namespace

RequestResult testAndSend(const std::string& baseUrl,
                          const std::string& accessToken,
                          const std::string& recipientType,
                          const std::string& recipientId,
                          const std::string& message) {
    if (accessToken.empty()) return {false, "请填写访问令牌。"};
    if (recipientType != "private" && recipientType != "group") return {false, "收件人类型只能为 private 或 group。"};
    if (recipientId.empty()) return {false, "请填写收件人 ID。"};

    const HttpResponse probe = send(baseUrl, "get_version_info", accessToken, "GET");
    if (!probe.completed) return {false, probe.error};
    if (probe.statusCode < 200 || probe.statusCode >= 300 || !isOneBotOk(probe.body)) {
        return {false, "OneBot 连接验证失败 (HTTP " + std::to_string(probe.statusCode) + "): " + shorten(probe.body)};
    }

    RequestResult result = sendNotification(baseUrl, accessToken, recipientType, recipientId, message);
    if (result.ok) result.detail = "连接已验证，测试消息已发送。";
    return result;
}

RequestResult sendNotification(const std::string& baseUrl,
                               const std::string& accessToken,
                               const std::string& recipientType,
                               const std::string& recipientId,
                               const std::string& message) {
    if (accessToken.empty()) return {false, "请填写访问令牌。"};
    if (recipientType != "private" && recipientType != "group") return {false, "收件人类型只能为 private 或 group。"};
    if (recipientId.empty()) return {false, "请填写收件人 ID。"};
    if (message.empty()) return {false, "通知内容不能为空。"};
    const std::string idField = recipientType == "group" ? "group_id" : "user_id";
    const std::string action = recipientType == "group" ? "send_group_msg" : "send_private_msg";
    const std::string payload = "{\"" + idField + "\":\"" + jsonEscape(recipientId) +
                                "\",\"message\":[{\"type\":\"text\",\"data\":{\"text\":\"" +
                                jsonEscape(message) + "\"}}]}";
    const HttpResponse sent = send(baseUrl, action, accessToken, "POST", payload);
    if (!sent.completed) return {false, sent.error};
    if (sent.statusCode < 200 || sent.statusCode >= 300 || !isOneBotOk(sent.body)) {
        return {false, "消息发送失败 (HTTP " + std::to_string(sent.statusCode) + "): " + shorten(sent.body)};
    }
    return {true, "消息已发送。"};
}

} // namespace onebot
