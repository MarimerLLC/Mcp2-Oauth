using Mcp2Oauth.Contracts;
using Microsoft.Extensions.Options;

namespace Mcp2Oauth.McpServer.Identity;

/// <summary>
/// HTTP middleware that sees the whole access token: the agent identity
/// (<c>azp</c>) and, for delegated tokens, the user it acts on behalf of.
/// It enforces the agent allowlist, logs both identities, and publishes a
/// <see cref="CallerIdentity"/> for the rest of the request.
/// </summary>
/// <remarks>
/// MCP 2026-07-28 requires the <c>Mcp-Method</c> and <c>Mcp-Name</c> headers on
/// Streamable HTTP requests (SEP-2243), so this middleware can log and
/// authorize on the MCP operation without parsing the JSON-RPC body.
/// </remarks>
public sealed class CallerIdentityMiddleware(
    RequestDelegate next,
    IOptionsMonitor<McpServerSettings> settings,
    CallerTelemetry telemetry,
    ILogger<CallerIdentityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated != true)
        {
            // Endpoint authorization turns this into a 401 with resource metadata.
            await next(context);
            return;
        }

        var agentAppId = principal.GetAgentAppId() ?? "(none)";
        var allowedAgents = settings.CurrentValue.AllowedAgents;
        var agentName = allowedAgents.GetValueOrDefault(agentAppId);
        var agent = new AgentIdentity(agentAppId, agentName, agentName is not null);

        UserIdentity? user = null;
        if (principal.IsDelegated())
        {
            user = new UserIdentity(
                ObjectId: principal.FindFirst(EntraClaims.ObjectId)?.Value ?? "",
                TenantId: principal.FindFirst(EntraClaims.TenantId)?.Value ?? "",
                Name: principal.FindFirst(EntraClaims.Name)?.Value,
                UserName: principal.FindFirst(EntraClaims.PreferredUserName)?.Value,
                Roles: principal.GetRoles());
        }

        var caller = new CallerIdentity(
            agent,
            user,
            user is null ? TokenKind.AppOnly : TokenKind.OnBehalfOfUser,
            principal.GetScopes(),
            user is null ? principal.GetRoles() : []);
        context.Features.Set(caller);

        var mcpMethod = context.Request.Headers["Mcp-Method"].ToString();
        var mcpName = context.Request.Headers["Mcp-Name"].ToString();

        telemetry.Record(caller, mcpMethod, mcpName, agent.IsAllowed);

        if (!agent.IsAllowed)
        {
            logger.LogWarning(
                "Rejected {McpMethod} {McpName}: agent {AgentAppId} is not on the allowlist (user {User})",
                mcpMethod, mcpName, agentAppId, user?.UserName ?? "(app-only)");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync($"Agent '{agentAppId}' is not allowed to call this MCP server.");
            return;
        }

        if (user is null)
        {
            logger.LogInformation(
                "{McpMethod} {McpName}: agent {AgentName} ({AgentAppId}) acting as itself (app-only, roles: {Roles})",
                mcpMethod, mcpName, agentName, agentAppId, string.Join(",", caller.AppRoles));
        }
        else
        {
            logger.LogInformation(
                "{McpMethod} {McpName}: agent {AgentName} ({AgentAppId}) on behalf of {UserName} ({UserObjectId})",
                mcpMethod, mcpName, agentName, agentAppId, user.UserName, user.ObjectId);
        }

        await next(context);
    }
}

/// <summary>
/// The caller identities for the current request, as established by
/// <see cref="CallerIdentityMiddleware"/>. Stored as an HTTP feature.
/// </summary>
public sealed record CallerIdentity(
    AgentIdentity Agent,
    UserIdentity? User,
    TokenKind TokenKind,
    IReadOnlyList<string> DelegatedScopes,
    IReadOnlyList<string> AppRoles);
