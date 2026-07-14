using System.Text;
using System.Text.Json;

namespace OneBotCodexCompanion;

public sealed class OneBotClient : IDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<string> TestConnectionAsync(string baseUrl, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(baseUrl, "get_version_info"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"OneBot 返回 HTTP {(int)response.StatusCode}：{Shorten(text)}");
        using var document = JsonDocument.Parse(text);
        EnsureSuccess(document.RootElement);
        var data = document.RootElement.TryGetProperty("data", out var value) ? value : default;
        var appName = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("app_name", out var name) ? name.GetString() : "OneBot";
        var version = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("app_version", out var appVersion) ? appVersion.GetString() : "";
        return string.IsNullOrWhiteSpace(version) ? appName ?? "OneBot 已连接" : $"{appName} {version}";
    }

    public async Task<string> TestAndSendAsync(AppSettings settings, string token, Recipient recipient, CancellationToken cancellationToken)
    {
        var server = await TestConnectionAsync(settings.BaseUrl, token, cancellationToken);
        await SendAsync(settings, token, recipient, "OneBot Codex 通知助手测试消息。", cancellationToken);
        return server;
    }

    public async Task SendAsync(AppSettings settings, string token, Recipient recipient, string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("请填写访问令牌。");
        if (!new[] { "group", "private" }.Contains(recipient.TargetType)) throw new InvalidOperationException("收件人类型必须为群聊或私聊。");
        if (string.IsNullOrWhiteSpace(recipient.TargetId)) throw new InvalidOperationException("请填写收件人 ID。");
        if (string.IsNullOrWhiteSpace(message)) throw new InvalidOperationException("请输入消息内容。");

        var recipientField = recipient.TargetType == "group" ? "group_id" : "user_id";
        var payload = new Dictionary<string, object?>
        {
            [recipientField] = recipient.TargetId,
            ["message"] = settings.MessageFormat == "string"
                ? message
                : new[] { new { type = "text", data = new { text = message } } }
        };
        var action = recipient.TargetType == "group" ? "send_group_msg" : "send_private_msg";
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(settings.BaseUrl, action));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"OneBot 返回 HTTP {(int)response.StatusCode}：{Shorten(text)}");
        using var document = JsonDocument.Parse(text);
        EnsureSuccess(document.RootElement);
    }

    private static Uri BuildUri(string baseUrl, string action)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/" + action, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("OneBot 地址必须以 http:// 或 https:// 开头。");
        }
        return uri;
    }

    private static void EnsureSuccess(JsonElement root)
    {
        if (root.TryGetProperty("status", out var status) && !string.Equals(status.GetString(), "ok", StringComparison.OrdinalIgnoreCase))
        {
            var message = root.TryGetProperty("message", out var detail) ? detail.GetString() : "未知 OneBot 错误。";
            throw new InvalidOperationException(message ?? "OneBot 拒绝了该请求。");
        }
    }

    private static string Shorten(string value) => value.Length > 300 ? value[..300] + "..." : value;

    public void Dispose() => _httpClient.Dispose();
}
