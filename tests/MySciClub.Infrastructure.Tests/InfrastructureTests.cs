using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySciClub.Application.Features.Members;
using MySciClub.Domain.Soci;
using MySciClub.Infrastructure.Exports;
using MySciClub.Infrastructure.Files;
using MySciClub.Infrastructure.Persistence;
using MySciClub.Infrastructure.Persistence.Entities;
using MySciClub.Infrastructure;
using SkiaSharp;
using Xunit;

namespace MySciClub.Infrastructure.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void Sql_server_model_has_expected_keys_indexes_sequence_and_concurrency_token()
    {
        using var db = CreateContext();
        var socio = db.Model.FindEntityType(typeof(SocioRecord))!;
        Assert.Equal("Soci", socio.GetTableName());
        Assert.Contains(socio.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(SocioRecord.CodiceFiscale));
        Assert.Contains(socio.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(SocioRecord.NumeroSocio));
        Assert.True(socio.FindProperty(nameof(SocioRecord.RowVersion))!.IsConcurrencyToken);
        Assert.DoesNotContain(socio.GetProperties(), x => x.Name.Contains("Tesser", StringComparison.OrdinalIgnoreCase));
        var sequence = Assert.Single(db.Model.GetSequences(), x => x.Name == "SeqNumeroSocio");
        Assert.Equal(1, sequence.StartValue);
        Assert.Equal(1, sequence.IncrementBy);
        var town = db.Model.FindEntityType(typeof(ItalianTownRecord))!;
        Assert.Contains(town.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual([nameof(ItalianTownRecord.CadastralCode)]));
        Assert.Contains(socio.GetForeignKeys(), x => x.Properties.Select(p => p.Name).SequenceEqual([nameof(SocioRecord.ComuneItalianoResidenzaId), nameof(SocioRecord.PaeseResidenzaId)]));
        var country = db.Model.FindEntityType(typeof(CountryRecord))!;
        Assert.Contains(country.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(CountryRecord.IsoAlpha2));
        var citizenship = db.Model.FindEntityType(typeof(SocioCitizenshipRecord))!;
        Assert.Contains(citizenship.GetIndexes(), x => x.IsUnique && x.GetFilter() == "[Principale] = 1");
        Assert.Equal("AuditLog", db.Model.FindEntityType(typeof(AuditRecord))!.GetTableName());
    }

    [Fact]
    public async Task Image_processor_converts_and_resizes_to_webp_without_upscaling()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(900, 450));
        using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.DarkBlue);
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        await using var input = new MemoryStream(png.ToArray());
        var converted = await new SkiaPhotoProcessor().ProcessAsync(input, "image/png");
        await using var output = converted.Content;
        var bytes = ((MemoryStream)output).ToArray();
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        Assert.Equal(SKEncodedImageFormat.Webp, codec!.EncodedFormat);
        using var decoded = SKBitmap.Decode(data);
        Assert.Equal(512, decoded!.Width);
        Assert.Equal(256, decoded.Height);
        Assert.DoesNotContain("EXIF", Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Image_processor_rejects_content_type_spoofing_and_oversize()
    {
        var processor = new SkiaPhotoProcessor();
        await using var wrongType = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync(wrongType, "image/jpeg"));
        await using var tooLarge = new MemoryStream(new byte[5 * 1024 * 1024 + 1]);
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync(tooLarge, "image/png"));
    }

    [Theory]
    [InlineData("image/jpeg", SKEncodedImageFormat.Jpeg)]
    [InlineData("image/png", SKEncodedImageFormat.Png)]
    [InlineData("image/webp", SKEncodedImageFormat.Webp)]
    public async Task Image_processor_accepts_each_approved_format(string contentType, SKEncodedImageFormat inputFormat)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(180, 90));
        using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.DarkGreen);
        using var encoded = bitmap.Encode(inputFormat, 80);
        await using var input = new MemoryStream(encoded.ToArray());
        var result = await new SkiaPhotoProcessor().ProcessAsync(input, contentType);
        await using var output = result.Content;
        using var data = SKData.CreateCopy(((MemoryStream)output).ToArray());
        using var codec = SKCodec.Create(data);
        Assert.Equal(SKEncodedImageFormat.Webp, codec!.EncodedFormat);
        Assert.Equal("image/webp", result.ContentType);
        Assert.Equal("socio-photo.webp", result.FileName);
        using var decoded = SKBitmap.Decode(data);
        Assert.Equal(180, decoded!.Width);
        Assert.Equal(90, decoded.Height);
    }

    [Fact]
    public async Task File_storage_returns_opaque_key_and_supports_open_delete()
    {
        var root = Path.Combine(Path.GetTempPath(), "MySciClubInfraTests", Guid.NewGuid().ToString("N"));
        using var db = CreateContext();
        try
        {
            var storage = new FilePhotoStorage(root, db);
            await using var input = new MemoryStream([1, 2, 3]);
            var key = await storage.StoreAsync(input, "ignored.png", "image/webp");
            Assert.Matches("^photos/[a-f0-9]{32}\\.webp$", key);
            Assert.DoesNotContain(root, key, StringComparison.OrdinalIgnoreCase);
            await using (var read = await storage.OpenReadAsync(key))
            {
                Assert.NotNull(read);
                Assert.Equal(3, read!.Length);
            }
            await storage.DeleteAsync(key);
            Assert.Null(await storage.OpenReadAsync(key));
            Assert.Throws<InvalidDataException>(() => { _ = storage.OpenReadAsync("../../secret"); });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Excel_export_contains_headers_freeze_filters_and_all_rows()
    {
        var rows = new[]
        {
            new SocioExportRow(9, "Ada", "Rossi", new DateOnly(1990, 2, 3), "Torino", Sesso.F,
                "RSSADA90B43L219K", "Italiana", "Via Uno", "10100", "Torino", "TO", "Italia", "a@example.it", "333", true),
            new SocioExportRow(10, "Luca", "Verdi", new DateOnly(1988, 6, 7), "Lione", Sesso.M,
                "VRDLCU88H07Z110X", "Francese", "Rue Deux", "69000", "Lione", null, "Francia", "l@example.it", "444", false)
        };
        var file = await new XlsxExportService().CreateAsync(rows);
        using var stream = new MemoryStream(file);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Contains("[Content_Types].xml", zip.Entries.Select(x => x.FullName));
        using var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = await sheet.ReadToEndAsync();
        Assert.Contains("state=\"frozen\"", xml);
        Assert.Contains("autoFilter", xml);
        Assert.Contains("Numero socio", xml);
        Assert.Contains("Stato tesseramento", xml);
        Assert.Contains("Non tesserato", xml);
        Assert.Contains("P3", xml);
    }

    [Fact]
    public async Task Tesseramento_adapter_fails_explicitly_until_source_exists()
    {
        await Assert.ThrowsAsync<TesseramentoUnavailableException>(() => new TesseramentoSourceNotConfigured().GetCurrentTesseratiAsync());
    }

    [Fact]
    public void Infrastructure_services_resolve_without_opening_a_database_connection()
    {
        var root = Path.Combine(Path.GetTempPath(), "MySciClubInfraTests", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MySciClub"] = "Server=localhost;Database=NeverOpened;User Id=unused;Password=unused;TrustServerCertificate=True",
            ["Storage:MemberPhotosRoot"] = root
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMySciClubInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISocioRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISocioCatalogRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<INumeroSocioGenerator>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MySciClub.Application.Abstractions.Audit.IAuditService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MySciClub.Application.Abstractions.FileStorage.IFileStorageService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPhotoProcessor>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IExcelExportService>());
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private static MySciClubDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MySciClubDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;User Id=unused;Password=unused;TrustServerCertificate=True")
            .Options;
        return new MySciClubDbContext(options);
    }
}
