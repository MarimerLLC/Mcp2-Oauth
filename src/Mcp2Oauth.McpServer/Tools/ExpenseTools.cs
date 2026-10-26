using System.ComponentModel;
using Mcp2Oauth.McpServer.Audit;
using Mcp2Oauth.McpServer.AutoApproval;
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
/// New expenses go through the org's <see cref="ExpensePolicy"/>, which may
/// approve them without a human.
/// </summary>
[McpServerToolType]
public sealed class ExpenseTools(IUserContext userContext, ExpenseStore store, ExpenseAudit audit, ExpensePolicy policy)
{
    [McpServerTool(Name = "list_my_expenses", ReadOnly = true)]
    [Authorize(Policy = Policies.OnBehalfOfUser)]
    [Description("Lists the expense reports belonging to the signed-in user.")]
    public IReadOnlyList<Expense> ListMyExpenses() =>
        store.ForOwner(userContext.User.ObjectId);

    [McpServerTool(Name = "submit_expense")]
    [Authorize(Policy = Policies.OnBehalfOfUser)]
    [Description("Submits a new expense report for the signed-in user. Expenses within the org's expense policy are approved automatically; the result says whether it was, and if not, why it waits for a human approver.")]
    public async Task<ExpenseSubmission> SubmitExpense(
        [Description("What the expense was for.")] string description,
        [Description("Amount in USD.")] decimal amount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw audit.Denied(AuditAction.SubmitExpense, "A description is required.", amount: amount);
        if (amount is <= 0 or > 10_000)
            throw audit.Denied(AuditAction.SubmitExpense, "Amount must be between 0 and 10,000 USD.", amount: amount);

        var user = userContext.User;
        var expense = audit.Succeeded(AuditAction.SubmitExpense,
            store.Add(user.ObjectId, user.Name ?? user.UserName ?? user.ObjectId, description, amount));

        // The server, not the user or the agent, decides on auto-approval. Anything
        // the policy doesn't clear, including a model failure, waits for a human.
        if (!policy.IsEnabled)
            return new(expense, "Waiting for a human approver.");

        var decision = await policy.EvaluateAsync(description, amount, cancellationToken);
        if (!decision.AutoApprove)
        {
            audit.PolicyDecided(expense, decision, policy.Actor);
            return new(expense, $"Waiting for a human approver. The expense policy didn't auto-approve it: {decision.Reason}");
        }

        var approved = audit.PolicyDecided(store.Approve(expense.Id, ExpensePolicy.ApproverName), decision, policy.Actor);
        return new(approved, $"Approved automatically under the expense policy: {decision.Reason}");
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

/// <summary>A new expense, and what happened to it: auto-approved, or waiting for a human.</summary>
public sealed record ExpenseSubmission(Expense Expense, string Approval);
