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
using System.Threading;

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
    private readonly object _sessionLock = new(); // Lock for session operations that need atomicity

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
    /// Set player's CharacterType (called when player signals ready with character selection).
    /// </summary>
    public void SetPlayerCharacterType(Guid playerId, string characterType)
    {
        if (string.IsNullOrEmpty(characterType))
            return;

        if (_playerToSession.TryGetValue(playerId, out var sessionId))
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                if (session.Players.TryGetValue(playerId, out var player))
                {
                    player.CharacterType = characterType;
                    _logger.LogInformation("Set CharacterType for player {PlayerId} to {CharacterType}",
                        playerId.ToString()[..8], characterType);
                }
            }
        }
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

        // ConcurrentDictionary is thread-safe
        session.Players[playerId] = playerState;
        _playerToSession[playerId] = session.SessionId;

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

        // Check if this is the first player in this session (for checkpoint initialization)
        // ConcurrentDictionary operations are thread-safe
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

    /// <summary>
    /// Get player metadata for a session (for loading screen).
    /// Returns list of player metadata with Id, Name, CharacterType, and Level.
    /// </summary>
    public List<PlayerMetadata> GetPlayerMetadata(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return new List<PlayerMetadata>();
        }

        return session.Players.Values.Select(p => new PlayerMetadata
        {
            Id = p.Id.ToString(),
            Name = p.Name,
            CharacterType = p.CharacterType,
            Level = p.Level
        }).ToList();
    }

    public bool JoinSession(JoinSessionRequest request)
    {
        var session = _sessions.GetOrAdd(request.SessionId, sid => CreateDefaultSession(sid));
        bool isNewRoom = false;

        // Initialize room checkpoints if this is the first player joining
        // ConcurrentDictionary operations are thread-safe
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
                        oldSession.Players.TryRemove(request.PlayerId, out _);

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

        // Early return if client already has latest version
        if (sinceVersion.HasValue && sinceVersion.Value >= session.Version)
        {
            return new StateResponse
            {
                SessionId = session.SessionId,
                Version = session.Version,
                Status = session.Status.ToString()
            };
        }

        // Try Redis cache first (only for multiplayer sessions with 2+ players)
        // Note: Single player sessions don't use cache (optimization for 2+ players only)
        if (_redis != null && session.Players.Count >= 2)
        {
            try
            {
                var cachedState = _redis.GetCachedSessionStateAsync(session.SessionId, session.Version).GetAwaiter().GetResult();
                if (cachedState != null)
                {
                    // Cache hit - return cached state (avoids rebuilding state for multiple clients)
                    // Log removed for performance (called in sync loop)
                    return cachedState;
                }
                else
                {
                    // Cache miss - will build new state below
                    // Log removed for performance (called in sync loop)
                }
            }
            catch (Exception ex)
            {
                // If cache fails, fall back to building state
                _logger.LogWarning(ex, "Failed to get cached state for {SessionId}, building new state", sessionId);
            }
        }
        else if (session.Players.Count < 2)
        {
            // Single player mode - cache disabled (not needed for optimization)
            // Log removed for performance (called in sync loop)
        }

        // Cache miss or single player - build state
        return BuildStateResponse(session);
    }

    /// <summary>
    /// Build StateResponse from session state.
    /// Extracted to separate method for reuse in caching.
    /// </summary>
    private StateResponse BuildStateResponse(SessionState session)
    {
        var response = new StateResponse
        {
            SessionId = session.SessionId,
            Version = session.Version,
            Status = session.Status.ToString(),
            CurrentSectionId = session.CurrentSectionId ?? -1, // -1 = no section (Unity JsonUtility doesn't support nullable)
            SectionName = session.CachedSection?.Name ?? string.Empty,
            Players = session.Players.Values
                .Select(p => new PlayerSnapshot(
                    p.Id, p.Name, p.CharacterType, p.X, p.Y, p.Hp, p.MaxHp, p.Sequence,
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
                .ToList(),
            Projectiles = session.Projectiles.Values
                .Select(p => new ProjectileSnapshot(p.Id, p.OwnerId, p.X, p.Y, p.DirX, p.DirY, p.Radius))
                .ToList()
        };

        return response;
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        foreach (var session in _sessions.Values)
        {
            ProcessInputs(session);
            ProcessEnemyAI(session); // Process enemy movement and AI
            ProcessEnemyRespawns(session);
            CleanupDeadEnemies(session); // Clean up dead enemies that won't respawn

            // Check boss defeat and advance section
            await CheckBossDefeatedAndAdvanceSection(session);

            // Check if all players are dead
            CheckAllPlayersDead(session);

            session.Version++;

            // Cache session state in Redis (only for multiplayer sessions with 2+ players)
            // Optimization: Single player sessions don't need caching (fewer requests)
            if (_redis != null && session.Players.Count >= 2)
            {
                // Build state and cache it asynchronously (don't block tick)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var state = BuildStateResponse(session);
                        await _redis.CacheSessionStateAsync(session.SessionId, session.Version, state);
                        // Log removed for performance (called in tick loop)
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error caching session state for {SessionId}", session.SessionId);
                    }
                });
            }
        }
    }

    #endregion

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
    /// Process enemy AI: detect players, chase, attack.
    /// Updates enemy positions server-authoritatively.
    /// </summary>
    private void ProcessEnemyAI(SessionState session)
    {
        // Get all alive players in session
        var alivePlayers = session.Players.Values
            .Where(p => p.Hp > 0)
            .ToList();

        if (alivePlayers.Count == 0)
        {
            // No players alive, enemies should be idle
            foreach (var enemy in session.Enemies.Values)
            {
                if (enemy.Status != EnemyStatus.Dead && enemy.Hp > 0)
                {
                    enemy.Status = EnemyStatus.Idle;
                }
            }
            return;
        }

        // Process each alive enemy
        foreach (var enemy in session.Enemies.Values)
        {
            // Skip dead enemies or enemies in knockback
            if (enemy.Status == EnemyStatus.Dead || enemy.Hp <= 0 || enemy.Status == EnemyStatus.Knockback)
                continue;

            // Update attack timer
            if (enemy.AttackTimer > 0)
            {
                enemy.AttackTimer -= TickDeltaTime;
            }

            // Find nearest player within detect range
            PlayerState? nearestPlayer = null;
            float nearestDistance = float.MaxValue;

            foreach (var player in alivePlayers)
            {
                float dx = player.X - enemy.X;
                float dy = player.Y - enemy.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                if (distance <= enemy.DetectRange && distance < nearestDistance)
                {
                    nearestPlayer = player;
                    nearestDistance = distance;
                }
            }

            // If player detected
            if (nearestPlayer != null)
            {
                // Add buffer zone: player must be further than AttackRange * 1.15 to exit attack state
                // This prevents rapid attack/retreat spam when player is at the edge of attack range
                float attackRangeWithBuffer = enemy.AttackRange * 1.15f;

                // Check if player is out of attack range with buffer (priority: check this first)
                if (nearestDistance > attackRangeWithBuffer)
                {
                    // Player moved out of attack range - chase player
                    enemy.Status = EnemyStatus.Chasing;

                    // Calculate direction to player
                    float dx = nearestPlayer.X - enemy.X;
                    float dy = nearestPlayer.Y - enemy.Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (distance > 0.001f) // Avoid division by zero
                    {
                        // Normalize direction
                        float dirX = dx / distance;
                        float dirY = dy / distance;

                        // Move enemy towards player
                        enemy.X += dirX * enemy.Speed * TickDeltaTime;
                        enemy.Y += dirY * enemy.Speed * TickDeltaTime;
                    }
                }
                // Check if in attack range and cooldown ready
                else if (nearestDistance <= enemy.AttackRange && enemy.AttackTimer <= 0)
                {
                    // Only attack if transitioning from non-attacking state (prevent spam damage)
                    // This ensures damage is only applied once when entering attack state
                    bool isNewAttack = enemy.Status != EnemyStatus.Attacking;

                    // Set attacking state and reset cooldown
                    enemy.Status = EnemyStatus.Attacking;
                    enemy.AttackTimer = enemy.AttackCooldown;

                    // Apply damage only when starting a new attack (not every tick while attacking)
                    if (isNewAttack)
                    {
                        int damage = enemy.Damage;
                        nearestPlayer.Hp = Math.Max(0, nearestPlayer.Hp - damage);
                    }
                }
                // Player is between AttackRange and attackRangeWithBuffer (buffer zone)
                else if (nearestDistance > enemy.AttackRange && nearestDistance <= attackRangeWithBuffer)
                {
                    // In buffer zone: if cooldown is ready, continue chasing to get closer
                    // If cooldown not ready, stay in attacking state (prevent immediate re-attack)
                    if (enemy.AttackTimer <= 0)
                    {
                        // Cooldown ready but in buffer zone - chase to get closer before attacking
                        enemy.Status = EnemyStatus.Chasing;

                        // Calculate direction to player
                        float dx = nearestPlayer.X - enemy.X;
                        float dy = nearestPlayer.Y - enemy.Y;
                        float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                        if (distance > 0.001f)
                        {
                            float dirX = dx / distance;
                            float dirY = dy / distance;
                            enemy.X += dirX * enemy.Speed * TickDeltaTime;
                            enemy.Y += dirY * enemy.Speed * TickDeltaTime;
                        }
                    }
                    else
                    {
                        // Cooldown not ready - stay in attacking state (prevent spam)
                        enemy.Status = EnemyStatus.Attacking;
                    }
                }
                else
                {
                    // In attack range but cooldown not ready - stay in attacking state but don't move
                    enemy.Status = EnemyStatus.Attacking;
                }
            }
            else
            {
                // No player detected - return to idle
                enemy.Status = EnemyStatus.Idle;
            }
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
        // Use in-memory cached section/checkpoint data (populated during InitializeRoomCheckpointsAsync)
        // This avoids blocking Redis/DB calls every tick which was causing 100ms+ delays
        bool needsLimitationCheck = session.CachedSection != null;

        // ConcurrentDictionary is thread-safe for enumeration
        foreach (var enemy in session.Enemies.Values)
        {
            // Only process dead enemies
            if (enemy.Status != EnemyStatus.Dead || enemy.Hp > 0)
                continue;

            // Skip boss respawns (boss doesn't respawn)
            if (enemy.IsBoss || enemy.RespawnDelay >= float.MaxValue)
            {
                continue;
            }

            // Increment respawn timer (atomic operation on enemy object)
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

                // Respawn enemy at spawn position (atomic operations on enemy object)
                enemy.Hp = enemy.MaxHp;
                enemy.X = enemy.SpawnX;
                enemy.Y = enemy.SpawnY;
                enemy.Status = EnemyStatus.Idle;
                enemy.RespawnTimer = 0f;

                // Log removed for performance (called frequently in tick loop)
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
        var enemiesToRemove = new List<Guid>();

        // ConcurrentDictionary is thread-safe for enumeration
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
                    // Log removed for performance (called in tick loop)
                }
                continue;
            }

            // Regular enemies: Remove if they've been dead too long and can't respawn
            // Wait at least RespawnDelay * 2 before removing (to allow respawn attempts)
            float cleanupDelay = enemy.RespawnDelay * 2.0f;
            if (enemy.RespawnTimer >= cleanupDelay)
            {
                enemiesToRemove.Add(enemy.Id);
                // Log removed for performance (called in tick loop)
            }
        }

        // Remove enemies from dictionary (ConcurrentDictionary.TryRemove is thread-safe)
        foreach (var enemyId in enemiesToRemove)
        {
            session.Enemies.TryRemove(enemyId, out _);
        }

        // Log removed for performance (called in tick loop)
        // Uncomment for debugging: _logger.LogDebug("Cleaned up {Count} dead enemies", enemiesToRemove.Count);
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
                // Log removed for performance (called frequently in tick loop)
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
                // Log removed for performance (called frequently in tick loop)
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
            // Log removed for performance (called frequently in tick loop)
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
        // ConcurrentDictionary enumeration is thread-safe
        var killedEnemy = session.Enemies.Values
            .FirstOrDefault(e => e.TypeId == enemyTypeId && e.Hp > 0);

        if (killedEnemy != null)
        {
            killedEnemy.Hp = 0;
            killedEnemy.Status = EnemyStatus.Dead;
            // Log removed for performance (called in tick loop)
        }
        else
        {
            // Enemy already dead or not found - this is normal since rewards are awarded automatically
            // Log removed for performance (called in tick loop)
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

        // ConcurrentDictionary.TryGetValue is thread-safe
        if (!session.Enemies.TryGetValue(enemyId, out var enemy))
        {
            _logger.LogWarning("ApplyDamageToEnemy: Enemy {EnemyId} not found in session {SessionId}. Looking for: {LookingFor}",
                enemyId.ToString()[..8], sessionId, enemyId);
            return null;
        }

        if (enemy.Hp <= 0)
        {
            // Log removed for performance (called in tick loop)
            return (enemy.Hp, enemy.MaxHp);
        }

        var oldHp = enemy.Hp;
        enemy.Hp = Math.Max(0, enemy.Hp - damageAmount);
        session.Version++;

        // Log removed for performance (called in tick loop)

        // Mark enemy as dead if HP reaches 0 and award kill rewards
        if (enemy.Hp <= 0)
        {
            enemy.Status = EnemyStatus.Dead;
            // Reset respawn timer to start counting from 0
            enemy.RespawnTimer = 0f;

            // Log removed for performance (called in tick loop)

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
                        // Log removed for performance (called in tick loop)

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

                            // Log removed for performance (called in tick loop)
                        }
                        else
                        {
                            _logger.LogWarning("ApplyDamageToEnemy: Player {PlayerId} not found in session {SessionId}", playerId, sessionId);
                        }
                    }
                    else
                    {
                        // Config not yet cached - will be loaded asynchronously for next kill
                        // Log removed for performance (called in tick loop)
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
                // Log removed for performance (called in tick loop)
            }
        }

        return (enemy.Hp, enemy.MaxHp);
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

        // ConcurrentDictionary.TryGetValue is thread-safe
        if (!session.Players.TryGetValue(playerId, out var player))
        {
            _logger.LogWarning("ApplyDamage: Player {PlayerId} state missing", playerId);
            return null;
        }

        player.Hp = Math.Max(0, player.Hp - damageAmount);
        session.Version++;

        // Log removed for performance (called in tick loop)

        return (player.Hp, player.MaxHp);
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

        // ConcurrentDictionary.TryGetValue is thread-safe
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

    #endregion

    #region Private Methods

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

                // Load first active GameSection (SectionId ASC - smallest ID is first section: Section 1, 2, 3...)
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

            // Generate deterministic seed from sessionId
            int seed = sessionId.GetHashCode();
            var random = new Random(seed);

            _logger.LogInformation("Initializing room {SessionId} with seed {Seed}, {CheckpointCount} checkpoints",
                sessionId, seed, sortedCheckpoints.Count);

            // Collect all enemies to spawn first (outside lock, can use await)
            var enemiesToSpawn = new List<(string typeId, float x, float y, EnemyConfig config, int checkpointId, bool isBoss)>();

            // Simplified: spawn enemies from each checkpoint's pool
            foreach (var checkpoint in sortedCheckpoints)
            {
                // Parse enemy pool JSON: ["slime", "goblin"] or ["boss_fish"]
                var enemyTypes = CheckpointService.ParseEnemyPool(checkpoint.EnemyPool);
                if (!enemyTypes.Any())
                {
                    _logger.LogWarning("Checkpoint {CheckpointName} has empty enemy pool", checkpoint.CheckpointName);
                    continue;
                }

                // Check if this checkpoint is for boss (enemy type starts with "boss_")
                bool isBossCheckpoint = enemyTypes.Any(type => type.StartsWith("boss_", StringComparison.OrdinalIgnoreCase));

                if (isBossCheckpoint)
                {
                    // Spawn boss (use first boss type from pool)
                    var bossTypeId = enemyTypes.First(type => type.StartsWith("boss_", StringComparison.OrdinalIgnoreCase));

                    // Try to load boss config directly
                    EnemyConfig? bossConfig = null;
                    try
                    {
                        var enemyConfigService = scope.ServiceProvider.GetService<EnemyConfigService>();
                        if (enemyConfigService != null)
                        {
                            bossConfig = await enemyConfigService.GetEnemyAsync(bossTypeId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load boss {TypeId} from EnemyConfigService", bossTypeId);
                    }

                    // If not found, try LoadBossConfigAsync fallback
                    if (bossConfig == null && section != null)
                    {
                        bossConfig = await LoadBossConfigAsync(scope.ServiceProvider, section, bossTypeId);
                    }

                    if (bossConfig != null)
                    {
                        enemiesToSpawn.Add((bossConfig.TypeId, checkpoint.X, checkpoint.Y, bossConfig, checkpoint.CheckpointId, true));
                        _logger.LogInformation("Boss {BossTypeId} will spawn at checkpoint {CheckpointId} ({X}, {Y})",
                            bossConfig.TypeId, checkpoint.CheckpointId, checkpoint.X, checkpoint.Y);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to load boss config for {BossTypeId} at checkpoint {CheckpointId}", bossTypeId, checkpoint.CheckpointId);
                    }
                }
                else
                {
                    // Spawn regular enemies (up to MaxEnemies)
                    int enemiesAtThisCheckpoint = checkpoint.MaxEnemies;

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
                    }
                }
            }

            // Declare variables before lock block
            int totalSpawned = 0;
            Guid? bossId = null;

            // Double-check enemies weren't added while we were loading configs (quick check without lock)
            if (sessionState.Enemies.Any())
            {
                _logger.LogInformation("Room {SessionId} already initialized while loading configs, skipping", sessionId);
                return;
            }

            // Prepare section info (outside lock)
            CachedSectionConfig? cachedSectionConfig = null;
            ConcurrentDictionary<int, CachedCheckpointConfig>? cachedCheckpoints = null;

            if (sectionToUse.HasValue && section != null)
            {
                cachedSectionConfig = new CachedSectionConfig
                {
                    SectionId = section.SectionId,
                    Name = section.Name,
                    EnemyCount = section.EnemyCount,
                    EnemyLevel = section.EnemyLevel,
                    SpawnRate = section.SpawnRate,
                    Duration = section.Duration ?? 0f // 0 = unlimited duration
                };

                // Cache checkpoint configs for respawn limitation checks
                cachedCheckpoints = new ConcurrentDictionary<int, CachedCheckpointConfig>(
                    sortedCheckpoints.ToDictionary(
                        c => c.CheckpointId,
                        c => new CachedCheckpointConfig
                        {
                            CheckpointId = c.CheckpointId,
                            MaxEnemies = c.MaxEnemies
                        }));
            }

            // Now lock only for final state updates (minimal lock scope)
            lock (_sessionLock)
            {
                // Double-check again after acquiring lock
                if (sessionState.Enemies.Any())
                {
                    _logger.LogInformation("Room {SessionId} already initialized while acquiring lock, skipping", sessionId);
                    return;
                }

                // Store section info in SessionState (quick assignment)
                // Note: CurrentSectionId may already be set (e.g., during section progression), so only update if different
                if (sectionToUse.HasValue && section != null && cachedSectionConfig != null && cachedCheckpoints != null)
                {
                    // Only update CurrentSectionId if it's different (may already be set during section progression)
                    if (sessionState.CurrentSectionId != sectionToUse.Value)
                    {
                        sessionState.CurrentSectionId = sectionToUse.Value;
                    }
                    sessionState.SectionStartTime = DateTime.UtcNow;
                    sessionState.CachedSection = cachedSectionConfig;
                    sessionState.CachedCheckpoints = cachedCheckpoints;

                    _logger.LogInformation("Session {SessionId} initialized with Section {SectionId} ({SectionName}), EnemyLevel={EnemyLevel}, SpawnRate={SpawnRate}. Cached {CheckpointCount} checkpoints.",
                        sessionId, section.SectionId, section.Name, section.EnemyLevel, section.SpawnRate, cachedCheckpoints.Count);
                }

                // Add all enemies (quick operations inside lock)
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
                        _logger.LogInformation("Spawned BOSS {EnemyId} ({TypeId}) Lv{Level} at checkpoint {CheckpointId} ({X}, {Y})",
                            enemyState.Id, typeId, enemyState.EnemyLevel, checkpointId, x, y);
                    }
                    else
                    {
                        _logger.LogInformation("Spawned enemy {EnemyId} ({TypeId}) Lv{Level} at checkpoint {CheckpointId} ({X}, {Y})",
                            enemyState.Id, typeId, enemyState.EnemyLevel, checkpointId, x, y);
                    }
                }

                // Only set CurrentBossId if boss was actually spawned
                if (bossId.HasValue)
                {
                    sessionState.CurrentBossId = bossId;
                    sessionState.IsBossAlive = true;
                }
                else
                {
                    sessionState.CurrentBossId = null;
                    sessionState.IsBossAlive = false;
                    _logger.LogInformation("Section {SectionId} has no boss, CurrentBossId set to null", sectionToUse);
                }
            }

            sessionState.Version++;
            _logger.LogInformation("Room {SessionId} initialized: spawned {TotalSpawned} enemies ({RegularCount} regular, {BossCount} boss) at {CheckpointCount} checkpoints",
                sessionId, totalSpawned, totalSpawned - (bossId.HasValue ? 1 : 0), bossId.HasValue ? 1 : 0, checkpoints.Count);
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
            AttackTimer = 0f, // Can attack immediately on spawn
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

        // ConcurrentDictionary.TryGetValue is thread-safe
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

        // Priority 1: GameSection.EnemyTypeId (only if not null/empty and not "-")
        if (section != null && !string.IsNullOrEmpty(section.EnemyTypeId) && section.EnemyTypeId != "-")
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
    /// Complete session: save to DB, save player progress, clear Redis cache.
    /// </summary>
    private async Task CompleteSessionAsync(string sessionId)
    {
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var sessionState))
            {
                _logger.LogWarning("Session {SessionId} not found for completion", sessionId);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var sessionTrackingService = scope.ServiceProvider.GetRequiredService<SessionTrackingService>();
            var playerService = scope.ServiceProvider.GetRequiredService<PlayerService>();

            // Convert sessionId string to Guid for SessionTrackingService
            if (!Guid.TryParse(sessionId, out var sessionGuid))
            {
                _logger.LogWarning("Invalid session ID format: {SessionId}", sessionId);
                return;
            }

            // Save player progress for all players in session (last save)
            var playerIds = sessionState.Players.Keys.ToList();
            foreach (var playerId in playerIds)
            {
                if (sessionState.Players.TryGetValue(playerId, out var playerState))
                {
                    try
                    {
                        await playerService.SaveProgressAsync(
                            playerId,
                            playerState.Exp,
                            playerState.Gold,
                            playerState.Level,
                            playerState.Hp
                        );
                        _logger.LogInformation("Saved final progress for player {PlayerId} in completed session {SessionId}: Level={Level}, Exp={Exp}, Gold={Gold}",
                            playerId.ToString()[..8], sessionId, playerState.Level, playerState.Exp, playerState.Gold);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save final progress for player {PlayerId} in session {SessionId}", playerId, sessionId);
                    }
                }
            }

            // Save session to database (end session)
            try
            {
                await sessionTrackingService.EndSessionAsync(sessionGuid, "Completed");
                _logger.LogInformation("Session {SessionId} saved to database as Completed", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save session {SessionId} to database", sessionId);
            }

            // Clear Redis cache for session
            if (_redis != null)
            {
                try
                {
                    // Clear session metadata cache
                    await _redis.InvalidateSessionMetadataAsync(sessionId);

                    // Clear temporary skill bonuses for all players
                    foreach (var playerId in playerIds)
                    {
                        try
                        {
                            await _redis.DeleteTemporarySkillBonusesAsync(sessionId, playerId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete skill bonuses cache for player {PlayerId} in session {SessionId}", playerId, sessionId);
                        }
                    }

                    _logger.LogInformation("Cleared Redis cache for session {SessionId}", sessionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clear Redis cache for session {SessionId}", sessionId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing session {SessionId}", sessionId);
        }
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

            // Debug: Log all active sections to see what's available
            var allActiveSections = await db.GameSections
                .Where(s => s.IsActive)
                .OrderBy(s => s.SectionId)
                .Select(s => new { s.SectionId, s.Name })
                .ToListAsync();

            _logger.LogInformation("LoadNextSectionAsync: Current SectionId={CurrentSectionId}, All active sections: [{Sections}]",
                currentSectionId, string.Join(", ", allActiveSections.Select(s => $"SectionId={s.SectionId}, Name={s.Name}")));

            // Find next section (SectionId > currentSectionId - ascending order: 1 -> 2 -> 3...)
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
                _logger.LogWarning("No next section found after SectionId {CurrentSectionId}. Available sections: [{Sections}]",
                    currentSectionId, string.Join(", ", allActiveSections.Select(s => $"SectionId={s.SectionId}")));
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

        // Quick check without lock (ConcurrentDictionary operations are thread-safe)
        if (!session.Enemies.TryGetValue(session.CurrentBossId.Value, out var boss))
        {
            // Boss not found, mark as not alive (atomic operation)
            session.IsBossAlive = false;
            _logger.LogWarning("Boss {BossId} not found in session {SessionId}", session.CurrentBossId.Value, session.SessionId);
            return;
        }

        // Check if boss is defeated (read operation, no lock needed)
        if (boss.Hp <= 0 || boss.Status == EnemyStatus.Dead)
        {
            // Only lock for state changes that need atomicity
            int? sectionIdToComplete = null;
            Guid bossIdToRemove = Guid.Empty;

            lock (_sessionLock)
            {
                // Double-check boss still exists and is dead
                if (!session.Enemies.TryGetValue(session.CurrentBossId.Value, out var bossCheck))
                {
                    return; // Boss already removed
                }

                if (bossCheck.Hp > 0 && bossCheck.Status != EnemyStatus.Dead)
                {
                    return; // Boss is still alive
                }

                session.IsBossAlive = false;
                bossIdToRemove = bossCheck.Id;

                // Mark section as complete
                if (session.CurrentSectionId.HasValue)
                {
                    if (!session.CompletedSections.Contains(session.CurrentSectionId.Value))
                    {
                        session.CompletedSections.Add(session.CurrentSectionId.Value);
                        sectionIdToComplete = session.CurrentSectionId.Value;
                    }
                }
            }

            // Remove boss outside lock (ConcurrentDictionary operation is thread-safe)
            session.Enemies.TryRemove(bossIdToRemove, out _);
            // Log removed for performance (called in tick loop)

            if (sectionIdToComplete.HasValue)
            {
                // Log removed for performance (called in tick loop)
            }

            // Check next section immediately (async, non-blocking)
            // This ensures status is set to Completed ASAP if no next section exists
            _ = Task.Run(async () =>
            {
                try
                {
                    if (sectionIdToComplete.HasValue)
                    {
                        var nextSection = await LoadNextSectionAsync(sectionIdToComplete.Value);

                        if (nextSection != null)
                        {
                            // Initialize next section
                            // Log removed for performance (called in tick loop)

                            // Clear old enemies and update CurrentSectionId BEFORE initializing (so clients see section change immediately)
                            lock (_sessionLock)
                            {
                                session.Enemies.Clear();
                                session.CurrentBossId = null;
                                session.IsBossAlive = false;
                                // Set CurrentSectionId immediately so clients can detect section change
                                session.CurrentSectionId = nextSection.SectionId;
                                // Clear cached section data (will be repopulated by InitializeRoomCheckpointsAsync)
                                session.CachedSection = null;
                                session.CachedCheckpoints.Clear();
                                session.SectionStartTime = null;
                                // Ensure status is still InProgress when advancing
                                if (session.Status != SessionStatus.InProgress)
                                {
                                    session.Status = SessionStatus.InProgress;
                                }
                                // Increment version to notify clients of section change
                                session.Version++;
                            }

                            _logger.LogInformation("Section {SectionId} set in session {SessionId}, initializing checkpoints...", nextSection.SectionId, session.SessionId);
                            await InitializeRoomCheckpointsAsync(session.SessionId, nextSection.SectionId);
                        }
                        else
                        {
                            // No more sections, session completed - set immediately
                            lock (_sessionLock)
                            {
                                if (session.Status == SessionStatus.InProgress)
                                {
                                    session.Status = SessionStatus.Completed;
                                    session.Version++; // Increment version to notify clients
                                    _logger.LogInformation("All sections completed in session {SessionId}. Session status set to Completed.", session.SessionId);
                                }
                            }

                            // Complete session: save to DB, save player progress, clear Redis cache
                            _ = Task.Run(async () => await CompleteSessionAsync(session.SessionId));
                        }
                    }
                    else
                    {
                        // No current section, mark as completed immediately
                        lock (_sessionLock)
                        {
                            if (session.Status == SessionStatus.InProgress)
                            {
                                session.Status = SessionStatus.Completed;
                                session.Version++; // Increment version to notify clients
                                _logger.LogInformation("No current section in session {SessionId}. Session status set to Completed.", session.SessionId);
                            }
                        }

                        // Complete session: save to DB, save player progress, clear Redis cache
                        _ = Task.Run(async () => await CompleteSessionAsync(session.SessionId));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error advancing section in session {SessionId}", session.SessionId);
                }
            });
        }
    }

    /// <summary>
    /// Check if all regular enemies are defeated and advance to next section if applicable.
    /// Only called if section has no boss (CurrentBossId == null).
    /// Called each tick in TickAsync().
    /// </summary>
    private async Task CheckAllEnemiesDefeatedAndAdvanceSection(SessionState session)
    {
        if (session.Status != SessionStatus.InProgress)
            return; // Session already completed or failed

        // Only check if section has no boss (read operation, no lock needed)
        if (session.CurrentBossId.HasValue)
            return; // Section has boss, use boss defeat logic instead

        if (!session.CurrentSectionId.HasValue)
            return; // No current section

        // Count alive regular enemies (non-boss) in current section (read operation, no lock needed)
        int aliveRegularEnemies = session.Enemies.Values
            .Count(e => e.SectionId == session.CurrentSectionId &&
                       !e.IsBoss &&
                       e.Status != EnemyStatus.Dead &&
                       e.Hp > 0);

        // If all regular enemies are defeated, advance section
        if (aliveRegularEnemies == 0)
        {
            int? sectionIdToComplete = session.CurrentSectionId;

            // Only lock for state updates
            lock (_sessionLock)
            {
                // Double-check section is still current and no enemies spawned
                if (!session.CurrentSectionId.HasValue || session.CurrentSectionId != sectionIdToComplete)
                    return; // Section changed

                // Re-check enemy count (double-check pattern)
                int recheckCount = session.Enemies.Values
                    .Count(e => e.SectionId == session.CurrentSectionId &&
                               !e.IsBoss &&
                               e.Status != EnemyStatus.Dead &&
                               e.Hp > 0);

                if (recheckCount > 0)
                    return; // Enemies spawned while checking

                // Mark section as complete
                if (!session.CompletedSections.Contains(session.CurrentSectionId.Value))
                {
                    session.CompletedSections.Add(session.CurrentSectionId.Value);
                    sectionIdToComplete = session.CurrentSectionId.Value;
                }
                else
                {
                    return; // Already processed
                }
            }

            _logger.LogInformation("All regular enemies defeated in section {SectionId} (no boss), advancing to next section",
                sectionIdToComplete.Value);

            // Load next section asynchronously (outside lock, non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    var nextSection = await LoadNextSectionAsync(sectionIdToComplete.Value);

                    if (nextSection != null)
                    {
                        // Initialize next section
                        _logger.LogInformation("Advancing to next section {SectionId} ({SectionName}) in session {SessionId}",
                            nextSection.SectionId, nextSection.Name, session.SessionId);

                        // Clear old enemies and update CurrentSectionId BEFORE initializing (so clients see section change immediately)
                        lock (_sessionLock)
                        {
                            session.Enemies.Clear();
                            session.CurrentBossId = null;
                            session.IsBossAlive = false;
                            // Set CurrentSectionId immediately so clients can detect section change
                            session.CurrentSectionId = nextSection.SectionId;
                            // Clear cached section data (will be repopulated by InitializeRoomCheckpointsAsync)
                            session.CachedSection = null;
                            session.CachedCheckpoints.Clear();
                            session.SectionStartTime = null;
                            // Ensure status is still InProgress when advancing
                            if (session.Status != SessionStatus.InProgress)
                            {
                                session.Status = SessionStatus.InProgress;
                            }
                            // Increment version to notify clients of section change
                            session.Version++;
                        }

                        _logger.LogInformation("Section {SectionId} set in session {SessionId}, initializing checkpoints...", nextSection.SectionId, session.SessionId);
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

                        // Complete session: save to DB, save player progress, clear Redis cache
                        _ = Task.Run(async () => await CompleteSessionAsync(session.SessionId));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error advancing section in session {SessionId}", session.SessionId);
                }
            });
        }
    }

    /// <summary>
    /// Get enemy config from in-memory cache (avoids DB/Redis queries during game tick).
    /// Cache is populated on-demand (lazy loading).
    /// Non-blocking: Returns cached value immediately if available, otherwise triggers async load and returns null.
    /// Caller should handle null gracefully (e.g., skip rewards if config not yet loaded).
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

        // Cache miss - trigger async load without blocking
        // Use a simple flag to prevent multiple loads of the same type (best-effort)
        // Multiple threads might still trigger loads, but that's acceptable - ConcurrentDictionary.TryAdd will handle duplicates
        if (!_enemyConfigCache.ContainsKey(typeId))
        {
            // Load asynchronously in background (non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var enemyConfigService = scope.ServiceProvider.GetService<EnemyConfigService>();
                    if (enemyConfigService != null)
                    {
                        var config = await enemyConfigService.GetEnemyAsync(typeId);
                        if (config != null)
                        {
                            // Use TryAdd to only cache if not already cached (thread-safe)
                            // Multiple threads might try to add the same config, but only one will succeed
                            if (_enemyConfigCache.TryAdd(typeId, config))
                            {
                                _logger.LogDebug("Cached enemy config for {TypeId} (ExpReward={ExpReward}, GoldReward={GoldReward})",
                                    typeId, config.ExpReward, config.GoldReward);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load enemy config for {TypeId}", typeId);
                }
            });
        }

        // Return null if not in cache - caller must handle this
        // This is acceptable because:
        // 1. First kill might miss rewards, but config will be cached for next kill
        // 2. Preloading all configs on startup avoids this issue entirely
        // 3. Non-blocking is critical for game tick performance
        return null;
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

