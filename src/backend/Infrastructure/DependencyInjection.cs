using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySciClub.Infrastructure.Identity;
using MySciClub.Infrastructure.Persistence;
using MySciClub.Infrastructure.Files;
using MySciClub.Infrastructure.Exports;
using MySciClub.Application.Abstractions.Audit;
using MySciClub.Application.Abstractions.FileStorage;
using MySciClub.Application.Abstractions.TechnicalLog;
using MySciClub.Application.Features.Members;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MySciClub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMySciClubInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<MySciClubDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("MySciClub");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "The 'ConnectionStrings:MySciClub' configuration value is required to use MySciClubDbContext.");
            }

            options.UseSqlServer(connectionString);
        });
        services.AddScoped<SqlSocioRepository>();
        services.AddScoped<ISocioRepository>(sp => sp.GetRequiredService<SqlSocioRepository>());
        services.AddScoped<ISocioCatalogRepository, SqlSocioCatalogRepository>();
        services.AddScoped<INumeroSocioGenerator, SqlNumeroSocioGenerator>();
        services.AddScoped<IAuditService, SqlAuditService>();
        services.AddScoped<IApplicationUnitOfWork, SqlApplicationUnitOfWork>();
        services.AddScoped<ISocioRelationChecker, CurrentSchemaSocioRelationChecker>();
        services.TryAddScoped<ICurrentTesseramentoSource, TesseramentoSourceNotConfigured>();
        services.AddScoped<IPhotoProcessor, SkiaPhotoProcessor>();
        services.AddScoped<IExcelExportService, XlsxExportService>();
        services.AddScoped<TechnicalLogService>();
        services.AddScoped<ITechnicalLogService>(sp => sp.GetRequiredService<TechnicalLogService>());
        var photoRoot = configuration["Storage:MemberPhotosRoot"];
        if (string.IsNullOrWhiteSpace(photoRoot))
        {
            var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localData)) throw new InvalidOperationException("Configure Storage:MemberPhotosRoot to an absolute directory outside the repository.");
            photoRoot = Path.Combine(localData, "MySciClub", "MemberPhotos");
        }
        services.AddScoped<FilePhotoStorage>(sp => new FilePhotoStorage(photoRoot, sp.GetRequiredService<MySciClubDbContext>()));
        services.AddScoped<IFileStorageService>(sp => sp.GetRequiredService<FilePhotoStorage>());
        services.AddScoped<IPhotoStorageMaintenance>(sp => sp.GetRequiredService<FilePhotoStorage>());
        return services;
    }
}
