using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySciClub.Infrastructure.Identity;

namespace MySciClub.Api.Tests;

internal sealed class FakeIdentityState
{
    public const string AdminEmail = "admin@example.test";
    public const string MemberEmail = "member@example.test";
    public const string AdminPassword = "TestPassword-Only!";
    public int FailedAttempts { get; set; }
    public bool AdminLocked { get; set; }
    public bool AdminEmailConfirmed { get; set; } = true;

    public IReadOnlyList<ApplicationUser> Users =>
    [
        new ApplicationUser { Id = 7, UserName = "admin", NormalizedUserName = "ADMIN", Email = AdminEmail,
            NormalizedEmail = AdminEmail.ToUpperInvariant(), EmailConfirmed = AdminEmailConfirmed },
        new ApplicationUser { Id = 9, UserName = "member", NormalizedUserName = "MEMBER", Email = MemberEmail,
            NormalizedEmail = MemberEmail.ToUpperInvariant(), EmailConfirmed = true },
        new ApplicationUser { Id = 8, UserName = "unconfirmed", NormalizedUserName = "UNCONFIRMED",
            Email = "unconfirmed@example.test", NormalizedEmail = "UNCONFIRMED@EXAMPLE.TEST", EmailConfirmed = false }
    ];

    public void Reset()
    {
        FailedAttempts = 0;
        AdminLocked = false;
        AdminEmailConfirmed = true;
    }
}

internal sealed class FakeIdentityUserStore(FakeIdentityState state) : IUserStore<ApplicationUser>, IUserEmailStore<ApplicationUser>, IUserRoleStore<ApplicationUser>
{
    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(state.Users.SingleOrDefault(x => x.Id.ToString() == userId));
    public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        Task.FromResult(state.Users.SingleOrDefault(x => x.NormalizedUserName == normalizedUserName));
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());
    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken) { user.Email = email; return Task.CompletedTask; }
    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);
    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.EmailConfirmed);
    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken) { user.EmailConfirmed = confirmed; return Task.CompletedTask; }
    public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(state.Users.SingleOrDefault(x => x.NormalizedEmail == normalizedEmail));
    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);
    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken) { user.NormalizedEmail = normalizedEmail; return Task.CompletedTask; }
    public Task AddToRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task RemoveFromRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult<IList<string>>(user.Id == 7 ? ["Administrator"] : []);
    public Task<bool> IsInRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken cancellationToken) => Task.FromResult(user.Id == 7 && normalizedRoleName == "ADMINISTRATOR");
    public Task<IList<ApplicationUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken) => Task.FromResult<IList<ApplicationUser>>(state.Users.Where(x => x.Id == 7 && normalizedRoleName == "ADMINISTRATOR").ToList());
    public void Dispose() { }
}

internal sealed class FakeIdentitySignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> options,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation,
    FakeIdentityState state)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, options, logger, schemes, confirmation)
{
    public override async Task<SignInResult> PasswordSignInAsync(ApplicationUser user, string password, bool isPersistent, bool lockoutOnFailure)
    {
        if (user.Id == 7 && state.AdminLocked) return SignInResult.LockedOut;
        if (!user.EmailConfirmed) return SignInResult.NotAllowed;
        if (user.Id is not (7 or 9) || password != FakeIdentityState.AdminPassword)
        {
            if (lockoutOnFailure && user.Id == 7) state.FailedAttempts++;
            if (user.Id == 7 && state.FailedAttempts >= 5) { state.AdminLocked = true; return SignInResult.LockedOut; }
            return SignInResult.Failed;
        }

        if (user.Id == 7) state.FailedAttempts = 0;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };
        if (user.Id == 7) claims.Add(new Claim(ClaimTypes.Role, "Administrator"));
        await Context.SignInAsync(IdentityConstants.ApplicationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme)));
        return SignInResult.Success;
    }
}
