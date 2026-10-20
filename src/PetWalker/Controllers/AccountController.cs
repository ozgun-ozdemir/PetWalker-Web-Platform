using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetWalker.Data;
using PetWalker.Models;
using PetWalker.Services;
using PetWalker.ViewModels;

namespace PetWalker.Controllers;

public class AccountController(
    PetWalkerDbContext db,
    IPasswordHasher<Customer> hasher,
    IEmailSender emailSender,
    ProfilePhotos photos) : Controller
{
    [HttpGet]
    public IActionResult SignUp() => View(new SignUpViewModel());

    [HttpPost, EnableRateLimiting(RateLimits.Forms)]
    public async Task<IActionResult> SignUp(SignUpViewModel model)
    {
        // Empty inputs bind as null, so only touch the fields once validation has passed.
        var email = model.Email?.Trim().ToLowerInvariant() ?? "";
        if (ModelState.IsValid && await db.Customers.AnyAsync(c => c.Email == email))
            ModelState.AddModelError(nameof(model.Email), "Bu e-posta ile kayıtlı bir hesap zaten var.");

        var dob = CheckDateOfBirth(model.DateOfBirth, nameof(model.DateOfBirth));
        if (!string.IsNullOrEmpty(model.Address) && !Istanbul.IsValidAddress(model.Address))
            ModelState.AddModelError(nameof(model.Address), "Listeden bir ilçe seçin.");

        if (!ModelState.IsValid)
            return View(model);

        var customer = new Customer
        {
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Email = email,
            Phone = model.Phone.Trim(),
            DateOfBirth = dob,
            Gender = model.Gender!.Value,
            Address = model.Address.Trim(),
        };
        customer.PasswordHash = hasher.HashPassword(customer, model.Password);

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        await SignInCustomerAsync(customer);
        TempData["Success"] = $"Hoş geldin {customer.FirstName}! Hesabın oluşturuldu.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public IActionResult SignIn(string? returnUrl = null) => View(new SignInViewModel { ReturnUrl = returnUrl });

    [HttpPost, EnableRateLimiting(RateLimits.SignIn)]
    public async Task<IActionResult> SignIn(SignInViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim().ToLowerInvariant();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == email);
        var result = customer is null
            ? PasswordVerificationResult.Failed
            : hasher.VerifyHashedPassword(customer, customer.PasswordHash, model.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "E-posta veya şifre hatalı.");
            return View(model);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            customer!.PasswordHash = hasher.HashPassword(customer, model.Password);
            await db.SaveChangesAsync();
        }

        await SignInCustomerAsync(customer!);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public new async Task<IActionResult> SignOut()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost, EnableRateLimiting(RateLimits.Forms)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim().ToLowerInvariant();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == email);
        if (customer is not null)
        {
            // A new random password is emailed; the user can change it later on "Profilimi Güncelle".
            var newPassword = NewRandomPassword();
            customer.PasswordHash = hasher.HashPassword(customer, newPassword);
            await db.SaveChangesAsync();

            await emailSender.SendAsync(customer.Email, "PetWalker yeni şifren",
                $"Merhaba {customer.FirstName},\n\nYeni şifren: {newPassword}\n\n" +
                "Bu şifreyle giriş yaptıktan sonra Profilim > Profilimi Güncelle sayfasından kendi şifreni belirleyebilirsin.");
        }

        // Same answer whether or not the account exists, so emails can't be enumerated.
        TempData["Success"] = "Bu e-posta kayıtlıysa, yeni şifren e-posta adresine gönderildi.";
        return RedirectToAction(nameof(SignIn));
    }

    [Authorize]
    // edit / addPet open the matching window when the page loads.
    public async Task<IActionResult> Profile(bool edit = false, bool addPet = false)
    {
        var id = User.GetCustomerId();
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null)
        {
            // Cookie points to an account that no longer exists.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(SignIn));
        }

        ViewData["OpenModal"] = edit ? "editModal" : addPet ? "petModal" : null;
        return View(await BuildProfileAsync(customer));
    }

    // Creates the walker profile, or updates it if it already exists.
    [Authorize, HttpPost]
    public async Task<IActionResult> BecomeWalker([Bind(Prefix = "WalkerForm")] BecomeWalkerViewModel model)
    {
        var id = User.GetCustomerId();
        var customer = await db.Customers.Include(c => c.WalkerProfile).FirstAsync(c => c.Id == id);

        model.Durations = model.Durations.Distinct().Where(BookWalkViewModel.AllowedDurations.Contains).Order().ToList();
        if (model.Durations.Count == 0)
            ModelState.AddModelError("WalkerForm.Durations", "En az bir süre seçin.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            TempData["OpenWalkerModal"] = true;
            return customer.WalkerProfile is { IsActive: true } existing
                ? RedirectToAction("Details", "Walkers", new { id = existing.Id })
                : RedirectToAction(nameof(Profile));
        }

        var isNew = customer.WalkerProfile is not { IsActive: true };
        var walker = customer.WalkerProfile ??= new Walker();
        walker.FirstName = customer.FirstName;
        walker.LastName = customer.LastName;
        walker.Address = customer.Address;
        walker.Phone = customer.Phone;
        walker.DateOfBirth = customer.DateOfBirth;
        walker.Gender = customer.Gender;
        walker.ExperienceYears = model.ExperienceYears;
        walker.PricePer30Min = model.PricePer30Min;
        walker.Bio = model.Bio.Trim();
        walker.OfferedDurations = string.Join(',', model.Durations);
        walker.LeftAtUtc = null; // Rejoining after "Ayrıl" reuses the same walker profile.
        await db.SaveChangesAsync();

        TempData["Success"] = isNew
            ? "Artık bir gezdiricisin! Profilin Gezdirici Ara sayfasında görünüyor."
            : "Gezdirici bilgilerin güncellendi.";
        return RedirectToAction("Details", "Walkers", new { id = walker.Id });
    }

    // Profile editing is a window on the Profilim page.
    [Authorize, HttpGet]
    public IActionResult Edit() => RedirectToAction(nameof(Profile), new { edit = true });

    [Authorize, HttpPost]
    public async Task<IActionResult> Edit([Bind(Prefix = "EditForm")] EditProfileViewModel model)
    {
        var customer = await db.Customers.Include(c => c.WalkerProfile).FirstAsync(c => c.Id == User.GetCustomerId());
        var email = model.Email?.Trim().ToLowerInvariant() ?? "";
        if (ModelState.IsValid && await db.Customers.AnyAsync(c => c.Email == email && c.Id != customer.Id))
            ModelState.AddModelError("EditForm." + nameof(model.Email), "Bu e-posta ile kayıtlı başka bir hesap var.");
        var dob = CheckDateOfBirth(model.DateOfBirth, "EditForm." + nameof(model.DateOfBirth));
        if (!string.IsNullOrEmpty(model.Address) && !Istanbul.IsValidAddress(model.Address))
            ModelState.AddModelError("EditForm." + nameof(model.Address), "Listeden bir ilçe seçin.");

        if (!ModelState.IsValid)
        {
            // Show Profilim again with the window open and the errors next to the fields.
            ViewData["OpenModal"] = "editModal";
            var vm = await BuildProfileAsync(customer);
            return View(nameof(Profile), new ProfileViewModel
            {
                Customer = vm.Customer, Pets = vm.Pets, WalkerProfile = vm.WalkerProfile, WalkerForm = vm.WalkerForm, EditForm = model,
            });
        }

        customer.FirstName = model.FirstName.Trim();
        customer.LastName = model.LastName.Trim();
        customer.DateOfBirth = dob;
        customer.Gender = model.Gender!.Value;
        customer.Address = model.Address.Trim();
        customer.Email = email;
        customer.Phone = model.Phone.Trim();
        if (!string.IsNullOrEmpty(model.NewPassword))
            customer.PasswordHash = hasher.HashPassword(customer, model.NewPassword);

        // The walker profile shows the same personal details.
        if (customer.WalkerProfile is { } walker)
        {
            walker.FirstName = customer.FirstName;
            walker.LastName = customer.LastName;
            walker.DateOfBirth = customer.DateOfBirth;
            walker.Gender = customer.Gender;
            walker.Address = customer.Address;
            walker.Phone = customer.Phone;
        }

        await db.SaveChangesAsync();
        await SignInCustomerAsync(customer); // refresh the name shown in the menu
        TempData["Success"] = "Profil bilgilerin güncellendi.";
        return RedirectToAction(nameof(Profile));
    }

    // "Hesabı Sil": removes the account and everything that belongs to it, for good.
    [Authorize, HttpPost]
    public async Task<IActionResult> DeleteAccount()
    {
        var id = User.GetCustomerId();
        var customer = await db.Customers.FirstAsync(c => c.Id == id);
        var walkerId = await db.Walkers.Where(w => w.CustomerId == id).Select(w => (int?)w.Id).FirstOrDefaultAsync();

        // Delete in dependency order: walks and messages first, then pets, the walker profile and the account.
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            if (walkerId is { } wId)
            {
                await db.Walks.Where(w => w.WalkerId == wId).ExecuteDeleteAsync();
                await db.Messages.Where(m => m.WalkerId == wId).ExecuteDeleteAsync();
            }
            await db.Walks.Where(w => w.CustomerId == id).ExecuteDeleteAsync();
            await db.Messages.Where(m => m.CustomerId == id).ExecuteDeleteAsync();
            await db.Pets.Where(p => p.OwnerId == id).ExecuteDeleteAsync();
            if (walkerId is { } wId2)
                await db.Walkers.Where(w => w.Id == wId2).ExecuteDeleteAsync();
            await db.Customers.Where(c => c.Id == id).ExecuteDeleteAsync();
            await tx.CommitAsync();
        }
        photos.Delete(customer.PhotoPath);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "Hesabın kalıcı olarak silindi.";
        return RedirectToAction("Index", "Home");
    }

    // Parses a GG/AA/YYYY date and checks the 18+ rule; adds model errors when needed.
    private DateOnly CheckDateOfBirth(string? text, string field)
    {
        if (string.IsNullOrWhiteSpace(text))
            return default;
        if (!TurkishDate.TryParse(text, out var dob))
        {
            ModelState.AddModelError(field, "Geçerli bir tarih girin (GG/AA/YYYY).");
            return default;
        }
        if (dob > DateOnly.FromDateTime(DateTime.Today.AddYears(-18)))
            ModelState.AddModelError(field, "18 yaşından büyük olmalısınız.");
        else if (dob < new DateOnly(1900, 1, 1))
            ModelState.AddModelError(field, "Geçerli bir doğum tarihi girin.");
        return dob;
    }

    // "Ayrıl": stop being a walker. The profile is kept (hidden) so past walks and reviews stay intact.
    [Authorize, HttpPost]
    public async Task<IActionResult> LeaveWalker()
    {
        var id = User.GetCustomerId();
        var walker = await db.Walkers.FirstOrDefaultAsync(w => w.CustomerId == id && w.LeftAtUtc == null);
        if (walker is null)
            return RedirectToAction(nameof(Profile));

        walker.LeftAtUtc = DateTime.UtcNow;

        // Open requests and upcoming walks can't happen any more.
        var now = DateTime.Now;
        var upcoming = await db.Walks
            .Where(w => w.WalkerId == walker.Id && w.StartsAt > now
                        && (w.Status == WalkStatus.Pending || w.Status == WalkStatus.Accepted))
            .ToListAsync();
        foreach (var walk in upcoming)
            walk.Status = walk.Status == WalkStatus.Pending ? WalkStatus.Rejected : WalkStatus.Cancelled;

        await db.SaveChangesAsync();
        TempData["Success"] = upcoming.Count == 0
            ? "Gezdiriciler arasından ayrıldın."
            : $"Gezdiriciler arasından ayrıldın. Bekleyen {upcoming.Count} yürüyüş iptal edildi.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize, HttpPost, RequestSizeLimit(ProfilePhotos.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> Photo(IFormFile? photo)
    {
        var customer = await db.Customers.FirstAsync(c => c.Id == User.GetCustomerId());
        var result = await photos.SaveAsync(photo, customer.PhotoPath);
        if (result.Error is not null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Profile));
        }

        customer.PhotoPath = result.Path;
        await db.SaveChangesAsync();
        TempData["Success"] = "Profil fotoğrafın güncellendi.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task<ProfileViewModel> BuildProfileAsync(Customer customer, BecomeWalkerViewModel? form = null)
    {
        var pets = await db.Pets.AsNoTracking().Where(p => p.OwnerId == customer.Id).OrderBy(p => p.Name).ToListAsync();
        var walker = customer.WalkerProfile
                     ?? await db.Walkers.AsNoTracking().FirstOrDefaultAsync(w => w.CustomerId == customer.Id);
        return new ProfileViewModel
        {
            Customer = customer,
            Pets = pets,
            WalkerProfile = walker is { IsActive: true } ? walker : null,
            WalkerForm = form ?? (walker is null
                ? new BecomeWalkerViewModel()
                : new BecomeWalkerViewModel
                {
                    ExperienceYears = walker.ExperienceYears,
                    PricePer30Min = walker.PricePer30Min,
                    Bio = walker.Bio ?? "",
                    Durations = [.. walker.Durations],
                }),
            EditForm = new EditProfileViewModel
            {
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                DateOfBirth = TurkishDate.Format(customer.DateOfBirth),
                Gender = customer.Gender == 0 ? null : customer.Gender,
                Address = customer.Address,
                Email = customer.Email,
                Phone = customer.Phone,
            },
        };
    }

    private async Task SignInCustomerAsync(Customer customer)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new(ClaimTypes.Name, customer.FullName),
            new(ClaimTypes.Email, customer.Email),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    // 10 characters from letters and digits that are easy to tell apart (no 0/O, 1/l/I).
    private static string NewRandomPassword() =>
        new(RandomNumberGenerator.GetItems<char>("ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789", 10));
}
