using GameServer.Models.Dto;
using GameServer.Models.Entities;
using GameServer.Models.States;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace GameServer.Services;

public class WorldService
{
    private readonly ILogger<WorldService> _logger;
    private readonly GameConfigService _config;

    private readonly ConcurrentDictionary<string, SessionState> _sessions = new();
    private readonly ConcurrentDictionary<Guid, string> _playerToSession = new();
    private readonly ConcurrentDictionary<Guid, InputCommand> _inputQueue = new();
    private readonly object _sessionLock = new();

    private const float TickDeltaTime = 0.05f; // 20Hz

    public WorldService(ILogger<WorldService> logger, GameConfigService config)
    {
        _logger = logger;
        _config = config;
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

    public RegisterResponse RegisterPlayer(string playerName)
    {
        var playerId = Guid.NewGuid();
        var token = Guid.NewGuid().ToString("N");

        // In-memory only: hash not required here. Persist would hash.
        var playerState = CreateDefaultPlayer(playerId, playerName);
        var session = _sessions.GetOrAdd("default", sid => CreateDefaultSession(sid));

        lock (_sessionLock)
        {
            session.Players[playerId] = playerState;
            _playerToSession[playerId] = session.SessionId;
        }

        _logger.LogInformation("Player registered: {Name} (ID: {PlayerId}) in session {Session}. Total players: {Count}",
            playerName, playerId.ToString()[..8], session.SessionId, session.Players.Count);

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

        lock (_sessionLock)
        {
            if (session.Players.TryGetValue(profile.Id, out var existing))
            {
                // Player already in session, just return
                _logger.LogInformation("Player {Name} already in session, returning existing", profile.Name);
            }
            else
            {
                // Create player state from database profile
                var defaults = _config.PlayerDefaults;
                var playerState = new PlayerState
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    X = defaults.SpawnX,
                    Y = defaults.SpawnY,
                    Hp = isNew ? profile.Stats.MaxHealth : profile.Stats.CurrentHealth,
                    MaxHp = profile.Stats.MaxHealth,
                    Damage = profile.Stats.Damage,
                    Range = profile.Stats.Range,
                    Speed = profile.Stats.Speed,
                    Level = profile.Level,
                    Exp = profile.Exp,
                    ExpToLevel = profile.ExpToLevel > 0 ? profile.ExpToLevel : _config.GetExpForNextLevel(profile.Level),
                    Gold = profile.Gold,
                    Sequence = 0
                };
                session.Players[profile.Id] = playerState;
            }
            _playerToSession[profile.Id] = session.SessionId;
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

    public bool JoinSession(JoinSessionRequest request)
    {
        var session = _sessions.GetOrAdd(request.SessionId, sid => CreateDefaultSession(sid));
        lock (_sessionLock)
        {
            if (session.Players.TryGetValue(request.PlayerId, out var existing))
            {
                // Reset HP/pos instead of creating duplicate
                var defaults = _config.PlayerDefaults;
                existing.Hp = existing.MaxHp;
                existing.X = defaults.SpawnX;
                existing.Y = defaults.SpawnY;
                existing.Sequence = 0;
                _logger.LogInformation("Player {PlayerId} rejoined, reset HP/pos to spawn ({X}, {Y})",
                    request.PlayerId, defaults.SpawnX, defaults.SpawnY);
            }
            else
            {
                var player = CreateDefaultPlayer(request.PlayerId, request.PlayerName);
                session.Players[request.PlayerId] = player;
            }
            _playerToSession[request.PlayerId] = session.SessionId;
            session.Version++;
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

                // Note: Enemies are now client-side only; server doesn't simulate them
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
                .Select(p => new PlayerSnapshot(p.Id, p.Name, p.X, p.Y, p.Hp, p.MaxHp, p.Sequence, p.Level, p.Exp, p.ExpToLevel, p.Gold))
                .ToList()
        };

        return response;
    }

    public Task TickAsync(CancellationToken cancellationToken)
    {
        foreach (var session in _sessions.Values)
        {
            ProcessInputs(session);
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

        var enemyCfg = _config.GetEnemy(enemyTypeId);
        if (enemyCfg == null)
        {
            _logger.LogWarning("ReportKill: Enemy type {Type} not found in config", enemyTypeId);
            return false;
        }

        AwardKillRewards(player, enemyCfg);
        session.Version++;
        return true;
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

            // Get spawn position from config
            var defaults = _config.PlayerDefaults;
            player.X = defaults.SpawnX;
            player.Y = defaults.SpawnY;

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
        player.Exp += enemy.ExpReward;
        player.Gold += enemy.GoldReward;

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

    private PlayerState CreateDefaultPlayer(Guid playerId, string playerName)
    {
        var defaults = _config.PlayerDefaults;
        var stats = defaults.Stats;
        return new PlayerState
        {
            Id = playerId,
            Name = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName,
            X = defaults.SpawnX,
            Y = defaults.SpawnY,
            Hp = stats.CurrentHealth,
            MaxHp = stats.MaxHealth,
            Damage = stats.Damage,
            Range = stats.WeaponRange,
            Speed = stats.Speed,
            Sequence = 0,
            Level = defaults.Level,
            Exp = defaults.Exp,
            Gold = defaults.Gold
        };
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

