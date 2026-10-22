using System.Text.Json;
using Kontext.Contracts;
using Kontext.Domain;
using Kontext.Kernel;

namespace Kontext.Sessions;

internal sealed class JsonSessionStore(SessionsOptions options) : ISessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Lock _gate = new();
    private readonly string _filePath = Path.Combine(options.StateDirectory, "sessions.json");

    public StoredSession? Find(IssueKey issue)
    {
        lock (_gate)
        {
            return Load().TryGetValue(issue.ToString(), out var stored)
                ? new StoredSession(issue, new TerminalSessionId(stored.SessionId), stored.WorktreePath, stored.BranchName)
                : null;
        }
    }

    public void Save(StoredSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            var all = Load();
            all[session.Issue.ToString()] = new StoredEntry(session.SessionId.Value, session.WorktreePath, session.BranchName);

            Directory.CreateDirectory(options.StateDirectory);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(all, JsonOptions));
        }
    }

    public void Remove(IssueKey issue)
    {
        lock (_gate)
        {
            var all = Load();
            if (all.Remove(issue.ToString()))
            {
                File.WriteAllText(_filePath, JsonSerializer.Serialize(all, JsonOptions));
            }
        }
    }

    private Dictionary<string, StoredEntry> Load()
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, StoredEntry>>(File.ReadAllText(_filePath)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record StoredEntry(Guid SessionId, string WorktreePath, string BranchName);
}
