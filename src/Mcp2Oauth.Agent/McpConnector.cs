using System.Net.Http.Headers;
using Mcp2Oauth.Contracts;
using Microsoft.Identity.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using ModelContextProtocol.Client;

namespace Mcp2Oauth.Agent;

public sealed class McpSettings
{
    public const string SectionName = "Mcp";

    public string Endpoint { get; set; } = "https://localhost:7101/mcp";

    /// <summary>The MCP server's Application ID URI, e.g. api://{mcp-app-id}.</summary>
    public string Audience { get; set; } = "";
}

/// <summary>
/// Gets an access token for the MCP server, then opens an MCP client with it.
/// This is where the agent authenticates itself to Entra:
/// <list type="bullet">
/// <item><see cref="CallMode.OnBehalfOf"/>: the incoming user token plus the agent's own
/// credential is exchanged for an MCP token (OAuth 2.0 on-behalf-of). The result
/// carries the user (<c>oid</c>, <c>scp</c>) and the agent (<c>azp</c>).</item>
/// <item><see cref="CallMode.AppOnly"/>: the agent's own credential alone (OAuth 2.0
/// client credentials). The result carries only the agent (<c>azp</c>, <c>roles</c>).</item>
/// </list>
/// </summary>
public sealed class McpConnector(
    IAuthorizationHeaderProvider authorizationHeaders,
    IHttpClientFactory httpClientFactory,
    Microsoft.Extensions.Options.IOptions<McpSettings> settings,
    ILoggerFactory loggerFactory)
{
    private static readonly string[] InterestingClaims =
        ["aud", "iss", "azp", "appid", "idtyp", "oid", "name", "preferred_username", "scp", "roles", "exp"];

    public async Task<McpSession> ConnectAsync(CallMode mode, CancellationToken cancellationToken)
    {
        var mcp = settings.Value;
        var header = mode switch
        {
            CallMode.OnBehalfOf => await authorizationHeaders.CreateAuthorizationHeaderForUserAsync(
                [$"{mcp.Audience}/Mcp.Tools"], cancellationToken: cancellationToken),
            CallMode.AppOnly => await authorizationHeaders.CreateAuthorizationHeaderForAppAsync(
                $"{mcp.Audience}/.default", cancellationToken: cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        var authorization = AuthenticationHeaderValue.Parse(header);
        var http = httpClientFactory.CreateClient("mcp");
        http.DefaultRequestHeaders.Authorization = authorization;

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(mcp.Endpoint),
                TransportMode = HttpTransportMode.StreamableHttp,
                Name = "mcp2oauth-server",
            },
            http,
            loggerFactory,
            ownsHttpClient: true);

        // clientInfo is self-asserted and sent in each request's _meta. The MCP
        // server shows it next to the Entra-attested azp claim for comparison.
        var client = await McpClient.CreateAsync(
            transport,
            new McpClientOptions { ClientInfo = new() { Name = "Mcp2Oauth.Agent", Version = "1.0.0" } },
            loggerFactory,
            cancellationToken);

        return new McpSession(client, Summarize(authorization.Parameter!));
    }

    /// <summary>
    /// Decodes (without validating) the token we're sending, for display in the UI.
    /// </summary>
    public static TokenSummary Summarize(string accessToken)
    {
        var jwt = new JsonWebToken(accessToken);
        var claims = jwt.Claims
            .Where(c => InterestingClaims.Contains(c.Type))
            .GroupBy(c => c.Type)
            .OrderBy(g => Array.IndexOf(InterestingClaims, g.Key))
            .Select(g => KeyValuePair.Create(g.Key, string.Join(" ", g.Select(c => c.Value))))
            .ToList();
        return new TokenSummary(claims);
    }
}

public sealed record McpSession(McpClient Client, TokenSummary Token) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Client.DisposeAsync();
}
