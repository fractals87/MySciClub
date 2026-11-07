using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using MySciClub.Application.Features.Members;

namespace MySciClub.Api.ApiSecurity;

public sealed class HttpAdministratorContext(IHttpContextAccessor accessor) : IAdministratorContext
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;
    public bool IsAdministrator => User?.IsInRole("Administrator") == true;
    public long? AdministratorId => long.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub"), out var id) ? id : null;
}
