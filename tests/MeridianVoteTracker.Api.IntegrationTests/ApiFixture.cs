using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace MeridianVoteTracker.Api.IntegrationTests;

/// <summary>
/// Starts a real PostgreSQL in Docker and the real API on top of it, so the tests exercise the actual
/// migrations, constraints, background worker and HTTP pipeline, not mocks.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly string _uploads = Path.Combine(Path.GetTempPath(), "meridian-tests-" + Guid.NewGuid().ToString("N"));
    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory => _factory!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Program.cs reads its settings from the environment, exactly as it does in Docker.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Storage__UploadDirectory", _uploads);
        _factory = new WebApplicationFactory<Program>();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        await _postgres.DisposeAsync();
        if (Directory.Exists(_uploads))
        {
            Directory.Delete(_uploads, recursive: true);
        }
    }

    /// <summary>A client acting as one of the predefined users (dana, omer, lena), or anonymous when <paramref name="user"/> is null.</summary>
    public HttpClient ClientFor(string? user)
    {
        var client = Factory.CreateClient();
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add("X-User", user);
        }
        return client;
    }

    public static async Task<ImportView> UploadAsync(HttpClient client, string fileName, params string[] lines)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);

        var response = await client.PostAsync("/api/imports", content);
        Assert.Equal(System.Net.HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImportView>(Json))!;
    }

    /// <summary>Polls the import until the background worker has finished it.</summary>
    public static async Task<ImportView> WaitForAsync(HttpClient client, Guid importId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var import = (await client.GetFromJsonAsync<ImportView>($"/api/imports/{importId}", Json))!;
            if (import.Status is "Completed" or "Failed")
            {
                return import;
            }
            await Task.Delay(200);
        }
        throw new TimeoutException($"Import {importId} did not finish in time.");
    }

    public static async Task<ImportView> UploadAndWaitAsync(HttpClient client, string fileName, params string[] lines)
    {
        var started = await UploadAsync(client, fileName, lines);
        return await WaitForAsync(client, started.Id);
    }

    public static async Task<PageView<RowView>> RowsAsync(HttpClient client, Guid importId, string? rowClass = null) =>
        (await client.GetFromJsonAsync<PageView<RowView>>(
            $"/api/imports/{importId}/rows" + (rowClass is null ? "" : $"?class={rowClass}"), Json))!;

    public sealed record CountsView(long New, long Unchanged, long Duplicate, long Invalid, long Contradiction, long Conflict, int VotesCreated, int ConflictsOpened);

    public sealed record ImportView(Guid Id, string FileName, string Status, string? Error, long TotalRows, CountsView Counts);

    public sealed record RowView(int LineNumber, string Class, string? Reason, string? VoteId, string? CountryCode, string? OriginalValue);

    public sealed record PageView<T>(List<T> Items, int Total, int Page, int PageSize);
}
