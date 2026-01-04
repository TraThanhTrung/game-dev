using GameServer.Models.Dto;
using GameServer.Models.Entities;
using GameServer.Models.States;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace GameServer.Services;

public class WorldService
{
    private readonly ILogger<WorldService> _logger;

    private readonly ConcurrentDictionary<string, SessionState> _sessions = new();
    private readonly ConcurrentDictionary<Guid, string> _playerToSession = new();
    private readonly ConcurrentDictionary<Guid, InputCommand> _inputQueue = new();
    private readonly object _sessionLock = new();

    private const float TickDeltaTime = 0.05f; // 20Hz
    private const int MaxProjectilesPerPlayer = 5;

    public WorldService(ILogger<WorldService> logger)
    {
        _logger = logger;
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
                var playerState = new PlayerState
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    X = 0,
                    Y = 0,
                    Hp = isNew ? profile.Stats.MaxHealth : profile.Stats.CurrentHealth,
                    MaxHp = profile.Stats.MaxHealth,
                    Damage = profile.Stats.Damage,
                    Range = profile.Stats.Range,
                    Speed = profile.Stats.Speed,
                    Level = profile.Level,
                    Exp = profile.Exp,
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
                existing.Hp = existing.MaxHp;
                existing.X = 0;
                existing.Y = 0;
                existing.Sequence = 0;
                _logger.LogInformation("Player {PlayerId} rejoined, reset HP/pos", request.PlayerId);
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

                // Re-spawn default enemy
                var enemy = CreateDefaultEnemy();
                session.Enemies[enemy.Id] = enemy;
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
                .Select(p => new PlayerSnapshot(p.Id, p.Name, p.X, p.Y, p.Hp, p.MaxHp, p.Sequence, p.Level, p.Exp, p.Gold))
                .ToList(),
            Enemies = session.Enemies.Values
                .Select(e => new EnemySnapshot(e.Id, e.TypeId, e.X, e.Y, e.Hp, e.MaxHp, e.Status.ToString()))
                .ToList(),
            Projectiles = session.Projectiles.Values
                .Select(pr => new ProjectileSnapshot(pr.Id, pr.OwnerId, pr.X, pr.Y, pr.DirX, pr.DirY, pr.Radius))
                .ToList()
        };

        return response;
    }

    public Task TickAsync(CancellationToken cancellationToken)
    {
        foreach (var session in _sessions.Values)
        {
            ProcessInputs(session);
            UpdateProjectiles(session);
            UpdateEnemies(session);
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

            if (cmd.Attack)
            {
                DoMeleeAttack(session, player);
            }

            if (cmd.Shoot)
            {
                SpawnProjectile(session, player, cmd);
            }
        }
    }

    private void DoMeleeAttack(SessionState session, PlayerState player)
    {
        foreach (var enemy in session.Enemies.Values)
        {
            if (enemy.IsDead) continue;
            float dx = enemy.X - player.X;
            float dy = enemy.Y - player.Y;
            float distSqr = dx * dx + dy * dy;
            float range = player.Range;
            if (distSqr <= range * range)
            {
                enemy.Hp -= player.Damage;
                _logger.LogDebug("{Player} melee hit {Enemy} for {Dmg} dmg (HP: {Hp}/{MaxHp})",
                    player.Name, enemy.TypeId, player.Damage, enemy.Hp, enemy.MaxHp);

                if (enemy.Hp <= 0)
                {
                    enemy.Hp = 0;
                    // Award Exp and Gold to player
                    AwardKillRewards(player, enemy);
                    _logger.LogInformation("{Player} killed {Enemy}! +{Exp} Exp, +{Gold} Gold (Total: Exp={TotalExp}, Gold={TotalGold})", 
                        player.Name, enemy.TypeId, enemy.ExpReward, enemy.GoldReward, player.Exp, player.Gold);
                }
                session.Version++;
                break;
            }
        }
    }

    private void AwardKillRewards(PlayerState player, EnemyState enemy)
    {
        player.Exp += enemy.ExpReward;
        player.Gold += enemy.GoldReward;

        // Check for level up
        int expNeeded = GetExpForNextLevel(player.Level);
        while (player.Exp >= expNeeded && player.Level < 100) // Cap at level 100
        {
            player.Exp -= expNeeded;
            player.Level++;
            _logger.LogInformation("{Player} leveled up to {Level}!", player.Name, player.Level);
            expNeeded = GetExpForNextLevel(player.Level);
        }
    }

    private int GetExpForNextLevel(int currentLevel)
    {
        // Simple formula: 100 * level
        return 100 * currentLevel;
    }

    private void SpawnProjectile(SessionState session, PlayerState player, InputCommand cmd)
    {
        // Limit projectiles per player
        var count = session.Projectiles.Values.Count(p => p.OwnerId == player.Id);
        if (count >= MaxProjectilesPerPlayer) return;

        var dir = Normalize(cmd.AimX, cmd.AimY);
        if (dir.x == 0 && dir.y == 0)
        {
            dir = Normalize(cmd.MoveX, cmd.MoveY);
            if (dir.x == 0 && dir.y == 0)
            {
                dir = (1, 0);
            }
        }

        var projectile = new ProjectileState
        {
            Id = Guid.NewGuid(),
            OwnerId = player.Id,
            X = player.X,
            Y = player.Y,
            DirX = dir.x,
            DirY = dir.y,
            Speed = 6f,
            Damage = player.Damage,
            Radius = 0.2f,
            TimeToLive = 3f
        };

        session.Projectiles[projectile.Id] = projectile;
        session.Version++;
    }

    private void UpdateProjectiles(SessionState session)
    {
        var toRemove = new List<Guid>();
        foreach (var pr in session.Projectiles.Values)
        {
            pr.X += pr.DirX * pr.Speed * TickDeltaTime;
            pr.Y += pr.DirY * pr.Speed * TickDeltaTime;
            pr.TimeToLive -= TickDeltaTime;

            foreach (var enemy in session.Enemies.Values)
            {
                if (enemy.IsDead) continue;
                float dx = enemy.X - pr.X;
                float dy = enemy.Y - pr.Y;
                float distSqr = dx * dx + dy * dy;
                if (distSqr <= pr.Radius * pr.Radius + 0.25f)
                {
                    enemy.Hp -= pr.Damage;
                    if (enemy.Hp <= 0)
                    {
                        enemy.Hp = 0;
                        // Award rewards to projectile owner
                        if (session.Players.TryGetValue(pr.OwnerId, out var owner))
                        {
                            AwardKillRewards(owner, enemy);
                            _logger.LogInformation("{Player} killed {Enemy} with projectile! +{Exp} Exp, +{Gold} Gold", 
                                owner.Name, enemy.TypeId, enemy.ExpReward, enemy.GoldReward);
                        }
                    }
                    toRemove.Add(pr.Id);
                    session.Version++;
                    break;
                }
            }

            if (pr.TimeToLive <= 0)
            {
                toRemove.Add(pr.Id);
            }
        }

        foreach (var id in toRemove)
        {
            session.Projectiles.Remove(id);
        }
    }

    private void UpdateEnemies(SessionState session)
    {
        foreach (var enemy in session.Enemies.Values)
        {
            // Handle dead enemies: respawn after delay
            if (enemy.IsDead)
            {
                enemy.RespawnTimer -= TickDeltaTime;
                if (enemy.RespawnTimer <= 0)
                {
                    // Respawn at spawn position
                    enemy.Hp = enemy.MaxHp;
                    enemy.X = enemy.SpawnX;
                    enemy.Y = enemy.SpawnY;
                    enemy.RespawnTimer = enemy.RespawnDelay;
                    enemy.Status = EnemyStatus.Idle;
                    session.Version++;
                }
                continue;
            }

            var target = session.Players.Values.FirstOrDefault(p => !p.IsDead);
            if (target == null)
            {
                enemy.Status = EnemyStatus.Idle;
                continue;
            }

            float dx = target.X - enemy.X;
            float dy = target.Y - enemy.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist <= enemy.AttackRange)
            {
                enemy.Status = EnemyStatus.Attacking;
                enemy.AttackTimer -= TickDeltaTime;
                if (enemy.AttackTimer <= 0)
                {
                    target.Hp -= enemy.Damage;
                    if (target.Hp < 0) target.Hp = 0;
                    enemy.AttackTimer = enemy.AttackCooldown;
                    session.Version++;
                }
                continue;
            }

            if (dist <= enemy.DetectRange)
            {
                enemy.Status = EnemyStatus.Chasing;
                var dir = (x: dx / dist, y: dy / dist);
                enemy.X += dir.x * enemy.Speed * TickDeltaTime;
                enemy.Y += dir.y * enemy.Speed * TickDeltaTime;
            }
            else
            {
                enemy.Status = EnemyStatus.Idle;
            }
        }
    }

    private SessionState CreateDefaultSession(string sessionId)
    {
        var session = new SessionState
        {
            SessionId = sessionId,
            Version = 1
        };

        // Spawn one dummy enemy
        var enemy = CreateDefaultEnemy();
        session.Enemies[enemy.Id] = enemy;

        return session;
    }

    private EnemyState CreateDefaultEnemy()
    {
        return new EnemyState
        {
            Id = Guid.NewGuid(),
            TypeId = "slime",
            X = 2f,
            Y = 2f,
            SpawnX = 2f,
            SpawnY = 2f,
            MaxHp = 30,
            Hp = 30,
            DetectRange = 6f,
            AttackRange = 1.2f,
            Speed = 2f,
            Damage = 5,
            AttackCooldown = 1.5f,
            AttackTimer = 0f,
            RespawnDelay = 5f,
            RespawnTimer = 5f
        };
    }

    private PlayerState CreateDefaultPlayer(Guid playerId, string playerName)
    {
        return new PlayerState
        {
            Id = playerId,
            Name = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName,
            X = 0,
            Y = 0,
            Hp = 50,
            MaxHp = 50,
            Damage = 10,
            Range = 1.5f,
            Speed = 4f,
            Sequence = 0,
            Level = 1,
            Exp = 0,
            Gold = 100
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

