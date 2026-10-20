using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWalker.Data;
using PetWalker.Models;
using PetWalker.Services;
using PetWalker.ViewModels;

namespace PetWalker.Controllers;

[Authorize]
public class WalksController(PetWalkerDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var customerId = User.GetCustomerId();
        var myWalks = await db.Walks.AsNoTracking()
            .Where(w => w.CustomerId == customerId)
            .Include(w => w.Pet)
            .Include(w => w.Walker)
            .OrderByDescending(w => w.StartsAt)
            .ToListAsync();

        var myWalkerId = await MyWalkerIdAsync(customerId);
        IReadOnlyList<Walk> bookedWithMe = myWalkerId is null
            ? []
            : await db.Walks.AsNoTracking()
                .Where(w => w.WalkerId == myWalkerId)
                .Include(w => w.Pet)
                .Include(w => w.Customer)
                .OrderByDescending(w => w.StartsAt)
                .ToListAsync();

        return View(new WalksViewModel { MyWalks = myWalks, BookedWithMe = bookedWithMe, IsWalker = myWalkerId is not null });
    }

    // ----- Owner actions -----

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var walk = await MyWalkAsync(id);
        if (walk is null)
            return NotFound();

        if (walk.Status is not (WalkStatus.Pending or WalkStatus.Accepted))
            TempData["Error"] = "Bu yürüyüş iptal edilemez.";
        else if (walk.StartsAt <= DateTime.Now)
            TempData["Error"] = "Başlamış veya tamamlanmış bir yürüyüş iptal edilemez.";
        else
        {
            walk.Status = WalkStatus.Cancelled;
            await db.SaveChangesAsync();
            TempData["Success"] = "Yürüyüş iptal edildi.";
        }
        return RedirectToAction(nameof(Index));
    }

    // Removes a cancelled or rejected walk, or a request that was never answered before its time, for good.
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var walk = await MyWalkAsync(id);
        if (walk is null)
            return NotFound();

        var expired = walk.Status == WalkStatus.Pending && walk.StartsAt <= DateTime.Now;
        if (walk.Status is not (WalkStatus.Cancelled or WalkStatus.Rejected) && !expired)
        {
            TempData["Error"] = "Yalnızca iptal edilen yürüyüşler silinebilir.";
            return RedirectToAction(nameof(Index));
        }

        db.Walks.Remove(walk);
        await db.SaveChangesAsync();
        TempData["Success"] = "Yürüyüş kaydı silindi.";
        return RedirectToAction(nameof(Index));
    }

    // The owner confirms the walk happened; it can then be rated.
    [HttpPost]
    public async Task<IActionResult> Complete(int id)
    {
        var walk = await MyWalkAsync(id);
        if (walk is null)
            return NotFound();

        if (walk.Status != WalkStatus.Accepted)
        {
            TempData["Error"] = "Yalnızca gezdiricinin onayladığı yürüyüşler tamamlanabilir.";
            return RedirectToAction(nameof(Index));
        }

        walk.Status = WalkStatus.Completed;
        await db.SaveChangesAsync();
        TempData["Success"] = "Yürüyüş tamamlandı. Şimdi puan verebilirsin.";
        TempData["RateWalkId"] = walk.Id;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Rate(RateWalkViewModel model)
    {
        var walk = await MyWalkAsync(model.WalkId);
        if (walk is null)
            return NotFound();

        var finished = walk.Status == WalkStatus.Completed
                       || (walk.Status == WalkStatus.Accepted && walk.StartsAt <= DateTime.Now);
        if (!finished)
            ModelState.AddModelError("", "Yalnızca tamamlanan yürüyüşler puanlanabilir.");
        else if (walk.Rating is not null)
            ModelState.AddModelError("", "Bu yürüyüşü zaten puanladın.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        walk.Rating = model.Rating;
        walk.Review = string.IsNullOrWhiteSpace(model.Review) ? null : model.Review.Trim();
        await db.SaveChangesAsync();

        TempData["Success"] = "Değerlendirmen için teşekkürler!";
        return RedirectToAction(nameof(Index));
    }

    // ----- Walker actions: answering booking requests -----

    [HttpPost]
    public async Task<IActionResult> Accept(int id)
    {
        var walk = await RequestToMeAsync(id);
        if (walk is null)
            return NotFound();

        if (walk.Status != WalkStatus.Pending || walk.StartsAt <= DateTime.Now)
            TempData["Error"] = "Bu istek artık yanıtlanamaz.";
        else if (await WalkScheduling.WalkerIsBusyAsync(db, walk.WalkerId, walk.StartsAt, walk.DurationMinutes, walk.Id, acceptedOnly: true))
            TempData["Error"] = "Bu saatte kabul ettiğin başka bir yürüyüş var. İsteği reddedebilir veya sahibiyle mesajlaşabilirsin.";
        else
        {
            walk.Status = WalkStatus.Accepted;
            await db.SaveChangesAsync();
            TempData["Success"] = "Rezervasyon isteği kabul edildi.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id)
    {
        var walk = await RequestToMeAsync(id);
        if (walk is null)
            return NotFound();

        if (walk.Status != WalkStatus.Pending)
            TempData["Error"] = "Bu istek artık yanıtlanamaz.";
        else
        {
            walk.Status = WalkStatus.Rejected;
            await db.SaveChangesAsync();
            TempData["Success"] = "Rezervasyon isteği reddedildi.";
        }
        return RedirectToAction(nameof(Index));
    }

    // The walker cancels an upcoming walk booked with them.
    [HttpPost]
    public async Task<IActionResult> WalkerCancel(int id)
    {
        var walk = await RequestToMeAsync(id);
        if (walk is null)
            return NotFound();

        if (walk.Status is not (WalkStatus.Pending or WalkStatus.Accepted) || walk.StartsAt <= DateTime.Now)
            TempData["Error"] = "Bu yürüyüş iptal edilemez.";
        else
        {
            walk.Status = WalkStatus.Cancelled;
            await db.SaveChangesAsync();
            TempData["Success"] = "Yürüyüş iptal edildi.";
        }
        return RedirectToAction(nameof(Index));
    }

    private Task<Walk?> MyWalkAsync(int id)
    {
        var customerId = User.GetCustomerId();
        return db.Walks.FirstOrDefaultAsync(w => w.Id == id && w.CustomerId == customerId);
    }

    private async Task<Walk?> RequestToMeAsync(int id)
    {
        var myWalkerId = await MyWalkerIdAsync(User.GetCustomerId());
        return myWalkerId is null ? null : await db.Walks.FirstOrDefaultAsync(w => w.Id == id && w.WalkerId == myWalkerId);
    }

    private Task<int?> MyWalkerIdAsync(int customerId) =>
        db.Walkers.Where(w => w.CustomerId == customerId).Select(w => (int?)w.Id).FirstOrDefaultAsync();
}
