using System.Net.Http.Headers;
using System.Net.Http.Json;
using Mcp2Oauth.Contracts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Mcp2Oauth.WebUI;

public sealed class AgentSettings
{
    public const string SectionName = "Agent";

    public string BaseUrl { get; set; } = "https://localhost:7102/";

    /// <summary>The agent API's delegated scope, e.g. api://{agent-app-id}/Agent.Chat.</summary>
    public string Scope { get; set; } = "";
}

/// <summary>
/// Calls the agent API with an access token for the signed-in user. The token's
/// audience is the agent, not the MCP server; the agent exchanges it (OBO).
/// </summary>
/// <remarks>
/// In an interactive server circuit there's no current HttpContext, so the user
/// is taken from the circuit's authentication state and passed to MSAL explicitly.
/// </remarks>
public sealed class AgentClient(
    HttpClient http,
    ITokenAcquisition tokenAcquisition,
    AuthenticationStateProvider authenticationState,
    IOptions<AgentSettings> settings)
{
    public Task<ProbeResponse> ProbeAsync(CallMode mode, CancellationToken cancellationToken = default) =>
        SendAsync<ProbeResponse>(HttpMethod.Post, $"api/probe?mode={mode}", null, cancellationToken);

    public Task<AgentChatResponse> ChatAsync(AgentChatRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AgentChatResponse>(HttpMethod.Post, "api/chat", JsonContent.Create(request), cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string uri, HttpContent? content, CancellationToken cancellationToken)
    {
        var user = (await authenticationState.GetAuthenticationStateAsync()).User;

        string token;
        try
        {
            token = await tokenAcquisition.GetAccessTokenForUserAsync([settings.Value.Scope], user: user);
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
        {
            throw new SignInRequiredException(ex);
        }

        using var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await TryReadProblemAsync(response, cancellationToken);
            throw new AgentCallException(
                $"Agent returned {(int)response.StatusCode} {response.ReasonPhrase}" +
                (problem is null ? "" : $": {problem.Title} - {problem.Detail}"));
        }

        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken))!;
    }

    private static async Task<ProblemDetails?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}

public sealed class AgentCallException(string message) : Exception(message);

public sealed class SignInRequiredException(Exception inner)
    : Exception("Your session needs a fresh sign-in.", inner);
