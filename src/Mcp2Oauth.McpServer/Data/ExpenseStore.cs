using System.Text.Json.Serialization;

namespace Mcp2Oauth.McpServer.Data;

[JsonConverter(typeof(JsonStringEnumConverter<ExpenseStatus>))]
public enum ExpenseStatus { Pending, Approved }

public sealed record Expense(
    int Id,
    string OwnerObjectId,
    string OwnerName,
    string Description,
    decimal Amount,
    ExpenseStatus Status,
    string? ApprovedBy);

/// <summary>
/// In-memory demo data, keyed by the Entra object id (<c>oid</c>) of the owning user.
/// </summary>
public sealed class ExpenseStore
{
    private readonly Lock _lock = new();
    private readonly List<Expense> _expenses =
    [
        // Seeded reports from other (fictional) users, so approvers have work to do.
        new(1, "00000000-0000-0000-0000-00000000a11c", "Alice Example", "Conference registration", 899m, ExpenseStatus.Pending, null),
        new(2, "00000000-0000-0000-0000-000000000b0b", "Bob Example", "Team lunch", 142.50m, ExpenseStatus.Pending, null),
        new(3, "00000000-0000-0000-0000-000000000b0b", "Bob Example", "Taxi to airport", 61m, ExpenseStatus.Approved, "Carol Example"),
    ];
    private int _nextId = 4;

    public int Count { get { lock (_lock) return _expenses.Count; } }

    public IReadOnlyList<Expense> ForOwner(string ownerObjectId)
    {
        lock (_lock) return _expenses.Where(e => e.OwnerObjectId == ownerObjectId).ToList();
    }

    public IReadOnlyList<Expense> Pending()
    {
        lock (_lock) return _expenses.Where(e => e.Status == ExpenseStatus.Pending).ToList();
    }

    public Expense? Find(int id)
    {
        lock (_lock) return _expenses.FirstOrDefault(e => e.Id == id);
    }

    public Expense Add(string ownerObjectId, string ownerName, string description, decimal amount)
    {
        lock (_lock)
        {
            var expense = new Expense(_nextId++, ownerObjectId, ownerName, description, amount, ExpenseStatus.Pending, null);
            _expenses.Add(expense);
            return expense;
        }
    }

    public Expense Approve(int id, string approverName)
    {
        lock (_lock)
        {
            var index = _expenses.FindIndex(e => e.Id == id);
            var approved = _expenses[index] with { Status = ExpenseStatus.Approved, ApprovedBy = approverName };
            _expenses[index] = approved;
            return approved;
        }
    }
}
