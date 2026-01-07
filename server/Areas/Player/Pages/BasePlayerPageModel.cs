using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GameServer.Areas.Player.Pages;

/// <summary>
/// Base page model for player pages that require authentication.
/// </summary>
public abstract class BasePlayerPageModel : PageModel
{
    #region Protected Properties
    protected Guid? PlayerId
    {
        get
        {
            var playerIdStr = HttpContext.Session.GetString("PlayerId");
            if (Guid.TryParse(playerIdStr, out var playerId))
            {
                return playerId;
            }
            return null;
        }
    }

    protected string? PlayerName => HttpContext.Session.GetString("PlayerName");
    #endregion

    #region Protected Methods
    protected bool IsPlayerLoggedIn()
    {
        return PlayerId.HasValue && !string.IsNullOrEmpty(PlayerName);
    }

    protected IActionResult RedirectToLogin()
    {
        return RedirectToPage("/Login");
    }

    protected IActionResult RequireLogin()
    {
        if (!IsPlayerLoggedIn())
        {
            return RedirectToLogin();
        }
        return null!;
    }
    #endregion

    #region Public Methods
    public override void OnPageHandlerExecuting(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context)
    {
        if (!IsPlayerLoggedIn())
        {
            context.Result = RedirectToLogin();
            return;
        }
        base.OnPageHandlerExecuting(context);
    }
    #endregion
}

