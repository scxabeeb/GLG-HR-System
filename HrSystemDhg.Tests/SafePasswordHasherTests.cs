using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public class SafePasswordHasherTests
{
    private readonly SafePasswordHasher hasher = new(Options.Create(new PasswordHasherOptions()), NullLogger<SafePasswordHasher>.Instance);
    [Theory]
    [InlineData("not-a-password-hash!")]
    [InlineData("%%%%")]
    [InlineData("")]
    [InlineData("AQ==")]
    public void DamagedStoredCredentialFailsWithoutThrowing(string stored)
        => Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(new(), stored, "SomePassword!123"));

    [Fact]
    public void ValidPasswordHashesKeepNormalVerification()
    {
        var user = new ApplicationUser();
        var stored = hasher.HashPassword(user, "SomePassword!123");
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, stored, "SomePassword!123"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, stored, "WrongPassword!123"));
    }
}
