using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MySciClub.Api.Tests;

public sealed class SecurityFoundationTests(MySciClubApiFactory factory)
    : IClassFixture<MySciClubApiFactory>
{
    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false
    });

    [Fact]
    public void IdentityOptions_RequireUniqueAndConfirmedEmailAndExplicitPolicies()
    {
        var options = factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.True(options.User.RequireUniqueEmail);
        Assert.True(options.SignIn.RequireConfirmedEmail);
        Assert.Equal(15, options.Password.RequiredLength);
        Assert.False(options.Password.RequireDigit);
        Assert.False(options.Password.RequireLowercase);
        Assert.False(options.Password.RequireUppercase);
        Assert.False(options.Password.RequireNonAlphanumeric);
        Assert.True(options.Lockout.AllowedForNewUsers);
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);
        Assert.Equal(TokenOptions.DefaultProvider, options.Tokens.PasswordResetTokenProvider);
    }

    [Fact]
    public void AuthenticationCookie_UsesSecureHostOnlyPolicyAndExplicitLifetime()
    {
        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal("__Host-MySciClub.Auth", options.Cookie.Name);
        Assert.Equal("/", options.Cookie.Path);
        Assert.Null(options.Cookie.Domain);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Strict, options.Cookie.SameSite);
        Assert.Equal(TimeSpan.FromHours(8), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
    }

    [Fact]
    public async Task AntiforgeryEndpoint_ReturnsRequestTokenAndHttpOnlySecureCookie()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/security/antiforgery-token");
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(payload?.RequestToken));
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-MySciClub.Antiforgery=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MutatingMvcAction_RejectsRequestWithoutAntiforgeryToken()
    {
        using var client = CreateClient();
        using var response = await client.PostAsync("/api/test-probe/mutation", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MutatingMvcAction_AcceptsRequestWithAntiforgeryToken()
    {
        using var client = CreateClient();
        var token = await GetRequestTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/test-probe/mutation");
        request.Headers.Add("X-CSRF-TOKEN", token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task OpenApi_IsAnonymousInDevelopment()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticationFailures_Return401And403WithoutRedirects()
    {
        using var client = CreateClient();

        using var unauthenticatedResponse = await client.GetAsync("/api/test-probe/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedResponse.StatusCode);
        Assert.Null(unauthenticatedResponse.Headers.Location);
        Assert.NotEqual("text/html", unauthenticatedResponse.Content.Headers.ContentType?.MediaType);

        var requestToken = await GetRequestTokenAsync(client);
        using var signInRequest = new HttpRequestMessage(HttpMethod.Post, "/api/test-probe/issue-cookie");
        signInRequest.Headers.Add("X-CSRF-TOKEN", requestToken);
        using var signInResponse = await client.SendAsync(signInRequest);
        Assert.Equal(HttpStatusCode.NoContent, signInResponse.StatusCode);

        using var forbiddenResponse = await client.GetAsync("/api/test-probe/administrator");
        Assert.True(
            forbiddenResponse.StatusCode == HttpStatusCode.Forbidden,
            await forbiddenResponse.Content.ReadAsStringAsync());
        Assert.Null(forbiddenResponse.Headers.Location);
        Assert.NotEqual("text/html", forbiddenResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MembersApi_RequiresAuthenticationAndAdministratorRole()
    {
        using var client = CreateClient();

        using var anonymous = await client.GetAsync("/api/soci");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal("application/problem+json", anonymous.Content.Headers.ContentType?.MediaType);

        var requestToken = await GetRequestTokenAsync(client);
        using var signIn = new HttpRequestMessage(HttpMethod.Post, "/api/test-probe/issue-cookie");
        signIn.Headers.Add("X-CSRF-TOKEN", requestToken);
        using var signInResponse = await client.SendAsync(signIn);
        Assert.Equal(HttpStatusCode.NoContent, signInResponse.StatusCode);

        using var nonAdministrator = await client.GetAsync("/api/soci");
        Assert.Equal(HttpStatusCode.Forbidden, nonAdministrator.StatusCode);
        Assert.Equal("application/problem+json", nonAdministrator.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/soci/cataloghi/paesi")]
    [InlineData("/api/soci/cataloghi/comuni")]
    [InlineData("/api/soci/42")]
    [InlineData("/api/soci/42/foto")]
    [InlineData("/api/soci/export")]
    public async Task EveryMemberReadRoute_RequiresAdministrator(string path)
    {
        using var client = CreateClient();

        using var anonymous = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var requestToken = await GetRequestTokenAsync(client);
        using var signIn = new HttpRequestMessage(HttpMethod.Post, "/api/test-probe/issue-cookie");
        signIn.Headers.Add("X-CSRF-TOKEN", requestToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(signIn)).StatusCode);

        using var nonAdministrator = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, nonAdministrator.StatusCode);
    }

    [Fact]
    public async Task MembersApi_MutationRejectsMissingAntiforgeryToken()
    {
        using var client = CreateClient();
        var requestToken = await GetRequestTokenAsync(client);
        using var signIn = new HttpRequestMessage(HttpMethod.Post, "/api/test-probe/issue-admin-cookie");
        signIn.Headers.Add("X-CSRF-TOKEN", requestToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(signIn)).StatusCode);

        using var response = await client.PostAsJsonAsync("/api/soci", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task LoginSessionAdministratorAccessAndLogout_UseAntiforgeryAndIdentityCookie()
    {
        factory.Services.GetRequiredService<FakeIdentityState>().Reset();
        using var client = CreateClient();
        var anonymousState = await client.GetFromJsonAsync<SessionResponse>("/api/security/session");
        Assert.False(anonymousState!.Autenticato);
        Assert.False(anonymousState.Amministratore);

        var token = await GetRequestTokenAsync(client);
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/security/login")
        {
            Content = JsonContent.Create(new { username = FakeIdentityState.AdminEmail, password = FakeIdentityState.AdminPassword })
        };
        login.Headers.Add("X-CSRF-TOKEN", token);
        using var loginResponse = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authCookie = Assert.Single(loginResponse.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-MySciClub.Auth=", authCookie);
        Assert.Contains("httponly", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", authCookie, StringComparison.OrdinalIgnoreCase);

        var authenticatedState = await client.GetFromJsonAsync<SessionResponse>("/api/security/session");
        Assert.True(authenticatedState!.Autenticato);
        Assert.True(authenticatedState.Amministratore);
        using var memberRoute = await client.GetAsync("/api/soci");
        Assert.Equal(HttpStatusCode.OK, memberRoute.StatusCode);

        token = await GetRequestTokenAsync(client);
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/security/logout");
        logout.Headers.Add("X-CSRF-TOKEN", token);
        using var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.False((await client.GetFromJsonAsync<SessionResponse>("/api/security/session"))!.Autenticato);
        using var afterLogout = await client.GetAsync("/api/soci");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task LoginRejectsBadUnconfirmedAndLockedAccountsWithoutRevealingIdentity()
    {
        var state = factory.Services.GetRequiredService<FakeIdentityState>();
        state.Reset();
        using var client = CreateClient();

        var invalid = await SendLoginAsync(client, "unknown@example.test", "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        var invalidBody = await invalid.Content.ReadAsStringAsync();
        Assert.Contains("Credenziali non valide", invalidBody);
        Assert.DoesNotContain("unknown@example.test", invalidBody);
        invalid.Dispose();

        var unconfirmed = await SendLoginAsync(client, "unconfirmed@example.test", "any");
        Assert.Equal(HttpStatusCode.Unauthorized, unconfirmed.StatusCode);
        unconfirmed.Dispose();

        state.AdminLocked = true;
        var locked = await SendLoginAsync(client, FakeIdentityState.AdminEmail, FakeIdentityState.AdminPassword);
        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
        Assert.Contains("temporaneamente bloccato", await locked.Content.ReadAsStringAsync());
        locked.Dispose();
    }

    [Fact]
    public async Task LoginAndLogoutRejectRequestsWithoutAntiforgeryTokens()
    {
        using var client = CreateClient();
        using var login = await client.PostAsJsonAsync("/api/security/login", new { username = FakeIdentityState.AdminEmail, password = FakeIdentityState.AdminPassword });
        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);

        var token = await GetRequestTokenAsync(client);
        using var authorizedLogin = new HttpRequestMessage(HttpMethod.Post, "/api/security/login")
        {
            Content = JsonContent.Create(new { username = FakeIdentityState.AdminEmail, password = FakeIdentityState.AdminPassword })
        };
        authorizedLogin.Headers.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(authorizedLogin)).StatusCode);
        using var logoutWithoutToken = await client.PostAsync("/api/security/logout", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, logoutWithoutToken.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedNonAdministratorGetsSessionWithoutAdminAccess()
    {
        using var client = CreateClient();
        var login = await SendLoginAsync(client, FakeIdentityState.MemberEmail, FakeIdentityState.AdminPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        login.Dispose();

        var session = await client.GetFromJsonAsync<SessionResponse>("/api/security/session");
        Assert.True(session!.Autenticato);
        Assert.False(session.Amministratore);
        using var members = await client.GetAsync("/api/soci");
        Assert.Equal(HttpStatusCode.Forbidden, members.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendLoginAsync(HttpClient client, string username, string password)
    {
        var token = await GetRequestTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/security/login")
        {
            Content = JsonContent.Create(new { username, password })
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private static async Task<string> GetRequestTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/security/antiforgery-token");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return Assert.IsType<string>(payload?.RequestToken);
    }

    private sealed record TokenResponse(string RequestToken);
    private sealed record SessionResponse(bool Autenticato, string? NomeVisualizzato, bool Amministratore);
}
