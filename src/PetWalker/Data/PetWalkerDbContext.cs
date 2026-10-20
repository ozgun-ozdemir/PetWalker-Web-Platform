using Microsoft.EntityFrameworkCore;
using PetWalker.Models;

namespace PetWalker.Data;

public class PetWalkerDbContext(DbContextOptions<PetWalkerDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Walker> Walkers => Set<Walker>();
    public DbSet<Walk> Walks => Set<Walk>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite has no decimal type; storing as double keeps SUM/AVG/ORDER BY working.
        builder.Properties<decimal>().HaveConversion<double>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Email).IsUnique();
            e.Ignore(c => c.FullName);
        });

        modelBuilder.Entity<Walker>(e =>
        {
            e.Ignore(w => w.FullName);
            e.Ignore(w => w.Age);
            e.Ignore(w => w.IsActive);
            e.Ignore(w => w.Durations);
            e.HasOne(w => w.Customer).WithOne(c => c.WalkerProfile)
                .HasForeignKey<Walker>(w => w.CustomerId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Pet>()
            .HasOne(p => p.Owner).WithMany(c => c.Pets)
            .HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Walk>(e =>
        {
            e.Ignore(w => w.EndsAt);
            e.Ignore(w => w.Blocks);
            e.HasOne(w => w.Customer).WithMany(c => c.Walks).HasForeignKey(w => w.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(w => w.Pet).WithMany(p => p.Walks).HasForeignKey(w => w.PetId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(w => w.Walker).WithMany(w => w.Walks).HasForeignKey(w => w.WalkerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.HasOne(m => m.Customer).WithMany(c => c.Messages).HasForeignKey(m => m.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Walker).WithMany(w => w.Messages).HasForeignKey(m => m.WalkerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new { m.CustomerId, m.WalkerId, m.SentAtUtc });
        });

        modelBuilder.Entity<Walker>().HasData(
            new Walker { Id = 1, FirstName = "Ayşe", LastName = "Yılmaz", Address = "Kadıköy, İstanbul", Phone = "05320000001", DateOfBirth = new DateOnly(1995, 4, 12), Gender = Gender.Female, ExperienceYears = 5, PricePer30Min = 150, Bio = "Büyük ırklarla deneyimli, hafta içi her gün müsait." },
            new Walker { Id = 2, FirstName = "Mehmet", LastName = "Kaya", Address = "Ümraniye, İstanbul", Phone = "05320000002", DateOfBirth = new DateOnly(1990, 9, 3), Gender = Gender.Male, ExperienceYears = 8, PricePer30Min = 200, Bio = "Veteriner teknikeri, ilaç takibi yapabilir." },
            new Walker { Id = 3, FirstName = "Zeynep", LastName = "Demir", Address = "Beşiktaş, İstanbul", Phone = "05320000003", DateOfBirth = new DateOnly(1999, 1, 25), Gender = Gender.Female, ExperienceYears = 2, PricePer30Min = 120, Bio = "Küçük ırklar ve yavru köpekler." },
            new Walker { Id = 4, FirstName = "Can", LastName = "Öztürk", Address = "Kadıköy, İstanbul", Phone = "05320000004", DateOfBirth = new DateOnly(1981, 6, 18), Gender = Gender.Male, ExperienceYears = 12, PricePer30Min = 140, Bio = "Moda sahilinde yürüyüşler, hafta sonu müsait." });
    }
}
