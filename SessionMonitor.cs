using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OneBotCodexCompanion;

/// <summary>
/// Watches Codex's local session event log. This works for desktop API-key
/// sessions because completion is emitted by the Codex runtime itself.
/// </summary>
public sealed class SessionMonitor : IAsyncDisposable
{
    private static readonly Regex ThreadIdPattern = new(@"([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})\.jsonl$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly Dictionary<string, long> _positions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _completedTasks = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cancellation;
    private Task? _loop;

    public event Action<string>? StatusChanged;
    public event Action<string>? GenerationCompleted;

    public bool IsRunning => _loop is { IsCompleted: false };

    public Task StartAsync() => StartAsync(GetDefaultSessionsDirectory());

    public async Task StartAsync(string sessionsDirectory)
    {
        await StopAsync();
        if (!Directory.Exists(sessionsDirectory))
        {
            throw new DirectoryNotFoundException($"Codex session directory does not exist: {sessionsDirectory}");
        }

        // Starting the companion must not replay historical task completions.
        foreach (var file in Directory.EnumerateFiles(sessionsDirectory, "*.jsonl", SearchOption.AllDirectories))
        {
            _positions[file] = new FileInfo(file).Length;
        }

        _cancellation = new CancellationTokenSource();
        _loop = MonitorAsync(sessionsDirectory, _cancellation.Token);
        StatusChanged?.Invoke("Local Codex session monitor started.");
    }

    public async Task StopAsync()
    {
        if (_cancellation is null) return;
        _cancellation.Cancel();
        try { if (_loop is not null) await _loop; } catch (OperationCanceledException) { }
        _cancellation.Dispose();
        _cancellation = null;
        _loop = null;
        _positions.Clear();
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    public static string GetDefaultSessionsDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");

    private async Task MonitorAsync(string sessionsDirectory, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(sessionsDirectory, "*.jsonl", SearchOption.AllDirectories))
                {
                    await ReadNewRecordsAsync(path, cancellationToken);
                }
            }
            catch (DirectoryNotFoundException)
            {
                throw;
            }
            catch (Exception exception)
            {
                StatusChanged?.Invoke($"Failed to read Codex session events; retrying: {exception.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private async Task ReadNewRecordsAsync(string path, CancellationToken cancellationToken)
    {
        var length = new FileInfo(path).Length;
        var position = _positions.GetValueOrDefault(path);
        if (length < position) position = 0;
        if (length == position)
        {
            _positions[path] = position;
            return;
        }

        var bytes = new byte[length - position];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(position, SeekOrigin.Begin);
        var read = 0;
        while (read < bytes.Length)
        {
            var count = await stream.ReadAsync(bytes.AsMemory(read), cancellationToken);
            if (count == 0) break;
            read += count;
        }

        var lineStart = 0;
        for (var index = 0; index < read; index++)
        {
            if (bytes[index] != (byte)'\n') continue;
            var lineEnd = index > lineStart && bytes[index - 1] == (byte)'\r' ? index - 1 : index;
            HandleRecord(path, Encoding.UTF8.GetString(bytes, lineStart, lineEnd - lineStart));
            position += index - lineStart + 1;
            lineStart = index + 1;
        }
        _positions[path] = position;
    }

    private void HandleRecord(string path, string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "event_msg") return;
            if (!root.TryGetProperty("payload", out var payload)) return;
            if (!payload.TryGetProperty("type", out var payloadType) || payloadType.GetString() != "task_complete") return;
            if (!payload.TryGetProperty("turn_id", out var turnIdValue)) return;

            var threadId = ExtractThreadId(path);
            if (string.IsNullOrWhiteSpace(threadId)) return;
            var key = $"{threadId}:{turnIdValue.GetString()}";
            if (!_completedTasks.Add(key)) return;

            StatusChanged?.Invoke($"Codex task completed: {threadId}");
            GenerationCompleted?.Invoke(threadId);
        }
        catch (JsonException)
        {
            // A partly flushed final line is retried on the following poll.
        }
    }

    private static string ExtractThreadId(string path)
    {
        var match = ThreadIdPattern.Match(path);
        return match.Success ? match.Groups[1].Value : "";
    }
}
