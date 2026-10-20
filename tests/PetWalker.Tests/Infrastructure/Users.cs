using PetWalker.Models;

namespace PetWalker.Tests.Infrastructure;

// Common steps: sign up, sign in, add a pet, become a walker.
public static class Users
{
    public const string Password = "Passw0rd!";
    private static int _counter;

    public static string NewEmail(string name = "user") => $"{name}{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@test.local";

    public static Dictionary<string, string> SignUpForm(string email, string firstName = "Test", string phone = "05551112233") => new()
    {
        ["FirstName"] = firstName,
        ["LastName"] = "Kullanıcı",
        ["DateOfBirth"] = "15/05/1995",
        ["Gender"] = nameof(Gender.Female),
        ["Address"] = "Kadıköy, İstanbul",
        ["Email"] = email,
        ["Phone"] = phone,
        ["Password"] = Password,
        ["AcceptKvkk"] = "true",
    };

    public static async Task<(TestClient Client, int CustomerId, string Email)> SignUpAsync(PetWalkerFactory factory, string firstName = "Test")
    {
        var client = factory.CreateTestClient();
        var email = NewEmail(firstName.ToLowerInvariant());
        var page = await client.PostAsync("/Account/SignUp", SignUpForm(email, firstName));
        if (page.Path != "/Account/Profile")
            throw new InvalidOperationException($"Sign-up failed, ended on {page.Path}");
        var id = await factory.WithDbAsync(db => Task.FromResult(db.Customers.Single(c => c.Email == email).Id));
        return (client, id, email);
    }

    public static async Task<int> AddPetAsync(PetWalkerFactory factory, TestClient client, int ownerId, string name = "Boncuk")
    {
        await client.PostAsync("/Pets/Create", new Dictionary<string, string>
        {
            ["PetForm.Name"] = name,
            ["PetForm.Type"] = nameof(PetType.Dog),
            ["PetForm.Breed"] = "Beagle",
            ["PetForm.Sex"] = nameof(PetSex.Male),
            ["PetForm.AgeYears"] = "3",
            ["PetForm.WeightKg"] = "12.5",
            ["PetForm.Color"] = "Kahverengi",
        }, formPage: "/Account/Profile");
        return await factory.WithDbAsync(db => Task.FromResult(db.Pets.Single(p => p.OwnerId == ownerId && p.Name == name).Id));
    }

    public static async Task<int> BecomeWalkerAsync(PetWalkerFactory factory, TestClient client, int customerId,
        int price = 100, string durations = "30,60,120")
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("WalkerForm.ExperienceYears", "4"),
            new("WalkerForm.PricePer30Min", price.ToString()),
            new("WalkerForm.Bio", "Hayvanları çok seviyorum, hafta içi müsaitim."),
        };
        // Durations are several checkboxes with the same name, so they are sent as repeated fields.
        var form = fields.Concat(durations.Split(',').Select(d => new KeyValuePair<string, string>("WalkerForm.Durations", d)));
        await PostRepeatedAsync(client, "/Account/BecomeWalker", form, "/Account/Profile");
        return await factory.WithDbAsync(db => Task.FromResult(db.Walkers.Single(w => w.CustomerId == customerId).Id));
    }

    public static async Task<Page> PostRepeatedAsync(TestClient client, string url, IEnumerable<KeyValuePair<string, string>> fields, string formPage)
    {
        var html = await client.Http.GetStringAsync(formPage);
        var token = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var all = fields.Append(new("__RequestVerificationToken", token));
        var response = await client.Http.PostAsync(url, new FormUrlEncodedContent(all));
        return new Page(response, await response.Content.ReadAsStringAsync());
    }
}
