using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PetWalker.Data;
using PetWalker.Services;

namespace PetWalker.Tests.Infrastructure;

// Starts the real app in memory with its own SQLite file, so every test class gets a fresh database.
// Emails are captured instead of logged, and the rate limits are raised so tests can sign up many users.
public class PetWalkerFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"petwalker-test-{Guid.NewGuid():N}.db");

    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PetWalker", $"Data Source={_dbPath}");
        builder.UseSetting("RateLimits:FormsPerTenMinutes", "100000");
        builder.UseSetting("RateLimits:SignInsPerFiveMinutes", "100000");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    public TestClient CreateTestClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, HandleCookies = true }));

    // Direct database access for setting up and checking test data.
    public async Task<T> WithDbAsync<T>(Func<PetWalkerDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<PetWalkerDbContext>());
    }

    public Task WithDbAsync(Func<PetWalkerDbContext, Task> action) =>
        WithDbAsync(async db => { await action(db); return 0; });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            if (File.Exists(f)) File.Delete(f);
    }
}

public class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string to, string subject, string body)
    {
        Sent.Enqueue((to, subject, body));
        return Task.CompletedTask;
    }
}
