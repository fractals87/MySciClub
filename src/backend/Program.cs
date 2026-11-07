using MySciClub.Infrastructure;
using MySciClub.Infrastructure.Identity;
using MySciClub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using MySciClub.Api.ApiSecurity;
using MySciClub.Application.Features.Members;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SocioExceptionHandler>();
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddOpenApi();
builder.Host.ConfigureServices((context, services) =>
    services.AddMySciClubInfrastructure(context.Configuration));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAdministratorContext, HttpAdministratorContext>();
builder.Services.AddScoped<CreateSocioUseCase>();
builder.Services.AddScoped<GetSocioUseCase>();
builder.Services.AddScoped<UpdateSocioUseCase>();
builder.Services.AddScoped<SearchSociUseCase>();
builder.Services.AddScoped<DeleteSocioUseCase>();
builder.Services.AddScoped<ManageSocioPhotoUseCases>();
builder.Services.AddScoped<ExportSociUseCase>();
builder.Services.AddScoped<SocioCatalogUseCases>();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;

        options.Password.RequiredLength = 15;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<long>>()
    .AddEntityFrameworkStores<MySciClubDbContext>()
    .AddDefaultTokenProviders()
    .AddSignInManager();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-MySciClub.Auth";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services
    .AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-MySciClub.Antiforgery";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(async (StatusCodeContext statusCodeContext) =>
{
    var response = statusCodeContext.HttpContext.Response;
    var status = response.StatusCode;
    var title = status switch
    {
        StatusCodes.Status401Unauthorized => "Autenticazione richiesta",
        StatusCodes.Status403Forbidden => "Accesso negato",
        StatusCodes.Status404NotFound => "Risorsa non trovata",
        StatusCodes.Status400BadRequest => "Richiesta non valida",
        _ => "Richiesta non completata"
    };
    var problem = new ProblemDetails { Status = status, Title = title, Type = $"https://httpstatuses.com/{status}" };
    problem.Extensions["traceId"] = statusCodeContext.HttpContext.TraceIdentifier;
    response.ContentType = "application/problem+json";
    await JsonSerializer.SerializeAsync(response.Body, problem, cancellationToken: statusCodeContext.HttpContext.RequestAborted);
});
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
