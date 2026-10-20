using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetWalker.Data;
using PetWalker.Models;
using PetWalker.Services;
using PetWalker.ViewModels;

namespace PetWalker.Controllers;

// A conversation is always between one customer and one walker.
// A user who is also a walker sees both sides: walkers they write to, and customers who wrote to them.
[Authorize]
public class ChatController(PetWalkerDbContext db) : Controller
{
    public async Task<IActionResult> Index(int? walkerId, int? customerId)
    {
        var me = User.GetCustomerId();
        var myWalkerId = await MyWalkerIdAsync(me);

        // When the last message of each of my conversations was sent (as a customer, and as a walker).
        var lastWithWalker = await db.Messages.Where(m => m.CustomerId == me)
            .GroupBy(m => m.WalkerId)
            .Select(g => new { g.Key, Last = g.Max(m => m.SentAtUtc) })
            .ToDictionaryAsync(x => x.Key, x => x.Last);
        var lastWithCustomer = myWalkerId is null
            ? []
            : await db.Messages.Where(m => m.WalkerId == myWalkerId)
                .GroupBy(m => m.CustomerId)
                .Select(g => new { g.Key, Last = g.Max(m => m.SentAtUtc) })
                .ToDictionaryAsync(x => x.Key, x => x.Last);

        var walkers = (await db.Walkers.AsNoTracking()
                .Where(w => w.Id != myWalkerId && w.LeftAtUtc == null)
                .OrderBy(w => w.FirstName).ThenBy(w => w.LastName)
                .ToListAsync())
            .Select(w => new ChatContact($"w{w.Id}", w.FullName,
                $"({w.Age} | {w.Gender.ToTurkish()} | {w.ExperienceYears} yıl deneyim)", w.Address,
                lastWithWalker.TryGetValue(w.Id, out var last) ? last : null))
            .ToList();

        var customers = new List<ChatContact>();
        if (myWalkerId is not null)
        {
            // Customers who wrote to me or booked a walk with me.
            var writers = await db.Customers.AsNoTracking()
                .Where(c => c.Messages.Any(m => m.WalkerId == myWalkerId) || c.Walks.Any(w => w.WalkerId == myWalkerId))
                .ToListAsync();
            customers = writers
                .OrderBy(c => c.FirstName)
                .Select(c => new ChatContact($"c{c.Id}", c.FullName, $"({Age.From(c.DateOfBirth)} | {c.Gender.ToTurkish()})", "Müşteri",
                    lastWithCustomer.TryGetValue(c.Id, out var last) ? last : null))
                .ToList();
        }

        ChatContact? active = null;
        IReadOnlyList<Message> messages = [];
        var asWalker = false;

        if (walkerId is { } wId && walkers.FirstOrDefault(c => c.Key == $"w{wId}") is { } walkerContact)
        {
            active = walkerContact;
            messages = await LoadAsync(me, wId);
        }
        else if (customerId is { } cId && myWalkerId is { } myId)
        {
            // A walker may open a conversation only with a customer who wrote to them or booked with them.
            active = customers.FirstOrDefault(c => c.Key == $"c{cId}");
            if (active is not null)
            {
                asWalker = true;
                messages = await LoadAsync(cId, myId);
            }
        }

        return View(new ChatViewModel
        {
            // Most recent conversation first.
            Conversations = [.. customers.Concat(walkers).Where(c => c.HasMessages).OrderByDescending(c => c.LastMessageAt)],
            Others = [.. customers.Where(c => !c.HasMessages), .. walkers.Where(w => !w.HasMessages)],
            Active = active,
            Messages = messages,
            ActiveAsWalker = asWalker,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Send(int? walkerId, int? customerId, string text)
    {
        var me = User.GetCustomerId();
        text = text?.Trim() ?? "";

        Message message;
        object route;
        if (walkerId is { } wId)
        {
            var walker = await db.Walkers.FindAsync(wId);
            if (walker is null || !walker.IsActive || walker.CustomerId == me)
                return NotFound();
            message = new Message { CustomerId = me, WalkerId = wId, SentByCustomer = true };
            route = new { walkerId = wId };
        }
        else if (customerId is { } cId && await MyWalkerIdAsync(me) is { } myWalkerId)
        {
            if (!await db.Messages.AnyAsync(m => m.CustomerId == cId && m.WalkerId == myWalkerId)
                && !await db.Walks.AnyAsync(w => w.CustomerId == cId && w.WalkerId == myWalkerId))
                return NotFound();
            message = new Message { CustomerId = cId, WalkerId = myWalkerId, SentByCustomer = false };
            route = new { customerId = cId };
        }
        else
        {
            return NotFound();
        }

        if (text.Length is 0 or > 1000)
        {
            TempData["Error"] = "Mesaj 1 ile 1000 karakter arasında olmalı.";
            return RedirectToAction(nameof(Index), route);
        }

        message.Text = text;
        message.SentAtUtc = DateTime.UtcNow;
        db.Messages.Add(message);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), route);
    }

    // Deletes the whole conversation with a walker (walkerId) or, as a walker, with a customer (customerId).
    [HttpPost]
    public async Task<IActionResult> Delete(int? walkerId, int? customerId)
    {
        var me = User.GetCustomerId();
        IQueryable<Message> conversation;
        if (walkerId is { } wId)
            conversation = db.Messages.Where(m => m.CustomerId == me && m.WalkerId == wId);
        else if (customerId is { } cId && await MyWalkerIdAsync(me) is { } myWalkerId)
            conversation = db.Messages.Where(m => m.CustomerId == cId && m.WalkerId == myWalkerId);
        else
            return NotFound();

        var deleted = await conversation.ExecuteDeleteAsync();
        TempData["Success"] = deleted > 0 ? "Sohbet silindi." : "Bu kişiyle silinecek mesaj yok.";
        return RedirectToAction(nameof(Index));
    }

    private Task<int?> MyWalkerIdAsync(int customerId) =>
        db.Walkers.Where(w => w.CustomerId == customerId).Select(w => (int?)w.Id).FirstOrDefaultAsync();

    private async Task<IReadOnlyList<Message>> LoadAsync(int customerId, int walkerId) =>
        await db.Messages.AsNoTracking()
            .Where(m => m.CustomerId == customerId && m.WalkerId == walkerId)
            .OrderBy(m => m.SentAtUtc)
            .ToListAsync();
}
