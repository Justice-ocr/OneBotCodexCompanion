# OneBot Codex Companion

Independent Windows desktop companion for OneBot V11 notifications. It owns the OneBot HTTP request, so it is not affected by the browser CORS, CSP, or mixed-content restrictions that block Codex++ user scripts.

## Run

Launch `publish/OneBotCodexCompanion.exe`. The current release is framework-dependent and requires the .NET 8 Windows Desktop Runtime, which is already available on this computer.

## Configure

1. Enter the OneBot HTTP URL, access token, recipient type, and recipient ID.
2. Click **Test and send**. This validates the token and sends a real test message to the configured default recipient.
3. Click **Save settings**. The token is protected using Windows DPAPI and saved under `%APPDATA%/OneBot Codex Companion/settings.json`.
4. Use **Manual notification** to send a test message.

## Thread Routes

Add a Codex thread ID and an alternate group or private recipient. When the CDP monitor reports completion for that thread, the route overrides the default recipient.

## Automatic Notifications

Enable **任务完成后自动发送通知** in the application. The companion monitors the local Codex session logs under `%USERPROFILE%\.codex\sessions` and sends a OneBot message when Codex writes a `task_complete` event.

This works for API-key login, account login, Codex Desktop, and Codex++ because it does not depend on browser CDP, request URLs, or page controls. Existing thread routes still select a different OneBot recipient for each Codex thread.
