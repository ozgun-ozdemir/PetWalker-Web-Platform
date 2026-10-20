using System.Net;
using Microsoft.AspNetCore.Hosting;
using PetWalker.Tests.Infrastructure;

namespace PetWalker.Tests;

// Uses the app's normal limits (not the raised test limits): 10 sign-in attempts per 5 minutes per IP.
public class RateLimitTests(RateLimitTests.DefaultLimitsFactory factory) : IClassFixture<RateLimitTests.DefaultLimitsFactory>
{
    public class DefaultLimitsFactory : PetWalkerFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimits:SignInsPerFiveMinutes", "10");
        }
    }

    [Fact]
    public async Task Too_many_sign_in_attempts_are_blocked()
    {
        var client = factory.CreateTestClient();
        var form = new Dictionary<string, string> { ["Email"] = "guess@test.local", ["Password"] = "wrong-pass" };

        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/Account/SignIn", form)).Status);

        var blocked = await client.PostAsync("/Account/SignIn", form);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.Status);
        Assert.True(blocked.Contains("Çok fazla deneme"));
    }
}
