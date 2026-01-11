using GameServer.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Data;

public class GameDbContext : IdentityDbContext<IdentityUser>
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
    {
    }

    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<PlayerStats> PlayerStats => Set<PlayerStats>();
    public DbSet<SkillUnlock> SkillUnlocks => Set<SkillUnlock>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<SessionPlayer> SessionPlayers => Set<SessionPlayer>();
    public DbSet<Enemy> Enemies => Set<Enemy>();
    public DbSet<GameSection> GameSections => Set<GameSection>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();

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

        modelBuilder.Entity<GameSession>()
            .HasMany(s => s.Players)
            .WithOne(p => p.Session)
            .HasForeignKey(p => p.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Enemy>()
            .HasIndex(e => e.TypeId)
            .IsUnique();

        modelBuilder.Entity<Enemy>()
            .Property(e => e.TypeId)
            .IsRequired();

        modelBuilder.Entity<GameSection>()
            .Property(g => g.Name)
            .IsRequired();

        modelBuilder.Entity<Checkpoint>()
            .HasIndex(c => c.CheckpointName)
            .IsUnique();

        modelBuilder.Entity<Checkpoint>()
            .Property(c => c.CheckpointName)
            .IsRequired();

        modelBuilder.Entity<Checkpoint>()
            .Property(c => c.EnemyPool)
            .IsRequired();

        // GameSection → Checkpoints relationship (1-n)
        modelBuilder.Entity<Checkpoint>()
            .HasOne(c => c.Section)
            .WithMany(s => s.Checkpoints)
            .HasForeignKey(c => c.SectionId)
            .OnDelete(DeleteBehavior.SetNull); // Allow orphaned checkpoints (backward compatibility)

        // Index on SectionId for performance
        modelBuilder.Entity<Checkpoint>()
            .HasIndex(c => c.SectionId);
    }
}

