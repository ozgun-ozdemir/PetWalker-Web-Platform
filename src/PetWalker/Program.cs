using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWalker.Data;
using PetWalker.Models;
using PetWalker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PetWalkerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("PetWalker")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/SignIn";
        options.LogoutPath = "/Account/SignOut";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddSingleton<IPasswordHasher<Customer>, PasswordHasher<Customer>>();
builder.Services.AddSingleton<ProfilePhotos>();
builder.Services.AddPetWalkerRateLimits(builder.Configuration);

// Real email when the "Email" settings are filled in (see README: SendGrid); otherwise emails go to the log.
var emailOptions = builder.Configuration.GetSection(EmailOptions.Section).Get<EmailOptions>() ?? new EmailOptions();
builder.Services.AddSingleton(emailOptions);
if (emailOptions.IsConfigured)
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();

builder.Services.AddControllersWithViews(options =>
{
    // Every POST/PUT/DELETE must carry a valid anti-forgery token (CSRF protection).
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PetWalkerDbContext>();
    await db.Database.MigrateAsync();
    if (app.Environment.IsDevelopment())
        await DevelopmentSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<Customer>>());
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Friendly pages for 404 and other error status codes, in every environment.
app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");

if (!app.Environment.IsDevelopment())
{
    // The "http" launch profile has no HTTPS port, so only redirect outside Development.
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program;
