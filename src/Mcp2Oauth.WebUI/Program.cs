using Mcp2Oauth.WebUI;
using Mcp2Oauth.WebUI.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

var agentSettings = builder.Configuration.GetSection(AgentSettings.SectionName).Get<AgentSettings>() ?? new();
builder.Services.Configure<AgentSettings>(builder.Configuration.GetSection(AgentSettings.SectionName));

// Sign users in with Entra ID and cache their tokens server side, so the
// circuit can get an access token for the agent API.
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi([agentSettings.Scope])
    .AddInMemoryTokenCaches();

// The token cache is in memory, so after a restart the auth cookie outlives it.
// Reject such cookies to force a fresh sign-in instead of failing later.
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme,
    options => options.Events = new RejectSessionCookieWhenAccountNotInCacheEvents([agentSettings.Scope]));

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHttpClient<AgentClient>(client => client.BaseAddress = new Uri(agentSettings.BaseUrl));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var authentication = app.MapGroup("/authentication");

authentication.MapGet("/login", (string? returnUrl) =>
    TypedResults.Challenge(new AuthenticationProperties { RedirectUri = LocalUrl(returnUrl) }))
    .AllowAnonymous();

authentication.MapPost("/logout", ([FromForm] string? returnUrl) =>
    TypedResults.SignOut(
        new AuthenticationProperties { RedirectUri = LocalUrl(returnUrl) },
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));

app.Run();

// Only redirect back within this app.
static string LocalUrl(string? returnUrl) =>
    returnUrl is { Length: > 0 } && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\")
        ? returnUrl
        : "/";
