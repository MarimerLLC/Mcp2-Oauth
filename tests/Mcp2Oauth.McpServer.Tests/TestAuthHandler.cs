using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mcp2Oauth.McpServer.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mcp2Oauth.McpServer.Tests;

/// <summary>
/// Stands in for Entra's JWT bearer validation. The test client sends the
/// access token's claims in a header, and this handler turns them into the
/// same principal the JWT handler would produce (raw claim names, no mapping).
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string ClaimsHeader = "X-Test-Claims";

    public static string Encode(IEnumerable<KeyValuePair<string, string>> claims) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(claims.ToList()));

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers[ClaimsHeader].ToString();
        if (header.Length == 0)
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = JsonSerializer.Deserialize<List<KeyValuePair<string, string>>>(Encoding.UTF8.GetString(Convert.FromBase64String(header)))!;
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.Key, c.Value)),
            SchemeName,
            EntraClaims.Name,
            EntraClaims.Roles);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
