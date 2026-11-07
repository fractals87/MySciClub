using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using MySciClub.Infrastructure.Identity;
using MySciClub.Application.Abstractions.Audit;
using MySciClub.Application.Abstractions.FileStorage;
using MySciClub.Application.Abstractions.TechnicalLog;
using MySciClub.Application.Features.Members;

namespace MySciClub.Api.Tests;

public sealed class MySciClubApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:MySciClub", "Server=localhost;Database=NeverOpened;User Id=unused;Password=unused;TrustServerCertificate=True");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddSingleton<FakeIdentityState>();
            services.AddSingleton<IUserStore<ApplicationUser>, FakeIdentityUserStore>();
            services.AddScoped<SignInManager<ApplicationUser>, FakeIdentitySignInManager>();
            services.AddSingleton<FakeSocioApiState>();
            services.AddScoped<ISocioRepository, FakeSocioRepository>();
            services.AddScoped<ISocioCatalogRepository, FakeSocioCatalog>();
            services.AddScoped<INumeroSocioGenerator, FakeSocioNumberGenerator>();
            services.AddScoped<IApplicationUnitOfWork, FakeSocioUnitOfWork>();
            services.AddScoped<IAuditService, FakeSocioAudit>();
            services.AddScoped<ISocioRelationChecker, FakeSocioRelations>();
            services.AddScoped<IPhotoProcessor, FakeSocioPhotoProcessor>();
            services.AddScoped<IFileStorageService, FakeSocioFileStorage>();
            services.AddScoped<IExcelExportService, FakeSocioExcel>();
            services.AddScoped<ITechnicalLogService, FakeSocioTechnicalLog>();
            services.AddExceptionHandler<TestExceptionHandler>();
            services.ConfigureApplicationCookie(options =>
            {
                options.Events.OnValidatePrincipal = _ => Task.CompletedTask;
            });
            services.AddControllers()
                .AddApplicationPart(typeof(SecurityProbeController).Assembly);
        });
}
}
