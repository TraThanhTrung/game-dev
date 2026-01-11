using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers;

[ApiController]
[Route("api/[controller]")]
// No [Authorize] - enemy configs are public data, no authentication required
public class EnemiesController : ControllerBase
{
    #region Private Fields
    private readonly EnemyConfigService _enemyConfigService;
    private readonly ILogger<EnemiesController> _logger;
    #endregion

    #region Constructor
    public EnemiesController(
        EnemyConfigService enemyConfigService,
        ILogger<EnemiesController> logger)
    {
        _enemyConfigService = enemyConfigService;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// GET /api/enemies - Get all active enemy configs (for Unity client).
    /// Returns all enemy configurations from database.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<EnemyConfigDto>>> GetAllEnemies()
    {
        try
        {
            var configs = await _enemyConfigService.GetAllEnemiesAsync();
            var dtos = configs.Select(c => new EnemyConfigDto
            {
                typeId = c.TypeId,
                expReward = c.ExpReward,
                goldReward = c.GoldReward,
                maxHealth = c.MaxHealth,
                damage = c.Damage,
                speed = c.Speed,
                detectRange = c.DetectRange,
                attackRange = c.AttackRange,
                attackCooldown = c.AttackCooldown,
                weaponRange = c.WeaponRange,
                knockbackForce = c.KnockbackForce,
                stunTime = c.StunTime,
                respawnDelay = c.RespawnDelay
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all enemies");
            return StatusCode(500, "Failed to load enemy configs");
        }
    }

    /// <summary>
    /// GET /api/enemies/{typeId} - Get enemy config by typeId.
    /// </summary>
    [HttpGet("{typeId}")]
    public async Task<ActionResult<EnemyConfigDto>> GetEnemy(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            return BadRequest("typeId is required");
        }

        try
        {
            var config = await _enemyConfigService.GetEnemyAsync(typeId);
            if (config == null)
            {
                return NotFound($"Enemy config not found for typeId: {typeId}");
            }

            var dto = new EnemyConfigDto
            {
                typeId = config.TypeId,
                expReward = config.ExpReward,
                goldReward = config.GoldReward,
                maxHealth = config.MaxHealth,
                damage = config.Damage,
                speed = config.Speed,
                detectRange = config.DetectRange,
                attackRange = config.AttackRange,
                attackCooldown = config.AttackCooldown,
                weaponRange = config.WeaponRange,
                knockbackForce = config.KnockbackForce,
                stunTime = config.StunTime,
                respawnDelay = config.RespawnDelay
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get enemy config for typeId: {TypeId}", typeId);
            return StatusCode(500, "Failed to load enemy config");
        }
    }
    #endregion

    #region DTOs
    public class EnemyConfigDto
    {
        public string typeId { get; set; } = string.Empty;
        public int expReward { get; set; }
        public int goldReward { get; set; }
        public int maxHealth { get; set; }
        public int damage { get; set; }
        public float speed { get; set; }
        public float detectRange { get; set; }
        public float attackRange { get; set; }
        public float attackCooldown { get; set; }
        public float weaponRange { get; set; }
        public float knockbackForce { get; set; }
        public float stunTime { get; set; }
        public float respawnDelay { get; set; }
    }
    #endregion
}

