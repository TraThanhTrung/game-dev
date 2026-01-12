using GameServer.Data;
using GameServer.Models.Dto;
using GameServer.Models.Entities;
using GameServer.Models.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;

namespace GameServer.Services;

public class WorldService
{
    private readonly ILogger<WorldService> _logger;
    private readonly GameConfigService _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly RedisService? _redis;

    private readonly ConcurrentDictionary<string, SessionState> _sessions = new();
    private readonly ConcurrentDictionary<Guid, string> _playerToSession = new();
    private readonly ConcurrentDictionary<Guid, InputCommand> _inputQueue = new();
    private readonly ConcurrentDictionary<string, bool> _initializedRooms = new(); // Track rooms that have spawned enemies
    private readonly object _sessionLock = new();

    // In-memory cache for enemy configs (loaded on demand, avoids DB/Redis queries during game tick)
    private readonly ConcurrentDictionary<string, GameServer.Services.EnemyConfig> _enemyConfigCache = new();
    private readonly object _enemyConfigCacheLock = new();
    private bool _enemyConfigsLoaded = false;

    private const float TickDeltaTime = 0.05f; // 20Hz

    public WorldService(ILogger<WorldService> logger, GameConfigService config, IServiceProvider serviceProvider, RedisService? redis = null)
    {
        _logger = logger;
        _config = config;
        _serviceProvider = serviceProvider;
        _redis = redis;
    }

    #region Public API

    /// <summary>
    /// Get player state by ID (for saving progress).
    /// </summary>
    public PlayerState? GetPlayerState(Guid playerId)
    {
        if (_playerToSession.TryGetValue(playerId, out var sessionId))
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                if (session.Players.TryGetValue(playerId, out var player))
                {
                    return player;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Get session ID for a player.
    /// </summary>
    public string? GetPlayerSessionId(Guid playerId)
    {
        return _playerToSession.TryGetValue(playerId, out var sessionId) ? sessionId : null;
    }

    /// <summary>
    /// DEPRECATED: Use RegisterOrLoadPlayer() instead.
    /// Players must be created via Admin Panel or Register page, then loaded via RegisterOrLoadPlayer().
    /// This method is kept for backward compatibility but creates a default in-memory player.
    /// </summary>
    [Obsolete("Use RegisterOrLoadPlayer() instead. Players must be created via Admin Panel or Register page.")]
    public RegisterResponse RegisterPlayer(string playerName)
    {
        var playerId = Guid.NewGuid();
        var token = Guid.NewGuid().ToString("N");

        // Create default in-memory player (deprecated - should use RegisterOrLoadPlayer)
#pragma warning disable CS0618 // Obsolete method intentionally used
        var playerState = CreateDefaultPlayer(playerId, playerName);
#pragma warning restore CS0618
        var session = _sessions.GetOrAdd("default", sid => CreateDefaultSession(sid));

        lock (_sessionLock)
        {
            session.Players[playerId] = playerState;
            _playerToSession[playerId] = session.SessionId;
        }

        _logger.LogWarning("RegisterPlayer is deprecated. Player {Name} created with defaults. Use RegisterOrLoadPlayer() instead.", playerName);

        return new RegisterResponse
        {
            PlayerId = playerId,
            Token = token,
            SessionId = session.SessionId
        };
    }

    /// <summary>
    /// Register or load player from database entity.
    /// Loads temporary skill bonuses from Redis if player rejoins session.
    /// </summary>
    public async Task<RegisterResponse> RegisterOrLoadPlayerAsync(PlayerProfile profile, bool isNew)
    {
        var session = _sessions.GetOrAdd("default", sid => CreateDefaultSession(sid));
        bool isFirstPlayerInSession = false;

        // Load temporary skill bonuses from Redis (before lock to avoid blocking)
        using var scope = _serviceProvider.CreateScope();
        var temporarySkillService = scope.ServiceProvider.GetRequiredService<TemporarySkillService>();
        var bonuses = await temporarySkillService.GetTemporarySkillBonusesAsync(session.SessionId, profile.Id);

        lock (_sessionLock)
        {
            // Check if this is the first player in this session (for checkpoint initialization)
            if (!session.Players.Any() && !_initializedRooms.ContainsKey(session.SessionId))
            {
                isFirstPlayerInSession = true;
                _initializedRooms[session.SessionId] = true;
            }

            if (session.Players.TryGetValue(profile.Id, out var existing))
            {
                // Player already in session, just return
                _logger.LogInformation("Player {Name} already in session, returning existing", profile.Name);
            }
            else
            {
                // Create player state from database profile
                // All base stats come from database (PlayerStats entity)
                var stats = profile.Stats;
                if (stats == null)
                {
                    _logger.LogError("Player {Name} has no Stats in database! Cannot load player.", profile.Name);
                    return new RegisterResponse { PlayerId = Guid.Empty };
                }

                var playerState = new PlayerState
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    X = stats.SpawnX,
                    Y = stats.SpawnY,
                    Hp = isNew ? stats.MaxHealth : stats.CurrentHealth,
                    Level = profile.Level,
                    Exp = profile.Exp,
                    ExpToLevel = profile.ExpToLevel > 0 ? profile.ExpToLevel : _config.GetExpForNextLevel(profile.Level),
                    Gold = profile.Gold,
                    Sequence = 0
                };

                // Apply base stats + temporary bonuses
                temporarySkillService.ApplyBonusesToBaseStats(playerState, stats, bonuses);

                // Set other stats from base
                playerState.Range = stats.Range;
                playerState.WeaponRange = stats.WeaponRange;
                playerState.KnockbackTime = stats.KnockbackTime;
                playerState.StunTime = stats.StunTime;
                playerState.BonusDamagePercent = stats.BonusDamagePercent;
                playerState.DamageReductionPercent = stats.DamageReductionPercent;

                session.Players[profile.Id] = playerState;

                if (bonuses != null)
                {
                    _logger.LogInformation("Player {Name} loaded: Base (DMG={BaseDamage}, SPD={BaseSpeed}, HP={BaseHp}) + Temp (DMG+{DamageBonus}, SPD+{SpeedBonus}, HP+{MaxHpBonus}) = Final (DMG={Damage}, SPD={Speed}, HP={Hp}/{MaxHp})",
                        profile.Name,
                        stats.Damage, stats.Speed, stats.MaxHealth,
                        bonuses.DamageBonus, bonuses.SpeedBonus, bonuses.MaxHealthBonus,
                        playerState.Damage, playerState.Speed, playerState.Hp, playerState.MaxHp);
                }
                else
                {
                    _logger.LogInformation("Player {Name} loaded from database: DMG={Damage}, SPD={Speed}, HP={Hp}/{MaxHp}, LVL={Level}, WPN={WeaponRange}, KB={KnockbackForce}",
                        profile.Name, playerState.Damage, playerState.Speed, playerState.Hp, playerState.MaxHp, playerState.Level, playerState.WeaponRange, playerState.KnockbackForce);
                }
            }
            _playerToSession[profile.Id] = session.SessionId;
            _logger.LogInformation("Player {PlayerId} added to session {SessionId}. Session has {EnemyCount} enemies.",
                profile.Id.ToString()[..8], session.SessionId, session.Enemies.Count);
        }

        // Initialize checkpoints if this is the first player in session
        if (isFirstPlayerInSession)
        {
            _ = Task.Run(async () => await InitializeRoomCheckpointsAsync(session.SessionId));
        }

        _logger.LogInformation("{Action} player: {Name} (ID: {Id}) Level={Level} Exp={Exp} Gold={Gold}",
            isNew ? "Created" : "Loaded", profile.Name, profile.Id.ToString()[..8],
            profile.Level, profile.Exp, profile.Gold);

        return new RegisterResponse
        {
            PlayerId = profile.Id,
            Token = profile.TokenHash,
            SessionId = session.SessionId
        };
    }

    /// <summary>
    /// Create a new room (SessionState) with the given sessionId from database.
    /// </summary>
    public void CreateRoom(Guid sessionId)
    {
        var sessionIdStr = sessionId.ToString();
        var isNewRoom = _sessions.TryAdd(sessionIdStr, CreateDefaultSession(sessionIdStr));

        if (isNewRoom)
        {
            _logger.LogInformation("Created room: {SessionId}", sessionIdStr);
            // Initialize checkpoints asynchronously (don't block)
            _ = Task.Run(async () => await InitializeRoomCheckpointsAsync(sessionIdStr));
        }
    }

    /// <summary>
    /// Get room info (player count, etc.) from in-memory state.
    /// Returns null if room doesn't exist.
    /// </summary>
    public (int playerCount, int version)? GetRoomInfo(string roomId)
    {
        if (_sessions.TryGetValue(roomId, out var session))
        {
            return (session.Players.Count, session.Version);
        }
        return null;
    }

    public bool JoinSession(JoinSessionRequest request)
    {
        var session = _sessions.GetOrAdd(request.SessionId, sid => CreateDefaultSession(sid));
        bool isNewRoom = false;

        lock (_sessionLock)
        {
            // Initialize room checkpoints if this is the first player joining
            if (!_initializedRooms.ContainsKey(request.SessionId))
            {
                isNewRoom = true;
                _initializedRooms[request.SessionId] = true;
            }

            if (session.Players.TryGetValue(request.PlayerId, out var existing))
            {
                // Reset HP/pos instead of creating duplicate
                // Spawn position from existing state (originally from database)
                const float defaultSpawnX = -16f;
                const float defaultSpawnY = 12f;
                existing.Hp = existing.MaxHp;
                existing.X = defaultSpawnX;
                existing.Y = defaultSpawnY;
                existing.Sequence = 0;
                _logger.LogInformation("Player {PlayerId} rejoined, reset HP/pos to spawn ({X}, {Y})",
                    request.PlayerId, defaultSpawnX, defaultSpawnY);
            }
            else
            {
                // Player not in this session yet - move from their old session
                // First, find and remove player from their current session
                if (_playerToSession.TryGetValue(request.PlayerId, out var oldSessionId) && oldSessionId != request.SessionId)
                {
                    if (_sessions.TryGetValue(oldSessionId, out var oldSession))
                    {
                        if (oldSession.Players.TryGetValue(request.PlayerId, out var playerState))
                        {
                            oldSession.Players.Remove(request.PlayerId);

                            // Reset player state for new room
                            const float defaultSpawnX = -16f;
                            const float defaultSpawnY = 12f;
                            playerState.Hp = playerState.MaxHp;
                            playerState.X = defaultSpawnX;
                            playerState.Y = defaultSpawnY;
                            playerState.Sequence = 0;

                            // Add to new session
                            session.Players[request.PlayerId] = playerState;
                            _logger.LogInformation("Player {PlayerId} moved from session {OldSession} to {NewSession}",
                                request.PlayerId.ToString()[..8], oldSessionId, request.SessionId);
                        }
                        else
                        {
                            _logger.LogError("Player {PlayerId} not found in old session {OldSession}", request.PlayerId, oldSessionId);
                            return false;
                        }
                    }
                }
                else
                {
                    _logger.LogError("Player {PlayerId} not found in any session. Player must be registered first.", request.PlayerId);
                    return false;
                }
            }
            _playerToSession[request.PlayerId] = session.SessionId;
            session.Version++;
        }

        // Spawn enemies at checkpoints if this is a new room (async, don't block)
        if (isNewRoom)
        {
            _ = Task.Run(async () => await InitializeRoomCheckpointsAsync(request.SessionId));
        }

        return true;
    }

    public void ResetSession(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            lock (_sessionLock)
            {
                // Clear all players from this session
                foreach (var playerId in session.Players.Keys.ToList())
                {
                    _playerToSession.TryRemove(playerId, out _);
                }
                session.Players.Clear();
                session.Enemies.Clear();
                session.Projectiles.Clear();

                // Clear initialized flag so checkpoints will re-initialize on next join
                _initializedRooms.TryRemove(sessionId, out _);

                session.Version++;
                _logger.LogInformation("Session {SessionId} reset", sessionId);
            }
        }
    }

    public void EnqueueInput(InputRequest input)
    {
        _inputQueue[input.PlayerId] = new InputCommand
        {
            PlayerId = input.PlayerId,
            SessionId = input.SessionId,
            MoveX = Clamp(input.MoveX),
            MoveY = Clamp(input.MoveY),
            AimX = input.AimX,
            AimY = input.AimY,
            Attack = input.Attack,
            Shoot = input.Shoot,
            Sequence = input.Sequence
        };
    }

    public StateResponse GetState(string sessionId, int? sinceVersion)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return new StateResponse
            {
                SessionId = sessionId,
                Version = 0
            };
        }

        if (sinceVersion.HasValue && sinceVersion.Value >= session.Version)
        {
            return new StateResponse
            {
                SessionId = session.SessionId,
                Version = session.Version
            };
        }

        var response = new StateResponse
        {
            SessionId = session.SessionId,
            Version = session.Version,
            Players = session.Players.Values
                .Select(p => new PlayerSnapshot(
                    p.Id, p.Name, p.X, p.Y, p.Hp, p.MaxHp, p.Sequence,
                    p.Level, p.Exp, p.ExpToLevel, p.Gold,
                    // Player stats (synced from database)
                    p.Damage, p.Range, p.Speed,
                    p.WeaponRange, p.KnockbackForce, p.KnockbackTime, p.StunTime,
                    p.BonusDamagePercent, p.DamageReductionPercent))
                .ToList(),
            Enemies = session.Enemies.Values
                .Where(e => e.Hp > 0) // Only return alive enemies
                .Select(e => new EnemySnapshot(
                    e.Id,
                    e.TypeId,
                    e.X,
                    e.Y,
                    e.Hp,
                    e.MaxHp,
                    e.Status.ToString().ToLower()
                ))
                .ToList()
        };

        // Log enemy IDs being sent (for debugging)
        if (response.Enemies.Any())
        {
            var ids = response.Enemies.Select(e => e.Id.ToString()[..8]).ToList();
            _logger.LogDebug("GetState: Sending {Count} enemies to client: [{Ids}]", response.Enemies.Count, string.Join(", ", ids));
        }

        return response;
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        foreach (var session in _sessions.Values)
        {
            ProcessInputs(session);
            ProcessEnemyRespawns(session);
            CleanupDeadEnemies(session); // Clean up dead enemies that won't respawn

            // Check boss defeat and advance section
            await CheckBossDefeatedAndAdvanceSection(session);

            // Check if all players are dead
            CheckAllPlayersDead(session);

            session.Version++;
        }
    }

    #endregion

    #region SignalR Support

    /// <summary>
    /// Get session snapshot for SignalR broadcast.
    /// Returns a GameStateSnapshot suitable for real-time updates.
    /// </summary>
    public Hubs.GameStateSnapshot? GetSessionSnapshot(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return null;
        }

        var snapshot = new Hubs.GameStateSnapshot
        {
            Sequence = session.Version,
            ServerTime = (float)(DateTime.UtcNow - DateTime.UtcNow.Date).TotalSeconds, // Seconds since midnight
            ConfirmedInputSequence = GetLatestConfirmedInputSequence(sessionId),
            Players = session.Players.Values.Select(p => new Hubs.PlayerSnapshot
            {
                Id = p.Id.ToString(),
                Name = p.Name,
                CharacterType = p.CharacterType,
                X = p.X,
                Y = p.Y,
                Hp = p.Hp,
                MaxHp = p.MaxHp,
                Level = p.Level,
                Status = p.IsDead ? "dead" : "idle",
                LastConfirmedInputSequence = p.LastConfirmedInputSequence
            }).ToList(),
            Enemies = session.Enemies.Values
                .Where(e => e.Hp > 0)
                .Select(e => new Hubs.EnemySnapshot
                {
                    Id = e.Id.ToString(),
                    TypeId = e.TypeId,
                    X = e.X,
                    Y = e.Y,
                    Hp = e.Hp,
                    MaxHp = e.MaxHp,
                    Status = e.Status.ToString().ToLower()
                }).ToList(),
            Projectiles = session.Projectiles.Values.Select(p => new Hubs.ProjectileSnapshot
            {
                Id = p.Id.ToString(),
                OwnerId = p.OwnerId.ToString(),
                X = p.X,
                Y = p.Y,
                VelocityX = p.DirX * p.Speed,
                VelocityY = p.DirY * p.Speed
            }).ToList()
        };

        return snapshot;
    }

    /// <summary>
    /// Queue input from SignalR client for processing in next game tick.
    /// </summary>
    public void QueueInput(Hubs.InputPayload input)
    {
        if (!Guid.TryParse(input.PlayerId, out var playerId))
        {
            _logger.LogWarning("[WorldService] Invalid player ID in input: {PlayerId}", input.PlayerId);
            return;
        }

        _inputQueue[playerId] = new InputCommand
        {
            PlayerId = playerId,
            SessionId = input.SessionId,
            MoveX = Clamp(input.MoveX),
            MoveY = Clamp(input.MoveY),
            AimX = 0,
            AimY = 0,
            Attack = input.Attack,
            Shoot = false,
            Sequence = input.Sequence
        };

        // Update last confirmed input sequence for the player
        if (_playerToSession.TryGetValue(playerId, out var sessionId))
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                if (session.Players.TryGetValue(playerId, out var playerState))
                {
                    playerState.LastConfirmedInputSequence = input.Sequence;
                }
            }
        }
    }

    /// <summary>
    /// Get the latest confirmed input sequence for a session.
    /// Used for client-side prediction reconciliation.
    /// </summary>
    private int GetLatestConfirmedInputSequence(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return 0;
        }

        // Return the maximum confirmed input sequence among all players
        var maxSequence = session.Players.Values
            .Select(p => p.LastConfirmedInputSequence)
            .DefaultIfEmpty(0)
            .Max();

        return maxSequence;
    }

    /// <summary>
    /// Get all active session IDs for broadcasting.
    /// </summary>
    public IEnumerable<string> GetActiveSessionIds()
    {
        return _sessions.Keys.ToList();
    }

    #endregion

    #region Internal helpers

    private void ProcessInputs(SessionState session)
    {
        foreach (var kv in _inputQueue)
        {
            var cmd = kv.Value;
            if (!_playerToSession.TryGetValue(cmd.PlayerId, out var sid) || sid != session.SessionId)
                continue;

            if (!session.Players.TryGetValue(cmd.PlayerId, out var player))
                continue;

            var dir = Normalize(cmd.MoveX, cmd.MoveY);
            player.X += dir.x * player.Speed * TickDeltaTime;
            player.Y += dir.y * player.Speed * TickDeltaTime;
            player.Sequence = Math.Max(player.Sequence, cmd.Sequence);
        }
    }

    /// <summary>
    /// Process automatic enemy respawns based on RespawnDelay timer.
    /// Enemies with Status=Dead and RespawnTimer >= RespawnDelay will be respawned.
    /// Boss enemies are skipped (they don't respawn).
    /// Respawn limitations are enforced:
    /// - Checkpoint MaxEnemies: cannot exceed checkpoint capacity
    /// - Section EnemyCount: cannot exceed section total capacity
    /// - Section Duration: no respawns after section duration expires
    /// </summary>
    private void ProcessEnemyRespawns(SessionState session)
    {
        lock (_sessionLock)
        {
            // Use in-memory cached section/checkpoint data (populated during InitializeRoomCheckpointsAsync)
            // This avoids blocking Redis/DB calls every tick which was causing 100ms+ delays
            bool needsLimitationCheck = session.CachedSection != null;

            foreach (var enemy in session.Enemies.Values.ToList())
            {
                // Only process dead enemies
                if (enemy.Status != EnemyStatus.Dead || enemy.Hp > 0)
                    continue;

                // Skip boss respawns (boss doesn't respawn)
                if (enemy.IsBoss || enemy.RespawnDelay >= float.MaxValue)
                {
                    continue;
                }

                // Increment respawn timer
                enemy.RespawnTimer += TickDeltaTime;

                // Check if respawn delay has been reached
                if (enemy.RespawnTimer >= enemy.RespawnDelay)
                {
                    // Check respawn limitations before respawning (using in-memory cached section/checkpoint)
                    if (needsLimitationCheck && !CanRespawnEnemy(session, enemy))
                    {
                        // Skip respawn due to limitations
                        continue;
                    }

                    // Respawn enemy at spawn position
                    enemy.Hp = enemy.MaxHp;
                    enemy.X = enemy.SpawnX;
                    enemy.Y = enemy.SpawnY;
                    enemy.Status = EnemyStatus.Idle;
                    enemy.RespawnTimer = 0f;

                    _logger.LogDebug("Auto-respawned enemy {EnemyId} ({TypeId}) Lv{Level} at checkpoint {CheckpointId} after {Delay}s",
                        enemy.Id, enemy.TypeId, enemy.EnemyLevel, enemy.CheckpointId, enemy.RespawnDelay);
                }
            }
        }
    }

    /// <summary>
    /// Clean up dead enemies that won't respawn or have been dead too long.
    /// - Boss enemies: Remove immediately after death (they don't respawn)
    /// - Regular enemies: Remove after RespawnDelay * 2 if they can't respawn (due to limitations)
    /// </summary>
    private void CleanupDeadEnemies(SessionState session)
    {
        lock (_sessionLock)
        {
            var enemiesToRemove = new List<Guid>();

            foreach (var enemy in session.Enemies.Values)
            {
                // Only process dead enemies
                if (enemy.Status != EnemyStatus.Dead || enemy.Hp > 0)
                    continue;

                // Boss enemies: Remove immediately (they don't respawn)
                if (enemy.IsBoss || enemy.RespawnDelay >= float.MaxValue)
                {
                    // Give a small delay (1 second) to allow boss defeat detection
                    if (enemy.RespawnTimer >= 1.0f)
                    {
                        enemiesToRemove.Add(enemy.Id);
                        _logger.LogDebug("Removing dead boss {EnemyId} ({TypeId}) from session", enemy.Id, enemy.TypeId);
                    }
                    continue;
                }

                // Regular enemies: Remove if they've been dead too long and can't respawn
                // Wait at least RespawnDelay * 2 before removing (to allow respawn attempts)
                float cleanupDelay = enemy.RespawnDelay * 2.0f;
                if (enemy.RespawnTimer >= cleanupDelay)
                {
                    enemiesToRemove.Add(enemy.Id);
                    _logger.LogDebug("Removing dead enemy {EnemyId} ({TypeId}) that couldn't respawn after {Delay}s",
                        enemy.Id, enemy.TypeId, cleanupDelay);
                }
            }

            // Remove enemies from dictionary
            foreach (var enemyId in enemiesToRemove)
            {
                session.Enemies.Remove(enemyId);
            }

            if (enemiesToRemove.Any())
            {
                _logger.LogInformation("Cleaned up {Count} dead enemies from session {SessionId}",
                    enemiesToRemove.Count, session.SessionId);
            }
        }
    }

    /// <summary>
    /// Check if an enemy can respawn based on respawn limitations.
    /// Returns false if respawn should be blocked.
    /// Uses in-memory cached section and checkpoint data from SessionState.
    /// </summary>
    private bool CanRespawnEnemy(SessionState session, EnemyState enemy)
    {
        if (!session.CurrentSectionId.HasValue || !enemy.CheckpointId.HasValue)
        {
            return true; // No limitations if no section/checkpoint
        }

        // If section not cached, allow respawn (fail-safe)
        if (session.CachedSection == null)
        {
            return true; // No section = no limitations
        }

        var sectionCache = session.CachedSection;

        // Check section duration
        if (sectionCache.Duration > 0 && session.SectionStartTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - session.SectionStartTime.Value).TotalSeconds;
            if (elapsed >= sectionCache.Duration)
            {
                // Section duration expired, no more respawns
                return false;
            }
        }

        // Count alive enemies at this checkpoint
        int aliveAtCheckpoint = session.Enemies.Values
            .Count(e => e.CheckpointId == enemy.CheckpointId &&
                       e.Status != EnemyStatus.Dead &&
                       e.Hp > 0);

        // Get checkpoint from in-memory cache
        if (session.CachedCheckpoints.TryGetValue(enemy.CheckpointId.Value, out var checkpoint))
        {
            if (aliveAtCheckpoint >= checkpoint.MaxEnemies)
            {
                // Checkpoint at capacity
                return false;
            }
        }

        // Count total alive enemies in this section
        int aliveInSection = session.Enemies.Values
            .Count(e => e.SectionId == session.CurrentSectionId &&
                       e.Status != EnemyStatus.Dead &&
                       e.Hp > 0);

        // Check section total capacity
        if (aliveInSection >= sectionCache.EnemyCount)
        {
            // Section at capacity
            return false;
        }

        return true; // All checks passed
    }

    /// <summary>
    /// Report kill (legacy method - rewards are now automatically awarded when enemy dies in ApplyDamageToEnemy).
    /// This method is kept for backward compatibility but only marks enemy as dead if found.
    /// Rewards are NOT awarded here to prevent duplicates.
    /// </summary>
    public bool ReportKill(Guid playerId, string enemyTypeId)
    {
        if (!_playerToSession.TryGetValue(playerId, out var sessionId) || !_sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("ReportKill: Player {PlayerId} not in session", playerId);
            return false;
        }

        if (!session.Players.TryGetValue(playerId, out var player))
        {
            _logger.LogWarning("ReportKill: Player {PlayerId} state missing", playerId);
            return false;
        }

        // NOTE: Rewards are now automatically awarded in ApplyDamageToEnemy() when enemy HP reaches 0.
        // This method is kept for backward compatibility but does NOT award rewards to prevent duplicates.

        // Mark enemy as dead if found in session (find by typeId since we don't have enemyId in kill report)
        lock (_sessionLock)
        {
            var killedEnemy = session.Enemies.Values
                .FirstOrDefault(e => e.TypeId == enemyTypeId && e.Hp > 0);

            if (killedEnemy != null)
            {
                killedEnemy.Hp = 0;
                killedEnemy.Status = EnemyStatus.Dead;
                _logger.LogInformation("ReportKill: Enemy {EnemyId} ({TypeId}) marked as dead by player {PlayerId} (rewards already awarded)",
                    killedEnemy.Id, enemyTypeId, playerId);
            }
            else
            {
                // Enemy already dead or not found - this is normal since rewards are awarded automatically
                _logger.LogDebug("ReportKill: Enemy type {TypeId} already dead or not found (rewards already awarded automatically)", enemyTypeId);
            }
        }

        session.Version++;
        return true;
    }

    /// <summary>
    /// Apply damage from player to enemy. Returns (hp, maxHp) if successful, null if enemy not found.
    /// </summary>
    public (int hp, int maxHp)? ApplyDamageToEnemy(Guid playerId, Guid enemyId, int damageAmount)
    {
        if (!_playerToSession.TryGetValue(playerId, out var sessionId))
        {
            _logger.LogWarning("ApplyDamageToEnemy: Player {PlayerId} not in any session", playerId);
            return null;
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("ApplyDamageToEnemy: Session {SessionId} not found", sessionId);
            return null;
        }

        if (damageAmount <= 0)
        {
            _logger.LogWarning("ApplyDamageToEnemy: Invalid damage amount {Damage}", damageAmount);
            return null;
        }

        lock (_sessionLock)
        {
            // Debug: Log all enemy IDs in session
            var enemyIds = session.Enemies.Keys.Select(id => id.ToString()[..8]).ToList();
            _logger.LogInformation("ApplyDamageToEnemy: Session {SessionId} has {Count} enemies: [{Ids}]",
                sessionId, session.Enemies.Count, string.Join(", ", enemyIds));

            if (!session.Enemies.TryGetValue(enemyId, out var enemy))
            {
                _logger.LogWarning("ApplyDamageToEnemy: Enemy {EnemyId} not found in session {SessionId}. Looking for: {LookingFor}",
                    enemyId.ToString()[..8], sessionId, enemyId);
                return null;
            }

            if (enemy.Hp <= 0)
            {
                _logger.LogDebug("ApplyDamageToEnemy: Enemy {EnemyId} is already dead", enemyId);
                return (enemy.Hp, enemy.MaxHp);
            }

            var oldHp = enemy.Hp;
            enemy.Hp = Math.Max(0, enemy.Hp - damageAmount);
            session.Version++;

            _logger.LogInformation("Applied {Damage} damage from player {PlayerId} to enemy {EnemyId} ({TypeId}). HP: {OldHp} -> {NewHp}/{MaxHp}, oldHp > 0: {OldHpPositive}",
                damageAmount, playerId, enemyId, enemy.TypeId, oldHp, enemy.Hp, enemy.MaxHp, oldHp > 0);

            // Mark enemy as dead if HP reaches 0 and award kill rewards
            if (enemy.Hp <= 0)
            {
                enemy.Status = EnemyStatus.Dead;
                // Reset respawn timer to start counting from 0
                enemy.RespawnTimer = 0f;

                _logger.LogInformation("Enemy {EnemyId} ({TypeId}) defeated by player {PlayerId} (respawn in {Delay}s)",
                    enemyId, enemy.TypeId, playerId, enemy.RespawnDelay);

                // Automatically award kill rewards when enemy dies (server-authoritative)
                // Only award if enemy was alive before this damage
                if (oldHp > 0)
                {
                    try
                    {
                        // Use in-memory cache to avoid blocking DB/Redis queries during game tick
                        // Cache is populated on-demand (lazy loading)
                        GameServer.Services.EnemyConfig? enemyCfg = GetEnemyConfigCached(enemy.TypeId);

                        if (enemyCfg != null)
                        {
                            // Log enemy config values for debugging (use LogInformation to ensure it shows)
                            _logger.LogInformation("Enemy config for {TypeId}: ExpReward={ExpReward}, GoldReward={GoldReward}, MaxHealth={MaxHealth}",
                                enemy.TypeId, enemyCfg.ExpReward, enemyCfg.GoldReward, enemyCfg.MaxHealth);

                            // Check if rewards are valid
                            if (enemyCfg.ExpReward <= 0 && enemyCfg.GoldReward <= 0)
                            {
                                _logger.LogWarning("Enemy {TypeId} has zero rewards (ExpReward={ExpReward}, GoldReward={GoldReward}). Please check database configuration.",
                                    enemy.TypeId, enemyCfg.ExpReward, enemyCfg.GoldReward);
                            }

                            if (session.Players.TryGetValue(playerId, out var player))
                            {
                                int oldExp = player.Exp;
                                int oldGold = player.Gold;
                                int oldLevel = player.Level;

                                AwardKillRewards(player, enemyCfg);

                                _logger.LogInformation("Awarded kill rewards to player {PlayerId} for killing {EnemyTypeId}: Exp={OldExp}+{ExpReward}={NewExp}, Gold={OldGold}+{GoldReward}={NewGold}, Level={OldLevel}->{NewLevel}",
                                    playerId, enemy.TypeId,
                                    oldExp, enemyCfg.ExpReward, player.Exp,
                                    oldGold, enemyCfg.GoldReward, player.Gold,
                                    oldLevel, player.Level);
                            }
                            else
                            {
                                _logger.LogWarning("ApplyDamageToEnemy: Player {PlayerId} not found in session {SessionId}", playerId, sessionId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("ApplyDamageToEnemy: Enemy config not found for typeId {TypeId}, cannot award rewards", enemy.TypeId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ApplyDamageToEnemy: Exception while awarding rewards for enemy {TypeId} to player {PlayerId}",
                            enemy.TypeId, playerId);
                    }
                }
                else
                {
                    _logger.LogDebug("ApplyDamageToEnemy: Enemy {EnemyId} was already dead (oldHp={OldHp}), skipping reward award", enemyId, oldHp);
                }
            }

            return (enemy.Hp, enemy.MaxHp);
        }
    }

    /// <summary>
    /// Apply damage to player from enemy. Returns (hp, maxHp) if successful, null if player not found.
    /// </summary>
    public (int hp, int maxHp)? ApplyDamage(Guid playerId, int damageAmount)
    {
        if (!_playerToSession.TryGetValue(playerId, out var sessionId))
        {
            _logger.LogWarning("ApplyDamage: Player {PlayerId} not in any session", playerId);
            return null;
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("ApplyDamage: Session {SessionId} not found", sessionId);
            return null;
        }

        lock (_sessionLock)
        {
            if (!session.Players.TryGetValue(playerId, out var player))
            {
                _logger.LogWarning("ApplyDamage: Player {PlayerId} state missing", playerId);
                return null;
            }

            player.Hp = Math.Max(0, player.Hp - damageAmount);
            session.Version++;

            _logger.LogDebug("Applied {Damage} damage to {Player}. HP: {Hp}/{MaxHp}",
                damageAmount, player.Name, player.Hp, player.MaxHp);

            return (player.Hp, player.MaxHp);
        }
    }

    /// <summary>
    /// Respawn player at spawn position with 50% health. Returns (x, y, hp, maxHp) if successful, null if player not found.
    /// </summary>
    public (float x, float y, int hp, int maxHp)? RespawnPlayer(Guid playerId)
    {
        if (!_playerToSession.TryGetValue(playerId, out var sessionId))
        {
            _logger.LogWarning("RespawnPlayer: Player {PlayerId} not in any session", playerId);
            return null;
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("RespawnPlayer: Session {SessionId} not found", sessionId);
            return null;
        }

        lock (_sessionLock)
        {
            if (!session.Players.TryGetValue(playerId, out var player))
            {
                _logger.LogWarning("RespawnPlayer: Player {PlayerId} state missing", playerId);
                return null;
            }

            // Hardcoded spawn position (player stats are managed via database)
            const float spawnX = -16f;
            const float spawnY = 12f;
            player.X = spawnX;
            player.Y = spawnY;

            // Respawn with 50% health
            player.Hp = player.MaxHp / 2;

            session.Version++;

            _logger.LogInformation("Respawned {Player} at ({X}, {Y}) with {Hp}/{MaxHp} HP",
                player.Name, player.X, player.Y, player.Hp, player.MaxHp);

            return (player.X, player.Y, player.Hp, player.MaxHp);
        }
    }

    private void AwardKillRewards(PlayerState player, EnemyConfig enemy)
    {
        if (enemy == null)
        {
            _logger.LogWarning("AwardKillRewards: Enemy config is null, cannot award rewards");
            return;
        }

        // Award EXP and Gold (apply exp bonus if available)
        float expMultiplier = 1f + Math.Max(0f, player.ExpBonusPercent);
        int expReward = (int)(enemy.ExpReward * expMultiplier);
        player.Exp += expReward;
        player.Gold += enemy.GoldReward;

        // Check for level up
        int expNeeded = _config.GetExpForNextLevel(player.Level);
        while (player.Exp >= expNeeded && player.Level < _config.ExpCurve.LevelCap)
        {
            player.Exp -= expNeeded;
            player.Level++;
            _logger.LogInformation("{Player} leveled up to {Level}!", player.Name, player.Level);
            expNeeded = _config.GetExpForNextLevel(player.Level);
        }

        // Update ExpToLevel after potential level changes
        player.ExpToLevel = _config.GetExpForNextLevel(player.Level);
    }

    private SessionState CreateDefaultSession(string sessionId)
    {
        return new SessionState
        {
            SessionId = sessionId,
            Version = 1
        };
    }

    /// <summary>
    /// DEPRECATED: Use RegisterOrLoadPlayer() instead.
    /// Players must be created via Admin Panel or Register page, not auto-generated.
    /// </summary>
    [Obsolete("Use RegisterOrLoadPlayer() instead. Players must be created via Admin Panel or Register page.")]
    private PlayerState CreateDefaultPlayer(Guid playerId, string playerName)
    {
        // Hardcoded defaults for backward compatibility
        // New players should be created via PlayerWebService.CreatePlayerAccountAsync()
        return new PlayerState
        {
            Id = playerId,
            Name = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName,
            X = -16f,
            Y = 12f,
            Hp = 50,
            MaxHp = 50,
            Damage = 10,
            Range = 1.5f,
            Speed = 4f,
            WeaponRange = 1.5f,
            KnockbackForce = 5f,
            KnockbackTime = 0.2f,
            StunTime = 0.3f,
            BonusDamagePercent = 0f,
            DamageReductionPercent = 0f,
            Sequence = 0,
            Level = 1,
            Exp = 0,
            Gold = 100
        };
    }

    /// <summary>
    /// Initialize checkpoints and spawn enemies for a room (called once when room is first created).
    /// Loads checkpoints from first active GameSection, or all active checkpoints as fallback.
    /// Spawns regular enemies at checkpoints 1..N-1 and boss at last checkpoint.
    /// </summary>
    private async Task InitializeRoomCheckpointsAsync(string sessionId, int? sectionId = null)
    {
        _logger.LogInformation("InitializeRoomCheckpointsAsync: Starting for session {SessionId}", sessionId);

        if (!_sessions.TryGetValue(sessionId, out var sessionState))
        {
            _logger.LogWarning("InitializeRoomCheckpoints: Session {SessionId} not found", sessionId);
            return;
        }

        // Check if already initialized for this section (don't respawn if enemies already exist for same section)
        // Allow re-initialization if we're initializing a different section (section progression)
        if (sessionState.Enemies.Any())
        {
            if (sectionId.HasValue && sessionState.CurrentSectionId == sectionId)
            {
                _logger.LogInformation("Room {SessionId} already initialized for section {SectionId} with {EnemyCount} enemies",
                    sessionId, sectionId, sessionState.Enemies.Count);
                return;
            }
            // If sectionId is different or null, allow re-initialization (section progression or fallback)
        }

        try
        {
            // Resolve CheckpointService and GameDbContext from DI (requires scope)
            using var scope = _serviceProvider.CreateScope();
            var checkpointService = scope.ServiceProvider.GetRequiredService<CheckpointService>();
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            List<Checkpoint> checkpoints;
            GameSection? section = null;

            // Determine which section to use
            int? sectionToUse = sectionId;

            if (!sectionToUse.HasValue)
            {
                // Try Redis cache first for first active section
                if (_redis != null)
                {
                    // Note: We can't cache "first active" query easily, so query DB
                    // But we can cache the result section once found
                }

                // Load first active GameSection
                section = await db.GameSections
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.SectionId)
                    .FirstOrDefaultAsync();

                if (section != null)
                {
                    sectionToUse = section.SectionId;
                    _logger.LogInformation("Using first active GameSection: {SectionName} (ID: {SectionId}) for room {SessionId}",
                        section.Name, sectionToUse, sessionId);
                }
            }
            else
            {
                // Try Redis cache first
                GameSectionCache? sectionCache = null;
                if (_redis != null)
                {
                    sectionCache = await _redis.GetGameSectionAsync(sectionToUse.Value);
                }

                if (sectionCache != null)
                {
                    // Convert cache to entity for compatibility
                    section = new GameSection
                    {
                        SectionId = sectionCache.SectionId,
                        Name = sectionCache.Name,
                        EnemyCount = sectionCache.EnemyCount,
                        EnemyLevel = sectionCache.EnemyLevel,
                        SpawnRate = sectionCache.SpawnRate,
                        Duration = sectionCache.Duration
                    };
                    _logger.LogInformation("Using cached GameSection: {SectionName} (ID: {SectionId}) for room {SessionId}",
                        section.Name, sectionToUse, sessionId);
                }
                else
                {
                    // Load from DB and cache
                    section = await db.GameSections.FindAsync(sectionToUse.Value);
                    if (section != null)
                    {
                        _logger.LogInformation("Using specified GameSection: {SectionName} (ID: {SectionId}) for room {SessionId}",
                            section.Name, sectionToUse, sessionId);

                        // Cache for next time
                        if (_redis != null)
                        {
                            var cache = new GameSectionCache
                            {
                                SectionId = section.SectionId,
                                Name = section.Name,
                                EnemyCount = section.EnemyCount,
                                EnemyLevel = section.EnemyLevel,
                                SpawnRate = section.SpawnRate,
                                Duration = section.Duration
                            };
                            await _redis.SetGameSectionAsync(section.SectionId, cache);
                        }
                    }
                }
            }

            // Load checkpoints by section if sectionId provided, otherwise load all active
            if (sectionToUse.HasValue)
            {
                // Try Redis cache first
                List<CheckpointCache>? checkpointsCache = null;
                if (_redis != null)
                {
                    checkpointsCache = await _redis.GetCheckpointsBySectionAsync(sectionToUse.Value);
                }

                if (checkpointsCache != null)
                {
                    // Convert cache to entities
                    checkpoints = checkpointsCache.Select(c => new Checkpoint
                    {
                        CheckpointId = c.CheckpointId,
                        CheckpointName = c.CheckpointName,
                        SectionId = c.SectionId,
                        X = c.X,
                        Y = c.Y,
                        EnemyPool = c.EnemyPool,
                        MaxEnemies = c.MaxEnemies,
                        IsActive = c.IsActive
                    }).ToList();
                    _logger.LogInformation("Loaded {Count} checkpoints from cache for section {SectionId}", checkpoints.Count, sectionToUse.Value);
                }
                else
                {
                    // Load from DB and cache
                    checkpoints = await checkpointService.GetCheckpointsBySectionAsync(sectionToUse.Value);
                    _logger.LogInformation("Loaded {Count} checkpoints for section {SectionId}", checkpoints.Count, sectionToUse.Value);

                    // Cache for next time
                    if (_redis != null && checkpoints.Any())
                    {
                        var cache = checkpoints.Select(c => new CheckpointCache
                        {
                            CheckpointId = c.CheckpointId,
                            CheckpointName = c.CheckpointName,
                            SectionId = c.SectionId,
                            X = c.X,
                            Y = c.Y,
                            EnemyPool = c.EnemyPool,
                            MaxEnemies = c.MaxEnemies,
                            IsActive = c.IsActive
                        }).ToList();
                        await _redis.SetCheckpointsBySectionAsync(sectionToUse.Value, cache);
                    }
                }
            }
            else
            {
                // Fallback: load all active checkpoints (backward compatibility)
                checkpoints = await checkpointService.GetAllActiveCheckpointsAsync();
                _logger.LogWarning("No GameSection specified, using all active checkpoints (fallback) for room {SessionId}", sessionId);
            }

            if (!checkpoints.Any())
            {
                _logger.LogWarning("No active checkpoints found for room {SessionId} (sectionId: {SectionId})",
                    sessionId, sectionToUse?.ToString() ?? "null");
                return;
            }

            // Sort checkpoints by CheckpointId to identify last checkpoint (boss checkpoint)
            var sortedCheckpoints = checkpoints.Where(c => c.IsActive).OrderBy(c => c.CheckpointId).ToList();
            if (!sortedCheckpoints.Any())
            {
                _logger.LogWarning("No active checkpoints after sorting for room {SessionId}", sessionId);
                return;
            }

            var lastCheckpoint = sortedCheckpoints.Last();
            var regularCheckpoints = sortedCheckpoints.Take(sortedCheckpoints.Count - 1).ToList();

            // Get first enemy type from first checkpoint for boss fallback
            string? firstEnemyType = null;
            if (sortedCheckpoints.Any())
            {
                var firstCheckpointEnemyTypes = CheckpointService.ParseEnemyPool(sortedCheckpoints.First().EnemyPool);
                firstEnemyType = firstCheckpointEnemyTypes.FirstOrDefault();
            }

            // Generate deterministic seed from sessionId
            int seed = sessionId.GetHashCode();
            var random = new Random(seed);

            _logger.LogInformation("Initializing room {SessionId} with seed {Seed}, {RegularCount} regular checkpoints, 1 boss checkpoint (ID: {BossCheckpointId})",
                sessionId, seed, regularCheckpoints.Count, lastCheckpoint.CheckpointId);

            // Collect all enemies to spawn first (outside lock, can use await)
            var enemiesToSpawn = new List<(string typeId, float x, float y, EnemyConfig config, int checkpointId, bool isBoss)>();

            // Calculate total desired enemies from all checkpoints (excluding boss)
            int totalDesiredEnemies = regularCheckpoints.Sum(c => c.MaxEnemies);
            int sectionMaxEnemies = section?.EnemyCount ?? int.MaxValue;

            // Boss counts as 1 enemy, so reserve 1 slot for boss
            int availableSlots = sectionMaxEnemies - 1; // Reserve 1 for boss
            int enemiesToSpawnCount = Math.Min(totalDesiredEnemies, availableSlots);

            if (totalDesiredEnemies > availableSlots)
            {
                _logger.LogWarning("Section {SectionId} has {Desired} desired enemies but only {Max} slots available (including boss). Spawning {Actual} enemies.",
                    sectionToUse, totalDesiredEnemies, sectionMaxEnemies, enemiesToSpawnCount);
            }

            // Spawn regular enemies at checkpoints 1..N-1 (respecting section EnemyCount)
            int spawnedCount = 0;
            foreach (var checkpoint in regularCheckpoints)
            {
                // Stop if we've reached section limit
                if (spawnedCount >= enemiesToSpawnCount)
                {
                    _logger.LogInformation("Reached section EnemyCount limit ({Limit}), stopping spawn at checkpoint {CheckpointId}",
                        sectionMaxEnemies, checkpoint.CheckpointId);
                    break;
                }

                // Parse enemy pool JSON: ["slime", "goblin", "slime"]
                var enemyTypes = CheckpointService.ParseEnemyPool(checkpoint.EnemyPool);
                if (!enemyTypes.Any())
                {
                    _logger.LogWarning("Checkpoint {CheckpointName} has empty enemy pool", checkpoint.CheckpointName);
                    continue;
                }

                // Calculate how many enemies to spawn at this checkpoint
                int remainingSlots = enemiesToSpawnCount - spawnedCount;
                int enemiesAtThisCheckpoint = Math.Min(checkpoint.MaxEnemies, remainingSlots);

                // Spawn random enemies from pool
                for (int i = 0; i < enemiesAtThisCheckpoint; i++)
                {
                    var enemyTypeId = enemyTypes[random.Next(enemyTypes.Length)];

                    // Try EnemyConfigService (database-first) directly
                    EnemyConfig? enemyConfig = null;
                    try
                    {
                        var enemyConfigService = scope.ServiceProvider.GetService<EnemyConfigService>();
                        if (enemyConfigService != null)
                        {
                            enemyConfig = await enemyConfigService.GetEnemyAsync(enemyTypeId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load enemy {TypeId} from EnemyConfigService", enemyTypeId);
                    }

                    if (enemyConfig == null)
                    {
                        _logger.LogWarning("Enemy type {TypeId} not found, skipping spawn at {CheckpointName}",
                            enemyTypeId, checkpoint.CheckpointName);
                        continue;
                    }

                    enemiesToSpawn.Add((enemyTypeId, checkpoint.X, checkpoint.Y, enemyConfig, checkpoint.CheckpointId, false));
                    spawnedCount++;
                }
            }

            // Spawn boss at last checkpoint
            var bossCheckpointEnemyTypes = CheckpointService.ParseEnemyPool(lastCheckpoint.EnemyPool);
            if (bossCheckpointEnemyTypes.Any())
            {
                // Load boss config
                var bossConfig = await LoadBossConfigAsync(scope.ServiceProvider, section, firstEnemyType);
                if (bossConfig != null)
                {
                    enemiesToSpawn.Add((bossConfig.TypeId, lastCheckpoint.X, lastCheckpoint.Y, bossConfig, lastCheckpoint.CheckpointId, true));
                    _logger.LogInformation("Boss {BossTypeId} will spawn at checkpoint {CheckpointId} ({X}, {Y})",
                        bossConfig.TypeId, lastCheckpoint.CheckpointId, lastCheckpoint.X, lastCheckpoint.Y);
                }
                else
                {
                    _logger.LogWarning("Failed to load boss config for checkpoint {CheckpointId}, skipping boss spawn", lastCheckpoint.CheckpointId);
                }
            }
            else
            {
                _logger.LogWarning("Boss checkpoint {CheckpointId} has empty enemy pool, skipping boss spawn", lastCheckpoint.CheckpointId);
            }

            // Now lock and add all enemies to session state
            lock (_sessionLock)
            {
                // Double-check enemies weren't added while we were loading configs
                if (sessionState.Enemies.Any())
                {
                    _logger.LogInformation("Room {SessionId} already initialized while loading configs, skipping", sessionId);
                    return;
                }

                // Store section info in SessionState
                if (sectionToUse.HasValue && section != null)
                {
                    sessionState.CurrentSectionId = sectionToUse.Value;
                    sessionState.SectionStartTime = DateTime.UtcNow;

                    // Cache section config in memory to avoid Redis/DB queries during game tick
                    sessionState.CachedSection = new CachedSectionConfig
                    {
                        SectionId = section.SectionId,
                        Name = section.Name,
                        EnemyCount = section.EnemyCount,
                        EnemyLevel = section.EnemyLevel,
                        SpawnRate = section.SpawnRate,
                        Duration = section.Duration ?? 0f // 0 = unlimited duration
                    };

                    // Cache checkpoint configs for respawn limitation checks
                    sessionState.CachedCheckpoints = sortedCheckpoints.ToDictionary(
                        c => c.CheckpointId,
                        c => new CachedCheckpointConfig
                        {
                            CheckpointId = c.CheckpointId,
                            MaxEnemies = c.MaxEnemies
                        });

                    _logger.LogInformation("Session {SessionId} initialized with Section {SectionId} ({SectionName}), EnemyLevel={EnemyLevel}, SpawnRate={SpawnRate}. Cached {CheckpointCount} checkpoints.",
                        sessionId, section.SectionId, section.Name, section.EnemyLevel, section.SpawnRate, sessionState.CachedCheckpoints.Count);
                }

                int totalSpawned = 0;
                Guid? bossId = null;

                foreach (var (typeId, x, y, config, checkpointId, isBoss) in enemiesToSpawn)
                {
                    // Create EnemyState with deterministic spawn position
                    var enemyState = CreateEnemyState(typeId, x, y, config);

                    // Set tracking fields
                    enemyState.CheckpointId = checkpointId;
                    enemyState.SectionId = sectionToUse;
                    enemyState.IsBoss = isBoss;
                    enemyState.EnemyLevel = section?.EnemyLevel ?? 1;

                    // Set BaseRespawnDelay and adjust RespawnDelay by SpawnRate
                    enemyState.BaseRespawnDelay = config.RespawnDelay;
                    if (isBoss)
                    {
                        // Boss doesn't respawn
                        enemyState.RespawnDelay = float.MaxValue;
                    }
                    else
                    {
                        // Regular enemies: adjust by SpawnRate (SpawnRate > 1 = faster respawn)
                        float spawnRate = section?.SpawnRate ?? 1.0f;
                        enemyState.RespawnDelay = config.RespawnDelay / spawnRate;
                    }

                    // Apply level scaling
                    ScaleEnemyStatsByLevel(enemyState, config, enemyState.EnemyLevel);

                    sessionState.Enemies[enemyState.Id] = enemyState;
                    totalSpawned++;

                    if (isBoss)
                    {
                        bossId = enemyState.Id;
                        sessionState.CurrentBossId = bossId;
                        sessionState.IsBossAlive = true;
                        _logger.LogInformation("Spawned BOSS {EnemyId} ({TypeId}) Lv{Level} at checkpoint {CheckpointId} ({X}, {Y})",
                            enemyState.Id, typeId, enemyState.EnemyLevel, checkpointId, x, y);
                    }
                    else
                    {
                        _logger.LogInformation("Spawned enemy {EnemyId} ({TypeId}) Lv{Level} at checkpoint {CheckpointId} ({X}, {Y})",
                            enemyState.Id, typeId, enemyState.EnemyLevel, checkpointId, x, y);
                    }
                }

                sessionState.Version++;
                _logger.LogInformation("Room {SessionId} initialized: spawned {TotalSpawned} enemies ({RegularCount} regular, {BossCount} boss) at {CheckpointCount} checkpoints",
                    sessionId, totalSpawned, totalSpawned - (bossId.HasValue ? 1 : 0), bossId.HasValue ? 1 : 0, checkpoints.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize checkpoints for room {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Create EnemyState from typeId, position, and config.
    /// </summary>
    private EnemyState CreateEnemyState(string typeId, float x, float y, EnemyConfig config)
    {
        return new EnemyState
        {
            Id = Guid.NewGuid(),
            TypeId = typeId,
            X = x,
            Y = y,
            SpawnX = x,
            SpawnY = y,
            Hp = config.MaxHealth,
            MaxHp = config.MaxHealth,
            Damage = config.Damage,
            Speed = config.Speed,
            DetectRange = config.DetectRange,
            AttackRange = config.AttackRange,
            AttackCooldown = config.AttackCooldown,
            RespawnDelay = config.RespawnDelay,
            BaseRespawnDelay = config.RespawnDelay,
            ExpReward = config.ExpReward,
            GoldReward = config.GoldReward,
            Status = EnemyStatus.Idle
        };
    }

    /// <summary>
    /// Manually respawn an enemy at its spawn position.
    /// </summary>
    public bool RespawnEnemy(Guid enemyId, string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("RespawnEnemy: Session {SessionId} not found", sessionId);
            return false;
        }

        lock (_sessionLock)
        {
            if (!session.Enemies.TryGetValue(enemyId, out var enemy))
            {
                _logger.LogWarning("RespawnEnemy: Enemy {EnemyId} not found in session {SessionId}", enemyId, sessionId);
                return false;
            }

            enemy.Hp = enemy.MaxHp;
            enemy.X = enemy.SpawnX;
            enemy.Y = enemy.SpawnY;
            enemy.Status = EnemyStatus.Idle;
            enemy.RespawnTimer = 0f;

            session.Version++;
            _logger.LogInformation("Respawned enemy {EnemyId} ({TypeId}) at ({X}, {Y})",
                enemy.Id, enemy.TypeId, enemy.X, enemy.Y);

            return true;
        }
    }

    /// <summary>
    /// Scale enemy stats by level. Formula: multiplier = 1f + (level - 1) * 0.1f (+10% per level above 1)
    /// Only scales: MaxHp, Hp, Damage. Speed and ranges remain unchanged.
    /// </summary>
    private void ScaleEnemyStatsByLevel(EnemyState enemy, EnemyConfig baseConfig, int level)
    {
        if (level <= 1) return; // No scaling for level 1

        float multiplier = 1f + (level - 1) * 0.1f; // +10% per level above 1

        enemy.MaxHp = (int)(baseConfig.MaxHealth * multiplier);
        enemy.Hp = enemy.MaxHp; // Full HP on spawn
        enemy.Damage = (int)(baseConfig.Damage * multiplier);
        // Speed, ranges don't scale (keep base values)
    }

    /// <summary>
    /// Load boss config. Priority: GameSection.EnemyTypeId, then boss_{firstEnemyType}, then create enhanced config from regular enemy.
    /// </summary>
    private async Task<EnemyConfig?> LoadBossConfigAsync(IServiceProvider serviceProvider, GameSection? section, string? firstEnemyType)
    {
        string? bossTypeId = null;

        // Priority 1: GameSection.EnemyTypeId
        if (section != null && !string.IsNullOrEmpty(section.EnemyTypeId))
        {
            bossTypeId = section.EnemyTypeId;
            _logger.LogInformation("Using GameSection.EnemyTypeId as boss typeId: {BossTypeId}", bossTypeId);
        }
        // Priority 2: boss_{firstEnemyType}
        else if (!string.IsNullOrEmpty(firstEnemyType))
        {
            bossTypeId = $"boss_{firstEnemyType}";
            _logger.LogInformation("Using boss_{FirstEnemyType} convention: {BossTypeId}", firstEnemyType, bossTypeId);
        }

        if (string.IsNullOrEmpty(bossTypeId))
        {
            _logger.LogWarning("Cannot determine boss typeId: section.EnemyTypeId is null and firstEnemyType is null");
            return null;
        }

        // Try to load boss config from EnemyConfigService
        try
        {
            var enemyConfigService = serviceProvider.GetService<EnemyConfigService>();
            if (enemyConfigService != null)
            {
                var bossConfig = await enemyConfigService.GetEnemyAsync(bossTypeId);
                if (bossConfig != null)
                {
                    _logger.LogInformation("Loaded boss config from database: {BossTypeId}", bossTypeId);
                    return bossConfig;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load boss config {BossTypeId} from EnemyConfigService, will create enhanced config", bossTypeId);
        }

        // Fallback: Create enhanced config from regular enemy
        if (!string.IsNullOrEmpty(firstEnemyType))
        {
            try
            {
                var enemyConfigService = serviceProvider.GetService<EnemyConfigService>();
                if (enemyConfigService != null)
                {
                    var baseConfig = await enemyConfigService.GetEnemyAsync(firstEnemyType);
                    if (baseConfig != null)
                    {
                        // Create enhanced boss config: 3x HP, 2x Damage, 0.8x Speed, 5x rewards
                        var enhancedConfig = new EnemyConfig
                        {
                            TypeId = bossTypeId,
                            MaxHealth = baseConfig.MaxHealth * 3,
                            Damage = baseConfig.Damage * 2,
                            Speed = baseConfig.Speed * 0.8f,
                            DetectRange = baseConfig.DetectRange,
                            AttackRange = baseConfig.AttackRange,
                            AttackCooldown = baseConfig.AttackCooldown,
                            RespawnDelay = float.MaxValue, // Boss doesn't respawn
                            ExpReward = baseConfig.ExpReward * 5,
                            GoldReward = baseConfig.GoldReward * 5
                        };

                        _logger.LogInformation("Created enhanced boss config from {BaseTypeId}: HP={Hp} (3x), DMG={Dmg} (2x), SPD={Spd} (0.8x), EXP={Exp} (5x), GOLD={Gold} (5x)",
                            firstEnemyType, enhancedConfig.MaxHealth, enhancedConfig.Damage, enhancedConfig.Speed,
                            enhancedConfig.ExpReward, enhancedConfig.GoldReward);
                        return enhancedConfig;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create enhanced boss config from {FirstEnemyType}", firstEnemyType);
            }
        }

        _logger.LogWarning("Could not load or create boss config for {BossTypeId}", bossTypeId);
        return null;
    }

    /// <summary>
    /// Load next active section from database (SectionId > CurrentSectionId AND IsActive == true).
    /// </summary>
    private async Task<GameSection?> LoadNextSectionAsync(int currentSectionId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            var nextSection = await db.GameSections
                .Where(s => s.SectionId > currentSectionId && s.IsActive)
                .OrderBy(s => s.SectionId)
                .FirstOrDefaultAsync();

            if (nextSection != null)
            {
                _logger.LogInformation("Loaded next section: {SectionName} (ID: {SectionId})", nextSection.Name, nextSection.SectionId);
            }
            else
            {
                _logger.LogInformation("No next section found after SectionId {CurrentSectionId}", currentSectionId);
            }

            return nextSection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load next section after SectionId {CurrentSectionId}", currentSectionId);
            return null;
        }
    }

    /// <summary>
    /// Check if all players in session are dead. If true, set Status = SessionStatus.Failed.
    /// </summary>
    private void CheckAllPlayersDead(SessionState session)
    {
        if (session.Status != SessionStatus.InProgress)
            return; // Already completed or failed

        if (!session.Players.Any())
            return; // No players in session

        bool allDead = session.Players.Values.All(p => p.Hp <= 0);

        if (allDead)
        {
            session.Status = SessionStatus.Failed;
            _logger.LogWarning("All players dead in session {SessionId}. Session status set to Failed.", session.SessionId);
        }
    }

    /// <summary>
    /// Check if boss is defeated and advance to next section if applicable.
    /// Called each tick in TickAsync().
    /// </summary>
    private async Task CheckBossDefeatedAndAdvanceSection(SessionState session)
    {
        if (session.Status != SessionStatus.InProgress)
            return; // Session already completed or failed

        if (!session.CurrentBossId.HasValue || !session.IsBossAlive)
            return; // No boss to check

        lock (_sessionLock)
        {
            // Check if boss still exists and is alive
            if (!session.Enemies.TryGetValue(session.CurrentBossId.Value, out var boss))
            {
                // Boss not found, mark as not alive
                session.IsBossAlive = false;
                _logger.LogWarning("Boss {BossId} not found in session {SessionId}", session.CurrentBossId.Value, session.SessionId);
                return;
            }

            // Check if boss is defeated
            if (boss.Hp <= 0 || boss.Status == EnemyStatus.Dead)
            {
                session.IsBossAlive = false;
                _logger.LogInformation("Boss {BossId} ({TypeId}) defeated in session {SessionId}", boss.Id, boss.TypeId, session.SessionId);

                // Remove boss from enemies dictionary immediately (boss doesn't respawn)
                session.Enemies.Remove(boss.Id);
                _logger.LogDebug("Removed defeated boss {BossId} from session {SessionId}", boss.Id, session.SessionId);

                // Mark section as complete
                if (session.CurrentSectionId.HasValue)
                {
                    if (!session.CompletedSections.Contains(session.CurrentSectionId.Value))
                    {
                        session.CompletedSections.Add(session.CurrentSectionId.Value);
                        _logger.LogInformation("Section {SectionId} marked as completed in session {SessionId}",
                            session.CurrentSectionId.Value, session.SessionId);
                    }
                }

                // Load next section (async, outside lock)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (session.CurrentSectionId.HasValue)
                        {
                            var nextSection = await LoadNextSectionAsync(session.CurrentSectionId.Value);

                            if (nextSection != null)
                            {
                                // Initialize next section
                                _logger.LogInformation("Advancing to next section {SectionId} ({SectionName}) in session {SessionId}",
                                    nextSection.SectionId, nextSection.Name, session.SessionId);

                                // Clear old enemies before initializing next section
                                lock (_sessionLock)
                                {
                                    session.Enemies.Clear();
                                    session.CurrentBossId = null;
                                    session.IsBossAlive = false;
                                }

                                await InitializeRoomCheckpointsAsync(session.SessionId, nextSection.SectionId);
                            }
                            else
                            {
                                // No more sections, session completed
                                lock (_sessionLock)
                                {
                                    session.Status = SessionStatus.Completed;
                                    _logger.LogInformation("All sections completed in session {SessionId}. Session status set to Completed.", session.SessionId);
                                }
                            }
                        }
                        else
                        {
                            // No current section, mark as completed
                            lock (_sessionLock)
                            {
                                session.Status = SessionStatus.Completed;
                                _logger.LogInformation("No current section in session {SessionId}. Session status set to Completed.", session.SessionId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error advancing section in session {SessionId}", session.SessionId);
                    }
                });
            }
        }
    }

    /// <summary>
    /// Get enemy config from in-memory cache (avoids DB/Redis queries during game tick).
    /// Cache is populated on-demand (lazy loading).
    /// </summary>
    private GameServer.Services.EnemyConfig? GetEnemyConfigCached(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            return null;

        // Try cache first (fast path - no lock needed for read)
        if (_enemyConfigCache.TryGetValue(typeId, out var cached))
        {
            return cached;
        }

        // Cache miss - load from DB/Redis and cache it (lock to prevent duplicate loads)
        lock (_enemyConfigCacheLock)
        {
            // Double-check after acquiring lock
            if (_enemyConfigCache.TryGetValue(typeId, out cached))
            {
                return cached;
            }

            // Load from EnemyConfigService (tries Redis, then DB)
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var enemyConfigService = scope.ServiceProvider.GetService<EnemyConfigService>();
                if (enemyConfigService != null)
                {
                    // Use async method but wait for result (only happens once per enemy type)
                    var configTask = enemyConfigService.GetEnemyAsync(typeId);
                    configTask.Wait(); // Block only on first load per enemy type
                    cached = configTask.Result;

                    if (cached != null)
                    {
                        _enemyConfigCache[typeId] = cached;
                        _logger.LogDebug("Cached enemy config for {TypeId} (ExpReward={ExpReward}, GoldReward={GoldReward})",
                            typeId, cached.ExpReward, cached.GoldReward);
                    }
                    else
                    {
                        _logger.LogWarning("Enemy config not found for {TypeId}", typeId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load enemy config for {TypeId}", typeId);
            }

            return cached;
        }
    }

    /// <summary>
    /// Preload all enemy configs into cache (optional - can be called on server start).
    /// This avoids any blocking calls during game tick.
    /// </summary>
    public async Task PreloadEnemyConfigsAsync()
    {
        if (_enemyConfigsLoaded)
            return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var enemyConfigService = scope.ServiceProvider.GetService<EnemyConfigService>();
            if (enemyConfigService == null)
            {
                _logger.LogWarning("EnemyConfigService not available for preloading");
                return;
            }

            var allEnemies = await enemyConfigService.GetAllEnemiesAsync();
            lock (_enemyConfigCacheLock)
            {
                foreach (var config in allEnemies)
                {
                    _enemyConfigCache[config.TypeId] = config;
                }
                _enemyConfigsLoaded = true;
            }

            _logger.LogInformation("Preloaded {Count} enemy configs into memory cache", allEnemies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preload enemy configs");
        }
    }

    private static float Clamp(float v) => MathF.Max(-1f, MathF.Min(1f, v));

    private static (float x, float y) Normalize(float x, float y)
    {
        float mag = MathF.Sqrt(x * x + y * y);
        if (mag <= 0.0001f) return (0, 0);
        return (x / mag, y / mag);
    }

    #endregion
}

internal class InputCommand
{
    public Guid PlayerId { get; set; }
    public string SessionId { get; set; } = "default";
    public float MoveX { get; set; }
    public float MoveY { get; set; }
    public float AimX { get; set; }
    public float AimY { get; set; }
    public bool Attack { get; set; }
    public bool Shoot { get; set; }
    public int Sequence { get; set; }
}

