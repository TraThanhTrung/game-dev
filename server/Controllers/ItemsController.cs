using GameServer.Models.Dto;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers;

[ApiController]
[Route("items")]
public class ItemsController : ControllerBase
{
    private readonly TemporaryItemService _temporaryItemService;
    private readonly WorldService _worldService;
    private readonly ILogger<ItemsController> _logger;

    public ItemsController(TemporaryItemService temporaryItemService, WorldService worldService, ILogger<ItemsController> logger)
    {
        _temporaryItemService = temporaryItemService;
        _worldService = worldService;
        _logger = logger;
    }

    /// <summary>
    /// Apply an item buff to player (for temporary stats).
    /// </summary>
    [HttpPost("use")]
    public async Task<IActionResult> UseItem([FromBody] UseItemRequest request)
    {
        if (string.IsNullOrEmpty(request.PlayerId) || !Guid.TryParse(request.PlayerId, out var playerId))
        {
            return BadRequest(new UseItemResponse
            {
                Success = false,
                Message = "Invalid PlayerId"
            });
        }

        if (string.IsNullOrEmpty(request.ItemId))
        {
            return BadRequest(new UseItemResponse
            {
                Success = false,
                Message = "ItemId is required"
            });
        }

        // Get player's session ID
        var playerState = _worldService.GetPlayerState(playerId);
        if (playerState == null)
        {
            return BadRequest(new UseItemResponse
            {
                Success = false,
                Message = "Player not found in any session"
            });
        }

        // Get session ID from WorldService
        string sessionId = _worldService.GetPlayerSessionId(playerId) ?? "default";

        // Only apply buff if item has duration (temporary item)
        if (request.DurationSeconds > 0)
        {
            // Apply temporary buff in Redis
            var result = await _temporaryItemService.ApplyItemBuffAsync(
                sessionId, playerId,
                request.ItemId, request.ItemName ?? request.ItemId,
                request.CurrentHealthBonus, request.SpeedBonus, request.DamageBonus,
                request.DurationSeconds);

            if (!result.Success)
            {
                return BadRequest(new UseItemResponse
                {
                    Success = false,
                    Message = result.ErrorMessage ?? "Failed to apply item buff"
                });
            }

            // Reload player stats with item buffs
            await ReloadPlayerStatsWithItemBuffsAsync(playerId, sessionId);
        }

        return Ok(new UseItemResponse
        {
            Success = true,
            Message = "Item used successfully"
        });
    }

    /// <summary>
    /// Get active item buffs for a player.
    /// </summary>
    [HttpGet("buffs/{playerId}")]
    public async Task<IActionResult> GetItemBuffs([FromRoute] string playerId)
    {
        if (string.IsNullOrEmpty(playerId) || !Guid.TryParse(playerId, out var pid))
        {
            return BadRequest(new GetItemBuffsResponse { Buffs = new List<ItemBuffInfo>() });
        }

        // Get player's session ID
        string sessionId = _worldService.GetPlayerSessionId(pid) ?? "default";

        // Get temporary item buffs from Redis
        var buffs = await _temporaryItemService.GetTemporaryItemBuffsAsync(sessionId, pid);

        // Clean up expired buffs
        if (buffs != null)
        {
            await _temporaryItemService.CleanupExpiredBuffsAsync(sessionId, pid);
        }

        var response = new GetItemBuffsResponse
        {
            Buffs = buffs?.ActiveBuffs
                .Where(b => b.ExpiresAt > DateTime.UtcNow)
                .Select(b => new ItemBuffInfo
                {
                    ItemId = b.ItemId,
                    ItemName = b.ItemName,
                    ExpiresAt = b.ExpiresAt,
                    CurrentHealthBonus = b.CurrentHealthBonus,
                    SpeedBonus = b.SpeedBonus,
                    DamageBonus = b.DamageBonus
                })
                .ToList() ?? new List<ItemBuffInfo>()
        };

        return Ok(response);
    }

    /// <summary>
    /// Reload player stats and apply active item buffs from Redis.
    /// Updates PlayerState in WorldService with temporary health boost.
    /// </summary>
    private async Task ReloadPlayerStatsWithItemBuffsAsync(Guid playerId, string sessionId)
    {
        try
        {
            var playerState = _worldService.GetPlayerState(playerId);
            if (playerState == null)
            {
                _logger.LogWarning("Cannot reload item buffs: PlayerState not found for {PlayerId}", playerId);
                return;
            }

            // Get temporary item buffs from Redis
            var buffs = await _temporaryItemService.GetTemporaryItemBuffsAsync(sessionId, playerId);

            // Apply item buffs to player state (mainly currentHealth)
            _temporaryItemService.ApplyBonusesToPlayerState(playerState, buffs);

            _logger.LogInformation("✅ Applied item buffs for {PlayerId}: HP={Hp}/{MaxHp}",
                playerId, playerState.Hp, playerState.MaxHp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading player stats with item buffs for {PlayerId}", playerId);
        }
    }
}

// Request/Response DTOs
public class UseItemRequest
{
    public string PlayerId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public int CurrentHealthBonus { get; set; }
    public int SpeedBonus { get; set; }
    public int DamageBonus { get; set; }
    public float DurationSeconds { get; set; }
}

public class UseItemResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class GetItemBuffsResponse
{
    public List<ItemBuffInfo> Buffs { get; set; } = new();
}

public class ItemBuffInfo
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int CurrentHealthBonus { get; set; }
    public int SpeedBonus { get; set; }
    public int DamageBonus { get; set; }
}

