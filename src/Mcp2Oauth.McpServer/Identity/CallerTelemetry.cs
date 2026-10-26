using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Mcp2Oauth.McpServer.Identity;

/// <summary>
/// Puts the caller identities into OpenTelemetry: tags on the request's trace,
/// and a request counter broken down by agent, token kind and MCP operation.
/// </summary>
public sealed class CallerTelemetry
{
    /// <summary>Matches the application name, which ServiceDefaults registers as a meter.</summary>
    public const string MeterName = "Mcp2Oauth.McpServer";

    private readonly Counter<long> _requests;

    public CallerTelemetry(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _requests = meter.CreateCounter<long>(
            "mcp2oauth.mcp.requests",
            unit: "{request}",
            description: "MCP requests by calling agent, token kind and MCP method.");
    }

    public void Record(CallerIdentity caller, string mcpMethod, string mcpName, bool allowed)
    {
        // The user identity goes on traces only. Metrics stay low-cardinality
        // (one series per agent, not per user).
        if (Activity.Current is { } activity)
        {
            activity.SetTag("mcp2oauth.agent.id", caller.Agent.AppId);
            activity.SetTag("mcp2oauth.agent.name", caller.Agent.DisplayName);
            activity.SetTag("mcp2oauth.agent.allowed", caller.Agent.IsAllowed);
            activity.SetTag("mcp2oauth.token_kind", caller.TokenKind.ToString());
            activity.SetTag("mcp.method.name", mcpMethod);
            if (mcpName.Length > 0)
                activity.SetTag("mcp2oauth.mcp.name", mcpName);
            if (caller.User is { } user)
            {
                activity.SetTag("user.id", user.ObjectId);
                activity.SetTag("user.name", user.UserName);
                activity.SetTag("user.roles", string.Join(",", user.Roles));
            }
        }

        _requests.Add(1,
            new KeyValuePair<string, object?>("mcp2oauth.agent.name", caller.Agent.DisplayName ?? caller.Agent.AppId),
            new KeyValuePair<string, object?>("mcp2oauth.token_kind", caller.TokenKind.ToString()),
            new KeyValuePair<string, object?>("mcp.method.name", mcpMethod),
            new KeyValuePair<string, object?>("mcp2oauth.mcp.name", mcpName),
            new KeyValuePair<string, object?>("mcp2oauth.outcome", allowed ? "allowed" : "rejected_agent"));
    }
}
