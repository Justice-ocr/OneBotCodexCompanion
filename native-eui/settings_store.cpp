#include "settings_store.h"

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <wincrypt.h>

#include <algorithm>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <string>

namespace settings {
namespace {

std::filesystem::path settingsPath() {
    const char* appData = std::getenv("APPDATA");
    const std::filesystem::path base = appData && *appData
        ? std::filesystem::path(appData)
        : std::filesystem::temp_directory_path();
    return base / "OneBot Codex Companion" / "native-eui-settings.json";
}

std::filesystem::path legacySettingsPath() {
    return settingsPath().parent_path() / "settings.json";
}

std::string escapeJson(const std::string& value) {
    std::string output;
    output.reserve(value.size() + 16);
    for (const unsigned char character : value) {
        switch (character) {
        case '\\': output += "\\\\"; break;
        case '"': output += "\\\""; break;
        case '\n': output += "\\n"; break;
        case '\r': output += "\\r"; break;
        case '\t': output += "\\t"; break;
        default: output += static_cast<char>(character); break;
        }
    }
    return output;
}

int hexValue(char value) {
    if (value >= '0' && value <= '9') return value - '0';
    if (value >= 'a' && value <= 'f') return value - 'a' + 10;
    if (value >= 'A' && value <= 'F') return value - 'A' + 10;
    return -1;
}

void appendUtf8(std::string& output, unsigned int codePoint) {
    if (codePoint <= 0x7f) {
        output += static_cast<char>(codePoint);
    } else if (codePoint <= 0x7ff) {
        output += static_cast<char>(0xc0 | (codePoint >> 6));
        output += static_cast<char>(0x80 | (codePoint & 0x3f));
    } else {
        output += static_cast<char>(0xe0 | (codePoint >> 12));
        output += static_cast<char>(0x80 | ((codePoint >> 6) & 0x3f));
        output += static_cast<char>(0x80 | (codePoint & 0x3f));
    }
}

std::string unescapeJson(const std::string& value) {
    std::string output;
    output.reserve(value.size());
    for (std::size_t index = 0; index < value.size(); ++index) {
        const char character = value[index];
        if (character != '\\') {
            output += character;
            continue;
        }
        if (++index >= value.size()) break;
        switch (value[index]) {
        case 'n': output += '\n'; break;
        case 'r': output += '\r'; break;
        case 't': output += '\t'; break;
        case 'b': output += '\b'; break;
        case 'f': output += '\f'; break;
        case 'u': {
            if (index + 4 >= value.size()) {
                output += 'u';
                break;
            }
            unsigned int codePoint = 0;
            bool valid = true;
            for (std::size_t offset = 1; offset <= 4; ++offset) {
                const int digit = hexValue(value[index + offset]);
                if (digit < 0) {
                    valid = false;
                    break;
                }
                codePoint = (codePoint << 4) | static_cast<unsigned int>(digit);
            }
            if (valid) {
                appendUtf8(output, codePoint);
                index += 4;
            } else {
                output += 'u';
            }
            break;
        }
        default: output += value[index]; break;
        }
    }
    return output;
}

std::string stringValue(const std::string& source, const std::string& key, std::size_t begin = 0) {
    const std::string marker = "\"" + key + "\"";
    const std::size_t found = source.find(marker, begin);
    if (found == std::string::npos) return {};
    std::size_t cursor = source.find(':', found + marker.size());
    if (cursor == std::string::npos) return {};
    cursor = source.find('"', cursor + 1);
    if (cursor == std::string::npos) return {};
    ++cursor;
    std::string value;
    bool escaping = false;
    for (; cursor < source.size(); ++cursor) {
        const char character = source[cursor];
        if (!escaping && character == '"') break;
        if (!escaping && character == '\\') {
            escaping = true;
            value += character;
            continue;
        }
        value += character;
        escaping = false;
    }
    return unescapeJson(value);
}

bool boolValue(const std::string& source, const std::string& key) {
    const std::string marker = "\"" + key + "\"";
    const std::size_t found = source.find(marker);
    if (found == std::string::npos) return false;
    std::size_t cursor = source.find(':', found + marker.size());
    if (cursor == std::string::npos) return false;
    ++cursor;
    while (cursor < source.size() && (source[cursor] == ' ' || source[cursor] == '\t' ||
                                     source[cursor] == '\r' || source[cursor] == '\n')) {
        ++cursor;
    }
    return source.compare(cursor, 4, "true") == 0;
}

std::string protect(const std::string& value) {
    if (value.empty()) return {};
    DATA_BLOB input{static_cast<DWORD>(value.size()), reinterpret_cast<BYTE*>(const_cast<char*>(value.data()))};
    DATA_BLOB encrypted{};
    if (!CryptProtectData(&input, L"OneBot Codex Companion", nullptr, nullptr, nullptr, 0, &encrypted)) return {};
    DWORD length = 0;
    CryptBinaryToStringA(encrypted.pbData, encrypted.cbData, CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, nullptr, &length);
    // CryptBinaryToStringA's size convention differs between Windows SDK versions.
    // Allocate a spare byte and trim only an actual terminator.
    std::string output(static_cast<std::size_t>(length) + 1, '\0');
    const bool encoded = CryptBinaryToStringA(encrypted.pbData, encrypted.cbData, CRYPT_STRING_BASE64 | CRYPT_STRING_NOCRLF, output.data(), &length);
    LocalFree(encrypted.pbData);
    if (!encoded) return {};
    output.resize(std::char_traits<char>::length(output.c_str()));
    return output;
}

std::string unprotect(const std::string& value) {
    if (value.empty()) return {};
    DWORD length = 0;
    if (!CryptStringToBinaryA(value.data(), static_cast<DWORD>(value.size()), CRYPT_STRING_BASE64, nullptr, &length, nullptr, nullptr)) return {};
    std::string binary(length, '\0');
    if (!CryptStringToBinaryA(value.data(), static_cast<DWORD>(value.size()), CRYPT_STRING_BASE64,
                              reinterpret_cast<BYTE*>(binary.data()), &length, nullptr, nullptr)) return {};
    DATA_BLOB input{length, reinterpret_cast<BYTE*>(binary.data())};
    DATA_BLOB plain{};
    if (!CryptUnprotectData(&input, nullptr, nullptr, nullptr, nullptr, 0, &plain)) return {};
    std::string output(reinterpret_cast<char*>(plain.pbData), plain.cbData);
    LocalFree(plain.pbData);
    return output;
}

void parseRoutes(AppSettings& settings, const std::string& source) {
    std::size_t cursor = 0;
    while (true) {
        const std::size_t thread = source.find("\"thread_id\"", cursor);
        if (thread == std::string::npos) return;
        const std::size_t end = source.find('}', thread);
        if (end == std::string::npos) return;
        Route route;
        route.threadId = stringValue(source, "thread_id", thread);
        route.recipient.type = stringValue(source, "target_type", thread);
        route.recipient.id = stringValue(source, "target_id", thread);
        if (!route.threadId.empty() && !route.recipient.id.empty()) {
            if (route.recipient.type != "group") route.recipient.type = "private";
            settings.routes.push_back(std::move(route));
        }
        cursor = end + 1;
    }
}

AppSettings loadLegacy() {
    AppSettings settings;
    std::ifstream input(legacySettingsPath(), std::ios::binary);
    if (!input) return settings;
    std::ostringstream buffer;
    buffer << input.rdbuf();
    const std::string source = buffer.str();
    const std::string baseUrl = stringValue(source, "BaseUrl");
    if (!baseUrl.empty()) settings.baseUrl = baseUrl;
    settings.accessToken = unprotect(stringValue(source, "EncryptedToken"));
    settings.defaultRecipient.type = stringValue(source, "TargetType");
    settings.defaultRecipient.id = stringValue(source, "TargetId");
    if (settings.defaultRecipient.type != "group") settings.defaultRecipient.type = "private";
    settings.monitorEnabled = boolValue(source, "MonitorEnabled");
    return settings;
}

} // namespace

AppSettings load() {
    AppSettings settings;
    std::ifstream input(settingsPath(), std::ios::binary);
    if (!input) return loadLegacy();
    std::ostringstream buffer;
    buffer << input.rdbuf();
    const std::string source = buffer.str();
    const std::string baseUrl = stringValue(source, "base_url");
    if (!baseUrl.empty()) settings.baseUrl = baseUrl;
    const std::string encryptedToken = stringValue(source, "encrypted_token");
    settings.accessToken = unprotect(encryptedToken);
    settings.defaultRecipient.type = stringValue(source, "default_type");
    settings.defaultRecipient.id = stringValue(source, "default_id");
    if (settings.defaultRecipient.type != "group") settings.defaultRecipient.type = "private";
    settings.monitorEnabled = boolValue(source, "monitor_enabled");
    settings.startWithWindows = boolValue(source, "start_with_windows");
    parseRoutes(settings, source);
    if (!encryptedToken.empty() && settings.accessToken.empty()) {
        const AppSettings legacy = loadLegacy();
        if (!legacy.accessToken.empty()) {
            settings.accessToken = legacy.accessToken;
            if (settings.defaultRecipient.id.empty()) settings.defaultRecipient = legacy.defaultRecipient;
            (void)save(settings);
        }
    }
    return settings;
}

bool save(const AppSettings& settings, std::string* error) {
    const std::string encryptedToken = protect(settings.accessToken);
    if (!settings.accessToken.empty() && encryptedToken.empty()) {
        if (error) *error = "无法使用 Windows DPAPI 保护访问令牌。";
        return false;
    }
    try {
        std::filesystem::create_directories(settingsPath().parent_path());
        std::ofstream output(settingsPath(), std::ios::binary | std::ios::trunc);
        if (!output) throw std::runtime_error("无法创建设置文件。");
        output << "{\n"
               << "  \"base_url\": \"" << escapeJson(settings.baseUrl) << "\",\n"
               << "  \"encrypted_token\": \"" << escapeJson(encryptedToken) << "\",\n"
               << "  \"default_type\": \"" << escapeJson(settings.defaultRecipient.type) << "\",\n"
               << "  \"default_id\": \"" << escapeJson(settings.defaultRecipient.id) << "\",\n"
               << "  \"monitor_enabled\": " << (settings.monitorEnabled ? "true" : "false") << ",\n"
               << "  \"start_with_windows\": " << (settings.startWithWindows ? "true" : "false") << ",\n"
               << "  \"routes\": [\n";
        for (std::size_t index = 0; index < settings.routes.size(); ++index) {
            const Route& route = settings.routes[index];
            output << "    {\"thread_id\": \"" << escapeJson(route.threadId)
                   << "\", \"target_type\": \"" << escapeJson(route.recipient.type)
                   << "\", \"target_id\": \"" << escapeJson(route.recipient.id) << "\"}";
            if (index + 1 < settings.routes.size()) output << ',';
            output << '\n';
        }
        output << "  ]\n}\n";
        return true;
    } catch (const std::exception& exception) {
        if (error) *error = exception.what();
        return false;
    }
}

void upsertRoute(AppSettings& settings, Route route) {
    const auto existing = std::find_if(settings.routes.begin(), settings.routes.end(), [&](const Route& item) {
        return item.threadId == route.threadId;
    });
    if (existing == settings.routes.end()) settings.routes.push_back(std::move(route));
    else *existing = std::move(route);
}

const Recipient& recipientFor(const AppSettings& settings, const std::string& threadId) {
    const auto route = std::find_if(settings.routes.begin(), settings.routes.end(), [&](const Route& item) {
        return item.threadId == threadId;
    });
    return route == settings.routes.end() ? settings.defaultRecipient : route->recipient;
}

} // namespace settings
