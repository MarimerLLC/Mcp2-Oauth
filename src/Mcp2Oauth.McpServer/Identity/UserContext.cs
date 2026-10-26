using Mcp2Oauth.Contracts;

namespace Mcp2Oauth.McpServer.Identity;

/// <summary>
/// The only identity the tools see: the user the agent is acting on behalf of.
/// Tools rely on these claims for data access and authorization decisions; the
/// agent identity stays in the middleware.
/// </summary>
public interface IUserContext
{
    /// <summary>True when the request carries a delegated (OBO) user identity.</summary>
    bool HasUser { get; }

    /// <summary>The OBO user. Throws if the request is app-only.</summary>
    UserIdentity User { get; }

    bool IsInRole(string role);
}

internal sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    private UserIdentity? CurrentUser =>
        accessor.HttpContext?.Features.Get<CallerIdentity>()?.User;

    public bool HasUser => CurrentUser is not null;

    public UserIdentity User => CurrentUser
        ?? throw new InvalidOperationException("This request has no on-behalf-of user identity.");

    public bool IsInRole(string role) =>
        CurrentUser?.Roles.Contains(role, StringComparer.OrdinalIgnoreCase) == true;
}
