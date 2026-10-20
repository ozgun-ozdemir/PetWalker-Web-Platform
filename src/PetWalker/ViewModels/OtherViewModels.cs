using System.ComponentModel.DataAnnotations;
using PetWalker.Models;

namespace PetWalker.ViewModels;

public class PetFormViewModel
{
    [Required(ErrorMessage = "İsim zorunludur."), StringLength(50), Display(Name = "İsim")]
    public string Name { get; set; } = "";

    [Range(0.1, 150, ErrorMessage = "Kilo 0.1 ile 150 arasında olmalı."), Display(Name = "Kilo (kg)")]
    public double WeightKg { get; set; }

    [Range(0, 40, ErrorMessage = "Yaş 0 ile 40 arasında olmalı."), Display(Name = "Yaş")]
    public int AgeYears { get; set; }

    [Required(ErrorMessage = "Kedi veya köpek seçin."), Display(Name = "Tür")]
    public PetType? Type { get; set; }

    [Required(ErrorMessage = "Cinsiyet seçin."), Display(Name = "Cinsiyet")]
    public PetSex? Sex { get; set; }

    [Required(ErrorMessage = "Cins zorunludur."), StringLength(50), Display(Name = "Cins")]
    public string Breed { get; set; } = "";

    [Required(ErrorMessage = "Renk zorunludur."), StringLength(30), Display(Name = "Renk")]
    public string Color { get; set; } = "";
}

public class ProfileViewModel
{
    public required Customer Customer { get; init; }
    public required IReadOnlyList<Pet> Pets { get; init; }
    public Walker? WalkerProfile { get; init; }
    public BecomeWalkerViewModel WalkerForm { get; init; } = new();

    public required EditProfileViewModel EditForm { get; init; }
    public PetFormViewModel PetForm { get; init; } = new();
}

public class BecomeWalkerViewModel
{
    [Range(0, 60, ErrorMessage = "Deneyim 0 ile 60 yıl arasında olmalı."), Display(Name = "Deneyim (yıl)")]
    public int ExperienceYears { get; set; }

    [Range(1, 10000, ErrorMessage = "Ücret 1 ile 10.000 ₺ arasında olmalı."), Display(Name = "Ücret (₺ / 30 dk)")]
    public decimal PricePer30Min { get; set; } = 150;

    [Required(ErrorMessage = "Açıklama zorunludur."), StringLength(1000, MinimumLength = 10, ErrorMessage = "Açıklama 10 ile 1000 karakter arasında olmalı."), Display(Name = "Açıklama")]
    public string Bio { get; set; } = "";

    // Which of the standard durations the walker offers.
    public List<int> Durations { get; set; } = [.. BookWalkViewModel.AllowedDurations];
}

public class WalkerSearchViewModel
{
    public string? Location { get; set; }
    public int? MinExperience { get; set; }
    public double? MinRating { get; set; }
    public string Sort { get; set; } = "rating-desc";
    public IReadOnlyList<WalkerCard> Results { get; set; } = [];

    public static readonly IReadOnlyList<(string Value, string Label)> SortOptions =
    [
        ("rating-desc", "Puana göre azalan"),
        ("rating-asc", "Puana göre artan"),
        ("experience-desc", "Deneyime göre azalan"),
        ("experience-asc", "Deneyime göre artan"),
        ("price-asc", "Ücrete göre artan"),
        ("price-desc", "Ücrete göre azalan"),
    ];
}

public record WalkerCard(Walker Walker, double? AverageRating, int RatingCount, string? PhotoPath);

public class WalkerProfileViewModel
{
    public required WalkerCard Card { get; init; }
    public required IReadOnlyList<Walk> Reviews { get; init; }
    public IReadOnlyList<Pet> MyPets { get; init; } = [];
    public BookWalkViewModel Booking { get; init; } = new();
    public bool IsMe { get; init; }

    // Filled when IsMe: the "Gezdirici Bilgilerim" form shown on my own walker profile.
    public BecomeWalkerViewModel? WalkerForm { get; init; }
}

public class BookWalkViewModel
{
    public int WalkerId { get; set; }

    [Required(ErrorMessage = "Evcil hayvan seçin."), Display(Name = "Evcil hayvan")]
    public int? PetId { get; set; }

    [Required(ErrorMessage = "Tarih ve saat zorunludur."), Display(Name = "Tarih ve saat")]
    public DateTime? StartsAt { get; set; }

    [Display(Name = "Süre (dk)")]
    public int DurationMinutes { get; set; } = 30;

    public static readonly int[] AllowedDurations = [30, 60, 120];

    public static string DurationLabel(int minutes) => minutes switch
    {
        < 60 => $"{minutes} dk",
        _ when minutes % 60 == 0 => $"{minutes / 60} saat",
        _ => $"{minutes / 60} saat {minutes % 60} dk",
    };
}

public class RateWalkViewModel
{
    public int WalkId { get; set; }

    [Range(1, 5, ErrorMessage = "Puan 1 ile 5 arasında olmalı.")]
    public int Rating { get; set; }

    [StringLength(500)]
    public string? Review { get; set; }
}

public class WalksViewModel
{
    public required IReadOnlyList<Walk> MyWalks { get; init; }

    // Walks other users booked with me; empty unless I am also a walker.
    public IReadOnlyList<Walk> BookedWithMe { get; init; } = [];
    public bool IsWalker { get; init; }
}

// One person in the chat sidebar: either a walker I can write to, or (when I am a walker) a customer who wrote to me.
public record ChatContact(string Key, string Name, string Details, string? Subtitle, DateTime? LastMessageAt)
{
    public bool HasMessages => LastMessageAt is not null;
}

public class ChatViewModel
{
    // People I already have messages with; these rows get the delete (trash) button.
    public required IReadOnlyList<ChatContact> Conversations { get; init; }

    // Everyone else I can start a conversation with.
    public required IReadOnlyList<ChatContact> Others { get; init; }
    public ChatContact? Active { get; init; }
    public IReadOnlyList<Message> Messages { get; init; } = [];

    // True when the active conversation is one where I am the walker.
    public bool ActiveAsWalker { get; init; }
}

public class ContactFormViewModel
{
    [Required(ErrorMessage = "Ad Soyad zorunludur."), StringLength(100), Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Konu zorunludur."), StringLength(150), Display(Name = "Konu")]
    public string Subject { get; set; } = "";

    [Required(ErrorMessage = "E-posta zorunludur."), EmailAddress(ErrorMessage = "Geçerli bir e-posta girin."), Display(Name = "E-posta")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Mesaj zorunludur."), StringLength(2000, MinimumLength = 10, ErrorMessage = "Mesaj en az 10 karakter olmalı."), Display(Name = "Mesajınız")]
    public string Body { get; set; } = "";

    [Range(typeof(bool), "true", "true", ErrorMessage = "KVKK koşullarını kabul etmelisiniz.")]
    public bool AcceptKvkk { get; set; }
}
