#pragma once

#include <string>

namespace autostart {

bool isEnabled();
bool setEnabled(bool enabled, std::string* error = nullptr);

} // namespace autostart
