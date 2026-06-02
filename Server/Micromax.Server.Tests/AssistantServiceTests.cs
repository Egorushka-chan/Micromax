using MicroMax.Server.Data;
using MicroMax.Server.Models;
using MicroMax.Server.Services;
using MicroMax.Server.Services.Assistant;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Execution;
using MicroMax.Server.Services.Assistant.Providers;
using MicroMax.Server.Services.Assistant.Recovery;
using MicroMax.Server.Services.Assistant.Registry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Micromax.Server.Tests;

public sealed class AssistantServiceTests
{
    [Fact]
    public async Task AmbiguousProductReturnsClarification()
    {
        await using var db = CreateDb();
        db.Products.AddRange(
            new Product { Id = 1, Sku = "GLV-001", Name = "Перчатки рабочие", Unit = "пар" },
            new Product { Id = 2, Sku = "GLV-002", Name = "Перчатки утепленные", Unit = "пар" });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.InterpretAsync("найди перчатки");

        Assert.Equal("Clarification", result.Mode);
        Assert.Equal(2, result.Choices.Count);
    }

    [Theory]
    [InlineData("спиши 2 GLV-001 из A-1", "write_off_product")]
    [InlineData("перемести 2 GLV-001 из A-1 в A-2", "move_product")]
    [InlineData("проведи поступление 2 GLV-001 в A-1", "post_receipt")]
    public async Task DangerousCommandsRequireConfirmation(string text, string commandType)
    {
        await using var db = CreateDb();
        SeedSingleProduct(db);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.InterpretAsync(text);

        Assert.Equal("Command", result.Mode);
        Assert.Equal(commandType, result.CommandType);
        Assert.True(result.RequiresConfirmation);
        Assert.Equal("High", result.RiskLevel);
    }

    [Fact]
    public async Task SafeCommandDoesNotRequireConfirmation()
    {
        await using var db = CreateDb();
        SeedSingleProduct(db);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.InterpretAsync("покажи сводку по складу");

        Assert.Equal("warehouse_summary", result.CommandType);
        Assert.False(result.RequiresConfirmation);
        Assert.Equal("None", result.RiskLevel);
    }

    private static MicroMaxDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MicroMaxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new MicroMaxDbContext(options);
    }

    private static AssistantService CreateService(MicroMaxDbContext db)
    {
        var registry = new AiCommandRegistry();
        var rules = new AiCommandRules(registry);
        var providers = new IAiCommandProvider[]
        {
            new MockAiCommandProvider(rules)
        };
        var selector = new AiProviderSelector(
            providers,
            new AiProviderAvailability(),
            new AiCommandNormalizer(registry, rules),
            NullLogger<AiProviderSelector>.Instance);

        return new AssistantService(db, selector);
    }

    private static void SeedSingleProduct(MicroMaxDbContext db)
    {
        var warehouse = new Warehouse { Id = 1, Name = "Микросклад" };
        var zone = new StorageZone { Id = 1, Warehouse = warehouse, Code = "A", Name = "Зона A" };
        var cell1 = new StorageCell { Id = 1, StorageZone = zone, Code = "A-1", Name = "Полка A-1" };
        var cell2 = new StorageCell { Id = 2, StorageZone = zone, Code = "A-2", Name = "Полка A-2" };
        var product = new Product { Id = 1, Sku = "GLV-001", Name = "Перчатки рабочие", Unit = "пар", MinQuantity = 10 };
        db.AddRange(warehouse, zone, cell1, cell2, product);
    }
}
