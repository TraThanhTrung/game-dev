using GameServer.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Data;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<PlayerStats> PlayerStats => Set<PlayerStats>();
    public DbSet<SkillUnlock> SkillUnlocks => Set<SkillUnlock>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PlayerProfile>()
            .HasOne(p => p.Stats)
            .WithOne(s => s.Player)
            .HasForeignKey<PlayerStats>(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlayerProfile>()
            .HasMany(p => p.Skills)
            .WithOne(s => s.Player)
            .HasForeignKey(s => s.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlayerProfile>()
            .HasMany(p => p.Inventory)
            .WithOne(i => i.Player)
            .HasForeignKey(i => i.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

