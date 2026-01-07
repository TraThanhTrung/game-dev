using GameServer.Data;
using GameServer.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Services;

public class SessionTrackingService
{
    #region Private Fields
    private readonly GameDbContext _db;
    private readonly ILogger<SessionTrackingService> _logger;
    #endregion

    #region Constructor
    public SessionTrackingService(
        GameDbContext db,
        ILogger<SessionTrackingService> logger)
    {
        _db = db;
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public async Task<GameSession> StartSessionAsync(int playerCount = 0)
    {
        var session = new GameSession
        {
            SessionId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow,
            Status = "Active",
            PlayerCount = playerCount
        };

        _db.GameSessions.Add(session);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Started game session {SessionId} with {PlayerCount} players", 
            session.SessionId, playerCount);

        return session;
    }

    public async Task EndSessionAsync(Guid sessionId, string status = "Completed")
    {
        var session = await _db.GameSessions.FindAsync(sessionId);
        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found for ending", sessionId);
            return;
        }

        session.EndTime = DateTime.UtcNow;
        session.Status = status;

        // Update play duration for all players
        var players = await _db.SessionPlayers
            .Where(sp => sp.SessionId == sessionId && sp.LeaveTime == null)
            .ToListAsync();

        foreach (var player in players)
        {
            player.LeaveTime = DateTime.UtcNow;
            player.PlayDuration = (int)((DateTime.UtcNow - player.JoinTime).TotalSeconds);
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Ended game session {SessionId} with status {Status}", 
            sessionId, status);
    }

    public async Task TrackPlayerJoinAsync(Guid sessionId, Guid playerId)
    {
        var existing = await _db.SessionPlayers
            .FirstOrDefaultAsync(sp => sp.SessionId == sessionId && sp.PlayerId == playerId);

        if (existing != null)
        {
            // Player already in session, update join time
            existing.JoinTime = DateTime.UtcNow;
            existing.LeaveTime = null;
            existing.PlayDuration = null;
        }
        else
        {
            var sessionPlayer = new SessionPlayer
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                PlayerId = playerId,
                JoinTime = DateTime.UtcNow
            };

            _db.SessionPlayers.Add(sessionPlayer);

            // Update session player count
            var session = await _db.GameSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.PlayerCount = await _db.SessionPlayers
                    .CountAsync(sp => sp.SessionId == sessionId && sp.LeaveTime == null);
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<GameSession?> GetSessionAsync(Guid sessionId)
    {
        return await _db.GameSessions.FindAsync(sessionId);
    }

    public async Task TrackPlayerLeaveAsync(Guid sessionId, Guid playerId)
    {
        var sessionPlayer = await _db.SessionPlayers
            .FirstOrDefaultAsync(sp => sp.SessionId == sessionId && 
                                      sp.PlayerId == playerId && 
                                      sp.LeaveTime == null);

        if (sessionPlayer != null)
        {
            sessionPlayer.LeaveTime = DateTime.UtcNow;
            sessionPlayer.PlayDuration = (int)((DateTime.UtcNow - sessionPlayer.JoinTime).TotalSeconds);

            // Update session player count
            var session = await _db.GameSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.PlayerCount = await _db.SessionPlayers
                    .CountAsync(sp => sp.SessionId == sessionId && sp.LeaveTime == null);
            }

            await _db.SaveChangesAsync();
        }
    }
    #endregion
}

