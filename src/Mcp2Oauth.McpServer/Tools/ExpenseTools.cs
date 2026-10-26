using System.ComponentModel;
using Mcp2Oauth.McpServer.Data;
using Mcp2Oauth.McpServer.Identity;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Mcp2Oauth.McpServer.Tools;

/// <summary>
/// Tools that act on the OBO user's data. They depend only on
/// <see cref="IUserContext"/>, never on which agent made the call.
/// </summary>
[McpServerToolType]
public sealed class ExpenseTools(IUserContext userContext, ExpenseStore store)
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
            throw new McpException("A description is required.");
        if (amount is <= 0 or > 10_000)
            throw new McpException("Amount must be between 0 and 10,000 USD.");

        var user = userContext.User;
        return store.Add(user.ObjectId, user.Name ?? user.UserName ?? user.ObjectId, description, amount);
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
        var expense = store.Find(id) ?? throw new McpException($"Expense {id} was not found.");
        if (expense.OwnerObjectId == user.ObjectId)
            throw new McpException("You can't approve your own expense.");
        if (expense.Status != ExpenseStatus.Pending)
            throw new McpException($"Expense {id} is already {expense.Status}.");

        return store.Approve(id, user.Name ?? user.UserName ?? user.ObjectId);
    }
}
