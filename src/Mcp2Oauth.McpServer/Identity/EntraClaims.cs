using System.Security.Claims;

namespace Mcp2Oauth.McpServer.Identity;

/// <summary>
/// Raw Entra ID access token claim names. Inbound claim mapping is turned off
/// in Program.cs, so these names arrive exactly as they appear in the JWT.
/// </summary>
public static class EntraClaims
{
    /// <summary>Client (agent) app id in v2 tokens.</summary>
    public const string AuthorizedParty = "azp";

    /// <summary>Client (agent) app id in v1 tokens.</summary>
    public const string AppId = "appid";

    /// <summary>Optional claim: "app" for app-only tokens, "user" for delegated tokens.</summary>
    public const string IdentityType = "idtyp";

    /// <summary>Delegated scopes, space separated. Present only in delegated (OBO) tokens.</summary>
    public const string Scope = "scp";

    /// <summary>App roles: application permissions (app-only) or the user's assigned roles (delegated).</summary>
    public const string Roles = "roles";

    public const string ObjectId = "oid";
    public const string TenantId = "tid";
    public const string Name = "name";
    public const string PreferredUserName = "preferred_username";

    public static string? GetAgentAppId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(AuthorizedParty) ?? principal.FindFirstValue(AppId);

    public static IReadOnlyList<string> GetScopes(this ClaimsPrincipal principal) =>
        principal.FindAll(Scope)
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();

    public static IReadOnlyList<string> GetRoles(this ClaimsPrincipal principal) =>
        principal.FindAll(Roles).Select(c => c.Value).ToList();

    /// <summary>
    /// A token is delegated (on behalf of a user) when it carries scopes. The
    /// <c>idtyp</c> optional claim makes this explicit when it's configured.
    /// </summary>
    public static bool IsDelegated(this ClaimsPrincipal principal)
    {
        var idtyp = principal.FindFirstValue(IdentityType);
        if (idtyp is not null)
            return idtyp == "user";
        return principal.HasClaim(c => c.Type == Scope);
    }
}
