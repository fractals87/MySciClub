using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MySciClub.Api.Tests;

public sealed class SociControllerTests(MySciClubApiFactory factory) : IClassFixture<MySciClubApiFactory>
{
    [Fact]
    public async Task TesseramentoUnavailable_IsReturnedAsProblemDetails503()
    {
        var state = factory.Services.GetRequiredService<FakeSocioApiState>();
        state.Reset();
        state.TesseramentoUnavailable = true;
        using var client = CreateClient();
        await SignInAsAdministratorAsync(client);

        using var response = await client.GetAsync("/api/soci");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("tesseramento_non_disponibile", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DeleteBlockedByRelations_ReturnsConflictProblemDetails()
    {
        var state = factory.Services.GetRequiredService<FakeSocioApiState>();
        state.Reset();
        state.DeletionBlockers = ["Prenotazioni"];
        using var client = CreateClient();
        var token = await SignInAsAdministratorAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/soci/42");
        request.Headers.Add("X-CSRF-TOKEN", token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EliminazioneBloccata", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PhotoUpload_RejectsUnsupportedContentTypeWithSafeProblemDetails()
    {
        using var client = CreateClient();
        var token = await SignInAsAdministratorAsync(client);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new MediaTypeHeaderValue("text/plain") } }, "file", "photo.txt");
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/soci/42/foto") { Content = content };
        request.Headers.Add("X-CSRF-TOKEN", token);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Fotografia non valida", problem.GetProperty("title").GetString());
        Assert.DoesNotContain("stack", problem.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storage", problem.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Administrator_CanSearchCreateReadUpdateManagePhotoExportAndDelete()
    {
        var state = factory.Services.GetRequiredService<FakeSocioApiState>();
        state.Reset();
        using var client = CreateClient();
        var token = await SignInAsAdministratorAsync(client);

        using (var countriesResponse = await client.GetAsync("/api/soci/cataloghi/paesi"))
        {
            var countries = await countriesResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(countries[0].TryGetProperty("id", out _));
            Assert.Equal("IT", countries[0].GetProperty("codiceIsoAlpha2").GetString());
        }
        using (var townsResponse = await client.GetAsync("/api/soci/cataloghi/comuni?countryIsoAlpha2=IT"))
        {
            var towns = await townsResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(towns[0].TryGetProperty("id", out _));
            Assert.Equal("H501", towns[0].GetProperty("codiceCatastale").GetString());
        }

        using (var response = await client.GetAsync("/api/soci?text=rossi&codiceCatastaleComune=H501&page=2&pageSize=50&sortBy=4&sortDirection=1"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            var page = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
            Assert.Equal("rossi", state.LastQuery?.Text);
            Assert.Equal(10, state.LastQuery?.ComuneId);
            Assert.Equal(2, state.LastQuery?.Page);
            Assert.Equal(50, state.LastQuery?.PageSize);
        }

        using (var response = await client.GetAsync("/api/soci/42"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Mario", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("nome").GetString());
        }

        var input = new
        {
            nome = "Ada", cognome = "Lovelace", dataNascita = "1980-01-01", codiceIsoPaeseNascita = "IT",
            codiceCatastaleComuneItalianoNascita = "H501", sesso = 1, cittadinanze = new[] { new { codiceIsoPaese = "IT", principale = true } },
            indirizzoResidenza = "Via Roma 1", codiceIsoPaeseResidenza = "IT", cap = "00100", codiceCatastaleComuneItalianoResidenza = "H501",
            email = "ada@example.test", cellulare = "+390000000", sceltaCodiceFiscale = 0
        };
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/soci")
        {
            Content = JsonContent.Create(input)
        };
        createRequest.Headers.Add("X-CSRF-TOKEN", token);
        using var createdResponse = await client.SendAsync(createRequest);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(created.TryGetProperty("id", out _));
        var numeroSocio = created.GetProperty("numeroSocio").GetInt64();
        Assert.True(numeroSocio > 0);

        using (var response = await client.GetAsync($"/api/soci/{numeroSocio}"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(numeroSocio, detail.GetProperty("numeroSocio").GetInt64());
            Assert.False(detail.TryGetProperty("id", out _));
        }
        using (var internalKeyRoute = await client.GetAsync("/api/soci/43"))
            Assert.Equal(HttpStatusCode.NotFound, internalKeyRoute.StatusCode);

        token = await GetRequestTokenAsync(client);
        var updateInput = new
        {
            nome = "Augusta", cognome = "Lovelace", dataNascita = "1980-01-01", codiceIsoPaeseNascita = "IT",
            codiceCatastaleComuneItalianoNascita = "H501", sesso = 1, cittadinanze = new[] { new { codiceIsoPaese = "IT", principale = true } },
            indirizzoResidenza = "Via Roma 1", codiceIsoPaeseResidenza = "IT", cap = "00100", codiceCatastaleComuneItalianoResidenza = "H501",
            email = "ada@example.test", cellulare = "+390000000", sceltaCodiceFiscale = 0
        };
        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/soci/{numeroSocio}") { Content = JsonContent.Create(updateInput) };
        updateRequest.Headers.Add("X-CSRF-TOKEN", token);
        using var updatedResponse = await client.SendAsync(updateRequest);
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        Assert.True((await updatedResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codiceFiscalePotenzialmenteObsoleto").GetBoolean());

        token = await GetRequestTokenAsync(client);
        using var photoContent = new MultipartFormDataContent();
        photoContent.Add(new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") } }, "file", "profile.jpg");
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/soci/{numeroSocio}/foto") { Content = photoContent };
        uploadRequest.Headers.Add("X-CSRF-TOKEN", token);
        using var uploadResponse = await client.SendAsync(uploadRequest);
        Assert.Equal(HttpStatusCode.NoContent, uploadResponse.StatusCode);

        using (var photoResponse = await client.GetAsync($"/api/soci/{numeroSocio}/foto"))
        {
            Assert.Equal(HttpStatusCode.OK, photoResponse.StatusCode);
            Assert.Equal("image/webp", photoResponse.Content.Headers.ContentType?.MediaType);
            Assert.Equal(new byte[] { 1, 2, 3 }, await photoResponse.Content.ReadAsByteArrayAsync());
        }

        using (var exportResponse = await client.GetAsync("/api/soci/export?page=1&pageSize=25"))
        {
            Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", exportResponse.Content.Headers.ContentType?.MediaType);
            Assert.Contains("ElencoSoci_", exportResponse.Content.Headers.ContentDisposition?.FileName ?? string.Empty);
        }

        token = await GetRequestTokenAsync(client);
        using var removePhotoRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/soci/{numeroSocio}/foto");
        removePhotoRequest.Headers.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(removePhotoRequest)).StatusCode);

        token = await GetRequestTokenAsync(client);
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/soci/{numeroSocio}");
        deleteRequest.Headers.Add("X-CSRF-TOKEN", token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(deleteRequest)).StatusCode);
        using var notFound = await client.GetAsync($"/api/soci/{numeroSocio}");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), HandleCookies = true, AllowAutoRedirect = false
    });

    private static async Task<string> SignInAsAdministratorAsync(HttpClient client)
    {
        var token = await GetRequestTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/test-probe/issue-admin-cookie");
        request.Headers.Add("X-CSRF-TOKEN", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return await GetRequestTokenAsync(client);
    }

    private static async Task<string> GetRequestTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/security/antiforgery-token");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.RequestToken;
    }

    private sealed record TokenResponse(string RequestToken);
}
