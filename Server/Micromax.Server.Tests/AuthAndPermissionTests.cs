using MicroMax.Server.Api.Auth;
using MicroMax.Server.Configuration;
using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Micromax.Server.Tests;

public sealed class AuthAndPermissionTests
{
    [Fact]
    public async Task RegisterStoresPasswordAsHashAndReturnsTokens()
    {
        await using var db = CreateDb();
        SeedRoles(db);

        var service = CreateAuthService(db);

        var response = await service.RegisterAsync(new RegisterRequest("admin@example.com", "Password123!", "Админ"));

        var user = await db.AppUsers.SingleAsync();
        Assert.Equal("admin@example.com", user.Email);
        Assert.NotEqual("Password123!", user.PasswordHash);
        Assert.False(user.CanAccessWebPanel);
        Assert.NotEmpty(response.AccessToken);
        Assert.NotEmpty(response.RefreshToken);
        Assert.Empty(response.User.Warehouses);
    }

    [Fact]
    public async Task UserAccountServiceCanCreateWebPanelUserWithoutWarehouseAssignments()
    {
        await using var db = CreateDb();
        SeedRoles(db);

        var service = CreateUserAccountService(db);

        var user = await service.CreateAsync(new CreateUserAccountRequest(
            "web@example.com",
            "Password123!",
            "Веб-пользователь",
            CanAccessWebPanel: true));

        Assert.Equal("web@example.com", user.Email);
        Assert.True(user.CanAccessWebPanel);
        Assert.Empty(user.WarehouseUsers);
    }

    [Fact]
    public async Task RefreshRevokesOldTokenAndIssuesNewOne()
    {
        await using var db = CreateDb();
        SeedRoles(db);

        var service = CreateAuthService(db);
        var registered = await service.RegisterAsync(new RegisterRequest("worker@example.com", "Password123!", "Сотрудник"));

        var refreshed = await service.RefreshAsync(new RefreshRequest(registered.RefreshToken));

        var tokens = await db.RefreshTokens.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.NotNull(tokens[0].RevokedAt);
        Assert.Null(tokens[1].RevokedAt);
        Assert.NotEqual(registered.RefreshToken, refreshed.RefreshToken);
    }

    [Fact]
    public async Task WarehousePermissionsRespectWarehouseRole()
    {
        await using var db = CreateDb();
        SeedRoles(db);

        var warehouse = new Warehouse { Id = 1, Name = "Склад" };
        var user = new AppUser
        {
            Id = 10,
            Email = "viewer@example.com",
            DisplayName = "Наблюдатель",
            PasswordHash = new PasswordHasher<AppUser>().HashPassword(new AppUser(), "Password123!"),
            IsActive = true
        };
        var viewerRole = await db.Roles.FirstAsync(x => x.Code == SystemRoleCodes.Viewer);
        var workerRole = await db.Roles.FirstAsync(x => x.Code == SystemRoleCodes.Worker);

        db.AddRange(warehouse, user);
        db.WarehouseUsers.Add(new WarehouseUser
        {
            WarehouseId = warehouse.Id,
            UserId = user.Id,
            RoleId = viewerRole.Id
        });
        await db.SaveChangesAsync();

        var permissions = new WarehousePermissionService(db);
        await permissions.EnsureWarehousePermissionAsync(user.Id, warehouse.Id, WarehousePermission.StockRead);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            permissions.EnsureWarehousePermissionAsync(user.Id, warehouse.Id, WarehousePermission.OperationsExecute));

        db.WarehouseUsers.Single().RoleId = workerRole.Id;
        await db.SaveChangesAsync();

        await permissions.EnsureWarehousePermissionAsync(user.Id, warehouse.Id, WarehousePermission.OperationsExecute);
    }

    private static AuthService CreateAuthService(MicroMaxDbContext db)
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "Tests",
            Audience = "Tests",
            SecretKey = "0123456789abcdef0123456789abcdef",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeDays = 30
        });

        return new AuthService(
            db,
            CreateUserAccountService(db),
            new PasswordHasher<AppUser>(),
            new JwtTokenService(jwtOptions),
            jwtOptions);
    }

    private static UserAccountService CreateUserAccountService(MicroMaxDbContext db) =>
        new(db, new PasswordHasher<AppUser>());

    private static MicroMaxDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MicroMaxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new MicroMaxDbContext(options);
    }

    private static void SeedRoles(MicroMaxDbContext db)
    {
        db.Roles.AddRange(
            new Role { Id = 1, Code = SystemRoleCodes.Admin, Name = "Администратор склада" },
            new Role { Id = 2, Code = SystemRoleCodes.Worker, Name = "Сотрудник склада" },
            new Role { Id = 3, Code = SystemRoleCodes.Viewer, Name = "Наблюдатель" });
        db.SaveChanges();
    }
}
