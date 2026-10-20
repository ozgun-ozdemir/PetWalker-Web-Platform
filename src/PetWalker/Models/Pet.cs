using System.ComponentModel.DataAnnotations;

namespace PetWalker.Models;

public class Pet
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = "";

    [Range(0.1, 150)]
    public double WeightKg { get; set; }

    [Range(0, 40)]
    public int AgeYears { get; set; }

    public PetType Type { get; set; }

    public PetSex Sex { get; set; }

    [Required, StringLength(50)]
    public string Breed { get; set; } = "";

    [Required, StringLength(30)]
    public string Color { get; set; } = "";

    public int OwnerId { get; set; }
    public Customer Owner { get; set; } = null!;

    public ICollection<Walk> Walks { get; set; } = new List<Walk>();
}
