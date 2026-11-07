using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MySciClub.Infrastructure.Identity;

namespace MySciClub.Api.Controllers;

[ApiController]
[Route("api/security")]
public sealed class SecurityController(IAntiforgery antiforgery, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("antiforgery-token")]
    [ProducesResponseType<AntiforgeryTokenResponse>(StatusCodes.Status200OK)]
    public ActionResult<AntiforgeryTokenResponse> GetAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Headers.CacheControl = "no-store, no-cache";

        return Ok(new AntiforgeryTokenResponse(tokens.RequestToken!));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<ActionResult<SessionResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Username) ?? await userManager.FindByNameAsync(request.Username);
        var result = user is null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
            {
                Status = StatusCodes.Status423Locked,
                Title = "Account bloccato",
                Detail = "L'account è temporaneamente bloccato. Riprova più tardi."
            });

        if (!result.Succeeded)
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Accesso non riuscito",
                Detail = "Credenziali non valide o account non abilitato."
            });

        var roles = await userManager.GetRolesAsync(user!);
        return Ok(new SessionResponse(true, user!.UserName, roles.Contains("Administrator", StringComparer.Ordinal)));
    }

    [AllowAnonymous]
    [HttpGet("session")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    public ActionResult<SessionResponse> GetSession() => Ok(new SessionResponse(
        User.Identity?.IsAuthenticated == true,
        User.Identity?.IsAuthenticated == true ? User.Identity.Name : null,
        User.IsInRole("Administrator")));

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }
}

public sealed record AntiforgeryTokenResponse(string RequestToken);
public sealed record SessionResponse(bool Autenticato, string? NomeVisualizzato, bool Amministratore);

public sealed class LoginRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(254)]
    public string Username { get; init; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(256)]
    public string Password { get; init; } = string.Empty;
}
