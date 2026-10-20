using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PetWalker.Data;
using PetWalker.Models;
using PetWalker.Services;
using PetWalker.ViewModels;

namespace PetWalker.Controllers;

public class HomeController(PetWalkerDbContext db, IEmailSender emailSender) : Controller
{
    public const string ContactEmail = "info@dogus.edu.tr";

    public IActionResult Index() => View();

    public IActionResult About() => View();

    [HttpGet]
    public IActionResult Contact() => View(new ContactFormViewModel());

    [HttpPost, EnableRateLimiting(RateLimits.Forms)]
    public async Task<IActionResult> Contact(ContactFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        db.ContactMessages.Add(new ContactMessage
        {
            FullName = model.FullName.Trim(),
            Subject = model.Subject.Trim(),
            Email = model.Email.Trim(),
            Body = model.Body.Trim(),
            SentAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await emailSender.SendAsync(ContactEmail, $"İletişim formu: {model.Subject}",
            $"{model.FullName} <{model.Email}>\n\n{model.Body}");

        TempData["Success"] = "Mesajın bize ulaştı. En kısa sürede dönüş yapacağız.";
        return RedirectToAction(nameof(Contact));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code = null) =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, StatusCode = code });
}
