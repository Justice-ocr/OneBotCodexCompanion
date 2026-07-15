#pragma once

#include <string>

namespace onebot {

struct RequestResult {
    bool ok = false;
    std::string detail;
};

RequestResult testAndSend(const std::string& baseUrl,
                          const std::string& accessToken,
                          const std::string& recipientType,
                          const std::string& recipientId,
                          const std::string& message);

RequestResult sendNotification(const std::string& baseUrl,
                               const std::string& accessToken,
                               const std::string& recipientType,
                               const std::string& recipientId,
                               const std::string& message);

} // namespace onebot
