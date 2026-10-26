using System.ComponentModel;
using Mcp2Oauth.McpServer.Audit;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace Mcp2Oauth.McpServer.Tools;

/// <summary>
/// Read access to the audit trail. It needs the Expense.Auditor role, which is
/// separate from Expense.Approver, so reviewing approvals and making them can be
/// given to different people.
/// </summary>
[McpServerToolType]
public sealed class AuditTools(AuditLog log)
{
    [McpServerTool(Name = "get_audit_log", ReadOnly = true)]
    [Authorize(Policy = Policies.Auditor)]
    [Description("Returns the audit trail of expense submissions and approvals, including refused attempts. Each entry names the user and the agent that acted for them. Requires the Expense.Auditor role.")]
    public IReadOnlyList<AuditEntry> GetAuditLog(
        [Description("Only return entries for this expense id.")] int? expenseId = null) =>
        log.Entries(expenseId);
}
