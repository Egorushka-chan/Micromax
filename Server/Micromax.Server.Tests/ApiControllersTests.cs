using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Identity;
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
    private const string AdminEmail = "admin@micromax.local";
    private const string AdminPassword = "Admin12345!";
    private const string WorkerEmail = "worker@micromax.local";
    private const string WorkerPassword = "Worker12345!";
    private const string ViewerEmail = "viewer@micromax.local";
    private const string ViewerPassword = "Viewer12345!";

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

    [Fact]
    public async Task AdminCanCreateProductBarcodeAndResolveIt()
    {
        using var client = await CreateAuthorizedClientAsync();
        var product = await CreateProductAsync(client);
        var barcodeValue = CreateUniqueBarcodeValue();

        using var createResponse = await client.PostAsJsonAsync($"/api/products/{product.Id}/barcodes", new
        {
            value = barcodeValue,
            symbology = "CODE_128"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var barcode = await createResponse.Content.ReadFromJsonAsync<BarcodeResponseDto>();
        Assert.NotNull(barcode);
        Assert.Equal(barcodeValue, barcode!.Value);

        var resolve = await client.GetFromJsonAsync<BarcodeResolveResponseDto>(
            $"/api/barcodes/resolve?value={Uri.EscapeDataString(barcodeValue)}");

        Assert.NotNull(resolve);
        Assert.True(resolve!.Found);
        Assert.Equal("Product", resolve.EntityType);
        Assert.Equal(product.Id, resolve.EntityId);
    }

    [Fact]
    public async Task WorkerAndViewerCanResolveProductBarcode()
    {
        using var adminClient = await CreateAuthorizedClientAsync();
        var product = await CreateProductAsync(adminClient);
        var barcodeValue = CreateUniqueBarcodeValue();

        using var createResponse = await adminClient.PostAsJsonAsync($"/api/products/{product.Id}/barcodes", new
        {
            value = barcodeValue,
            symbology = "CODE_128"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var workerClient = await CreateAuthorizedClientAsync(WorkerEmail, WorkerPassword);
        var workerResolve = await workerClient.GetFromJsonAsync<BarcodeResolveResponseDto>(
            $"/api/barcodes/resolve?value={Uri.EscapeDataString(barcodeValue)}");
        Assert.NotNull(workerResolve);
        Assert.True(workerResolve!.Found);
        Assert.Equal("Product", workerResolve.EntityType);
        Assert.Equal(product.Id, workerResolve.EntityId);

        using var viewerClient = await CreateAuthorizedClientAsync(ViewerEmail, ViewerPassword);
        var viewerResolve = await viewerClient.GetFromJsonAsync<BarcodeResolveResponseDto>(
            $"/api/barcodes/resolve?value={Uri.EscapeDataString(barcodeValue)}");
        Assert.NotNull(viewerResolve);
        Assert.True(viewerResolve!.Found);
        Assert.Equal("Product", viewerResolve.EntityType);
        Assert.Equal(product.Id, viewerResolve.EntityId);
    }

    [Fact]
    public async Task WorkerCannotCreateProductBarcode()
    {
        using var adminClient = await CreateAuthorizedClientAsync();
        var product = await CreateProductAsync(adminClient);

        using var workerClient = await CreateAuthorizedClientAsync(WorkerEmail, WorkerPassword);
        using var response = await workerClient.PostAsJsonAsync($"/api/products/{product.Id}/barcodes", new
        {
            value = CreateUniqueBarcodeValue(),
            symbology = "CODE_128"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status403Forbidden, problem!.Status);
    }

    [Fact]
    public async Task AdminCanCreateCellBarcodeAndResolveIt()
    {
        using var client = await CreateAuthorizedClientAsync();
        var cellId = await GetCellIdByCodeAsync(client, "A-1");
        var barcodeValue = CreateUniqueBarcodeValue();

        using var createResponse = await client.PostAsJsonAsync($"/api/cells/{cellId}/barcodes", new
        {
            value = barcodeValue,
            symbology = "CODE_128"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var resolve = await client.GetFromJsonAsync<BarcodeResolveResponseDto>(
            $"/api/barcodes/resolve?value={Uri.EscapeDataString(barcodeValue)}");

        Assert.NotNull(resolve);
        Assert.True(resolve!.Found);
        Assert.Equal("Cell", resolve.EntityType);
        Assert.Equal(cellId, resolve.EntityId);
    }

    [Fact]
    public async Task DeactivatedBarcodeIsNoLongerResolved()
    {
        using var client = await CreateAuthorizedClientAsync();
        var product = await CreateProductAsync(client);
        var barcodeValue = CreateUniqueBarcodeValue();

        using var createResponse = await client.PostAsJsonAsync($"/api/products/{product.Id}/barcodes", new
        {
            value = barcodeValue,
            symbology = "CODE_128"
        });

        var barcode = await createResponse.Content.ReadFromJsonAsync<BarcodeResponseDto>();
        Assert.NotNull(barcode);

        using var deleteResponse = await client.DeleteAsync($"/api/barcodes/{barcode!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var resolve = await client.GetFromJsonAsync<BarcodeResolveResponseDto>(
            $"/api/barcodes/resolve?value={Uri.EscapeDataString(barcodeValue)}");

        Assert.NotNull(resolve);
        Assert.False(resolve!.Found);
        Assert.Equal(barcodeValue, resolve.Value);
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(
        string email = AdminEmail,
        string password = AdminPassword)
    {
        if (email == WorkerEmail)
        {
            await SeedUserAsync(WorkerEmail, WorkerPassword, SystemRoleCodes.Worker, "Worker User");
        }
        else if (email == ViewerEmail)
        {
            await SeedUserAsync(ViewerEmail, ViewerPassword, SystemRoleCodes.Viewer, "Viewer User");
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected successful login response, got {(int)response.StatusCode}. Body:{Environment.NewLine}{body}");

        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private async Task<ProductResponseDto> CreateProductAsync(HttpClient client)
    {
        var sku = $"BC-{Guid.NewGuid():N}".ToUpperInvariant();
        using var response = await client.PostAsJsonAsync("/api/products", new
        {
            sku,
            name = $"Тестовый товар {sku}",
            unit = "шт",
            minQuantity = 1m
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected successful product creation response, got {(int)response.StatusCode}. Body:{Environment.NewLine}{body}");

        var product = await response.Content.ReadFromJsonAsync<ProductResponseDto>();
        Assert.NotNull(product);
        return product!;
    }

    private async Task SeedUserAsync(string email, string password, string roleCode, string displayName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MicroMaxDbContext>();

        if (await db.AppUsers.AnyAsync(x => x.Email == email))
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        var role = await db.Roles.FirstAsync(x => x.Code == roleCode);
        var warehouseId = await db.Warehouses.Select(x => x.Id).FirstAsync();

        var user = new AppUser
        {
            Email = email,
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        user.PasswordHash = hasher.HashPassword(user, password);

        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        db.WarehouseUsers.Add(new WarehouseUser
        {
            WarehouseId = warehouseId,
            UserId = user.Id,
            RoleId = role.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static string CreateUniqueBarcodeValue()
    {
        return $"#70#{Guid.NewGuid():N}".ToUpperInvariant();
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

    private sealed record BarcodeResponseDto(int Id, string Value);

    private sealed record BarcodeResolveResponseDto(
        bool Found,
        string Value,
        string? EntityType,
        int? EntityId,
        string? Title,
        string? Subtitle);

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
