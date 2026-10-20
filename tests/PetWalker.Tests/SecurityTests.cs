using System.Net;
using System.Net.Http.Headers;
using PetWalker.Tests.Infrastructure;

namespace PetWalker.Tests;

public class SecurityTests(PetWalkerFactory factory) : IClassFixture<PetWalkerFactory>
{
    [Fact]
    public async Task Forms_without_an_anti_forgery_token_are_rejected()
    {
        var (client, _, _) = await Users.SignUpAsync(factory);

        var response = await client.Http.PostAsync("/Chat/Send", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["walkerId"] = "1", ["text"] = "csrf",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Passwords_are_stored_hashed()
    {
        var (_, id, _) = await Users.SignUpAsync(factory);

        var hash = await factory.WithDbAsync(db => Task.FromResult(db.Customers.Single(c => c.Id == id).PasswordHash));

        Assert.DoesNotContain(Users.Password, hash);
        Assert.True(hash.Length > 40);
    }

    [Fact]
    public async Task Users_cannot_touch_other_users_pets_or_walks()
    {
        var (owner, ownerId, _) = await Users.SignUpAsync(factory);
        var petId = await Users.AddPetAsync(factory, owner, ownerId);
        var (stranger, _, _) = await Users.SignUpAsync(factory);

        var deletePet = await stranger.PostAsync($"/Pets/Delete/{petId}", formPage: "/Account/Profile");

        Assert.Equal(HttpStatusCode.NotFound, deletePet.Status);
        Assert.True(await factory.WithDbAsync(db => Task.FromResult(db.Pets.Any(p => p.Id == petId))));
    }

    [Fact]
    public async Task Owners_can_edit_their_pets_but_not_others()
    {
        var (owner, ownerId, _) = await Users.SignUpAsync(factory);
        var petId = await Users.AddPetAsync(factory, owner, ownerId, "Boncuk");
        var form = new Dictionary<string, string>
        {
            ["PetForm.Name"] = "Boncuk'un Kardeşi", ["PetForm.Type"] = "Cat", ["PetForm.Breed"] = "Tekir",
            ["PetForm.Sex"] = "Female", ["PetForm.AgeYears"] = "2", ["PetForm.WeightKg"] = "4.5", ["PetForm.Color"] = "Gri",
        };

        var (stranger, _, _) = await Users.SignUpAsync(factory);
        var byStranger = await stranger.PostAsync($"/Pets/Edit/{petId}", form, "/Account/Profile");
        Assert.Equal(HttpStatusCode.NotFound, byStranger.Status);

        var page = await owner.PostAsync($"/Pets/Edit/{petId}", form, "/Account/Profile");
        Assert.True(page.Contains("Boncuk'un Kardeşi güncellendi."));
        var pet = await factory.WithDbAsync(db => Task.FromResult(db.Pets.Single(p => p.Id == petId)));
        Assert.Equal(PetWalker.Models.PetType.Cat, pet.Type);
        Assert.Equal(4.5, pet.WeightKg);
        // A name with an apostrophe must not end up inside a script.
        Assert.DoesNotContain("confirm('Emin misiniz? Boncuk'un", page.RawHtml);
    }

    [Fact]
    public async Task Sign_in_ignores_return_urls_to_other_sites()
    {
        var (_, _, email) = await Users.SignUpAsync(factory);

        var page = await factory.CreateTestClient().PostAsync("/Account/SignIn", new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = Users.Password, ["ReturnUrl"] = "https://evil.example/steal",
        });

        Assert.Equal("localhost", page.Response.RequestMessage!.RequestUri!.Host);
        Assert.Equal("/", page.Path);
    }

    [Fact]
    public async Task Photo_upload_accepts_images_only()
    {
        var (client, id, _) = await Users.SignUpAsync(factory);
        var html = await client.Http.GetStringAsync("/Account/Profile");
        var token = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

        async Task<Page> Upload(byte[] bytes, string name)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(token), "__RequestVerificationToken");
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "photo", name);
            var r = await client.Http.PostAsync("/Account/Photo", form);
            return new Page(r, await r.Content.ReadAsStringAsync());
        }

        // A text file renamed to .png is refused.
        var fake = await Upload("not an image"u8.ToArray(), "evil.png");
        Assert.True(fake.Contains("Yalnızca JPG, PNG veya WEBP"));

        // A real (tiny) PNG is accepted.
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];
        var ok = await Upload(png, "me.png");
        Assert.True(ok.Contains("Profil fotoğrafın güncellendi."));
        var path = await factory.WithDbAsync(db => Task.FromResult(db.Customers.Single(c => c.Id == id).PhotoPath));
        Assert.StartsWith("/uploads/profiles/", path);
    }

    [Fact]
    public async Task Contact_form_requires_kvkk_and_saves_the_message()
    {
        var client = factory.CreateTestClient();
        var form = new Dictionary<string, string>
        {
            ["FullName"] = "Ziyaretçi", ["Subject"] = "Soru", ["Email"] = "z@test.local", ["Body"] = "Merhaba, bir sorum var.",
        };

        var withoutKvkk = await client.PostAsync("/Home/Contact", form);
        Assert.True(withoutKvkk.Contains("KVKK koşullarını kabul etmelisiniz."));

        form["AcceptKvkk"] = "true";
        var sent = await client.PostAsync("/Home/Contact", form);
        Assert.True(sent.Contains("Mesajın bize ulaştı."));
        Assert.True(await factory.WithDbAsync(db => Task.FromResult(db.ContactMessages.Any(m => m.Email == "z@test.local"))));
    }

    [Fact]
    public async Task Unknown_pages_show_the_turkish_404_page()
    {
        var page = await factory.CreateTestClient().GetAsync("/bu-sayfa-yok");

        Assert.Equal(HttpStatusCode.NotFound, page.Status);
        Assert.True(page.Contains("Sayfa bulunamadı"));
    }
}
