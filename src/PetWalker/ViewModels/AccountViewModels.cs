using System.ComponentModel.DataAnnotations;
using PetWalker.Models;

namespace PetWalker.ViewModels;

public class SignUpViewModel
{
    public const string PhonePattern = @"^0\d{10}$";

    [Required(ErrorMessage = "Ad zorunludur."), StringLength(50), Display(Name = "Ad")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Soyad zorunludur."), StringLength(50), Display(Name = "Soyad")]
    public string LastName { get; set; } = "";

    // Typed as GG/AA/YYYY (day first), see TurkishDate.
    [Required(ErrorMessage = "Doğum tarihi zorunludur."), RegularExpression(TurkishDate.Pattern, ErrorMessage = "Tarihi GG/AA/YYYY biçiminde girin."), Display(Name = "Doğum Tarihi")]
    public string DateOfBirth { get; set; } = "";

    [Required(ErrorMessage = "Cinsiyet seçin."), Display(Name = "Cinsiyet")]
    public Gender? Gender { get; set; }

    [Required(ErrorMessage = "Adres zorunludur."), StringLength(200), Display(Name = "Adres")]
    public string Address { get; set; } = "";

    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(256), Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Telefon zorunludur."), RegularExpression(PhonePattern, ErrorMessage = "Telefon 0 ile başlamalı ve 11 haneli olmalı."), Display(Name = "Telefon")]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Şifre zorunludur."), StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalı."), DataType(DataType.Password), Display(Name = "Şifre")]
    public string Password { get; set; } = "";

    [Range(typeof(bool), "true", "true", ErrorMessage = "KVKK koşullarını kabul etmelisiniz.")]
    public bool AcceptKvkk { get; set; }
}

public class SignInViewModel
{
    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress, Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Şifre zorunludur."), DataType(DataType.Password), Display(Name = "Şifre")]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress, Display(Name = "E-posta")]
    public string Email { get; set; } = "";
}

// The account details from the sign-up form, editable on "Profilimi Güncelle".
public class EditProfileViewModel
{
    [Required(ErrorMessage = "Ad zorunludur."), StringLength(50), Display(Name = "Ad")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Soyad zorunludur."), StringLength(50), Display(Name = "Soyad")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Doğum tarihi zorunludur."), RegularExpression(TurkishDate.Pattern, ErrorMessage = "Tarihi GG/AA/YYYY biçiminde girin."), Display(Name = "Doğum Tarihi")]
    public string DateOfBirth { get; set; } = "";

    [Required(ErrorMessage = "Cinsiyet seçin."), Display(Name = "Cinsiyet")]
    public Gender? Gender { get; set; }

    [Required(ErrorMessage = "Adres zorunludur."), StringLength(200), Display(Name = "Adres")]
    public string Address { get; set; } = "";

    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), StringLength(256), Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Telefon zorunludur."), RegularExpression(SignUpViewModel.PhonePattern, ErrorMessage = "Telefon 0 ile başlamalı ve 11 haneli olmalı."), Display(Name = "Telefon")]
    public string Phone { get; set; } = "";

    // Optional: leave empty to keep the current password.
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalı."), DataType(DataType.Password), Display(Name = "Yeni Şifre")]
    public string? NewPassword { get; set; }
}

// Dates are typed and shown day first (GG/AA/YYYY), whatever the browser's language is.
public static class TurkishDate
{
    public const string Pattern = @"^\d{2}[./]\d{2}[./]\d{4}$";

    public static bool TryParse(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text?.Trim().Replace('/', '.'), "dd.MM.yyyy",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);

    public static string Format(DateOnly date) => date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
