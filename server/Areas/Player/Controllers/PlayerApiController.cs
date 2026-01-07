using GameServer.Areas.Player.Models;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Areas.Player.Controllers;

/// <summary>
/// API controller for player web panel AJAX calls.
/// Provides JSON endpoints for profile, stats, history, and results.
/// </summary>
[ApiController]
[Route("player/api/[controller]")]
public class PlayerApiController : ControllerBase
{
    #region Private Fields
    private readonly PlayerWebService _playerWebService;
    private readonly ILogger<PlayerApiController> _logger;
    #endregion

    #region Constructor
    public PlayerApiController(PlayerWebService playerWebService, ILogger<PlayerApiController> logger)
    {
        _playerWebService = playerWebService;
        _logger = logger;
    }
    #endregion

    #region Private Methods
    private Guid? GetPlayerIdFromSession()
    {
        var playerIdStr = HttpContext.Session.GetString("PlayerId");
        if (Guid.TryParse(playerIdStr, out var playerId))
        {
            return playerId;
        }
        return null;
    }

    private bool IsPlayerLoggedIn()
    {
        return GetPlayerIdFromSession().HasValue && 
               !string.IsNullOrEmpty(HttpContext.Session.GetString("PlayerName"));
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Get player profile as JSON.
    /// GET /player/api/playerapi/profile
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        if (!IsPlayerLoggedIn())
        {
            return Unauthorized(new { error = "Not logged in" });
        }

        var playerId = GetPlayerIdFromSession()!.Value;
        var player = await _playerWebService.GetPlayerProfileAsync(playerId);

        if (player == null)
        {
            return NotFound(new { error = "Player not found" });
        }

        var profile = new PlayerProfileViewModel
        {
            PlayerId = player.Id,
            Name = player.Name,
            Level = player.Level,
            Exp = player.Exp,
            ExpToLevel = player.ExpToLevel,
            Gold = player.Gold,
            CreatedAt = player.CreatedAt,
            InventoryItemCount = player.Inventory?.Count ?? 0,
            SkillCount = player.Skills?.Count ?? 0,
            Stats = new PlayerStatsViewModel
            {
                Damage = player.Stats?.Damage ?? 0,
                Range = player.Stats?.Range ?? 0,
                KnockbackForce = player.Stats?.KnockbackForce ?? 0,
                Speed = player.Stats?.Speed ?? 0,
                MaxHealth = player.Stats?.MaxHealth ?? 0,
                CurrentHealth = player.Stats?.CurrentHealth ?? 0
            }
        };

        return Ok(profile);
    }

    /// <summary>
    /// Get player stats as JSON.
    /// GET /player/api/playerapi/stats
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        if (!IsPlayerLoggedIn())
        {
            return Unauthorized(new { error = "Not logged in" });
        }

        var playerId = GetPlayerIdFromSession()!.Value;
        var stats = await _playerWebService.GetPlayerStatsAsync(playerId);

        if (stats == null)
        {
            return NotFound(new { error = "Stats not found" });
        }

        var statsViewModel = new PlayerStatsViewModel
        {
            Damage = stats.Damage,
            Range = stats.Range,
            KnockbackForce = stats.KnockbackForce,
            Speed = stats.Speed,
            MaxHealth = stats.MaxHealth,
            CurrentHealth = stats.CurrentHealth
        };

        return Ok(statsViewModel);
    }

    /// <summary>
    /// Get player match history as JSON.
    /// GET /player/api/playerapi/history?page=1&pageSize=20
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!IsPlayerLoggedIn())
        {
            return Unauthorized(new { error = "Not logged in" });
        }

        var playerId = GetPlayerIdFromSession()!.Value;

        // Get sessions
        var sessions = await _playerWebService.GetPlayerSessionsAsync(playerId, page, pageSize);
        var totalMatches = await _playerWebService.GetPlayerMatchCountAsync(playerId);

        // Map to view models
        var history = sessions.Select(sp => new MatchHistoryViewModel
        {
            SessionId = sp.SessionId,
            StartTime = sp.Session?.StartTime ?? sp.JoinTime,
            EndTime = sp.Session?.EndTime,
            PlayerCount = sp.Session?.PlayerCount ?? 0,
            Status = sp.Session?.Status ?? "Unknown",
            PlayDuration = sp.PlayDuration,
            JoinTime = sp.JoinTime,
            LeaveTime = sp.LeaveTime
        }).ToList();

        return Ok(new
        {
            history,
            totalMatches,
            currentPage = page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)totalMatches / pageSize)
        });
    }

    /// <summary>
    /// Get player game results as JSON.
    /// GET /player/api/playerapi/results?page=1&pageSize=20
    /// Note: This depends on GameResult entity from Plan 3.
    /// </summary>
    [HttpGet("results")]
    public async Task<IActionResult> GetResults([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!IsPlayerLoggedIn())
        {
            return Unauthorized(new { error = "Not logged in" });
        }

        var playerId = GetPlayerIdFromSession()!.Value;
        var results = await _playerWebService.GetPlayerGameResultsAsync(playerId, page, pageSize);

        return Ok(new
        {
            results,
            message = "Game results feature will be available after Plan 3 implementation.",
            currentPage = page,
            pageSize,
            totalResults = results.Count
        });
    }
    #endregion
}

