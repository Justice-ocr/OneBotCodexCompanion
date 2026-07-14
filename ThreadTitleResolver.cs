using System.Text.Json;

namespace OneBotCodexCompanion;

public static class ThreadTitleResolver
{
    public static string? GetTitle(string threadId)
    {
        if (string.IsNullOrWhiteSpace(threadId)) return null;

        var indexPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "session_index.jsonl");
        if (!File.Exists(indexPath)) return null;

        string? title = null;
        try
        {
            using var stream = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                using var document = JsonDocument.Parse(line);
                var item = document.RootElement;
                if (!item.TryGetProperty("id", out var id) || !string.Equals(id.GetString(), threadId, StringComparison.OrdinalIgnoreCase)) continue;
                if (item.TryGetProperty("thread_name", out var name) && !string.IsNullOrWhiteSpace(name.GetString()))
                {
                    title = name.GetString()?.Trim();
                }
            }
        }
        catch (IOException)
        {
            // Codex may update the index while the completion event is handled.
        }
        catch (JsonException)
        {
            // A partial final index line is ignored; the ID remains a safe fallback.
        }

        return title;
    }
}
