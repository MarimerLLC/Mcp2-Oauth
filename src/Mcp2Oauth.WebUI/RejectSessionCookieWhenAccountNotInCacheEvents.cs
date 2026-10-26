using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace Mcp2Oauth.WebUI;

/// <summary>
/// Rejects the auth cookie when the user's account is no longer in the token
/// cache (for example, after an app restart with an in-memory cache).
/// </summary>
internal sealed class RejectSessionCookieWhenAccountNotInCacheEvents(string[] scopes) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        try
        {
            var tokenAcquisition = context.HttpContext.RequestServices.GetRequiredService<ITokenAcquisition>();
            await tokenAcquisition.GetAccessTokenForUserAsync(scopes, user: context.Principal);
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
            when (ex.InnerException is MsalUiRequiredException { ErrorCode: "user_null" })
        {
            context.RejectPrincipal();
        }
    }
}
