using System.ComponentModel.DataAnnotations;

namespace PetWalker.Models;

public class Walker
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string FirstName { get; set; } = "";

    [Required, StringLength(50)]
    public string LastName { get; set; } = "";

    [Required, StringLength(200)]
    public string Address { get; set; } = "";

    [Required, StringLength(20)]
    public string Phone { get; set; } = "";

    public DateOnly DateOfBirth { get; set; }

    public Gender Gender { get; set; }

    [Range(0, 60)]
    public int ExperienceYears { get; set; }

    // Price per 30 minutes of walking.
    public decimal PricePer30Min { get; set; }

    [StringLength(1000)]
    public string? Bio { get; set; }

    // Durations (in minutes) this walker offers, comma separated, e.g. "30,60,120".
    [Required, StringLength(50)]
    public string OfferedDurations { get; set; } = "30,60,120";

    // Set when the walker left the platform ("Ayrıl"). Left walkers are hidden and can't be booked.
    public DateTime? LeftAtUtc { get; set; }

    // The user account behind this walker. Null for the sample walkers seeded without an account.
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    public bool IsActive => LeftAtUtc is null;

    public IReadOnlyList<int> Durations =>
        OfferedDurations.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).Order().ToList();

    public int Age => Models.Age.From(DateOfBirth);

    public ICollection<Walk> Walks { get; set; } = new List<Walk>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
