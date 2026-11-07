using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace MySciClub.Api.Tests;

[ApiController]
[Route("api/test-probe")]
public sealed class SecurityProbeController : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("mutation")]
    public IActionResult Mutation() => NoContent();

    [AllowAnonymous]
    [HttpPost("issue-cookie")]
    public async Task<IActionResult> IssueCookie()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(ClaimTypes.Name, "test-admin")
        ],
        IdentityConstants.ApplicationScheme);

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            new ClaimsPrincipal(identity));

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("issue-admin-cookie")]
    public async Task<IActionResult> IssueAdminCookie()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "test-admin-id"),
            new Claim(ClaimTypes.Name, "test-admin"),
            new Claim(ClaimTypes.Role, "Administrator")
        ], IdentityConstants.ApplicationScheme);

        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, new ClaimsPrincipal(identity));
        return NoContent();
    }

    [Authorize]
    [HttpGet("protected")]
    public IActionResult Protected() => Ok();

    [Authorize(Roles = "Administrator")]
    [HttpGet("administrator")]
    public IActionResult Administrator() => Ok();
}
