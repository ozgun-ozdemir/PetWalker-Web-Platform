using System.ComponentModel.DataAnnotations;

namespace PetWalker.Models;

public enum WalkStatus
{
    // Requested by the owner, waiting for the walker to accept or reject.
    Pending = 0,
    Cancelled = 1,
    Accepted = 2,
    Rejected = 3,
    // The owner confirmed the walk happened; it can now be rated.
    Completed = 4,
}

public class Walk
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public int PetId { get; set; }
    public Pet Pet { get; set; } = null!;

    public int WalkerId { get; set; }
    public Walker Walker { get; set; } = null!;

    public DateTime StartsAt { get; set; }

    public int DurationMinutes { get; set; }

    public decimal Price { get; set; }

    public WalkStatus Status { get; set; } = WalkStatus.Pending;

    public DateTime EndsAt => StartsAt.AddMinutes(DurationMinutes);

    // Pending and accepted walks occupy the walker's time; cancelled and rejected ones don't.
    public bool Blocks => Status is WalkStatus.Pending or WalkStatus.Accepted;

    [Range(1, 5)]
    public int? Rating { get; set; }

    [StringLength(500)]
    public string? Review { get; set; }
}
