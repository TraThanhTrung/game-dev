using GameServer.Models.Dto;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace GameServer.Controllers;

[ApiController]
[Route("skills")]
public class SkillsController : ControllerBase
{
    private readonly TemporarySkillService _temporarySkillService;
    private readonly WorldService _worldService;
    private readonly ILogger<SkillsController> _logger;

    public SkillsController(TemporarySkillService temporarySkillService, WorldService worldService, ILogger<SkillsController> logger)
    {
        _temporarySkillService = temporarySkillService;
        _worldService = worldService;
        _logger = logger;
    }

    /// <summary>
    /// Upgrade a skill for a player.
    /// </summary>
    [HttpPost("upgrade")]
    public async Task<IActionResult> UpgradeSkill([FromBody] SkillUpgradeRequest request)
    {
        if (string.IsNullOrEmpty(request.PlayerId) || !Guid.TryParse(request.PlayerId, out var playerId))
        {
            return BadRequest(new SkillUpgradeResponse
            {
                Success = false,
                Message = "Invalid PlayerId"
            });
        }

        if (string.IsNullOrEmpty(request.SkillId))
        {
            return BadRequest(new SkillUpgradeResponse
            {
                Success = false,
                Message = "SkillId is required"
            });
        }

        // Get player's session ID
        var playerState = _worldService.GetPlayerState(playerId);
        if (playerState == null)
        {
            return BadRequest(new SkillUpgradeResponse
            {
                Success = false,
                SkillId = request.SkillId,
                Message = "Player not found in any session"
            });
        }

        // Get session ID from WorldService
        string sessionId = _worldService.GetPlayerSessionId(playerId) ?? "default";

        // Upgrade temporary skill in Redis
        var result = await _temporarySkillService.UpgradeTemporarySkillAsync(sessionId, playerId, request.SkillId);
        
        if (!result.Success)
        {
            return BadRequest(new SkillUpgradeResponse
            {
                Success = false,
                SkillId = request.SkillId,
                Message = result.ErrorMessage ?? "Failed to upgrade skill"
            });
        }

        // Reload player stats with temporary bonuses
        await ReloadPlayerStatsWithBonusesAsync(playerId, sessionId);

        return Ok(new SkillUpgradeResponse
        {
            Success = true,
            SkillId = request.SkillId,
            Level = result.Level,
            Message = "Skill upgraded successfully"
        });
    }

    /// <summary>
    /// Get all skills for a player.
    /// </summary>
    [HttpGet("{playerId}")]
    public async Task<IActionResult> GetSkills([FromRoute] string playerId)
    {
        if (string.IsNullOrEmpty(playerId) || !Guid.TryParse(playerId, out var pid))
        {
            return BadRequest(new GetSkillsResponse { Skills = new List<SkillInfo>() });
        }

        // Get player's session ID
        string sessionId = _worldService.GetPlayerSessionId(pid) ?? "default";

        // Get temporary skills from Redis
        var bonuses = await _temporarySkillService.GetTemporarySkillBonusesAsync(sessionId, pid);
        
        var response = new GetSkillsResponse
        {
            Skills = bonuses?.SkillLevels.Select(kvp => new SkillInfo
            {
                SkillId = kvp.Key,
                Level = kvp.Value
            }).ToList() ?? new List<SkillInfo>() // Return empty if no temporary skills
        };

        return Ok(response);
    }

    /// <summary>
    /// Reload player stats from database and apply temporary bonuses from Redis.
    /// Updates PlayerState in WorldService with combined stats.
    /// </summary>
    private async Task ReloadPlayerStatsWithBonusesAsync(Guid playerId, string sessionId)
    {
        try
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();

            var player = await playerService.GetPlayerAsync(playerId);
            if (player == null || player.Stats == null)
            {
                _logger.LogWarning("Cannot reload stats: Player {PlayerId} or Stats not found", playerId);
                return;
            }

            var playerState = _worldService.GetPlayerState(playerId);
            if (playerState == null)
            {
                _logger.LogWarning("Cannot reload stats: PlayerState not found for {PlayerId}", playerId);
                return;
            }

            // Get temporary bonuses from Redis
            var bonuses = await _temporarySkillService.GetTemporarySkillBonusesAsync(sessionId, playerId);

            // Apply base stats + temporary bonuses
            var baseStats = player.Stats;
            _temporarySkillService.ApplyBonusesToBaseStats(playerState, baseStats, bonuses);

            // Keep other stats from base
            playerState.Range = baseStats.Range;
            playerState.WeaponRange = baseStats.WeaponRange;
            playerState.KnockbackTime = baseStats.KnockbackTime;
            playerState.StunTime = baseStats.StunTime;
            playerState.BonusDamagePercent = baseStats.BonusDamagePercent;
            playerState.DamageReductionPercent = baseStats.DamageReductionPercent;

            _logger.LogInformation("✅ Reloaded player stats for {PlayerId}: Base (Speed={BaseSpeed}, Damage={BaseDamage}, MaxHp={BaseMaxHp}) + Temp (Speed+{SpeedBonus}, Damage+{DamageBonus}, MaxHp+{MaxHpBonus}) = Final (Speed={FinalSpeed}, Damage={FinalDamage}, MaxHp={FinalMaxHp})",
                playerId,
                baseStats.Speed, baseStats.Damage, baseStats.MaxHealth,
                bonuses?.SpeedBonus ?? 0, bonuses?.DamageBonus ?? 0, bonuses?.MaxHealthBonus ?? 0,
                playerState.Speed, playerState.Damage, playerState.MaxHp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading player stats with bonuses for {PlayerId}", playerId);
        }
    }
}

