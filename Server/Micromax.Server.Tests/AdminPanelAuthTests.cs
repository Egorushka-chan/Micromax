using System.Net;
using System.Text.RegularExpressions;
using MicroMax.Server.Data;
using MicroMax.Server.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/Login", response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task AdminCanLoginAndOpenPanel()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client, AdminEmail, AdminPassword);

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WorkerCannotLoginToPanel()
    {
        await SeedWorkerAsync();

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var loginPage = await client.GetAsync("/Login");
        var loginHtml = await loginPage.Content.ReadAsStringAsync();
        var antiForgeryToken = ExtractAntiForgeryToken(loginHtml);

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
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client, AdminEmail, AdminPassword);

        var panelPage = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, panelPage.StatusCode);

        var logoutResponse = await client.PostAsync("/Logout", content: null);

        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.Equal("/Login", logoutResponse.Headers.Location?.OriginalString);

        var responseAfterLogout = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, responseAfterLogout.StatusCode);
    }

    private async Task LoginAsync(HttpClient client, string email, string password)
    {
        var loginPage = await client.GetAsync("/Login");
        var loginHtml = await loginPage.Content.ReadAsStringAsync();
        var antiForgeryToken = ExtractAntiForgeryToken(loginHtml);

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
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<MicroMaxDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<MicroMaxDbContext>>();
            services.RemoveAll<MicroMaxDbContext>();
            services.AddDbContext<MicroMaxDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
