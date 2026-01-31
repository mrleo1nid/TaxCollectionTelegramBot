using Microsoft.EntityFrameworkCore;
using TaxCollectionTelegramBot.Data.Entities;

namespace TaxCollectionTelegramBot.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserConfig> UserConfigs => Set<UserConfig>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionParticipant> CollectionParticipants => Set<CollectionParticipant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.TelegramId);
            entity.Property(u => u.Username).HasMaxLength(255);
            entity.Property(u => u.FirstName).HasMaxLength(255);
        });

        modelBuilder.Entity<UserConfig>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity
                .HasOne(c => c.User)
                .WithMany(u => u.Configs)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.TotalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<CollectionParticipant>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.AmountToPay).HasPrecision(18, 2);

            entity
                .HasOne(p => p.Collection)
                .WithMany(c => c.Participants)
                .HasForeignKey(p => p.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(p => p.User)
                .WithMany(u => u.Participations)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => new { p.CollectionId, p.UserId }).IsUnique();
        });
    }
}
