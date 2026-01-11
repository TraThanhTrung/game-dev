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

    private readonly ConcurrentDictionary<string, SessionState> _sessions = new();
    private readonly ConcurrentDictionary<Guid, string> _playerToSession = new();
    private readonly ConcurrentDictionary<Guid, InputCommand> _inputQueue = new();
    private readonly ConcurrentDictionary<string, bool> _initializedRooms = new(); // Track rooms that have spawned enemies
    private readonly object _sessionLock = new();

    private const float TickDeltaTime = 0.05f; // 20Hz

    public WorldService(ILogger<WorldService> logger, GameConfigService config, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _config = config;
        _serviceProvider = serviceProvider;
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
    /// </summary>
    public RegisterResponse RegisterOrLoadPlayer(PlayerProfile profile, bool isNew)
    {
        var session = _sessions.GetOrAdd("default", sid => CreateDefaultSession(sid));
        bool isFirstPlayerInSession = false;

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
                // All stats must come from database (PlayerStats entity)
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
                    MaxHp = stats.MaxHealth,
                    Damage = stats.Damage,
                    Range = stats.Range,
                    Speed = stats.Speed,
                    WeaponRange = stats.WeaponRange,
                    KnockbackForce = stats.KnockbackForce,
                    KnockbackTime = stats.KnockbackTime,
                    StunTime = stats.StunTime,
                    BonusDamagePercent = stats.BonusDamagePercent,
                    DamageReductionPercent = stats.DamageReductionPercent,
                    Level = profile.Level,
                    Exp = profile.Exp,
                    ExpToLevel = profile.ExpToLevel > 0 ? profile.ExpToLevel : _config.GetExpForNextLevel(profile.Level),
                    Gold = profile.Gold,
                    Sequence = 0
                };
                session.Players[profile.Id] = playerState;

                _logger.LogInformation("Player {Name} loaded from database: DMG={Damage}, SPD={Speed}, HP={Hp}/{MaxHp}, LVL={Level}, WPN={WeaponRange}, KB={KnockbackForce}",
                    profile.Name, playerState.Damage, playerState.Speed, playerState.Hp, playerState.MaxHp, playerState.Level, playerState.WeaponRange, playerState.KnockbackForce);
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

    public Task TickAsync(CancellationToken cancellationToken)
    {
        foreach (var session in _sessions.Values)
        {
            ProcessInputs(session);
            ProcessEnemyRespawns(session);
            session.Version++;
        }

        return Task.CompletedTask;
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
    /// </summary>
    private void ProcessEnemyRespawns(SessionState session)
    {
        lock (_sessionLock)
        {
            foreach (var enemy in session.Enemies.Values.ToList())
            {
                // Only process dead enemies
                if (enemy.Status != EnemyStatus.Dead || enemy.Hp > 0)
                    continue;

                // Increment respawn timer
                enemy.RespawnTimer += TickDeltaTime;

                // Check if respawn delay has been reached
                if (enemy.RespawnTimer >= enemy.RespawnDelay)
                {
                    // Respawn enemy at spawn position
                    enemy.Hp = enemy.MaxHp;
                    enemy.X = enemy.SpawnX;
                    enemy.Y = enemy.SpawnY;
                    enemy.Status = EnemyStatus.Idle;
                    enemy.RespawnTimer = 0f;

                    _logger.LogDebug("Auto-respawned enemy {EnemyId} ({TypeId}) at ({X}, {Y}) after {Delay}s",
                        enemy.Id, enemy.TypeId, enemy.X, enemy.Y, enemy.RespawnDelay);
                }
            }
        }
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
                        // Resolve EnemyConfigService directly from DI to ensure we query from database
                        // Instead of using GameConfigService which may fallback to game-config.json
                        GameServer.Services.EnemyConfig? enemyCfg = null;

                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var enemyConfigService = scope.ServiceProvider.GetService<EnemyConfigService>();
                            if (enemyConfigService != null)
                            {
                                enemyCfg = enemyConfigService.GetEnemy(enemy.TypeId);
                                _logger.LogInformation("ApplyDamageToEnemy: Resolved EnemyConfigService, looking up config for TypeId={TypeId}, Config found={Found}",
                                    enemy.TypeId, enemyCfg != null);
                            }
                            else
                            {
                                _logger.LogWarning("ApplyDamageToEnemy: EnemyConfigService not available, cannot load enemy config for {TypeId}", enemy.TypeId);
                                // EnemyConfigService is required - no fallback
                            }
                        }

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

        // Award EXP and Gold
        player.Exp += enemy.ExpReward;
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
    /// </summary>
    private async Task InitializeRoomCheckpointsAsync(string sessionId, int? sectionId = null)
    {
        _logger.LogInformation("InitializeRoomCheckpointsAsync: Starting for session {SessionId}", sessionId);

        if (!_sessions.TryGetValue(sessionId, out var sessionState))
        {
            _logger.LogWarning("InitializeRoomCheckpoints: Session {SessionId} not found", sessionId);
            return;
        }

        // Check if already initialized (don't respawn if enemies already exist)
        if (sessionState.Enemies.Any())
        {
            _logger.LogInformation("Room {SessionId} already initialized with {EnemyCount} enemies",
                sessionId, sessionState.Enemies.Count);
            return;
        }

        try
        {
            // Resolve CheckpointService and GameDbContext from DI (requires scope)
            using var scope = _serviceProvider.CreateScope();
            var checkpointService = scope.ServiceProvider.GetRequiredService<CheckpointService>();
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            List<Checkpoint> checkpoints;

            // Determine which section to use
            int? sectionToUse = sectionId;

            if (!sectionToUse.HasValue)
            {
                // Load first active GameSection
                var firstSection = await db.GameSections
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.SectionId)
                    .FirstOrDefaultAsync();

                if (firstSection != null)
                {
                    sectionToUse = firstSection.SectionId;
                    _logger.LogInformation("Using first active GameSection: {SectionName} (ID: {SectionId}) for room {SessionId}",
                        firstSection.Name, sectionToUse, sessionId);
                }
            }

            // Load checkpoints by section if sectionId provided, otherwise load all active
            if (sectionToUse.HasValue)
            {
                checkpoints = await checkpointService.GetCheckpointsBySectionAsync(sectionToUse.Value);
                _logger.LogInformation("Loaded {Count} checkpoints for section {SectionId}", checkpoints.Count, sectionToUse.Value);
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

            // Generate deterministic seed from sessionId
            int seed = sessionId.GetHashCode();
            var random = new Random(seed);

            _logger.LogInformation("Initializing room {SessionId} with seed {Seed} and {CheckpointCount} checkpoints",
                sessionId, seed, checkpoints.Count);

            // Collect all enemies to spawn first (outside lock, can use await)
            var enemiesToSpawn = new List<(string typeId, float x, float y, EnemyConfig config)>();

            foreach (var checkpoint in checkpoints)
            {
                if (!checkpoint.IsActive) continue;

                // Parse enemy pool JSON: ["slime", "goblin", "slime"]
                var enemyTypes = CheckpointService.ParseEnemyPool(checkpoint.EnemyPool);
                if (!enemyTypes.Any())
                {
                    _logger.LogWarning("Checkpoint {CheckpointName} has empty enemy pool", checkpoint.CheckpointName);
                    continue;
                }

                // Spawn random enemies from pool
                for (int i = 0; i < checkpoint.MaxEnemies; i++)
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
                        _logger.LogWarning(ex, "Failed to load enemy {TypeId} from EnemyConfigService, trying fallback", enemyTypeId);
                    }

                    // EnemyConfigService is required - no fallback to GameConfigService
                    if (enemyConfig == null)
                    {
                        _logger.LogWarning("Enemy type {TypeId} not found in database or game-config.json, skipping spawn at {CheckpointName}",
                            enemyTypeId, checkpoint.CheckpointName);
                        continue;
                    }

                    enemiesToSpawn.Add((enemyTypeId, checkpoint.X, checkpoint.Y, enemyConfig));
                }
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

                int totalSpawned = 0;

                foreach (var (typeId, x, y, config) in enemiesToSpawn)
                {
                    // Create EnemyState with deterministic spawn position
                    var enemyState = CreateEnemyState(typeId, x, y, config);
                    sessionState.Enemies[enemyState.Id] = enemyState;
                    totalSpawned++;

                    // Log with full enemy ID for debugging
                    _logger.LogInformation("Spawned enemy {EnemyId} ({TypeId}) at ({X}, {Y}) in session {SessionId}",
                        enemyState.Id, typeId, x, y, sessionId);
                }

                sessionState.Version++;
                _logger.LogInformation("Room {SessionId} initialized: spawned {TotalSpawned} enemies at {CheckpointCount} checkpoints",
                    sessionId, totalSpawned, checkpoints.Count);
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

