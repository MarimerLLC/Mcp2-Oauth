using System.Text.Json.Serialization;

namespace Mcp2Oauth.Contracts;

/// <summary>
/// How the agent authenticates to the MCP server.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CallMode>))]
public enum CallMode
{
    /// <summary>Exchange the user's token for an MCP token (OAuth 2.0 on-behalf-of).</summary>
    OnBehalfOf,

    /// <summary>Use the agent's own identity with no user (OAuth 2.0 client credentials).</summary>
    AppOnly,
}

public sealed record ChatTurn(string Role, string Text);

public sealed record AgentChatRequest(IReadOnlyList<ChatTurn> Messages, CallMode Mode);

/// <param name="Blocked">
/// The model provider's content filter refused the conversation, e.g. as a prompt
/// injection. The client should leave the refused message out of later requests,
/// or every following turn is refused too.
/// </param>
public sealed record AgentChatResponse(
    string Reply,
    IReadOnlyList<ToolCallTrace> ToolCalls,
    IReadOnlyList<string> AvailableTools,
    TokenSummary McpToken,
    bool Blocked = false);

public sealed record ToolCallTrace(string Name, string Arguments, string Result);

/// <summary>
/// Calls the MCP server's <c>whoami</c> tool directly (no LLM), so the identity
/// flow can be shown even before Azure OpenAI is configured.
/// </summary>
public sealed record ProbeResponse(
    IdentityReport? Identity,
    string? Error,
    IReadOnlyList<string> AvailableTools,
    TokenSummary McpToken);

/// <summary>
/// The interesting claims from the access token the agent sent to the MCP server.
/// </summary>
public sealed record TokenSummary(IReadOnlyList<KeyValuePair<string, string>> Claims);
