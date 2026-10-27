using Mcp2Oauth.McpServer.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Mcp2Oauth.McpServer;

/// <summary>
/// Authorization policies applied to individual MCP tools with
/// <see cref="AuthorizeAttribute"/>. With <c>AddAuthorizationFilters()</c> the SDK
/// also hides tools from <c>tools/list</c> when the caller fails their policy.
/// </summary>
public static class Policies
{
    /// <summary>Delegated scope the agent requests when acting for a user.</summary>
    public const string ToolsScope = "Mcp.Tools";

    /// <summary>Application permission granted to the agent for app-only calls.</summary>
    public const string StatusReadAppRole = "Mcp.Status.Read";

    /// <summary>App role assigned to users (in Entra) who may approve expenses.</summary>
    public const string ApproverUserRole = "Expense.Approver";

    /// <summary>App role assigned to users (in Entra) who may read the audit log.</summary>
    public const string AuditorUserRole = "Expense.Auditor";

    /// <summary>The caller is an agent acting on behalf of a user.</summary>
    public const string OnBehalfOfUser = nameof(OnBehalfOfUser);

    /// <summary>The OBO user holds the Expense.Approver role.</summary>
    public const string Approver = nameof(Approver);

    /// <summary>The OBO user holds the Expense.Auditor role.</summary>
    public const string Auditor = nameof(Auditor);

    /// <summary>Either an OBO call, or an app-only agent with the status permission.</summary>
    public const string StatusReader = nameof(StatusReader);

    public static void Add(AuthorizationOptions options)
    {
        // #region slide-policies
        options.AddPolicy(OnBehalfOfUser, p => p
            .RequireAuthenticatedUser()
            .RequireAssertion(c => c.User.IsDelegated() && c.User.GetScopes().Contains(ToolsScope)));

        options.AddPolicy(Approver, p => p
            .Combine(options.GetPolicy(OnBehalfOfUser)!)
            .RequireAssertion(c => c.User.GetRoles().Contains(ApproverUserRole)));
        // #endregion

        options.AddPolicy(Auditor, p => p
            .Combine(options.GetPolicy(OnBehalfOfUser)!)
            .RequireAssertion(c => c.User.GetRoles().Contains(AuditorUserRole)));

        options.AddPolicy(StatusReader, p => p
            .RequireAuthenticatedUser()
            .RequireAssertion(c => c.User.IsDelegated()
                ? c.User.GetScopes().Contains(ToolsScope)
                : c.User.GetRoles().Contains(StatusReadAppRole)));
    }
}
