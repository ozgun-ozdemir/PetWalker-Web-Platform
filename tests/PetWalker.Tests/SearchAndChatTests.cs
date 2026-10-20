using System.Net;
using PetWalker.Models;
using PetWalker.Tests.Infrastructure;

namespace PetWalker.Tests;

public class SearchAndChatTests(PetWalkerFactory factory) : IClassFixture<PetWalkerFactory>
{
    [Fact]
    public async Task Search_filters_by_district_and_sorts_by_price()
    {
        var (client, _, _) = await Users.SignUpAsync(factory);

        // Seeded walkers: Ayşe (Kadıköy, 150), Can (Kadıköy, 140), Mehmet (Ümraniye, 200), Zeynep (Beşiktaş, 120).
        var kadikoy = await client.GetAsync("/Walkers?Location=Kad%C4%B1k%C3%B6y&Sort=price-asc");
        Assert.True(kadikoy.Contains("Can Öztürk"));
        Assert.True(kadikoy.Contains("Ayşe Yılmaz"));
        Assert.False(kadikoy.Contains("Mehmet Kaya"));
        Assert.True(kadikoy.Html.IndexOf("Can Öztürk") < kadikoy.Html.IndexOf("Ayşe Yılmaz"), "cheapest first");

        var experienced = await client.GetAsync("/Walkers?MinExperience=8");
        Assert.True(experienced.Contains("Mehmet Kaya"));
        Assert.False(experienced.Contains("Zeynep Demir"));
    }

    [Fact]
    public async Task Becoming_a_walker_lists_the_user_in_search()
    {
        var (client, id, _) = await Users.SignUpAsync(factory, "Yenigezdirici");
        await Users.BecomeWalkerAsync(factory, client, id);

        var (other, _, _) = await Users.SignUpAsync(factory);
        var page = await other.GetAsync("/Walkers?Location=Kad%C4%B1k%C3%B6y");

        Assert.True(page.Contains("Yenigezdirici Kullanıcı"));
    }

    [Fact]
    public async Task Owner_and_walker_can_message_each_other()
    {
        var (owner, ownerId, _) = await Users.SignUpAsync(factory, "Mesajci");
        var (walker, walkerCustomerId, _) = await Users.SignUpAsync(factory, "Cevapci");
        var walkerId = await Users.BecomeWalkerAsync(factory, walker, walkerCustomerId);

        await owner.PostAsync("/Chat/Send", new Dictionary<string, string> { ["walkerId"] = walkerId.ToString(), ["text"] = "Merhaba, yarın müsait misin?" }, "/Chat");
        var walkerInbox = await walker.GetAsync($"/Chat?customerId={ownerId}");
        Assert.True(walkerInbox.Contains("Merhaba, yarın müsait misin?"));

        await walker.PostAsync("/Chat/Send", new Dictionary<string, string> { ["customerId"] = ownerId.ToString(), ["text"] = "Evet, 10:00 uygun." }, "/Chat");
        var ownerInbox = await owner.GetAsync($"/Chat?walkerId={walkerId}");
        Assert.True(ownerInbox.Contains("Evet, 10:00 uygun."));
    }

    [Fact]
    public async Task Messages_are_private_and_escaped()
    {
        var (owner, ownerId, _) = await Users.SignUpAsync(factory, "Gizli");
        await owner.PostAsync("/Chat/Send", new Dictionary<string, string> { ["walkerId"] = "1", ["text"] = "<script>alert(1)</script>" }, "/Chat");

        var mine = await owner.GetAsync("/Chat?walkerId=1");
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", mine.RawHtml);

        // Another user doesn't see this conversation...
        var (other, _, _) = await Users.SignUpAsync(factory, "Meraklı");
        var theirs = await other.GetAsync("/Chat?walkerId=1");
        Assert.DoesNotContain("alert(1)", theirs.RawHtml);

        // ...and can't write "as the walker" to a customer who never contacted them.
        var asWalker = await other.PostAsync("/Chat/Send", new Dictionary<string, string> { ["customerId"] = ownerId.ToString(), ["text"] = "x" }, "/Chat");
        Assert.Equal(HttpStatusCode.NotFound, asWalker.Status);
    }

    [Fact]
    public async Task Deleting_a_conversation_removes_it_from_the_list()
    {
        var (owner, ownerId, _) = await Users.SignUpAsync(factory, "Silen");
        await owner.PostAsync("/Chat/Send", new Dictionary<string, string> { ["walkerId"] = "2", ["text"] = "silinecek mesaj" }, "/Chat");
        Assert.True((await owner.GetAsync("/Chat")).Contains("Mehmet Kaya"));

        await owner.PostAsync("/Chat/Delete", new Dictionary<string, string> { ["walkerId"] = "2" }, "/Chat");

        Assert.False(await factory.WithDbAsync(db => Task.FromResult(db.Messages.Any(m => m.CustomerId == ownerId))));
        Assert.True((await owner.GetAsync("/Chat")).Contains("Henüz bir sohbetin yok."));
    }

    [Fact]
    public async Task Conversations_are_listed_newest_first()
    {
        var (owner, _, _) = await Users.SignUpAsync(factory, "Sirali");
        async Task Send(int walkerId) =>
            await owner.PostAsync("/Chat/Send", new Dictionary<string, string> { ["walkerId"] = walkerId.ToString(), ["text"] = "selam" }, "/Chat");

        await Send(1); // Ayşe
        await Send(3); // Zeynep
        await Send(2); // Mehmet, last

        var page = await owner.GetAsync("/Chat");
        var mehmet = page.Html.IndexOf("Mehmet Kaya");
        var zeynep = page.Html.IndexOf("Zeynep Demir");
        var ayse = page.Html.IndexOf("Ayşe Yılmaz");
        Assert.True(mehmet < zeynep && zeynep < ayse, "newest conversation should be on top");
    }

    [Fact]
    public async Task Empty_or_too_long_messages_are_refused()
    {
        var (owner, ownerId, _) = await Users.SignUpAsync(factory);

        await owner.PostAsync("/Chat/Send", new Dictionary<string, string> { ["walkerId"] = "3", ["text"] = "   " }, "/Chat");
        await owner.PostAsync("/Chat/Send", new Dictionary<string, string> { ["walkerId"] = "3", ["text"] = new string('a', 1001) }, "/Chat");

        Assert.False(await factory.WithDbAsync(db => Task.FromResult(db.Messages.Any(m => m.CustomerId == ownerId))));
    }
}
