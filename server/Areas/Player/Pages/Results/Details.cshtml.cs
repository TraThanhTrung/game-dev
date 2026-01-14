using GameServer.Areas.Player.Models;
using GameServer.Data;
using GameServer.Models.Dto;
using GameServer.Models.Entities;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GameServer.Areas.Player.Pages.Results;

/// <summary>
/// Results detail page - shows specific session results with enemies and players.
/// </summary>
public class DetailsModel : BasePlayerPageModel
{
    #region Private Fields
    private readonly GameDbContext _db;
    private readonly AdminService _adminService;
    private readonly ILogger<DetailsModel> _logger;
    #endregion

    #region Public Properties
    public ResultsDetailViewModel ViewModel { get; set; } = new();
    #endregion

    #region Constructor
    public DetailsModel(
        GameDbContext db,
        AdminService adminService,
        ILogger<DetailsModel> logger)
    {
        _db = db;
        _adminService = adminService;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public async Task<IActionResult> OnGetAsync(Guid sessionId)
    {
        if (!PlayerId.HasValue)
        {
            return RedirectToLogin();
        }

        try
        {
            // Parse sessionId as Guid
            if (sessionId == Guid.Empty)
            {
                ViewModel.ErrorMessage = "Invalid session ID format";
                return Page();
            }

            // Load GameSession from database
            var gameSession = await _db.GameSessions
                .Include(s => s.Players)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (gameSession == null)
            {
                ViewModel.ErrorMessage = $"Session {sessionId} not found";
                return Page();
            }

            // Security check: Verify that the session belongs to the logged-in player
            var playerParticipated = gameSession.Players.Any(p => p.PlayerId == PlayerId.Value);
            if (!playerParticipated)
            {
                ViewModel.ErrorMessage = "You do not have access to this session";
                return Page();
            }

            // Check if session is completed
            if (gameSession.Status != "Completed" || gameSession.EndTime == null)
            {
                ViewModel.ErrorMessage = "This session is not completed yet";
                return Page();
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
                .Where(sp => sp.SessionId == sessionId)
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

            ViewModel.MatchResult = result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading match result for session {SessionId}", sessionId);
            ViewModel.ErrorMessage = "An error occurred while loading the match result";
        }

        return Page();
    }
    #endregion
}

