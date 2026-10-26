using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Mcp2Oauth.Agent;
using Mcp2Oauth.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.AI;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

// The agent is both a protected API (the web UI calls it with a user token) and
// a confidential client with its own credential, which it uses for OBO and
// client credentials calls to the MCP server.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration, "AzureAd")
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

// v2 tokens carry the client id as aud, so accept that as well as the Application ID URI.
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters.ValidAudiences =
        [builder.Configuration["AzureAd:ClientId"], builder.Configuration["AzureAd:Audience"]];
    options.TokenValidationParameters.NameClaimType = "name";
    options.TokenValidationParameters.RoleClaimType = "roles";
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AgentChat", policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(c => c.User.FindAll("scp")
            .Any(s => s.Value.Split(' ').Contains("Agent.Chat"))));

builder.Services.Configure<McpSettings>(builder.Configuration.GetSection(McpSettings.SectionName));
builder.Services.AddHttpClient("mcp");
builder.Services.AddScoped<McpConnector>();
builder.Services.AddScoped<AgentService>();

// Azure OpenAI is optional: the identity probe works without it.
var openAi = builder.Configuration.GetSection("AzureOpenAI");
if (openAi["Endpoint"] is { Length: > 0 } endpoint && openAi["Deployment"] is { Length: > 0 } deployment)
{
    var azureClient = openAi["ApiKey"] is { Length: > 0 } apiKey
        ? new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
        : new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());

    builder.Services.AddChatClient(azureClient.GetChatClient(deployment).AsIChatClient())
        .UseFunctionInvocation()
        .UseLogging();
}

var app = builder.Build();

// Return failures (MCP 401/403, missing consent, LLM errors) as problem details
// so the web UI can show what went wrong in the identity flow.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    await Results.Problem(
        title: error?.GetType().Name ?? "Error",
        detail: error?.GetBaseException().Message,
        statusCode: StatusCodes.Status502BadGateway).ExecuteAsync(context);
}));
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api").RequireAuthorization("AgentChat");

api.MapPost("/probe", (CallMode mode, AgentService agent, CancellationToken ct) =>
    agent.ProbeAsync(mode, ct));

api.MapPost("/chat", (AgentChatRequest request, AgentService agent, CancellationToken ct) =>
    agent.ChatAsync(request, ct));

app.MapGet("/", () => "Mcp2Oauth agent service.");

app.Run();
