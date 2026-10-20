using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace PetWalker.Services;

// Limits how often one IP address can submit forms, against spam and password guessing.
public static class RateLimits
{
    public const string Forms = "forms";
    public const string SignIn = "sign-in";

    // The numbers can be changed in configuration ("RateLimits:FormsPerTenMinutes", "RateLimits:SignInsPerFiveMinutes"),
    // e.g. raised in automated tests that sign up many users.
    public static IServiceCollection AddPetWalkerRateLimits(this IServiceCollection services, IConfiguration config)
    {
        var forms = config.GetValue("RateLimits:FormsPerTenMinutes", 5);
        var signIns = config.GetValue("RateLimits:SignInsPerFiveMinutes", 10);
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Contact form, sign-up and password reset: 5 per 10 minutes by default.
            options.AddPolicy(Forms, ctx => PerIp(ctx, permits: forms, window: TimeSpan.FromMinutes(10)));
            // Sign-in attempts: 10 per 5 minutes by default.
            options.AddPolicy(SignIn, ctx => PerIp(ctx, permits: signIns, window: TimeSpan.FromMinutes(5)));
        });
    }

    private static RateLimitPartition<string> PerIp(HttpContext ctx, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 });
}
