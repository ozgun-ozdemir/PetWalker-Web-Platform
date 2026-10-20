using System.ComponentModel.DataAnnotations;

namespace PetWalker.Models;

public class Customer
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string FirstName { get; set; } = "";

    [Required, StringLength(50)]
    public string LastName { get; set; } = "";

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(11)]
    public string Phone { get; set; } = "";

    [Required]
    public string PasswordHash { get; set; } = "";

    public DateOnly DateOfBirth { get; set; }

    public Gender Gender { get; set; }

    [Required, StringLength(200)]
    public string Address { get; set; } = "";

    // Web path of the uploaded profile photo, e.g. /uploads/profiles/abc.jpg. Null = default picture.
    [StringLength(200)]
    public string? PhotoPath { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    // Set when the customer has also registered as a walker.
    public Walker? WalkerProfile { get; set; }

    public ICollection<Pet> Pets { get; set; } = new List<Pet>();
    public ICollection<Walk> Walks { get; set; } = new List<Walk>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
