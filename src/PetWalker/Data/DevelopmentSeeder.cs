using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PetWalker.Models;

namespace PetWalker.Data;

// Creates demo accounts in Development so the app can be tried right away:
// a pet owner, and a user who is also a walker (to try the walker side of messaging and bookings).
public static class DevelopmentSeeder
{
    public const string DemoEmail = "demo@petwalker.test";
    public const string DemoWalkerEmail = "gezdirici@petwalker.test";
    public const string DemoPassword = "Demo1234!";

    public static async Task SeedAsync(PetWalkerDbContext db, IPasswordHasher<Customer> hasher)
    {
        if (!await db.Customers.AnyAsync(c => c.Email == DemoEmail))
        {
            var demo = NewCustomer(hasher, "Demo", "Kullanıcı", DemoEmail, "05550000000", Gender.Female, new DateOnly(2000, 1, 1));
            demo.Pets.Add(new Pet { Name = "Karabaş", Type = PetType.Dog, Sex = PetSex.Male, Breed = "Kangal", Color = "Krem", AgeYears = 3, WeightKg = 45 });
            db.Customers.Add(demo);
        }

        if (!await db.Customers.AnyAsync(c => c.Email == DemoWalkerEmail))
        {
            var walkerUser = NewCustomer(hasher, "Elif", "Şahin", DemoWalkerEmail, "05550000001", Gender.Female, new DateOnly(1997, 3, 8));
            walkerUser.WalkerProfile = new Walker
            {
                FirstName = walkerUser.FirstName,
                LastName = walkerUser.LastName,
                Address = walkerUser.Address,
                Phone = walkerUser.Phone,
                DateOfBirth = walkerUser.DateOfBirth,
                Gender = walkerUser.Gender,
                ExperienceYears = 3,
                PricePer30Min = 130,
                Bio = "Demo gezdirici hesabı. Hafta sonları müsaitim.",
            };
            db.Customers.Add(walkerUser);
        }

        await db.SaveChangesAsync();
        await SeedSampleReviewsAsync(db, hasher);
    }

    // Sample owners with finished, rated walks, so some walkers have reviews and others have none yet.
    private static async Task SeedSampleReviewsAsync(PetWalkerDbContext db, IPasswordHasher<Customer> hasher)
    {
        const string reviewerEmail = "yorumcu1@petwalker.test";
        if (await db.Customers.AnyAsync(c => c.Email == reviewerEmail))
            return;

        var selin = NewCustomer(hasher, "Selin", "Arslan", reviewerEmail, "05550000002", Gender.Female, new DateOnly(1994, 5, 2));
        var burak = NewCustomer(hasher, "Burak", "Koç", "yorumcu2@petwalker.test", "05550000003", Gender.Male, new DateOnly(1989, 11, 21));
        var zeytin = new Pet { Name = "Zeytin", Type = PetType.Dog, Sex = PetSex.Female, Breed = "Golden Retriever", Color = "Sarı", AgeYears = 4, WeightKg = 30 };
        var duman = new Pet { Name = "Duman", Type = PetType.Dog, Sex = PetSex.Male, Breed = "Husky", Color = "Gri", AgeYears = 2, WeightKg = 24 };
        selin.Pets.Add(zeytin);
        burak.Pets.Add(duman);
        db.Customers.AddRange(selin, burak);

        var day = DateTime.Today;
        // (walker id, owner, pet, days ago, rating, review). Walker 3 (Zeynep) is left without reviews on purpose.
        var reviews = new (int WalkerId, Customer Owner, Pet Pet, int DaysAgo, int Rating, string Review)[]
        {
            (1, selin, zeytin, 20, 5, "Zeytin'le çok iyi anlaştı, yürüyüş sonrası fotoğraf da gönderdi."),
            (1, burak, duman, 12, 4, "Dakik ve ilgili. Tekrar çalışırım."),
            (2, selin, zeytin, 9, 5, "Çok deneyimli, içim rahat etti."),
            (4, burak, duman, 6, 3, "İyiydi ama biraz geç geldi."),
            (4, selin, zeytin, 3, 4, "Sahil yürüyüşü harikaydı."),
        };
        foreach (var r in reviews)
        {
            var walker = await db.Walkers.FindAsync(r.WalkerId);
            if (walker is null)
                continue;
            db.Walks.Add(new Walk
            {
                Customer = r.Owner,
                Pet = r.Pet,
                WalkerId = walker.Id,
                StartsAt = day.AddDays(-r.DaysAgo).AddHours(10),
                DurationMinutes = 60,
                Price = walker.PricePer30Min * 2,
                Status = WalkStatus.Accepted,
                Rating = r.Rating,
                Review = r.Review,
            });
        }
        await db.SaveChangesAsync();
    }

    private static Customer NewCustomer(IPasswordHasher<Customer> hasher, string first, string last, string email,
        string phone, Gender gender, DateOnly dob)
    {
        var c = new Customer
        {
            FirstName = first,
            LastName = last,
            Email = email,
            Phone = phone,
            Gender = gender,
            DateOfBirth = dob,
            Address = "Ümraniye, İstanbul",
        };
        c.PasswordHash = hasher.HashPassword(c, DemoPassword);
        return c;
    }
}
