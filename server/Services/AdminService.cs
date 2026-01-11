using GameServer.Data;
using GameServer.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Services;

public class AdminService
{
    #region Private Fields
    private readonly GameDbContext _db;
    private readonly ILogger<AdminService> _logger;
    private readonly IServiceProvider _serviceProvider;
    #endregion

    #region Constructor
    public AdminService(
        GameDbContext db,
        ILogger<AdminService> logger,
        IServiceProvider serviceProvider)
    {
        _db = db;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    #endregion

    #region Public Methods - Dashboard Stats
    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var totalUsers = await _db.PlayerProfiles.CountAsync();
        var totalSessions = await _db.GameSessions.CountAsync();
        var activeSessions = await _db.GameSessions
            .CountAsync(s => s.Status == "Active");
        var matchesToday = await _db.GameSessions
            .CountAsync(s => s.StartTime >= today && s.StartTime < tomorrow);

        return new DashboardStatsDto
        {
            TotalUsers = totalUsers,
            TotalSessions = totalSessions,
            ActiveSessions = activeSessions,
            MatchesToday = matchesToday
        };
    }
    #endregion

    #region Public Methods - Users
    public async Task<(List<PlayerProfile> Users, int Total)> GetUsersAsync(
        int page = 1, 
        int pageSize = 20, 
        string? search = null)
    {
        var query = _db.PlayerProfiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.Name.Contains(search));
        }

        var total = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (users, total);
    }

    public async Task<PlayerProfile?> GetUserDetailsAsync(Guid playerId)
    {
        return await _db.PlayerProfiles
            .Include(p => p.Stats)
            .Include(p => p.Skills)
            .Include(p => p.Inventory)
            .FirstOrDefaultAsync(p => p.Id == playerId);
    }

    public async Task<TimeSpan> GetPlayerPlayTimeAsync(Guid playerId)
    {
        var totalSeconds = await _db.SessionPlayers
            .Where(sp => sp.PlayerId == playerId && sp.PlayDuration != null)
            .SumAsync(sp => sp.PlayDuration ?? 0);

        return TimeSpan.FromSeconds(totalSeconds);
    }

    public async Task<bool> DeleteUserAsync(Guid playerId)
    {
        var player = await _db.PlayerProfiles
            .Include(p => p.Stats)
            .Include(p => p.Skills)
            .Include(p => p.Inventory)
            .FirstOrDefaultAsync(p => p.Id == playerId);
        
        if (player == null)
            return false;

        // Xóa SessionPlayers (không có cascade delete)
        var sessionPlayers = await _db.SessionPlayers
            .Where(sp => sp.PlayerId == playerId)
            .ToListAsync();
        _db.SessionPlayers.RemoveRange(sessionPlayers);

        // Xóa PlayerProfile (sẽ cascade delete Stats, Skills, Inventory)
        _db.PlayerProfiles.Remove(player);
        
        await _db.SaveChangesAsync();
        
        _logger.LogInformation("Deleted user: {Name} (ID: {Id})", player.Name, playerId);
        return true;
    }
    #endregion

    #region Public Methods - Matches
    public async Task<(List<GameSession> Matches, int Total)> GetMatchesAsync(
        int page = 1,
        int pageSize = 20,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? gameSectionId = null)
    {
        var query = _db.GameSessions.AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(m => m.StartTime >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(m => m.StartTime <= toDate.Value);
        }

        var total = await query.CountAsync();
        var matches = await query
            .Include(m => m.Players)
            .OrderByDescending(m => m.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (matches, total);
    }

    public async Task<GameSession?> GetMatchDetailsAsync(Guid sessionId)
    {
        return await _db.GameSessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
    }
    #endregion

    #region Public Methods - Active Sessions
    public async Task<List<GameSession>> GetActiveSessionsAsync()
    {
        return await _db.GameSessions
            .Where(s => s.Status == "Active")
            .Include(s => s.Players)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();
    }
    #endregion

    #region Public Methods - Enemies
    public async Task<List<Enemy>> GetEnemiesAsync()
    {
        return await _db.Enemies
            .OrderBy(e => e.TypeId)
            .ToListAsync();
    }

    public async Task<Enemy?> GetEnemyAsync(int enemyId)
    {
        return await _db.Enemies.FindAsync(enemyId);
    }

    public async Task<Enemy> CreateEnemyAsync(Enemy enemy)
    {
        enemy.CreatedAt = DateTime.UtcNow;
        _db.Enemies.Add(enemy);
        await _db.SaveChangesAsync();
        return enemy;
    }

    public async Task<Enemy?> UpdateEnemyAsync(int enemyId, Enemy updatedEnemy)
    {
        var enemy = await _db.Enemies.FindAsync(enemyId);
        if (enemy == null)
            return null;

        // Store old TypeId for cache invalidation (in case TypeId changed)
        var oldTypeId = enemy.TypeId;
        var newTypeId = updatedEnemy.TypeId;

        enemy.TypeId = updatedEnemy.TypeId;
        enemy.Name = updatedEnemy.Name;
        enemy.ExpReward = updatedEnemy.ExpReward;
        enemy.GoldReward = updatedEnemy.GoldReward;
        enemy.MaxHealth = updatedEnemy.MaxHealth;
        enemy.Damage = updatedEnemy.Damage;
        enemy.Speed = updatedEnemy.Speed;
        enemy.DetectRange = updatedEnemy.DetectRange;
        enemy.AttackRange = updatedEnemy.AttackRange;
        enemy.AttackCooldown = updatedEnemy.AttackCooldown;
        enemy.WeaponRange = updatedEnemy.WeaponRange;
        enemy.KnockbackForce = updatedEnemy.KnockbackForce;
        enemy.StunTime = updatedEnemy.StunTime;
        enemy.RespawnDelay = updatedEnemy.RespawnDelay;
        enemy.IsActive = updatedEnemy.IsActive;
        enemy.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Invalidate Redis cache for enemy config (old and new TypeId if changed)
        try
        {
            var enemyConfigService = _serviceProvider.GetService<EnemyConfigService>();
            if (enemyConfigService != null)
            {
                await enemyConfigService.InvalidateCacheAsync(oldTypeId);
                if (oldTypeId != newTypeId)
                {
                    await enemyConfigService.InvalidateCacheAsync(newTypeId);
                }
                _logger.LogInformation("Invalidated Redis cache for enemy config: {OldTypeId} -> {NewTypeId}", oldTypeId, newTypeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate Redis cache for enemy {TypeId}", newTypeId);
            // Continue even if cache invalidation fails
        }

        return enemy;
    }

    public async Task<bool> DeleteEnemyAsync(int enemyId)
    {
        var enemy = await _db.Enemies.FindAsync(enemyId);
        if (enemy == null)
            return false;

        _db.Enemies.Remove(enemy);
        await _db.SaveChangesAsync();
        return true;
    }
    #endregion

    #region Public Methods - Game Sections
    public async Task<List<GameSection>> GetGameSectionsAsync()
    {
        return await _db.GameSections
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<GameSection?> GetGameSectionAsync(int sectionId)
    {
        return await _db.GameSections.FindAsync(sectionId);
    }

    public async Task<GameSection> CreateGameSectionAsync(GameSection section)
    {
        section.CreatedAt = DateTime.UtcNow;
        _db.GameSections.Add(section);
        await _db.SaveChangesAsync();
        return section;
    }

    public async Task<GameSection?> UpdateGameSectionAsync(int sectionId, GameSection updatedSection)
    {
        var section = await _db.GameSections.FindAsync(sectionId);
        if (section == null)
            return null;

        section.Name = updatedSection.Name;
        section.Description = updatedSection.Description;
        section.EnemyTypeId = updatedSection.EnemyTypeId;
        section.EnemyCount = updatedSection.EnemyCount;
        section.EnemyLevel = updatedSection.EnemyLevel;
        section.SpawnRate = updatedSection.SpawnRate;
        section.Duration = updatedSection.Duration;
        section.IsActive = updatedSection.IsActive;
        section.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return section;
    }

    public async Task<bool> DeleteGameSectionAsync(int sectionId)
    {
        var section = await _db.GameSections
            .Include(s => s.Checkpoints)
            .FirstOrDefaultAsync(s => s.SectionId == sectionId);
        
        if (section == null)
            return false;

        // Set checkpoints' SectionId to null (orphan them) before deleting section
        foreach (var checkpoint in section.Checkpoints)
        {
            checkpoint.SectionId = null;
        }

        _db.GameSections.Remove(section);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Get checkpoint count for a GameSection.
    /// </summary>
    public async Task<int> GetCheckpointCountForSectionAsync(int sectionId)
    {
        return await _db.Checkpoints
            .CountAsync(c => c.SectionId == sectionId);
    }

    /// <summary>
    /// Get active checkpoint count for a GameSection.
    /// </summary>
    public async Task<int> GetActiveCheckpointCountForSectionAsync(int sectionId)
    {
        return await _db.Checkpoints
            .CountAsync(c => c.SectionId == sectionId && c.IsActive);
    }
    #endregion

    #region Public Methods - Checkpoints
    public async Task<List<Checkpoint>> GetCheckpointsAsync(int? sectionId = null)
    {
        var query = _db.Checkpoints
            .Include(c => c.Section)
            .AsQueryable();
        
        if (sectionId.HasValue)
        {
            query = query.Where(c => c.SectionId == sectionId.Value);
        }
        
        return await query
            .OrderBy(c => c.CheckpointName)
            .ToListAsync();
    }

    public async Task<Checkpoint?> GetCheckpointAsync(int checkpointId)
    {
        return await _db.Checkpoints
            .Include(c => c.Section)
            .FirstOrDefaultAsync(c => c.CheckpointId == checkpointId);
    }

    public async Task<Checkpoint> CreateCheckpointAsync(Checkpoint checkpoint)
    {
        checkpoint.CreatedAt = DateTime.UtcNow;
        _db.Checkpoints.Add(checkpoint);
        await _db.SaveChangesAsync();
        
        // Invalidate checkpoint cache
        var checkpointService = _serviceProvider.GetService<CheckpointService>();
        checkpointService?.InvalidateCache();
        
        return checkpoint;
    }

    public async Task<Checkpoint?> UpdateCheckpointAsync(int checkpointId, Checkpoint updatedCheckpoint)
    {
        var checkpoint = await _db.Checkpoints.FindAsync(checkpointId);
        if (checkpoint == null)
            return null;

        checkpoint.CheckpointName = updatedCheckpoint.CheckpointName;
        checkpoint.SectionId = updatedCheckpoint.SectionId;
        checkpoint.X = updatedCheckpoint.X;
        checkpoint.Y = updatedCheckpoint.Y;
        checkpoint.EnemyPool = updatedCheckpoint.EnemyPool;
        checkpoint.MaxEnemies = updatedCheckpoint.MaxEnemies;
        checkpoint.IsActive = updatedCheckpoint.IsActive;
        checkpoint.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        
        // Invalidate checkpoint cache (both old and new if section changed)
        var checkpointService = _serviceProvider.GetService<CheckpointService>();
        checkpointService?.InvalidateCache(checkpoint.CheckpointName);
        
        return checkpoint;
    }

    public async Task<bool> DeleteCheckpointAsync(int checkpointId)
    {
        var checkpoint = await _db.Checkpoints.FindAsync(checkpointId);
        if (checkpoint == null)
            return false;

        var checkpointName = checkpoint.CheckpointName;
        _db.Checkpoints.Remove(checkpoint);
        await _db.SaveChangesAsync();
        
        // Invalidate checkpoint cache
        var checkpointService = _serviceProvider.GetService<CheckpointService>();
        checkpointService?.InvalidateCache(checkpointName);
        
        return true;
    }
    #endregion
}

#region DTOs
public class DashboardStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalSessions { get; set; }
    public int ActiveSessions { get; set; }
    public int MatchesToday { get; set; }
}
#endregion








