using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OneBotCodexCompanion;

public sealed class CdpMonitor : IAsyncDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private CancellationTokenSource? _cancellation;
    private Task? _loop;
    private long _messageId;

    public event Action<string>? StatusChanged;
    public event Action<string>? GenerationCompleted;

    public bool IsRunning => _loop is { IsCompleted: false };

    public async Task StartAsync(string endpoint)
    {
        await StopAsync();
        _cancellation = new CancellationTokenSource();
        _loop = MonitorWithRetryAsync(endpoint, _cancellation.Token);
        await Task.Delay(250);
        if (_loop.IsFaulted) await _loop;
    }

    public async Task StopAsync()
    {
        if (_cancellation is null) return;
        _cancellation.Cancel();
        try { if (_loop is not null) await _loop; } catch (OperationCanceledException) { }
        _cancellation.Dispose();
        _cancellation = null;
        _loop = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _httpClient.Dispose();
    }

    private async Task MonitorWithRetryAsync(string endpoint, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await MonitorConnectedAsync(endpoint, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke($"CDP monitor disconnected; retrying in 3 seconds: {exception.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }

    private async Task MonitorConnectedAsync(string endpoint, CancellationToken cancellationToken)
    {
        var baseUri = endpoint.TrimEnd('/');
        var targetsJson = await _httpClient.GetStringAsync(baseUri + "/json/list", cancellationToken);
        using var targetDocument = JsonDocument.Parse(targetsJson);
        var pages = targetDocument.RootElement.EnumerateArray()
            .Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "page" && item.TryGetProperty("webSocketDebuggerUrl", out _))
            .ToList();
        var target = pages.FirstOrDefault(IsCodexTarget);
        if (target.ValueKind == JsonValueKind.Undefined && pages.Count > 0) target = pages[0];
        if (target.ValueKind == JsonValueKind.Undefined) throw new InvalidOperationException("No inspectable Codex page was found.");
        var socketUrl = target.GetProperty("webSocketDebuggerUrl").GetString() ?? throw new InvalidOperationException("The selected page has no CDP WebSocket URL.");

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(socketUrl), cancellationToken);
        StatusChanged?.Invoke("CDP monitor connected.");

        var trackedRequests = new Dictionary<string, string>(StringComparer.Ordinal);
        var currentThreadId = "";
        var wasGenerating = false;
        var lastNotification = DateTimeOffset.MinValue;

        void NotifyCompletion(string threadId, string source)
        {
            if (DateTimeOffset.UtcNow - lastNotification < TimeSpan.FromSeconds(5)) return;
            lastNotification = DateTimeOffset.UtcNow;
            StatusChanged?.Invoke($"Generation completed via {source}.");
            GenerationCompleted?.Invoke(threadId);
        }

        void HandleEvent(JsonElement message)
        {
            if (!message.TryGetProperty("method", out var methodProperty)) return;
            var method = methodProperty.GetString();
            if (!message.TryGetProperty("params", out var parameters)) return;

            if (method == "Network.requestWillBeSent")
            {
                var requestId = parameters.TryGetProperty("requestId", out var idProperty) ? idProperty.GetString() : null;
                var url = parameters.TryGetProperty("request", out var request)
                    && request.TryGetProperty("url", out var urlProperty)
                    ? urlProperty.GetString()
                    : null;
                if (!string.IsNullOrWhiteSpace(requestId) && IsGenerationRequest(url))
                {
                    trackedRequests[requestId] = currentThreadId;
                    StatusChanged?.Invoke("Detected an API generation request.");
                }
                return;
            }

            if (method is "Network.loadingFinished" or "Network.loadingFailed")
            {
                var requestId = parameters.TryGetProperty("requestId", out var idProperty) ? idProperty.GetString() : null;
                if (!string.IsNullOrWhiteSpace(requestId) && trackedRequests.Remove(requestId, out var threadId))
                {
                    NotifyCompletion(threadId, "API stream");
                }
            }
        }

        await SendCommandAsync(socket, "Network.enable", new { }, HandleEvent, cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            var thread = await EvaluateAsync(socket, "(()=>document.querySelector('[data-app-action-sidebar-thread-active=\\\"true\\\"][data-app-action-sidebar-thread-id]')?.getAttribute('data-app-action-sidebar-thread-id')||location.href)()", HandleEvent, cancellationToken);
            currentThreadId = ExtractThreadId(thread.GetString() ?? "");

            var generation = await EvaluateAsync(socket, "(()=>[...document.querySelectorAll('button,[role=button]')].some(e=>{const label=`${e.getAttribute('aria-label')||''} ${e.textContent||''}`.trim();return !e.hasAttribute('disabled')&&(/stop generating/i.test(label)||/^stop$/i.test(label)||/^\\u505c\\u6b62\\u751f\\u6210?$/.test(label));}))()", HandleEvent, cancellationToken);
            var isGenerating = generation.ValueKind == JsonValueKind.True;
            if (wasGenerating && !isGenerating)
            {
                NotifyCompletion(currentThreadId, "UI state");
            }
            wasGenerating = isGenerating;
            await Task.Delay(1000, cancellationToken);
        }
    }

    private async Task<JsonElement> EvaluateAsync(ClientWebSocket socket, string expression, Action<JsonElement> handleEvent, CancellationToken cancellationToken)
    {
        var result = await SendCommandAsync(socket, "Runtime.evaluate", new { expression, returnByValue = true }, handleEvent, cancellationToken);
        return result.GetProperty("result").GetProperty("value").Clone();
    }

    private async Task<JsonElement> SendCommandAsync(ClientWebSocket socket, string method, object parameters, Action<JsonElement> handleEvent, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _messageId);
        var request = JsonSerializer.Serialize(new { id, method, @params = parameters });
        await socket.SendAsync(Encoding.UTF8.GetBytes(request), WebSocketMessageType.Text, true, cancellationToken);

        var buffer = new ArraySegment<byte>(new byte[32768]);
        while (true)
        {
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);
                stream.Write(buffer.Array!, buffer.Offset, result.Count);
            } while (!result.EndOfMessage);

            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
            var message = document.RootElement;
            if (message.TryGetProperty("method", out _))
            {
                handleEvent(message);
                continue;
            }
            if (!message.TryGetProperty("id", out var responseId) || responseId.GetInt64() != id) continue;
            if (message.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.ToString());
            return message.GetProperty("result").Clone();
        }
    }

    private static bool IsCodexTarget(JsonElement target)
    {
        var url = target.TryGetProperty("url", out var urlValue) ? urlValue.GetString() : "";
        var title = target.TryGetProperty("title", out var titleValue) ? titleValue.GetString() : "";
        return url?.Contains("chatgpt", StringComparison.OrdinalIgnoreCase) == true
            || url?.Contains("codex", StringComparison.OrdinalIgnoreCase) == true
            || title?.Contains("codex", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsGenerationRequest(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var value = url.ToLowerInvariant();
        return value.Contains("/responses")
            || value.Contains("/chat/completions")
            || value.Contains("/completions")
            || value.Contains(":generatecontent")
            || value.Contains("/messages");
    }

    private static string ExtractThreadId(string value)
    {
        if (value.StartsWith("local:", StringComparison.OrdinalIgnoreCase)) return value;
        var match = Regex.Match(value, @"(?:thread|conversation)s?/([^/?#]+)", RegexOptions.IgnoreCase);
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : "";
    }
}
