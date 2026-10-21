using System.Security.Claims;

namespace HealthcareRecruitment.Api.Services.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The authenticated user's database id, taken from the JWT's NameIdentifier claim.</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No NameIdentifier claim on the current principal.");
        return int.Parse(value);
    }
}
