using System.Net;
using System.Security.Claims;
using System.Text.Json.Nodes;
using Mcp2Oauth.McpServer.AutoApproval;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Abstractions;

namespace Mcp2Oauth.McpServer.Tests;

/// <summary>
/// The call to Microsoft-Decision-1, against a stub endpoint. The response bodies
/// are the shapes the real model returns.
/// </summary>
public sealed class DecisionModelClassifierTests
{
    private const string TeamLunchAnswer =
        """
        {"model":"microsoft-decision-1","answers":{
          "category":{"choice":"meal","confidence":0.9992,"probabilities":{"meal":0.9994,"other":0.0003,"rideshare":0.0002},"type":"choice"},
          "excluded":{"noul":0.0004,"type":"noul"}},
         "usage":{"input_tokens":114,"output_tokens":2}}
        """;

    private static readonly ExpensePolicySettings Policy = new()
    {
        Endpoint = "https://foundry.example.test/",
        Deployment = "decision-1",
        TimeoutSeconds = 0.5,
        Categories =
        {
            ["meal"] = new() { Description = "A meal.", Limit = 75 },
            ["other"] = new() { Description = "Anything else." },
        },
        Exclusions = new() { Summary = "alcohol, a gift or entertainment", WhenTrue = "It names one.", WhenFalse = "It names none." },
    };

    [Fact]
    public async Task Calls_the_model_as_the_server_with_the_description_as_data()
    {
        var endpoint = new StubEndpoint(_ => new(HttpStatusCode.OK) { Content = new StringContent(TeamLunchAnswer) });
        var tokens = new StubTokens();

        var classification = await Classifier(endpoint, tokens).ClassifyAsync("Team lunch", TestContext.Current.CancellationToken);

        Assert.Equal(new ExpenseClassification("meal", 0.9992, 0.0004), classification);

        // The server's own token (client credentials), not an API key or the user's token.
        Assert.Equal(["https://cognitiveservices.azure.com/.default"], tokens.AppScopes);
        var request = Assert.Single(endpoint.Requests);
        Assert.Equal("https://foundry.example.test/providers/microsoft/v1/systemone", request.Uri);
        Assert.Equal("Bearer server-token", request.Authorization);
        Assert.False(request.HasApiKey);
        Assert.NotNull(request.ContentLength);

        var body = JsonNode.Parse(request.Body)!;
        Assert.Equal("decision-1", (string?)body["model"]);
        Assert.Equal("Team lunch", (string?)body["state"]!["expense_description"]);
        Assert.Equal("choice", (string?)body["questions"]!["category"]!["type"]);
        Assert.Equal("A meal.", (string?)body["questions"]!["category"]!["criteria"]!["meal"]);
        Assert.Equal("noul", (string?)body["questions"]!["excluded"]!["type"]);
        Assert.Equal("Does this expense include alcohol, a gift or entertainment?", (string?)body["questions"]!["excluded"]!["instructions"]);
        Assert.DoesNotContain("Team lunch", body["questions"]!.ToJsonString());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, TeamLunchAnswer)]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, """{"answers":{"category":{"choice":"meal","confidence":0.9}}}""")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task Returns_null_when_the_model_does_not_answer_both_questions(HttpStatusCode status, string body)
    {
        var endpoint = new StubEndpoint(_ => new(status) { Content = new StringContent(body) });

        Assert.Null(await Classifier(endpoint, new StubTokens()).ClassifyAsync("Team lunch", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Returns_null_when_the_model_times_out()
    {
        var endpoint = new StubEndpoint(_ => new(HttpStatusCode.OK) { Content = new StringContent(TeamLunchAnswer) }, TimeSpan.FromSeconds(10));

        Assert.Null(await Classifier(endpoint, new StubTokens()).ClassifyAsync("Team lunch", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Returns_null_when_the_server_cannot_get_a_token()
    {
        var endpoint = new StubEndpoint(_ => new(HttpStatusCode.OK) { Content = new StringContent(TeamLunchAnswer) });

        Assert.Null(await Classifier(endpoint, new StubTokens(fail: true)).ClassifyAsync("Team lunch", TestContext.Current.CancellationToken));
        Assert.Empty(endpoint.Requests);
    }

    private static DecisionModelClassifier Classifier(StubEndpoint endpoint, StubTokens tokens) =>
        new(new HttpClient(endpoint), tokens, Options.Create(Policy), NullLogger<DecisionModelClassifier>.Instance);

    private sealed record RecordedRequest(string Uri, string? Authorization, bool HasApiKey, long? ContentLength, string Body);

    private sealed class StubEndpoint(Func<HttpRequestMessage, HttpResponseMessage> respond, TimeSpan delay = default) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(
                request.RequestUri!.ToString(),
                request.Headers.TryGetValues("Authorization", out var authorization) ? authorization.Single() : null,
                request.Headers.Contains("api-key"),
                request.Content!.Headers.ContentLength,
                await request.Content.ReadAsStringAsync(cancellationToken)));
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);
            return respond(request);
        }
    }

    private sealed class StubTokens(bool fail = false) : IAuthorizationHeaderProvider
    {
        public List<string> AppScopes { get; } = [];

        public Task<string> CreateAuthorizationHeaderForAppAsync(string scopes, AuthorizationHeaderProviderOptions? downstreamApiOptions = null, CancellationToken cancellationToken = default)
        {
            AppScopes.Add(scopes);
            return fail
                ? Task.FromException<string>(new InvalidOperationException("No client credentials are configured."))
                : Task.FromResult("Bearer server-token");
        }

        public Task<string> CreateAuthorizationHeaderForUserAsync(IEnumerable<string> scopes, AuthorizationHeaderProviderOptions? authorizationHeaderProviderOptions = null, ClaimsPrincipal? claimsPrincipal = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The expense policy must never use the user's token.");

        public Task<string> CreateAuthorizationHeaderAsync(IEnumerable<string> scopes, AuthorizationHeaderProviderOptions? options = null, ClaimsPrincipal? claimsPrincipal = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The expense policy must use an app token.");
    }
}
