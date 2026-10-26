using System.ComponentModel;
using Mcp2Oauth.McpServer.Audit;
using Mcp2Oauth.McpServer.Data;
using Mcp2Oauth.McpServer.Identity;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace Mcp2Oauth.McpServer.Tools;

/// <summary>
/// Tools that act on the OBO user's data. They depend only on
/// <see cref="IUserContext"/>, never on which agent made the call. Every
/// submission and approval, including refused ones, goes to the audit trail
/// through <see cref="ExpenseAudit"/>, which adds the agent identity itself.
/// </summary>
[McpServerToolType]
public sealed class ExpenseTools(IUserContext userContext, ExpenseStore store, ExpenseAudit audit)
{
    [McpServerTool(Name = "list_my_expenses", ReadOnly = true)]
    [Authorize(Policy = Policies.OnBehalfOfUser)]
    [Description("Lists the expense reports belonging to the signed-in user.")]
    public IReadOnlyList<Expense> ListMyExpenses() =>
        store.ForOwner(userContext.User.ObjectId);

    [McpServerTool(Name = "submit_expense")]
    [Authorize(Policy = Policies.OnBehalfOfUser)]
    [Description("Submits a new expense report for the signed-in user.")]
    public Expense SubmitExpense(
        [Description("What the expense was for.")] string description,
        [Description("Amount in USD.")] decimal amount)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw audit.Denied(AuditAction.SubmitExpense, "A description is required.", amount: amount);
        if (amount is <= 0 or > 10_000)
            throw audit.Denied(AuditAction.SubmitExpense, "Amount must be between 0 and 10,000 USD.", amount: amount);

        var user = userContext.User;
        var expense = store.Add(user.ObjectId, user.Name ?? user.UserName ?? user.ObjectId, description, amount);
        return audit.Succeeded(AuditAction.SubmitExpense, expense);
    }

    [McpServerTool(Name = "list_pending_approvals", ReadOnly = true)]
    [Authorize(Policy = Policies.Approver)]
    [Description("Lists other people's expense reports waiting for approval. Requires the Expense.Approver role.")]
    public IReadOnlyList<Expense> ListPendingApprovals() =>
        store.Pending().Where(e => e.OwnerObjectId != userContext.User.ObjectId).ToList();

    [McpServerTool(Name = "approve_expense")]
    [Authorize(Policy = Policies.Approver)]
    [Description("Approves a pending expense report. Requires the Expense.Approver role; you can't approve your own.")]
    public Expense ApproveExpense([Description("The expense id.")] int id)
    {
        var user = userContext.User;
        var expense = store.Find(id)
            ?? throw audit.Denied(AuditAction.ApproveExpense, $"Expense {id} was not found.", expenseId: id);
        if (expense.OwnerObjectId == user.ObjectId)
            throw audit.Denied(AuditAction.ApproveExpense, "You can't approve your own expense.", expense);
        if (expense.Status != ExpenseStatus.Pending)
            throw audit.Denied(AuditAction.ApproveExpense, $"Expense {id} is already {expense.Status}.", expense);

        var approved = store.Approve(id, user.Name ?? user.UserName ?? user.ObjectId);
        return audit.Succeeded(AuditAction.ApproveExpense, approved);
    }
}
