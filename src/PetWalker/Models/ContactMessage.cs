using System.ComponentModel.DataAnnotations;

namespace PetWalker.Models;

// A message sent from the contact form on the İletişim page.
public class ContactMessage
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string FullName { get; set; } = "";

    [Required, StringLength(150)]
    public string Subject { get; set; } = "";

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(2000)]
    public string Body { get; set; } = "";

    public DateTime SentAtUtc { get; set; }
}
