using System.Diagnostics;
using Mcp2Oauth.McpServer.AutoApproval;
using Mcp2Oauth.McpServer.Data;
using Mcp2Oauth.McpServer.Identity;
using ModelContextProtocol;

namespace Mcp2Oauth.McpServer.Audit;

/// <summary>
/// Records the current request's financial actions in the <see cref="AuditLog"/>.
/// The tools say what happened; this class adds who did it, taking both the
/// user and the agent from <see cref="CallerIdentity"/>. So the audit trail
/// names the agent, while the tools themselves still never see it.
/// </summary>
public sealed class ExpenseAudit(IHttpContextAccessor accessor, AuditLog log)
{
    public Expense Succeeded(AuditAction action, Expense expense)
    {
        Append(action, AuditOutcome.Succeeded, expense.Id, expense.Amount, expense.OwnerName, null);
        return expense;
    }

    /// <summary>
    /// Records the expense policy's decision on a new expense. The server made it
    /// under its own identity, so the entry names <paramref name="service"/> as well
    /// as the user and agent behind the submission.
    /// </summary>
    public Expense PolicyDecided(Expense expense, PolicyDecision decision, AuditActor service)
    {
        var outcome = decision.AutoApprove ? AuditOutcome.Succeeded : AuditOutcome.Referred;
        Append(AuditAction.AutoApproveExpense, outcome, expense.Id, expense.Amount, expense.OwnerName, decision.Reason, service);
        return expense;
    }

    /// <summary>Records a refused action and returns the exception for the tool to throw.</summary>
    public McpException Denied(AuditAction action, string reason, Expense expense)
    {
        Append(action, AuditOutcome.Denied, expense.Id, expense.Amount, expense.OwnerName, reason);
        return new McpException(reason);
    }

    /// <summary>Records a refused action and returns the exception for the tool to throw.</summary>
    public McpException Denied(AuditAction action, string reason, int? expenseId = null, decimal? amount = null)
    {
        Append(action, AuditOutcome.Denied, expenseId, amount, null, reason);
        return new McpException(reason);
    }

    private void Append(AuditAction action, AuditOutcome outcome, int? expenseId, decimal? amount, string? expenseOwner, string? reason, AuditActor? service = null)
    {
        var caller = accessor.HttpContext?.Features.Get<CallerIdentity>()
            ?? throw new InvalidOperationException("No caller identity was established for this request.");
        var user = caller.User
            ?? throw new InvalidOperationException("Financial actions require an on-behalf-of user.");

        var activity = Activity.Current;
        var entry = log.Append(new AuditEntry(
            Sequence: 0,
            Timestamp: default,
            Action: action,
            Outcome: outcome,
            ExpenseId: expenseId,
            Amount: amount,
            ExpenseOwner: expenseOwner,
            User: new AuditActor(user.ObjectId, user.UserName ?? user.Name),
            Agent: new AuditActor(caller.Agent.AppId, caller.Agent.DisplayName),
            Service: service,
            Reason: reason,
            TraceId: activity?.TraceId.ToHexString()));

        // Link the trace back to the audit entry, as the entry links to the trace.
        activity?.SetTag("mcp2oauth.audit.sequence", entry.Sequence);
        activity?.SetTag("mcp2oauth.audit.outcome", entry.Outcome.ToString());
    }
}
