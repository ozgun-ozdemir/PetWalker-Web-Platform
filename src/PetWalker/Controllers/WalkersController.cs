using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWalker.Data;
using PetWalker.Models;
using PetWalker.Services;
using PetWalker.ViewModels;

namespace PetWalker.Controllers;

// Searching and viewing walkers requires an account.
[Authorize]
public class WalkersController(PetWalkerDbContext db) : Controller
{
    private static readonly CompareInfo Turkish = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;

    public async Task<IActionResult> Index(WalkerSearchViewModel search)
    {
        var query = db.Walkers.AsNoTracking().Where(w => w.LeftAtUtc == null);
        if (search.MinExperience is { } minExp)
            query = query.Where(w => w.ExperienceYears >= minExp);

        IEnumerable<WalkerCard> cards = await ToCards(query).ToListAsync();

        // Konum is an Istanbul district from the dropdown; addresses are stored as "Kadıköy, İstanbul".
        if (!string.IsNullOrWhiteSpace(search.Location))
        {
            var address = Istanbul.ToAddress(search.Location.Trim());
            cards = cards.Where(c => Turkish.Compare(c.Walker.Address, address, CompareOptions.IgnoreCase) == 0);
        }
        if (search.MinRating is { } minRating)
            // Compare against the whole-star value shown on the card (4.6 is shown and filtered as 5).
            cards = cards.Where(c => c.AverageRating is { } avg && Math.Round(avg, MidpointRounding.AwayFromZero) >= minRating);

        search.Results = search.Sort switch
        {
            "rating-asc" => cards.OrderBy(c => c.AverageRating ?? 0).ThenBy(c => c.RatingCount).ToList(),
            "experience-desc" => cards.OrderByDescending(c => c.Walker.ExperienceYears).ToList(),
            "experience-asc" => cards.OrderBy(c => c.Walker.ExperienceYears).ToList(),
            "price-asc" => cards.OrderBy(c => c.Walker.PricePer30Min).ToList(),
            "price-desc" => cards.OrderByDescending(c => c.Walker.PricePer30Min).ToList(),
            _ => cards.OrderByDescending(c => c.AverageRating ?? 0).ThenByDescending(c => c.RatingCount).ToList(),
        };
        return View(search);
    }

    public async Task<IActionResult> Details(int id)
    {
        var card = await ToCards(db.Walkers.AsNoTracking().Where(w => w.Id == id)).FirstOrDefaultAsync();
        var me = User.GetCustomerId();
        if (card is null || (!card.Walker.IsActive && card.Walker.CustomerId != me))
            return NotFound();

        var reviews = await db.Walks.AsNoTracking()
            .Where(w => w.WalkerId == id && w.Rating != null)
            .Include(w => w.Customer)
            .OrderByDescending(w => w.StartsAt)
            .Take(10)
            .ToListAsync();

        var isMe = card.Walker.CustomerId == me;
        var myPets = await db.Pets.AsNoTracking().Where(p => p.OwnerId == me).OrderBy(p => p.Name).ToListAsync();

        return View(new WalkerProfileViewModel
        {
            Card = card,
            Reviews = reviews,
            MyPets = myPets,
            IsMe = isMe,
            WalkerForm = isMe
                ? new BecomeWalkerViewModel
                {
                    ExperienceYears = card.Walker.ExperienceYears,
                    PricePer30Min = card.Walker.PricePer30Min,
                    Bio = card.Walker.Bio ?? "",
                    Durations = [.. card.Walker.Durations],
                }
                : null,
            Booking = new BookWalkViewModel
            {
                WalkerId = id,
                StartsAt = DateTime.Today.AddDays(1).AddHours(10),
                DurationMinutes = card.Walker.Durations.FirstOrDefault(30),
            },
        });
    }

    [HttpPost]
    public async Task<IActionResult> Book(BookWalkViewModel model)
    {
        var customerId = User.GetCustomerId();
        var walker = await db.Walkers.FindAsync(model.WalkerId);
        if (walker is null || !walker.IsActive)
            return NotFound();

        if (walker.CustomerId == customerId)
            ModelState.AddModelError("", "Kendine yürüyüş planlayamazsın.");
        if (!walker.Durations.Contains(model.DurationMinutes))
            ModelState.AddModelError(nameof(model.DurationMinutes), "Bu gezdirici bu süreyi sunmuyor.");
        if (model.StartsAt is { } start && start <= DateTime.Now)
            ModelState.AddModelError(nameof(model.StartsAt), "Yürüyüş tarihi gelecekte olmalı.");
        if (model.PetId is { } petId && !await db.Pets.AnyAsync(p => p.Id == petId && p.OwnerId == customerId))
            ModelState.AddModelError(nameof(model.PetId), "Bu evcil hayvan size ait değil.");

        // Conflict check: the walker and the pet must both be free for the whole walk.
        if (ModelState.IsValid)
        {
            if (await WalkScheduling.WalkerIsBusyAsync(db, walker.Id, model.StartsAt!.Value, model.DurationMinutes))
                ModelState.AddModelError(nameof(model.StartsAt), "Gezdiricinin bu saatte başka bir yürüyüşü var. Lütfen başka bir saat seç.");
            else if (await WalkScheduling.PetIsBusyAsync(db, model.PetId!.Value, model.StartsAt.Value, model.DurationMinutes))
                ModelState.AddModelError(nameof(model.PetId), "Evcil hayvanının bu saatte başka bir yürüyüşü var.");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id = model.WalkerId });
        }

        db.Walks.Add(new Walk
        {
            CustomerId = customerId,
            PetId = model.PetId!.Value,
            WalkerId = walker.Id,
            StartsAt = model.StartsAt!.Value,
            DurationMinutes = model.DurationMinutes,
            // Price is calculated on the server; never trust a price sent by the browser.
            Price = walker.PricePer30Min * model.DurationMinutes / 30,
        });
        await db.SaveChangesAsync();

        TempData["Success"] = $"Rezervasyon isteğin {walker.FullName} adlı gezdiriciye gönderildi. Onayladığında burada göreceksin.";
        return RedirectToAction("Index", "Walks");
    }

    private static IQueryable<WalkerCard> ToCards(IQueryable<Walker> walkers) =>
        walkers.Select(w => new WalkerCard(
            w,
            w.Walks.Where(x => x.Rating != null).Average(x => (double?)x.Rating),
            w.Walks.Count(x => x.Rating != null),
            w.Customer != null ? w.Customer.PhotoPath : null));
}
