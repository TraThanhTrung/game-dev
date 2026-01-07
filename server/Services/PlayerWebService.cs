using GameServer.Data;
using GameServer.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace GameServer.Services;

/// <summary>
/// Service for player web panel operations.
/// Handles queries for player profile, stats, match history, and related data.
/// </summary>
public class PlayerWebService
{
    #region Private Fields
    private readonly GameDbContext _db;
    private readonly ILogger<PlayerWebService> _logger;
    private readonly GameConfigService? _configService;
    #endregion

    #region Constructor
    public PlayerWebService(GameDbContext db, ILogger<PlayerWebService> logger, GameConfigService? configService = null)
    {
        _db = db;
        _logger = logger;
        _configService = configService;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Get player profile by ID.
    /// </summary>
    public async Task<PlayerProfile?> GetPlayerProfileAsync(Guid playerId)
    {
        return await _db.PlayerProfiles
            .Include(p => p.Stats)
            .Include(p => p.Inventory)
            .Include(p => p.Skills)
            .FirstOrDefaultAsync(p => p.Id == playerId);
    }

    /// <summary>
    /// Get player profile by name.
    /// </summary>
    public async Task<PlayerProfile?> GetPlayerProfileByNameAsync(string playerName)
    {
        return await _db.PlayerProfiles
            .Include(p => p.Stats)
            .Include(p => p.Inventory)
            .Include(p => p.Skills)
            .FirstOrDefaultAsync(p => p.Name.ToLower() == playerName.ToLower());
    }

    /// <summary>
    /// Get player stats by ID.
    /// </summary>
    public async Task<PlayerStats?> GetPlayerStatsAsync(Guid playerId)
    {
        return await _db.PlayerStats
            .FirstOrDefaultAsync(s => s.PlayerId == playerId);
    }

    /// <summary>
    /// Get player match history (GameSessions where player participated).
    /// </summary>
    public async Task<List<GameSession>> GetPlayerMatchHistoryAsync(Guid playerId, int page = 1, int pageSize = 20)
    {
        var skip = (page - 1) * pageSize;

        return await _db.GameSessions
            .Include(s => s.Players)
            .Where(s => s.Players.Any(p => p.PlayerId == playerId))
            .OrderByDescending(s => s.StartTime)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <summary>
    /// Get total count of matches for a player.
    /// </summary>
    public async Task<int> GetPlayerMatchCountAsync(Guid playerId)
    {
        return await _db.GameSessions
            .Where(s => s.Players.Any(p => p.PlayerId == playerId))
            .CountAsync();
    }

    /// <summary>
    /// Get player play time (total duration from all sessions).
    /// </summary>
    public async Task<int> GetPlayerPlayTimeAsync(Guid playerId)
    {
        var totalSeconds = await _db.SessionPlayers
            .Where(sp => sp.PlayerId == playerId && sp.PlayDuration.HasValue)
            .SumAsync(sp => sp.PlayDuration!.Value);

        return totalSeconds;
    }

    /// <summary>
    /// Get player's session participation details.
    /// </summary>
    public async Task<List<SessionPlayer>> GetPlayerSessionsAsync(Guid playerId, int page = 1, int pageSize = 20)
    {
        var skip = (page - 1) * pageSize;

        return await _db.SessionPlayers
            .Include(sp => sp.Session)
            .Where(sp => sp.PlayerId == playerId)
            .OrderByDescending(sp => sp.JoinTime)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <summary>
    /// Get player game results (placeholder - depends on Plan 3 GameResult entity).
    /// </summary>
    public async Task<List<object>> GetPlayerGameResultsAsync(Guid playerId, int page = 1, int pageSize = 20)
    {
        // TODO: Implement when GameResult entity is available from Plan 3
        // For now, return empty list
        await Task.CompletedTask;
        return new List<object>();
    }

    /// <summary>
    /// Get player profile by email.
    /// </summary>
    public async Task<PlayerProfile?> GetPlayerProfileByEmailAsync(string email)
    {
        return await _db.PlayerProfiles
            .FirstOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == email.ToLower());
    }

    /// <summary>
    /// Get player profile by Google ID.
    /// </summary>
    public async Task<PlayerProfile?> GetPlayerProfileByGoogleIdAsync(string googleId)
    {
        return await _db.PlayerProfiles
            .FirstOrDefaultAsync(p => p.GoogleId != null && p.GoogleId == googleId);
    }

    /// <summary>
    /// Create new player account with username, email, and password.
    /// </summary>
    public async Task<(bool Success, Guid PlayerId, string? ErrorMessage)> CreatePlayerAccountAsync(
        string username, string email, string password)
    {
        try
        {
            // Hash password using SHA256 (simple hashing for student project)
            var passwordHash = HashPassword(password);

            // Get default config for new player
            if (_configService == null)
            {
                return (false, Guid.Empty, "Configuration service not available");
            }

            var defaults = _configService.PlayerDefaults;
            var stats = defaults.Stats;

            var newPlayer = new PlayerProfile
            {
                Id = Guid.NewGuid(),
                Name = username,
                Email = email,
                PasswordHash = passwordHash,
                TokenHash = Guid.NewGuid().ToString("N"),
                Level = defaults.Level,
                Exp = defaults.Exp,
                ExpToLevel = _configService.GetExpForNextLevel(defaults.Level),
                Gold = defaults.Gold,
                CreatedAt = DateTime.UtcNow,
                Stats = new PlayerStats
                {
                    Damage = stats.Damage,
                    Range = stats.WeaponRange,
                    KnockbackForce = stats.KnockbackForce,
                    Speed = stats.Speed,
                    MaxHealth = stats.MaxHealth,
                    CurrentHealth = stats.CurrentHealth
                }
            };
            newPlayer.Stats.PlayerId = newPlayer.Id;

            _db.PlayerProfiles.Add(newPlayer);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created new player account: {Name} (Email: {Email}, ID: {Id})", 
                username, email, newPlayer.Id);

            return (true, newPlayer.Id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating player account: {Name}", username);
            return (false, Guid.Empty, "Failed to create account. Please try again.");
        }
    }

    /// <summary>
    /// Verify password for a player.
    /// </summary>
    public bool VerifyPassword(PlayerProfile player, string password)
    {
        if (string.IsNullOrEmpty(player.PasswordHash))
        {
            return false;
        }

        var hashedPassword = HashPassword(password);
        return player.PasswordHash == hashedPassword;
    }

    /// <summary>
    /// Hash password using SHA256.
    /// </summary>
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Create or link Google account to player.
    /// </summary>
    public async Task<(bool Success, Guid PlayerId, string? ErrorMessage)> CreateOrLinkGoogleAccountAsync(
        string googleId, string email, string? username = null)
    {
        try
        {
            // Check if Google account already exists
            var existingGoogle = await GetPlayerProfileByGoogleIdAsync(googleId);
            if (existingGoogle != null)
            {
                return (true, existingGoogle.Id, null);
            }

            // Check if email already exists
            var existingEmail = await GetPlayerProfileByEmailAsync(email);
            if (existingEmail != null)
            {
                // Link Google account to existing email
                existingEmail.GoogleId = googleId;
                await _db.SaveChangesAsync();
                return (true, existingEmail.Id, null);
            }

            // Create new account with Google
            if (_configService == null)
            {
                return (false, Guid.Empty, "Configuration service not available");
            }

            var defaults = _configService.PlayerDefaults;
            var stats = defaults.Stats;

            // Use email username if username not provided
            var playerName = username ?? email.Split('@')[0];

            var newPlayer = new PlayerProfile
            {
                Id = Guid.NewGuid(),
                Name = playerName,
                Email = email,
                GoogleId = googleId,
                PasswordHash = HashPassword("1234"), // Default password for Gmail users
                TokenHash = Guid.NewGuid().ToString("N"),
                Level = defaults.Level,
                Exp = defaults.Exp,
                ExpToLevel = _configService.GetExpForNextLevel(defaults.Level),
                Gold = defaults.Gold,
                CreatedAt = DateTime.UtcNow,
                Stats = new PlayerStats
                {
                    Damage = stats.Damage,
                    Range = stats.WeaponRange,
                    KnockbackForce = stats.KnockbackForce,
                    Speed = stats.Speed,
                    MaxHealth = stats.MaxHealth,
                    CurrentHealth = stats.CurrentHealth
                }
            };
            newPlayer.Stats.PlayerId = newPlayer.Id;

            _db.PlayerProfiles.Add(newPlayer);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created new player with Google: {Name} (Email: {Email}, GoogleId: {GoogleId}, ID: {Id})", 
                playerName, email, googleId, newPlayer.Id);

            return (true, newPlayer.Id, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/linking Google account: {Email}", email);
            return (false, Guid.Empty, "Failed to create account. Please try again.");
        }
    }
    #endregion
}

