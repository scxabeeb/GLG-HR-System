using HrSystemDhg.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HrSystemDhg.Web.Services;

// Treat damaged stored credentials as a failed sign-in, never as a server error.
public sealed class SafePasswordHasher(IOptions<PasswordHasherOptions> options, ILogger<SafePasswordHasher> logger)
    : PasswordHasher<ApplicationUser>(options)
{
    public override PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        try { return base.VerifyHashedPassword(user, hashedPassword, providedPassword); }
        catch (FormatException)
        {
            logger.LogWarning("Invalid stored password encoding for account {UserId}; password reset required", user.Id);
            return PasswordVerificationResult.Failed;
        }
    }
}
