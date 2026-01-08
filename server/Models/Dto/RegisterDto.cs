namespace GameServer.Models.Dto;

public class RegisterRequest
{
    public string PlayerName { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string PlayerName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterResponse
{
    public Guid PlayerId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string SessionId { get; set; } = "default";
}

