using System.Text.Json.Serialization;

namespace Mcp2Oauth.McpServer.Audit;

[JsonConverter(typeof(JsonStringEnumConverter<AuditAction>))]
public enum AuditAction { SubmitExpense, ApproveExpense, AutoApproveExpense }

/// <summary>
/// <see cref="Referred"/> is the expense policy declining to auto-approve: the
/// expense isn't refused, it waits for a human approver.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuditOutcome>))]
public enum AuditOutcome { Succeeded, Denied, Referred }

/// <summary>Who acted: an Entra object id (user) or app id (agent or service), and a display name.</summary>
public sealed record AuditActor(string Id, string? Name);

/// <summary>
/// One financial action on an expense, with both identities behind it: the user
/// it was done for, and the agent that made the call. When the server decided by
/// itself (the expense policy), <see cref="Service"/> names it as well.
/// </summary>
public sealed record AuditEntry(
    long Sequence,
    DateTimeOffset Timestamp,
    AuditAction Action,
    AuditOutcome Outcome,
    int? ExpenseId,
    decimal? Amount,
    string? ExpenseOwner,
    AuditActor User,
    AuditActor Agent,
    AuditActor? Service,
    string? Reason,
    string? TraceId);

/// <summary>
/// Append-only, in-memory audit trail of expense submissions and approvals.
/// Entries are never changed or removed. Each one is also written to the
/// structured log, so it shows up in the Aspire dashboard next to its trace.
/// </summary>
public sealed class AuditLog
{
    private readonly Lock _lock = new();
    private readonly List<AuditEntry> _entries;
    private readonly TimeProvider _time;
    private readonly ILogger<AuditLog> _logger;

    public AuditLog(TimeProvider time, ILogger<AuditLog> logger)
    {
        _time = time;
        _logger = logger;

        // History for ExpenseStore's seeded reports, so the trail matches the data.
        var seed = new AuditActor("seed", "Seed data");
        var alice = new AuditActor("00000000-0000-0000-0000-00000000a11c", "Alice Example");
        var bob = new AuditActor("00000000-0000-0000-0000-000000000b0b", "Bob Example");
        var carol = new AuditActor("00000000-0000-0000-0000-000000000ca1", "Carol Example");
        var start = time.GetUtcNow().AddDays(-3);
        _entries =
        [
            new(1, start, AuditAction.SubmitExpense, AuditOutcome.Succeeded, 1, 899m, alice.Name, alice, seed, null, null, null),
            new(2, start.AddHours(2), AuditAction.SubmitExpense, AuditOutcome.Succeeded, 2, 142.50m, bob.Name, bob, seed, null, null, null),
            new(3, start.AddHours(3), AuditAction.SubmitExpense, AuditOutcome.Succeeded, 3, 61m, bob.Name, bob, seed, null, null, null),
            new(4, start.AddDays(1), AuditAction.ApproveExpense, AuditOutcome.Succeeded, 3, 61m, bob.Name, carol, seed, null, null, null),
        ];
    }

    public int Count { get { lock (_lock) return _entries.Count; } }

    /// <summary>All entries in the order they were recorded, optionally for one expense.</summary>
    public IReadOnlyList<AuditEntry> Entries(int? expenseId = null)
    {
        lock (_lock) return _entries.Where(e => expenseId is null || e.ExpenseId == expenseId).ToList();
    }

    /// <summary>Appends an entry. The log assigns its sequence number and timestamp.</summary>
    public AuditEntry Append(AuditEntry entry)
    {
        lock (_lock)
        {
            entry = entry with { Sequence = _entries.Count + 1, Timestamp = _time.GetUtcNow() };
            _entries.Add(entry);
        }

        var level = entry.Outcome == AuditOutcome.Denied ? LogLevel.Warning : LogLevel.Information;
        if (entry.Service is { } service)
            _logger.Log(level,
                "Audit #{AuditSequence}: {AuditAction} {AuditOutcome}, expense {ExpenseId} ({Amount} USD) by {ServiceName} ({ServiceAppId}), submitted by {UserName} ({UserObjectId}) via agent {AgentName} ({AgentAppId}). {Reason}",
                entry.Sequence, entry.Action, entry.Outcome, entry.ExpenseId, entry.Amount, service.Name, service.Id,
                entry.User.Name, entry.User.Id, entry.Agent.Name, entry.Agent.Id, entry.Reason);
        else
            _logger.Log(level,
                "Audit #{AuditSequence}: {AuditAction} {AuditOutcome}, expense {ExpenseId} ({Amount} USD) by {UserName} ({UserObjectId}) via agent {AgentName} ({AgentAppId}). {Reason}",
                entry.Sequence, entry.Action, entry.Outcome, entry.ExpenseId, entry.Amount,
                entry.User.Name, entry.User.Id, entry.Agent.Name, entry.Agent.Id, entry.Reason);
        return entry;
    }
}
