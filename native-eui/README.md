# OneBot Codex Companion Native

基于 EUI-NEO 的原生 Windows 版本。它不依赖 Codex++ 注入脚本，而是监视 Codex 本地会话日志中的 `task_complete` 事件，因此可用于 API Key 登录的 Codex Desktop。

## 运行

构建产物为 `build-native/Release/onebot_codex_eui.exe`。

1. 在“连接设置”填写 OneBot HTTP 地址、访问令牌、默认收件人类型（`private` 或 `group`）及收件人 ID。
2. 点击“测试并发送”。此操作会先调用 `get_version_info`，然后发送一条真实测试消息。
3. 点击“保存设置”。访问令牌通过 Windows 当前用户 DPAPI 加密后保存到 `%APPDATA%\\OneBot Codex Companion\\native-eui-settings.json`。
4. 在“自动通知”中启动监视。启动时仅从已有会话文件末尾开始读取，不会补发历史任务；后续每个 `task_complete` 只会发送一次。
5. 勾选“随 Windows 登录启动”可写入当前用户的开机启动项，不需要管理员权限。登录启动会携带 `--background` 参数并直接进入托盘，不弹出主窗口。

关闭主窗口时程序会隐藏到系统托盘并继续监视。托盘菜单中的 `Show` 用于恢复窗口，`Exit` 才会彻底退出程序。

“对话路由”允许为单个 Codex 对话 ID 覆盖默认收件人。任务名称会优先从 `%USERPROFILE%\\.codex\\session_index.jsonl` 解析，缺少名称时才使用对话 ID。

## 构建

需要 Visual Studio Build Tools（C++ 桌面开发组件）和 CMake。使用本地 EUI-NEO 源码：

```powershell
cmake -S native-eui -B native-eui/build-native -G "Visual Studio 17 2022" -A x64 `
  -DEUI_NEO_SOURCE_DIR=E:\Codex\_references\EUI-NEO `
  -DEUI_BUILD_APPS=OFF -DEUI_BUILD_USER_APPS=OFF
cmake --build native-eui/build-native --config Release --target onebot_codex_eui
```

未提供 `EUI_NEO_SOURCE_DIR` 时，CMake 会拉取固定提交的 EUI-NEO。Release 使用静态 MSVC 运行时，运行时只依赖 Windows 系统组件。
