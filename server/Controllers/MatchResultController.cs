using GameServer.Data;
using GameServer.Models.Dto;
using GameServer.Models.Entities;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Json;

namespace GameServer.Controllers;

[ApiController]
[Route("api/game")]
public class MatchResultController : ControllerBase
{
    #region Private Fields
    private readonly GameDbContext _db;
    private readonly AdminService _adminService;
    private readonly ILogger<MatchResultController> _logger;
    #endregion

    #region Constructor
    public MatchResultController(
        GameDbContext db,
        AdminService adminService,
        ILogger<MatchResultController> logger)
    {
        _db = db;
        _adminService = adminService;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// GET /api/game/match-result/{sessionId} - Get match result data for a session.
    /// </summary>
    [HttpGet("match-result/{sessionId}")]
    public async Task<ActionResult<MatchResultDto>> GetMatchResult([FromRoute] string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return BadRequest("Session ID is required");
        }

        // Parse sessionId as Guid
        if (!Guid.TryParse(sessionId, out Guid sessionGuid))
        {
            return BadRequest("Invalid session ID format");
        }

        // Load GameSession from database
        var gameSession = await _db.GameSessions
            .Include(s => s.Players)
            .FirstOrDefaultAsync(s => s.SessionId == sessionGuid);

        if (gameSession == null)
        {
            return NotFound($"Session {sessionId} not found");
        }

        // Build MatchResultDto
        var result = new MatchResultDto
        {
            SessionId = gameSession.SessionId,
            StartTime = gameSession.StartTime,
            EndTime = gameSession.EndTime,
            PlayerCount = gameSession.PlayerCount,
            Status = gameSession.Status
        };

        // Load players with their profiles
        var sessionPlayers = await _db.SessionPlayers
            .Where(sp => sp.SessionId == sessionGuid)
            .Include(sp => sp.Session)
            .ToListAsync();

        var playerIds = sessionPlayers.Select(sp => sp.PlayerId).ToList();
        var playerProfiles = await _db.PlayerProfiles
            .Where(p => playerIds.Contains(p.Id))
            .ToListAsync();

        // Map players to PlayerInfoDto
        foreach (var sessionPlayer in sessionPlayers)
        {
            var profile = playerProfiles.FirstOrDefault(p => p.Id == sessionPlayer.PlayerId);
            if (profile != null)
            {
                result.Players.Add(new PlayerInfoDto
                {
                    PlayerId = profile.Id,
                    AvatarPath = profile.AvatarPath,
                    Name = profile.Name,
                    Level = profile.Level,
                    Gold = profile.Gold
                });
            }
        }

        // Load enemies from checkpoints
        // For simplified version, get all checkpoints from all sections
        // In a more sophisticated implementation, we'd track which checkpoints were reached
        var allCheckpoints = await _adminService.GetCheckpointsAsync(null);
        
        // Group checkpoints by section and get unique enemy types
        var enemyTypeSet = new HashSet<string>();
        var enemyTypeToSection = new Dictionary<string, string>();
        var enemyTypeToCheckpoint = new Dictionary<string, string>();

        foreach (var checkpoint in allCheckpoints)
        {
            if (checkpoint.SectionId.HasValue && !string.IsNullOrEmpty(checkpoint.EnemyPool))
            {
                // Parse EnemyPool JSON array
                try
                {
                    var enemyTypes = JsonSerializer.Deserialize<List<string>>(checkpoint.EnemyPool);
                    if (enemyTypes != null)
                    {
                        // Get section name
                        var section = await _db.GameSections
                            .FirstOrDefaultAsync(s => s.SectionId == checkpoint.SectionId.Value);
                        
                        string sectionName = section?.Name ?? $"Section {checkpoint.SectionId}";

                        foreach (var enemyType in enemyTypes)
                        {
                            if (!string.IsNullOrEmpty(enemyType) && !enemyTypeSet.Contains(enemyType))
                            {
                                enemyTypeSet.Add(enemyType);
                                enemyTypeToSection[enemyType] = sectionName;
                                enemyTypeToCheckpoint[enemyType] = checkpoint.CheckpointName;
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning($"Failed to parse EnemyPool for checkpoint {checkpoint.CheckpointId}: {ex.Message}");
                }
            }
        }

        // Load enemy names from database
        var enemyNames = await _db.Enemies
            .Where(e => enemyTypeSet.Contains(e.TypeId) && e.IsActive)
            .ToDictionaryAsync(e => e.TypeId, e => e.Name);

        // Create EnemyTypeInfoDto for each unique enemy type
        foreach (var enemyType in enemyTypeSet)
        {
            // Get enemy name from database, fallback to capitalized typeId if not found
            string enemyName = enemyNames.GetValueOrDefault(enemyType, string.Empty);
            if (string.IsNullOrEmpty(enemyName) && !string.IsNullOrEmpty(enemyType))
            {
                // Fallback: capitalize first letter
                if (enemyType.Length > 1)
                {
                    enemyName = char.ToUpper(enemyType[0]) + enemyType.Substring(1);
                }
                else
                {
                    enemyName = enemyType.ToUpper();
                }
            }

            result.Enemies.Add(new EnemyTypeInfoDto
            {
                EnemyTypeId = enemyType,
                Name = enemyName,
                SectionName = enemyTypeToSection.GetValueOrDefault(enemyType, "Unknown"),
                CheckpointName = enemyTypeToCheckpoint.GetValueOrDefault(enemyType, null)
            });
        }

        return Ok(result);
    }
    #endregion
}

