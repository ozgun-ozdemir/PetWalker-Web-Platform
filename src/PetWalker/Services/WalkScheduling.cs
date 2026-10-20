using Microsoft.EntityFrameworkCore;
using PetWalker.Data;
using PetWalker.Models;

namespace PetWalker.Services;

// Conflict checks for bookings: a walker (or a pet) can't be in two walks at the same time.
public static class WalkScheduling
{
    public static Task<bool> WalkerIsBusyAsync(PetWalkerDbContext db, int walkerId, DateTime start, int minutes,
        int? ignoreWalkId = null, bool acceptedOnly = false) =>
        OverlapsAsync(db.Walks.Where(w => w.WalkerId == walkerId), start, minutes, ignoreWalkId, acceptedOnly);

    public static Task<bool> PetIsBusyAsync(PetWalkerDbContext db, int petId, DateTime start, int minutes) =>
        OverlapsAsync(db.Walks.Where(w => w.PetId == petId), start, minutes, null, false);

    private static async Task<bool> OverlapsAsync(IQueryable<Walk> walks, DateTime start, int minutes,
        int? ignoreWalkId, bool acceptedOnly)
    {
        var end = start.AddMinutes(minutes);

        // Narrow down in SQL (no walk is longer than a day), then compare exact times in memory,
        // because SQLite can't add minutes to a date inside a query.
        var nearby = await walks
            .Where(w => w.StartsAt < end && w.StartsAt > start.AddDays(-1))
            .Where(w => w.Id != ignoreWalkId)
            .Where(w => acceptedOnly
                ? w.Status == WalkStatus.Accepted
                : w.Status == WalkStatus.Pending || w.Status == WalkStatus.Accepted)
            .ToListAsync();

        return nearby.Any(w => w.StartsAt < end && w.EndsAt > start);
    }
}
