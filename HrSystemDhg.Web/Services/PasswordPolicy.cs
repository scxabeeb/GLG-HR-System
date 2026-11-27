using Microsoft.AspNetCore.Identity;

namespace HrSystemDhg.Web.Services;

public static class PasswordPolicy
{
    public static void Configure(PasswordOptions options)
    {
        options.RequiredLength = 4;
        options.RequiredUniqueChars = 1;
        options.RequireDigit = false;
        options.RequireLowercase = false;
        options.RequireUppercase = false;
        options.RequireNonAlphanumeric = false;
    }
}
