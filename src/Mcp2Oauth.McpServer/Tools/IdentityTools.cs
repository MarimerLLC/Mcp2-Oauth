using System.ComponentModel;
using Mcp2Oauth.Contracts;
using Mcp2Oauth.McpServer.Identity;
using ModelContextProtocol.Server;

namespace Mcp2Oauth.McpServer.Tools;

[McpServerToolType]
public sealed class IdentityTools(IHttpContextAccessor accessor)
{
    /// <summary>
    /// Diagnostic tool that echoes what the middleware saw. It's the one tool that
    /// reads the agent identity, so the demo can show both identities end to end.
    /// </summary>
    [McpServerTool(Name = "whoami", ReadOnly = true, Idempotent = true)]
    [Description("Reports the identities the MCP server sees for this request: the calling agent and, if present, the user it acts on behalf of.")]
    public IdentityReport WhoAmI(ModelContextProtocol.Server.McpServer server)
    {
        var caller = accessor.HttpContext?.Features.Get<CallerIdentity>()
            ?? throw new InvalidOperationException("No caller identity was established for this request.");

        // clientInfo arrives in each request's _meta under 2026-07-28. It's
        // self-asserted, unlike the agent identity Entra put in the token.
        var clientInfo = server.ClientInfo is { } info
            ? new SelfAssertedClient(info.Name, info.Version)
            : null;

        return new IdentityReport(
            caller.TokenKind,
            caller.Agent,
            caller.User,
            caller.DelegatedScopes,
            caller.AppRoles,
            clientInfo);
    }
}
