using System.Net;
using System.Text.RegularExpressions;
using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Micromax.Server.Tests;

public sealed class AdminPanelAuthTests : IClassFixture<AdminPanelWebApplicationFactory>
{
    private const string AdminEmail = "admin@micromax.local";
    private const string AdminPassword = "Admin12345!";
    private const string WorkerEmail = "worker@micromax.local";
    private const string WorkerPassword = "Worker12345!";

    private readonly AdminPanelWebApplicationFactory _factory;

    public AdminPanelAuthTests(AdminPanelWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousRequestToPanelRedirectsToLogin()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/Login", response.Headers.Location!.AbsolutePath);
    }

    [Theory]
    [InlineData("/Warehouses")]
    [InlineData("/Zones")]
    [InlineData("/Cells")]
    [InlineData("/Products")]
    [InlineData("/Stocks")]
    [InlineData("/Operations")]
    [InlineData("/Users")]
    [InlineData("/OperationLog")]
    public async Task AnonymousRequestToEachAdminPageRedirectsToLogin(string url)
    {
        using var client = CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/Login", response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task AdminCanLoginAndOpenPanel()
    {
        using var client = CreateClient();

        await LoginAsync(client, AdminEmail, AdminPassword);

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Warehouses")]
    [InlineData("/Zones")]
    [InlineData("/Cells")]
    [InlineData("/Products")]
    [InlineData("/Stocks")]
    [InlineData("/Operations")]
    [InlineData("/Users")]
    [InlineData("/OperationLog")]
    public async Task AdminCanOpenAllAdminPages(string url)
    {
        using var client = CreateClient();

        await LoginAsync(client, AdminEmail, AdminPassword);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WorkerCannotLoginToPanel()
    {
        await SeedWorkerAsync();

        using var client = CreateClient();

        var antiForgeryToken = await GetAntiForgeryTokenAsync(client, "/Login");
        var response = await client.PostAsync(
            "/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiForgeryToken,
                ["Input.Email"] = WorkerEmail,
                ["Input.Password"] = WorkerPassword,
                ["Input.RememberMe"] = "false",
                ["ReturnUrl"] = "/"
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var panelResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, panelResponse.StatusCode);
    }

    [Fact]
    public async Task LogoutClearsCookieAndProtectsPanelAgain()
    {
        using var client = CreateClient();

        await LoginAsync(client, AdminEmail, AdminPassword);

        var panelPage = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, panelPage.StatusCode);

        var logoutResponse = await client.PostAsync("/Logout", content: null);

        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.Equal("/Login", logoutResponse.Headers.Location?.OriginalString);

        var responseAfterLogout = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, responseAfterLogout.StatusCode);
    }

    [Fact]
    public async Task AdminCanRunOperationFromOperationsPage()
    {
        using var client = CreateClient();

        await LoginAsync(client, AdminEmail, AdminPassword);

        int productId;
        int targetCellId;
        var uniqueComment = $"integration-receive-{Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MicroMaxDbContext>();
            productId = await db.Products.Select(x => x.Id).FirstAsync();
            targetCellId = await db.StorageCells.Select(x => x.Id).FirstAsync();
        }

        var antiForgeryToken = await GetAntiForgeryTokenAsync(client, "/Operations");
        var response = await client.PostAsync(
            "/Operations?handler=Run",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiForgeryToken,
                ["OperationForm.Type"] = ((int)WarehouseOperationType.Receive).ToString(),
                ["OperationForm.ProductId"] = productId.ToString(),
                ["OperationForm.TargetCellId"] = targetCellId.ToString(),
                ["OperationForm.Quantity"] = "5",
                ["OperationForm.Comment"] = uniqueComment
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Operations", response.Headers.Location?.OriginalString);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<MicroMaxDbContext>();
        var operation = await verificationDb.WarehouseOperations
            .Include(x => x.Logs)
            .SingleAsync(x => x.Comment == uniqueComment);

        Assert.Equal(WarehouseOperationType.Receive, operation.Type);
        Assert.Equal(productId, operation.ProductId);
        Assert.Equal(targetCellId, operation.TargetCellId);
        Assert.Equal(5m, operation.Quantity);
        Assert.NotNull(operation.AppUserId);
        Assert.Contains(operation.Logs, x => x.Message.Contains("Приёмка", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdminCanManageUsersAndWarehouseRolesFromUsersPage()
    {
        using var client = CreateClient();

        await LoginAsync(client, AdminEmail, AdminPassword);

        var email = $"panel-user-{Guid.NewGuid():N}@micromax.local";
        const string displayName = "Panel Managed User";

        var createToken = await GetAntiForgeryTokenAsync(client, "/Users");
        var createResponse = await client.PostAsync(
            "/Users?handler=Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = createToken,
                ["CreateForm.Email"] = email,
                ["CreateForm.DisplayName"] = displayName
            }));

        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);

        int userId;
        int warehouseId;
        int workerRoleId;
        int adminRoleId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MicroMaxDbContext>();
            var createdUser = await db.AppUsers.SingleAsync(x => x.Email == email);
            userId = createdUser.Id;
            warehouseId = await db.Warehouses.Select(x => x.Id).FirstAsync();
            workerRoleId = await db.Roles.Where(x => x.Code == SystemRoleCodes.Worker).Select(x => x.Id).FirstAsync();
            adminRoleId = await db.Roles.Where(x => x.Code == SystemRoleCodes.Admin).Select(x => x.Id).FirstAsync();
        }

        var assignToken = await GetAntiForgeryTokenAsync(client, $"/Users?selectedUserId={userId}");
        var assignResponse = await client.PostAsync(
            "/Users?handler=AssignWarehouse",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = assignToken,
                ["AssignmentForm.UserId"] = userId.ToString(),
                ["AssignmentForm.WarehouseId"] = warehouseId.ToString(),
                ["AssignmentForm.RoleId"] = workerRoleId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, assignResponse.StatusCode);

        var changeRoleToken = await GetAntiForgeryTokenAsync(client, $"/Users?selectedUserId={userId}");
        var changeRoleResponse = await client.PostAsync(
            "/Users?handler=ChangeRole",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = changeRoleToken,
                ["userId"] = userId.ToString(),
                ["warehouseId"] = warehouseId.ToString(),
                ["roleId"] = adminRoleId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, changeRoleResponse.StatusCode);

        var toggleToken = await GetAntiForgeryTokenAsync(client, $"/Users?selectedUserId={userId}");
        var toggleResponse = await client.PostAsync(
            "/Users?handler=ToggleActive",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = toggleToken,
                ["userId"] = userId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, toggleResponse.StatusCode);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<MicroMaxDbContext>();
        var membership = await verificationDb.WarehouseUsers
            .Include(x => x.Role)
            .SingleAsync(x => x.UserId == userId && x.WarehouseId == warehouseId);
        var user = await verificationDb.AppUsers.SingleAsync(x => x.Id == userId);

        Assert.Equal(SystemRoleCodes.Admin, membership.Role!.Code);
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task AdminCanFilterStocksAndOperationLogPages()
    {
        using var client = CreateClient();

        await LoginAsync(client, AdminEmail, AdminPassword);

        int productId;
        string productName;
        string productSku;
        var uniqueComment = $"manual-log-{Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MicroMaxDbContext>();
            var adminUserId = await db.AppUsers.Where(x => x.Email == AdminEmail).Select(x => x.Id).FirstAsync();
            var adminWarehouseIds = await db.WarehouseUsers
                .Where(x => x.UserId == adminUserId && x.Role!.Code == SystemRoleCodes.Admin)
                .Select(x => x.WarehouseId)
                .ToListAsync();
            var firstStock = await db.StockBalances
                .Include(x => x.Product)
                .FirstAsync(x => x.Quantity > 0 && adminWarehouseIds.Contains(x.StorageCell!.StorageZone!.WarehouseId));

            productId = firstStock.ProductId;
            productName = firstStock.Product!.Name;
            productSku = firstStock.Product.Sku;

            if (!await db.WarehouseOperations.AnyAsync(x => x.Comment == uniqueComment))
            {
                var warehouseId = await db.StorageCells
                    .Where(x => x.Id == firstStock.StorageCellId)
                    .Select(x => x.StorageZone!.WarehouseId)
                    .FirstAsync();
                db.WarehouseOperations.Add(new WarehouseOperation
                {
                    WarehouseId = warehouseId,
                    ProductId = productId,
                    TargetCellId = firstStock.StorageCellId,
                    AppUserId = adminUserId,
                    Type = WarehouseOperationType.Receive,
                    Quantity = 1,
                    Comment = uniqueComment,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();

                var operationId = await db.WarehouseOperations
                    .Where(x => x.Comment == uniqueComment)
                    .Select(x => x.Id)
                    .SingleAsync();
                db.OperationLogs.Add(new OperationLog
                {
                    WarehouseOperationId = operationId,
                    Message = "Тестовая запись журнала.",
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }

        var stocksResponse = await client.GetAsync($"/Stocks?productId={productId}");
        var stocksHtml = await stocksResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, stocksResponse.StatusCode);
        Assert.Contains(productSku, stocksHtml);

        var logResponse = await client.GetAsync($"/OperationLog?productId={productId}");
        var logHtml = await logResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, logResponse.StatusCode);
        Assert.Contains(uniqueComment, logHtml);
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

    private async Task LoginAsync(HttpClient client, string email, string password)
    {
        var antiForgeryToken = await GetAntiForgeryTokenAsync(client, "/Login");

        var loginResponse = await client.PostAsync(
            "/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiForgeryToken,
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.RememberMe"] = "false",
                ["ReturnUrl"] = "/"
            }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/", loginResponse.Headers.Location?.OriginalString);
    }

    private async Task SeedWorkerAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MicroMaxDbContext>();
        if (await db.AppUsers.AnyAsync(x => x.Email == WorkerEmail))
        {
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        var workerRole = await db.Roles.FirstAsync(x => x.Code == SystemRoleCodes.Worker);
        var warehouseId = await db.Warehouses.Select(x => x.Id).FirstAsync();
        var user = new AppUser
        {
            Email = WorkerEmail,
            DisplayName = "Worker User",
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        user.PasswordHash = hasher.HashPassword(user, WorkerPassword);

        db.AppUsers.Add(user);
        await db.SaveChangesAsync();

        db.WarehouseUsers.Add(new WarehouseUser
        {
            WarehouseId = warehouseId,
            UserId = user.Id,
            RoleId = workerRole.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<string> GetAntiForgeryTokenAsync(HttpClient client, string path)
    {
        var page = await client.GetAsync(path);
        var html = await page.Content.ReadAsStringAsync();
        Assert.True(
            page.IsSuccessStatusCode,
            $"Expected successful response for '{path}', got {(int)page.StatusCode}. Body:{Environment.NewLine}{html}");

        return ExtractAntiForgeryToken(html);
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase);

        Assert.True(match.Success, "Antiforgery token was not found on the page.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}

public sealed class AdminPanelWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"admin-panel-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
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
