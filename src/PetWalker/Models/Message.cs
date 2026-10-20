using System.ComponentModel.DataAnnotations;

namespace PetWalker.Models;

// A message in the private conversation between one customer and one walker.
public class Message
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public int WalkerId { get; set; }
    public Walker Walker { get; set; } = null!;

    public bool SentByCustomer { get; set; }

    [Required, StringLength(1000)]
    public string Text { get; set; } = "";

    public DateTime SentAtUtc { get; set; }
}
