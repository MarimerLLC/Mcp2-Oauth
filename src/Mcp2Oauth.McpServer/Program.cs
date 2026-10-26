using Mcp2Oauth.McpServer;
using Mcp2Oauth.McpServer.Data;
using Mcp2Oauth.McpServer.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using ModelContextProtocol.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration.GetSection(McpServerSettings.SectionName).Get<McpServerSettings>() ?? new();
builder.Services.Configure<McpServerSettings>(builder.Configuration.GetSection(McpServerSettings.SectionName));

var tenantId = builder.Configuration["AzureAd:TenantId"];
var audience = builder.Configuration["AzureAd:Audience"];

// Validate Entra-issued JWTs for this API. Challenges go to the MCP handler so
// a 401 carries WWW-Authenticate: Bearer resource_metadata="..." per the MCP spec.
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
});
authentication.AddMicrosoftIdentityWebApi(builder.Configuration, "AzureAd");
authentication.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        Resource = settings.PublicUrl,
        ResourceName = "Mcp2Oauth demo MCP server",
        AuthorizationServers = { $"https://login.microsoftonline.com/{tenantId}/v2.0" },
        ScopesSupported = [$"{audience}/{Policies.ToolsScope}"],
    };
});

// Keep the raw Entra claim names (azp, oid, scp, roles...) so the demo shows
// exactly what's in the token. v2 tokens carry the client id as aud, so accept
// that as well as the Application ID URI.
builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters.ValidAudiences = [builder.Configuration["AzureAd:ClientId"], audience];
    options.TokenValidationParameters.NameClaimType = EntraClaims.Name;
    options.TokenValidationParameters.RoleClaimType = EntraClaims.Roles;
});

builder.Services.AddAuthorization(Policies.Add);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddSingleton<ExpenseStore>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "Mcp2Oauth.McpServer", Version = "1.0.0" };
    })
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithToolsFromAssembly();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Runs after authentication, before the MCP endpoint: sees agent + user.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/mcp"),
    branch => branch.UseMiddleware<CallerIdentityMiddleware>());

app.MapGet("/", () => "Mcp2Oauth MCP server. The MCP endpoint is /mcp.");
app.MapMcp("/mcp").RequireAuthorization();

app.Run();
