using System.ComponentModel;
using Mcp2Oauth.McpServer.Data;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace Mcp2Oauth.McpServer.Tools;

/// <summary>
/// A tool that doesn't need a user, so an app-only agent can call it too.
/// </summary>
[McpServerToolType]
public sealed class StatusTools(ExpenseStore store, TimeProvider time)
{
    [McpServerTool(Name = "get_service_status", ReadOnly = true)]
    [Authorize(Policy = Policies.StatusReader)]
    [Description("Returns the expense service's status and aggregate counts. Contains no per-user data.")]
    public object GetServiceStatus() => new
    {
        Status = "Healthy",
        ServerTimeUtc = time.GetUtcNow(),
        TotalExpenses = store.Count,
        PendingExpenses = store.Pending().Count,
    };
}
