using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Player.Pages;

/// <summary>
/// Logout page for players.
/// </summary>
public class LogoutModel : PageModel
{
    #region Private Fields
    private readonly ILogger<LogoutModel> _logger;
    #endregion

    #region Constructor
    public LogoutModel(ILogger<LogoutModel> logger)
    {
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public IActionResult OnPost()
    {
        var playerName = HttpContext.Session.GetString("PlayerName");
        
        HttpContext.Session.Clear();

        if (!string.IsNullOrEmpty(playerName))
        {
            _logger.LogInformation("Player logged out: {Name}", playerName);
        }

        return Redirect("/");
    }
    #endregion
}

