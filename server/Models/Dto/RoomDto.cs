namespace GameServer.Models.Dto;

public class CreateRoomRequest
{
    // Accept string for Unity JsonUtility compatibility (Unity sends playerId as string)
    // ASP.NET Core will try to bind to PlayerId (Guid) first, but we also accept string
    public string? PlayerId { get; set; }
    public string Token { get; set; } = string.Empty;
    
    // Helper method to get PlayerId as Guid
    public Guid GetPlayerId()
    {
        if (string.IsNullOrWhiteSpace(PlayerId))
            return Guid.Empty;
        
        if (Guid.TryParse(PlayerId, out var parsedId))
            return parsedId;
        
        return Guid.Empty;
    }
}

public class CreateRoomResponse
{
    public string RoomId { get; set; } = string.Empty;
}

public class JoinRoomRequest
{
    // Accept string for Unity JsonUtility compatibility (Unity sends playerId as string)
    public string? PlayerId { get; set; }
    public string RoomId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    
    // Helper method to get PlayerId as Guid
    public Guid GetPlayerId()
    {
        if (string.IsNullOrWhiteSpace(PlayerId))
            return Guid.Empty;
        
        if (Guid.TryParse(PlayerId, out var parsedId))
            return parsedId;
        
        return Guid.Empty;
    }
}

public class JoinRoomResponse
{
    public bool Success { get; set; }
    public string RoomId { get; set; } = string.Empty;
}

