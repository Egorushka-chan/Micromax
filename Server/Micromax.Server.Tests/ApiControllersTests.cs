using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MicroMax.Server.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Micromax.Server.Tests;

public sealed class ApiControllersTests(ApiControllersTests.ApiWebApplicationFactory factory)
    : IClassFixture<ApiControllersTests.ApiWebApplicationFactory>
{
    [Fact]
    public async Task CreateWarehouseReturnsCreatedAndLocation()
    {
        using var client = await CreateAuthorizedClientAsync();

        using var response = await client.PostAsJsonAsync("/api/warehouses", new
        {
            name = "Новый склад",
            address = "Тестовый адрес"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var warehouse = await response.Content.ReadFromJsonAsync<WarehouseResponseDto>();
        Assert.NotNull(warehouse);
        Assert.Equal("Новый склад", warehouse.Name);
        Assert.EndsWith($"/api/warehouses/{warehouse.Id}", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddExistingWarehouseUserReturnsConflictProblemDetails()
    {
        using var client = await CreateAuthorizedClientAsync();
        var warehouseId = await GetWarehouseIdAsync(client);

        using var response = await client.PostAsJsonAsync($"/api/warehouses/{warehouseId}/users", new
        {
            email = "admin@micromax.local",
            roleCode = "ADMIN"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("Конфликт состояния", problem.Title);
        Assert.Equal("Пользователь уже добавлен в этот склад.", problem.Detail);
    }

    [Fact]
    public async Task MoveOperationWithInsufficientBalanceReturnsConflictProblemDetails()
    {
        using var client = await CreateAuthorizedClientAsync();

        var productId = await GetProductIdBySkuAsync(client, "GLV-001");
        var sourceCellId = await GetCellIdByCodeAsync(client, "A-1");
        var targetCellId = await GetCellIdByCodeAsync(client, "A-2");

        using var response = await client.PostAsJsonAsync("/api/operations/move", new
        {
            productId,
            sourceCellId,
            targetCellId,
            quantity = 9999m,
            comment = "Проверка конфликта"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("Недостаточный остаток в исходной ячейке.", problem.Detail);
    }

    [Fact]
    public async Task UnauthorizedRequestReturnsProblemDetails()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
        Assert.Equal("Требуется аутентификация", problem.Title);
        Assert.Equal("Для доступа к ресурсу требуется действующий токен.", problem.Detail);
    }

    [Fact]
    public async Task SwaggerDocumentContainsProblemDetailsForMoveOperation()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var json = await client.GetStringAsync("/swagger/v1/swagger.json");
        var root = JsonNode.Parse(json);

        var conflictResponse = root?["paths"]?["/api/operations/move"]?["post"]?["responses"]?["409"];
        Assert.NotNull(conflictResponse);
        Assert.Equal("application/problem+json", conflictResponse!["content"]?.AsObject().First().Key);
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@micromax.local",
            password = "Admin12345!"
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected successful login response, got {(int)response.StatusCode}. Body:{Environment.NewLine}{body}");

        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<int> GetWarehouseIdAsync(HttpClient client)
    {
        var warehouses = await client.GetFromJsonAsync<List<WarehouseResponseDto>>("/api/warehouses");
        return Assert.Single(warehouses!).Id;
    }

    private static async Task<int> GetProductIdBySkuAsync(HttpClient client, string sku)
    {
        var products = await client.GetFromJsonAsync<List<ProductResponseDto>>("/api/products");
        return Assert.Single(products!, x => x.Sku == sku).Id;
    }

    private static async Task<int> GetCellIdByCodeAsync(HttpClient client, string code)
    {
        var cells = await client.GetFromJsonAsync<List<CellResponseDto>>("/api/cells");
        return Assert.Single(cells!, x => x.Code == code).Id;
    }

    private sealed record AuthResponseDto(string AccessToken);

    private sealed record WarehouseResponseDto(int Id, string Name, string? Address);

    private sealed record ProductResponseDto(int Id, string Sku);

    private sealed record CellResponseDto(int Id, string Code);

    public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"api-tests-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<MicroMaxDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MicroMaxDbContext>>();
                services.RemoveAll<MicroMaxDbContext>();
                services.AddDbContext<MicroMaxDbContext>(options =>
                    options
                        .UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            });
        }
    }
}
