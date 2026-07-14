using System.Text.Json;

namespace OneBotCodexCompanion;

public static class CdpDiscovery
{
    private static readonly int[] CandidatePorts = [9229, 9222, 9230];

    public static async Task<string?> FindCodexEndpointAsync(CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        foreach (var port in CandidatePorts)
        {
            var endpoint = $"http://127.0.0.1:{port}";
            try
            {
                var payload = await httpClient.GetStringAsync(endpoint + "/json", cancellationToken);
                using var document = JsonDocument.Parse(payload);
                if (document.RootElement.ValueKind != JsonValueKind.Array) continue;
                var hasCodexPage = document.RootElement.EnumerateArray().Any(target =>
                {
                    var type = target.TryGetProperty("type", out var typeValue) ? typeValue.GetString() : "";
                    var url = target.TryGetProperty("url", out var urlValue) ? urlValue.GetString() : "";
                    var title = target.TryGetProperty("title", out var titleValue) ? titleValue.GetString() : "";
                    return string.Equals(type, "page", StringComparison.OrdinalIgnoreCase)
                        && (url?.Contains("chatgpt", StringComparison.OrdinalIgnoreCase) == true
                            || url?.Contains("codex", StringComparison.OrdinalIgnoreCase) == true
                            || title?.Contains("codex", StringComparison.OrdinalIgnoreCase) == true);
                });
                if (hasCodexPage) return endpoint;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            catch (JsonException) { }
        }
        return null;
    }
}
