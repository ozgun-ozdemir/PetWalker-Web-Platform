using PetWalker.Tests.Infrastructure;

namespace PetWalker.Tests;

public class AccountTests(PetWalkerFactory factory) : IClassFixture<PetWalkerFactory>
{
    [Fact]
    public async Task SignUp_with_valid_data_signs_the_user_in()
    {
        var (client, _, _) = await Users.SignUpAsync(factory, "Ayla");

        var profile = await client.GetAsync("/Account/Profile");
        Assert.Equal("/Account/Profile", profile.Path);
        Assert.True(profile.Contains("Ayla Kullanıcı"));
        Assert.True(profile.Contains("Kadıköy, İstanbul"));
    }

    [Theory]
    [InlineData("Phone", "5551112233", "Telefon 0 ile başlamalı ve 11 haneli olmalı.")]
    [InlineData("Phone", "0555111223a", "Telefon 0 ile başlamalı ve 11 haneli olmalı.")]
    [InlineData("Email", "not-an-email", "Geçerli bir e-posta girin.")]
    [InlineData("AcceptKvkk", "false", "KVKK koşullarını kabul etmelisiniz.")]
    [InlineData("DateOfBirth", "15/05/2015", "18 yaşından büyük olmalısınız.")]
    [InlineData("DateOfBirth", "31/02/1990", "Geçerli bir tarih girin")]
    [InlineData("Address", "Bornova, İzmir", "Listeden bir ilçe seçin.")]
    [InlineData("Password", "kisa", "Şifre en az 8 karakter olmalı.")]
    public async Task SignUp_rejects_invalid_fields(string field, string value, string expectedError)
    {
        var client = factory.CreateTestClient();
        var form = Users.SignUpForm(Users.NewEmail());
        form[field] = value;

        var page = await client.PostAsync("/Account/SignUp", form);

        Assert.Equal("/Account/SignUp", page.Path);
        Assert.True(page.Contains(expectedError), $"Expected '{expectedError}'");
    }

    [Fact]
    public async Task SignUp_rejects_an_email_that_is_already_used()
    {
        var (_, _, email) = await Users.SignUpAsync(factory);

        var page = await factory.CreateTestClient().PostAsync("/Account/SignUp", Users.SignUpForm(email.ToUpperInvariant()));

        Assert.True(page.Contains("Bu e-posta ile kayıtlı bir hesap zaten var."));
    }

    [Fact]
    public async Task SignIn_works_only_with_the_right_password()
    {
        var (_, _, email) = await Users.SignUpAsync(factory);
        var client = factory.CreateTestClient();

        var wrong = await client.PostAsync("/Account/SignIn", new Dictionary<string, string> { ["Email"] = email, ["Password"] = "wrong-pass" });
        Assert.True(wrong.Contains("E-posta veya şifre hatalı."));

        var right = await client.PostAsync("/Account/SignIn", new Dictionary<string, string> { ["Email"] = email, ["Password"] = Users.Password });
        Assert.Equal("/", right.Path);
        Assert.True(right.Contains("Çıkış Yap"));
    }

    [Theory]
    [InlineData("/Walkers")]
    [InlineData("/Walks")]
    [InlineData("/Chat")]
    [InlineData("/Account/Profile")]
    public async Task Member_pages_send_visitors_to_sign_in(string url)
    {
        var page = await factory.CreateTestClient().GetAsync(url);

        Assert.Equal("/Account/SignIn", page.Path);
        Assert.Contains("ReturnUrl", page.Query);
    }

    [Fact]
    public async Task Forgot_password_emails_a_new_password_that_works()
    {
        var (_, _, email) = await Users.SignUpAsync(factory);
        var client = factory.CreateTestClient();

        await client.PostAsync("/Account/ForgotPassword", new Dictionary<string, string> { ["Email"] = email });

        var mail = Assert.Single(factory.Emails.Sent, m => m.To == email);
        var newPassword = System.Text.RegularExpressions.Regex.Match(mail.Body, @"Yeni şifren: (\S+)").Groups[1].Value;
        Assert.Equal(10, newPassword.Length);

        var oldLogin = await client.PostAsync("/Account/SignIn", new Dictionary<string, string> { ["Email"] = email, ["Password"] = Users.Password });
        Assert.True(oldLogin.Contains("E-posta veya şifre hatalı."));
        var newLogin = await client.PostAsync("/Account/SignIn", new Dictionary<string, string> { ["Email"] = email, ["Password"] = newPassword });
        Assert.Equal("/", newLogin.Path);
    }

    [Fact]
    public async Task Forgot_password_gives_the_same_answer_for_unknown_emails()
    {
        var page = await factory.CreateTestClient().PostAsync("/Account/ForgotPassword",
            new Dictionary<string, string> { ["Email"] = "nobody@test.local" });

        Assert.True(page.Contains("Bu e-posta kayıtlıysa"));
        Assert.DoesNotContain(factory.Emails.Sent, m => m.To == "nobody@test.local");
    }

    [Fact]
    public async Task Edit_profile_updates_details_and_password()
    {
        var (client, id, _) = await Users.SignUpAsync(factory, "Eski");
        var newEmail = Users.NewEmail("yeni");

        await client.PostAsync("/Account/Edit", new Dictionary<string, string>
        {
            ["EditForm.FirstName"] = "Yeni",
            ["EditForm.LastName"] = "İsim",
            ["EditForm.DateOfBirth"] = "01/01/1990",
            ["EditForm.Gender"] = "Male",
            ["EditForm.Address"] = "Beşiktaş, İstanbul",
            ["EditForm.Email"] = newEmail,
            ["EditForm.Phone"] = "05001112233",
            ["EditForm.NewPassword"] = "YeniSifre123",
        }, formPage: "/Account/Profile");

        var c = await factory.WithDbAsync(db => Task.FromResult(db.Customers.Single(x => x.Id == id)));
        Assert.Equal("Yeni", c.FirstName);
        Assert.Equal("Beşiktaş, İstanbul", c.Address);
        Assert.Equal(newEmail, c.Email);

        var login = await factory.CreateTestClient().PostAsync("/Account/SignIn",
            new Dictionary<string, string> { ["Email"] = newEmail, ["Password"] = "YeniSifre123" });
        Assert.Equal("/", login.Path);
    }

    [Fact]
    public async Task Delete_account_removes_the_user_and_their_data()
    {
        var (client, id, email) = await Users.SignUpAsync(factory);
        await Users.AddPetAsync(factory, client, id);
        await Users.BecomeWalkerAsync(factory, client, id);

        await client.PostAsync("/Account/DeleteAccount", formPage: "/Account/Profile");

        await factory.WithDbAsync(db =>
        {
            Assert.False(db.Customers.Any(c => c.Id == id));
            Assert.False(db.Pets.Any(p => p.OwnerId == id));
            Assert.False(db.Walkers.Any(w => w.CustomerId == id));
            return Task.CompletedTask;
        });
        var login = await factory.CreateTestClient().PostAsync("/Account/SignIn",
            new Dictionary<string, string> { ["Email"] = email, ["Password"] = Users.Password });
        Assert.True(login.Contains("E-posta veya şifre hatalı."));
    }
}
