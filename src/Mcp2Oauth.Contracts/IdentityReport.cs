using System.Text.Json.Serialization;

namespace Mcp2Oauth.Contracts;

/// <summary>
/// What the MCP server saw about the caller of a request. Produced by the
/// server's middleware and returned by the <c>whoami</c> tool.
/// </summary>
public sealed record IdentityReport(
    TokenKind TokenKind,
    AgentIdentity Agent,
    UserIdentity? User,
    IReadOnlyList<string> DelegatedScopes,
    IReadOnlyList<string> AppRoles,
    SelfAssertedClient? ClientInfo);

/// <summary>
/// The calling agent, taken from the Entra-issued <c>azp</c>/<c>appid</c> claim.
/// Entra only issues that claim after authenticating the agent's own credential,
/// so it can be trusted (unlike <see cref="SelfAssertedClient"/>).
/// </summary>
public sealed record AgentIdentity(string AppId, string? DisplayName, bool IsAllowed);

/// <summary>
/// The user the agent is acting on behalf of. Null for app-only tokens.
/// </summary>
public sealed record UserIdentity(
    string ObjectId,
    string TenantId,
    string? Name,
    string? UserName,
    IReadOnlyList<string> Roles);

/// <summary>
/// The clientInfo the MCP client sent in the request <c>_meta</c>. This is
/// self-asserted and is shown only for comparison with the attested agent identity.
/// </summary>
public sealed record SelfAssertedClient(string Name, string? Version);

[JsonConverter(typeof(JsonStringEnumConverter<TokenKind>))]
public enum TokenKind
{
    /// <summary>Delegated token: an agent acting on behalf of a signed-in user (OBO).</summary>
    OnBehalfOfUser,

    /// <summary>App-only token: the agent acting as itself (client credentials).</summary>
    AppOnly,
}
