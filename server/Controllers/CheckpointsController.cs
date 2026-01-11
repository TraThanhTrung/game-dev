using GameServer.Models.Entities;
using GameServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication
public class CheckpointsController : ControllerBase
{
    #region Private Fields
    private readonly AdminService _adminService;
    private readonly ILogger<CheckpointsController> _logger;
    #endregion

    #region Constructor
    public CheckpointsController(
        AdminService adminService,
        ILogger<CheckpointsController> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// GET /api/checkpoints - Get all checkpoints (for Admin Panel).
    /// Optional query parameter: sectionId to filter by GameSection.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Checkpoint>>> GetAllCheckpoints([FromQuery] int? sectionId = null)
    {
        var checkpoints = await _adminService.GetCheckpointsAsync(sectionId);
        return Ok(checkpoints);
    }

    /// <summary>
    /// GET /api/checkpoints/{id} - Get checkpoint by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Checkpoint>> GetCheckpoint(int id)
    {
        var checkpoint = await _adminService.GetCheckpointAsync(id);
        if (checkpoint == null)
        {
            return NotFound();
        }
        return Ok(checkpoint);
    }

    /// <summary>
    /// POST /api/checkpoints - Create checkpoint (Admin only).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Checkpoint>> CreateCheckpoint([FromBody] Checkpoint checkpoint)
    {
        if (checkpoint == null)
        {
            return BadRequest("Checkpoint data is required");
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(checkpoint.CheckpointName))
        {
            return BadRequest("CheckpointName is required");
        }

        if (string.IsNullOrWhiteSpace(checkpoint.EnemyPool))
        {
            checkpoint.EnemyPool = "[]";
        }

        try
        {
            var created = await _adminService.CreateCheckpointAsync(checkpoint);
            return CreatedAtAction(nameof(GetCheckpoint), new { id = created.CheckpointId }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create checkpoint");
            return StatusCode(500, "Failed to create checkpoint");
        }
    }

    /// <summary>
    /// PUT /api/checkpoints/{id} - Update checkpoint (Admin only).
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<Checkpoint>> UpdateCheckpoint(int id, [FromBody] Checkpoint checkpoint)
    {
        if (checkpoint == null)
        {
            return BadRequest("Checkpoint data is required");
        }

        var updated = await _adminService.UpdateCheckpointAsync(id, checkpoint);
        if (updated == null)
        {
            return NotFound();
        }

        return Ok(updated);
    }

    /// <summary>
    /// DELETE /api/checkpoints/{id} - Delete checkpoint (Admin only).
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCheckpoint(int id)
    {
        var deleted = await _adminService.DeleteCheckpointAsync(id);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
    #endregion
}
