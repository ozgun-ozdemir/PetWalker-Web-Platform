using PetWalker.Models;
using PetWalker.Tests.Infrastructure;

namespace PetWalker.Tests;

// Owner books → walker accepts/rejects → walk is completed → owner rates it.
public class BookingTests(PetWalkerFactory factory) : IClassFixture<PetWalkerFactory>
{
    private static string At(DateTime t) => t.ToString("yyyy-MM-dd HH:mm");
    private static DateTime Tomorrow(int hour) => DateTime.Today.AddDays(1).AddHours(hour);

    private record Setup(TestClient Owner, int OwnerId, int PetId, TestClient Walker, int WalkerCustomerId, int WalkerId);

    private async Task<Setup> OwnerAndWalkerAsync(int price = 100, string durations = "30,60,120")
    {
        var (owner, ownerId, _) = await Users.SignUpAsync(factory, "Sahip");
        var petId = await Users.AddPetAsync(factory, owner, ownerId);
        var (walker, walkerCustomerId, _) = await Users.SignUpAsync(factory, "Gezdirici");
        var walkerId = await Users.BecomeWalkerAsync(factory, walker, walkerCustomerId, price, durations);
        return new(owner, ownerId, petId, walker, walkerCustomerId, walkerId);
    }

    private static Task<Page> BookAsync(Setup s, DateTime start, int minutes, int? petId = null, string? formPage = null) =>
        s.Owner.PostAsync("/Walkers/Book", new Dictionary<string, string>
        {
            ["WalkerId"] = s.WalkerId.ToString(),
            ["PetId"] = (petId ?? s.PetId).ToString(),
            ["StartsAt"] = At(start),
            ["DurationMinutes"] = minutes.ToString(),
        }, formPage: formPage ?? $"/Walkers/Details/{s.WalkerId}");

    private Task<Walk> WalkAsync(int walkerId, DateTime start) =>
        factory.WithDbAsync(db => Task.FromResult(db.Walks.Single(w => w.WalkerId == walkerId && w.StartsAt == start)));

    [Fact]
    public async Task Booking_creates_a_pending_request_priced_on_the_server()
    {
        var s = await OwnerAndWalkerAsync(price: 150);

        var page = await BookAsync(s, Tomorrow(10), 60);

        Assert.Equal("/Walks", page.Path);
        var walk = await WalkAsync(s.WalkerId, Tomorrow(10));
        Assert.Equal(WalkStatus.Pending, walk.Status);
        Assert.Equal(300m, walk.Price); // 150 ₺ per 30 min × 60 min
        var walkerView = await s.Walker.GetAsync("/Walks");
        Assert.True(walkerView.Contains("1 yeni istek"));
    }

    [Fact]
    public async Task Booking_is_refused_for_bad_input()
    {
        var s = await OwnerAndWalkerAsync(durations: "30,60");
        var (stranger, strangerId, _) = await Users.SignUpAsync(factory, "Yabanci");
        var strangersPet = await Users.AddPetAsync(factory, stranger, strangerId, "Pamuk");

        Assert.True((await BookAsync(s, DateTime.Now.AddHours(-2), 30)).Contains("Yürüyüş tarihi gelecekte olmalı."));
        Assert.True((await BookAsync(s, Tomorrow(10), 120)).Contains("Bu gezdirici bu süreyi sunmuyor."));
        Assert.True((await BookAsync(s, Tomorrow(10), 30, strangersPet)).Contains("Bu evcil hayvan size ait değil."));
        Assert.Equal(0, await factory.WithDbAsync(db => Task.FromResult(db.Walks.Count(w => w.WalkerId == s.WalkerId))));
    }

    [Fact]
    public async Task A_walker_cannot_book_themselves()
    {
        var (client, id, _) = await Users.SignUpAsync(factory);
        var petId = await Users.AddPetAsync(factory, client, id);
        var walkerId = await Users.BecomeWalkerAsync(factory, client, id);

        var page = await client.PostAsync("/Walkers/Book", new Dictionary<string, string>
        {
            ["WalkerId"] = walkerId.ToString(), ["PetId"] = petId.ToString(),
            ["StartsAt"] = At(Tomorrow(9)), ["DurationMinutes"] = "30",
        }, formPage: "/Account/Profile");

        Assert.True(page.Contains("Kendine yürüyüş planlayamazsın."));
    }

    [Fact]
    public async Task Overlapping_bookings_are_refused_but_back_to_back_is_fine()
    {
        var s = await OwnerAndWalkerAsync();
        var (other, otherId, _) = await Users.SignUpAsync(factory, "Diger");
        var otherPet = await Users.AddPetAsync(factory, other, otherId, "Duman");
        var otherSetup = s with { Owner = other, OwnerId = otherId, PetId = otherPet };

        await BookAsync(s, Tomorrow(10), 60);                          // 10:00–11:00
        var overlap = await BookAsync(otherSetup, Tomorrow(10).AddMinutes(30), 60); // 10:30–11:30
        Assert.True(overlap.Contains("Gezdiricinin bu saatte başka bir yürüyüşü var."));

        var samePet = await BookAsync(s, Tomorrow(10).AddMinutes(30), 30);
        Assert.True(samePet.Contains("başka bir yürüyüşü var"));

        var afterwards = await BookAsync(otherSetup, Tomorrow(11), 30);  // 11:00–11:30, right after
        Assert.Equal("/Walks", afterwards.Path);
        Assert.Equal(WalkStatus.Pending, (await WalkAsync(s.WalkerId, Tomorrow(11))).Status);
    }

    [Fact]
    public async Task Walker_accepts_or_rejects_requests_and_others_cannot()
    {
        var s = await OwnerAndWalkerAsync();
        await BookAsync(s, Tomorrow(9), 30);
        await BookAsync(s, Tomorrow(14), 30);
        var first = await WalkAsync(s.WalkerId, Tomorrow(9));
        var second = await WalkAsync(s.WalkerId, Tomorrow(14));

        // The owner can't accept their own request: it isn't a request to them.
        var byOwner = await s.Owner.PostAsync($"/Walks/Accept/{first.Id}", formPage: "/Walks");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, byOwner.Status);

        await s.Walker.PostAsync($"/Walks/Accept/{first.Id}", formPage: "/Walks");
        await s.Walker.PostAsync($"/Walks/Reject/{second.Id}", formPage: "/Walks");

        Assert.Equal(WalkStatus.Accepted, (await WalkAsync(s.WalkerId, Tomorrow(9))).Status);
        Assert.Equal(WalkStatus.Rejected, (await WalkAsync(s.WalkerId, Tomorrow(14))).Status);
    }

    [Fact]
    public async Task Owner_and_walker_can_cancel_upcoming_walks()
    {
        var s = await OwnerAndWalkerAsync();
        await BookAsync(s, Tomorrow(8), 30);
        await BookAsync(s, Tomorrow(16), 30);
        var byOwner = await WalkAsync(s.WalkerId, Tomorrow(8));
        var byWalker = await WalkAsync(s.WalkerId, Tomorrow(16));
        await s.Walker.PostAsync($"/Walks/Accept/{byWalker.Id}", formPage: "/Walks");

        await s.Owner.PostAsync($"/Walks/Cancel/{byOwner.Id}", formPage: "/Walks");
        await s.Walker.PostAsync($"/Walks/WalkerCancel/{byWalker.Id}", formPage: "/Walks");

        Assert.Equal(WalkStatus.Cancelled, (await WalkAsync(s.WalkerId, Tomorrow(8))).Status);
        Assert.Equal(WalkStatus.Cancelled, (await WalkAsync(s.WalkerId, Tomorrow(16))).Status);
    }

    [Fact]
    public async Task Cancelled_walks_can_be_deleted_but_active_ones_cannot()
    {
        var s = await OwnerAndWalkerAsync();
        await BookAsync(s, Tomorrow(12), 30);
        var walk = await WalkAsync(s.WalkerId, Tomorrow(12));

        var tooEarly = await s.Owner.PostAsync($"/Walks/Delete/{walk.Id}", formPage: "/Walks");
        Assert.True(tooEarly.Contains("Yalnızca iptal edilen yürüyüşler silinebilir."));

        await s.Owner.PostAsync($"/Walks/Cancel/{walk.Id}", formPage: "/Walks");
        await s.Owner.PostAsync($"/Walks/Delete/{walk.Id}", formPage: "/Walks");
        Assert.False(await factory.WithDbAsync(db => Task.FromResult(db.Walks.Any(w => w.Id == walk.Id))));
    }

    [Fact]
    public async Task Unanswered_requests_whose_time_passed_can_be_deleted()
    {
        var s = await OwnerAndWalkerAsync();
        var past = DateTime.Today.AddDays(-2).AddHours(10);
        var id = await factory.WithDbAsync(async db =>
        {
            var w = new Walk { CustomerId = s.OwnerId, PetId = s.PetId, WalkerId = s.WalkerId, StartsAt = past, DurationMinutes = 30, Price = 100 };
            db.Walks.Add(w);
            await db.SaveChangesAsync();
            return w.Id;
        });

        await s.Owner.PostAsync($"/Walks/Delete/{id}", formPage: "/Walks");

        Assert.False(await factory.WithDbAsync(db => Task.FromResult(db.Walks.Any(w => w.Id == id))));
    }

    [Fact]
    public async Task Completing_a_walk_allows_one_rating_which_shows_on_the_walker()
    {
        var s = await OwnerAndWalkerAsync();
        await BookAsync(s, Tomorrow(18), 30);
        var walk = await WalkAsync(s.WalkerId, Tomorrow(18));

        // Not accepted yet: can't be completed or rated.
        var early = await s.Owner.PostAsync("/Walks/Rate", new Dictionary<string, string> { ["WalkId"] = walk.Id.ToString(), ["Rating"] = "5" }, "/Walks");
        Assert.True(early.Contains("Yalnızca tamamlanan yürüyüşler puanlanabilir."));

        await s.Walker.PostAsync($"/Walks/Accept/{walk.Id}", formPage: "/Walks");
        await s.Owner.PostAsync($"/Walks/Complete/{walk.Id}", formPage: "/Walks");
        Assert.Equal(WalkStatus.Completed, (await WalkAsync(s.WalkerId, Tomorrow(18))).Status);

        await s.Owner.PostAsync("/Walks/Rate", new Dictionary<string, string>
            { ["WalkId"] = walk.Id.ToString(), ["Rating"] = "4", ["Review"] = "Çok iyiydi" }, "/Walks");
        var again = await s.Owner.PostAsync("/Walks/Rate", new Dictionary<string, string>
            { ["WalkId"] = walk.Id.ToString(), ["Rating"] = "1" }, "/Walks");
        Assert.True(again.Contains("Bu yürüyüşü zaten puanladın."));

        var rated = await WalkAsync(s.WalkerId, Tomorrow(18));
        Assert.Equal(4, rated.Rating);
        var profile = await s.Owner.GetAsync($"/Walkers/Details/{s.WalkerId}");
        Assert.True(profile.Contains("(1 değerlendirme)"));
        Assert.True(profile.Contains("Çok iyiydi"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    public async Task Ratings_must_be_between_1_and_5(string rating)
    {
        var s = await OwnerAndWalkerAsync();
        await BookAsync(s, Tomorrow(7), 30);
        var walk = await WalkAsync(s.WalkerId, Tomorrow(7));
        await s.Walker.PostAsync($"/Walks/Accept/{walk.Id}", formPage: "/Walks");
        await s.Owner.PostAsync($"/Walks/Complete/{walk.Id}", formPage: "/Walks");

        var page = await s.Owner.PostAsync("/Walks/Rate", new Dictionary<string, string> { ["WalkId"] = walk.Id.ToString(), ["Rating"] = rating }, "/Walks");

        Assert.True(page.Contains("Puan 1 ile 5 arasında olmalı."));
        Assert.Null((await WalkAsync(s.WalkerId, Tomorrow(7))).Rating);
    }

    [Fact]
    public async Task Leaving_the_walker_list_cancels_open_walks_and_hides_the_walker()
    {
        var s = await OwnerAndWalkerAsync();
        await BookAsync(s, Tomorrow(9), 30);
        await BookAsync(s, Tomorrow(15), 30);
        var accepted = await WalkAsync(s.WalkerId, Tomorrow(15));
        await s.Walker.PostAsync($"/Walks/Accept/{accepted.Id}", formPage: "/Walks");

        await s.Walker.PostAsync("/Account/LeaveWalker", formPage: "/Account/Profile");

        Assert.Equal(WalkStatus.Rejected, (await WalkAsync(s.WalkerId, Tomorrow(9))).Status);
        Assert.Equal(WalkStatus.Cancelled, (await WalkAsync(s.WalkerId, Tomorrow(15))).Status);
        var search = await s.Owner.GetAsync("/Walkers");
        Assert.False(search.Contains($"/Walkers/Details/{s.WalkerId}\""));
        var profile = await s.Owner.GetAsync($"/Walkers/Details/{s.WalkerId}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, profile.Status);
        var book = await BookAsync(s, Tomorrow(17), 30, formPage: "/Walks");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, book.Status);
    }
}
