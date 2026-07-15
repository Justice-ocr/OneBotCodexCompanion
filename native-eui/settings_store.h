#pragma once

#include <string>
#include <vector>

namespace settings {

struct Recipient {
    std::string type = "private";
    std::string id;
};

struct Route {
    std::string threadId;
    Recipient recipient;
};

struct AppSettings {
    std::string baseUrl = "http://59.110.13.83:3000";
    std::string accessToken;
    Recipient defaultRecipient;
    bool monitorEnabled = false;
    bool startWithWindows = false;
    std::vector<Route> routes;
};

AppSettings load();
bool save(const AppSettings& settings, std::string* error = nullptr);
void upsertRoute(AppSettings& settings, Route route);
const Recipient& recipientFor(const AppSettings& settings, const std::string& threadId);

} // namespace settings
