using System.Text.Json;
using Mcp2Oauth.McpServer.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Mcp2Oauth.McpServer.Tests;

/// <summary>
/// Hosts the real MCP server in memory, with Entra token validation replaced by
/// <see cref="TestAuthHandler"/>. Each test gets a fresh server, so the
/// in-memory expense store and audit log start from their seed data.
/// </summary>
public sealed class TestMcpServer : WebApplicationFactory<Program>
{
    public const string AgentAppId = "aaaaaaaa-0000-0000-0000-00000000a6e7";
    public const string AgentName = "Test Agent";
    public const string UnknownAgentAppId = "bbbbbbbb-0000-0000-0000-00000000bad0";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("AzureAd:TenantId", "00000000-0000-0000-0000-0000000000aa");
        builder.UseSetting("AzureAd:ClientId", "00000000-0000-0000-0000-0000000000bb");
        builder.UseSetting("AzureAd:Audience", "api://00000000-0000-0000-0000-0000000000bb");
        builder.UseSetting($"McpServer:AllowedAgents:{AgentAppId}", AgentName);

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
            services.PostConfigure<AuthenticationOptions>(options =>
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName);
        });
    }

    /// <summary>Connects an MCP client that presents a token with these claims.</summary>
    public async Task<McpClient> ConnectAsync(TestToken token)
    {
        var http = CreateDefaultClient(new Uri("https://localhost/"));
        http.DefaultRequestHeaders.Add(TestAuthHandler.ClaimsHeader, TestAuthHandler.Encode(token.Claims));

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri("https://localhost/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }
}

/// <summary>The claims of an Entra access token, as the MCP server would receive them.</summary>
public sealed record TestToken(IReadOnlyList<KeyValuePair<string, string>> Claims)
{
    /// <summary>A delegated (OBO) token: the agent acting for a user.</summary>
    public static TestToken OnBehalfOf(TestUser user, string agentAppId = TestMcpServer.AgentAppId) => new(
    [
        new(EntraClaims.AuthorizedParty, agentAppId),
        new(EntraClaims.IdentityType, "user"),
        new(EntraClaims.Scope, Policies.ToolsScope),
        new(EntraClaims.ObjectId, user.ObjectId),
        new(EntraClaims.TenantId, "00000000-0000-0000-0000-0000000000aa"),
        new(EntraClaims.Name, user.Name),
        new(EntraClaims.PreferredUserName, user.UserName),
        .. user.Roles.Select(r => KeyValuePair.Create(EntraClaims.Roles, r)),
    ]);

    /// <summary>An app-only token: the agent acting as itself.</summary>
    public static TestToken AppOnly(string agentAppId = TestMcpServer.AgentAppId) => new(
    [
        new(EntraClaims.AuthorizedParty, agentAppId),
        new(EntraClaims.IdentityType, "app"),
        new(EntraClaims.Roles, Policies.StatusReadAppRole),
    ]);
}

public sealed record TestUser(string ObjectId, string Name, string UserName, params string[] Roles)
{
    public static readonly TestUser Employee =
        new("00000000-0000-0000-0000-0000000e0001", "Erin Employee", "erin@example.test");

    public static readonly TestUser Approver =
        new("00000000-0000-0000-0000-0000000a0001", "Andy Approver", "andy@example.test", Policies.ApproverUserRole);

    public static readonly TestUser Auditor =
        new("00000000-0000-0000-0000-0000000a0d17", "Ada Auditor", "ada@example.test", Policies.AuditorUserRole);
}

public static class McpClientExtensions
{
    /// <summary>Calls a tool and deserializes its JSON result. Fails the test if the tool returned an error.</summary>
    public static async Task<T> CallAsync<T>(this McpClient client, string tool, Dictionary<string, object?>? arguments = null)
    {
        var result = await client.CallToolAsync(tool, arguments, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.IsError == true, $"{tool} failed: {result.Text()}");
        return result.StructuredContent is { } structured
            ? structured.Deserialize<T>(JsonSerializerOptions.Web)!
            : JsonSerializer.Deserialize<T>(result.Text(), JsonSerializerOptions.Web)!;
    }

    /// <summary>Calls a tool that's expected to fail, and returns its error message.</summary>
    public static async Task<string> CallExpectingErrorAsync(this McpClient client, string tool, Dictionary<string, object?>? arguments = null)
    {
        var result = await client.CallToolAsync(tool, arguments, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.IsError == true, $"{tool} was expected to fail but returned: {result.Text()}");
        return result.Text();
    }

    public static async Task<IReadOnlyList<string>> ToolNamesAsync(this McpClient client) =>
        (await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken)).Select(t => t.Name).ToList();

    private static string Text(this CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(t => t.Text));
}
